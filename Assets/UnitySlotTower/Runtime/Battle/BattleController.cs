using System;
using UnityEngine;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 戦闘マスの進行（<see cref="BattleNodeRun"/>）を組み立てて実時間で進める。コインはスロット筐体と共有する（T301）。
    /// スロット操作中も止まらない（FR-011）。戦闘は ▶（<see cref="StartWave"/>）で始める。autoStart なら準備に入るとすぐ始める。
    /// ルートと流れ（T350）までのつなぎとして、<see cref="Sequence"/> の順（最初の戦闘 → 通常 → ボス）に戦闘マスを続け、
    /// 戦闘マスが始まるとフィールドの表示をそのマップに差し替える。
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class BattleController : MonoBehaviour
    {
        [Header("Tables")]
        [SerializeField] private TextAsset gameConfigTable;
        [SerializeField] private TextAsset unitsTable;
        [SerializeField] private TextAsset unitRangesTable;
        [SerializeField] private TextAsset unitUpgradesTable;
        [SerializeField] private TextAsset enemiesTable;
        [SerializeField] private TextAsset wavesTable;
        [SerializeField] private TextAsset encountersTable;

        [SerializeField] private BattleFieldView fieldView;
        [Tooltip("所持コインを共有するスロット筐体。")]
        [SerializeField] private SlotCabinet slotCabinet;
        [Tooltip("準備に入ったらすぐ戦闘を始める（確認用。通常は ▶ で始める）。")]
        [SerializeField] private bool autoStart;

        /// <summary>T350 までのつなぎで続ける戦闘マスの種類（i 番目が i+1 階）。</summary>
        private static readonly EncounterKind[] Sequence = { EncounterKind.First, EncounterKind.Battle, EncounterKind.Boss };

        private SimulationClock _clock;
        private GameConfig _config;
        private RunState _state;
        private EncounterTable _encounters;
        private EnemyTable _enemies;
        private CsvTable _waves;
        private IRandom _random;

        public UnitTable Units { get; private set; }

        /// <summary>味方の攻撃範囲（一時停止中の説明の範囲図にも使う: T404）。</summary>
        public UnitRanges Ranges { get; private set; }

        /// <summary>いまの戦闘マス。戦闘マスが変わると別のものになる。</summary>
        public BattleNodeRun Run { get; private set; }

        /// <summary>いまの戦闘マスの階（1 から）。</summary>
        public int Floor { get; private set; }

        /// <summary>新しい戦闘マスの準備に入った（最初の戦闘マスは Start の中なので起きない）。</summary>
        public event Action NodeBegan;
        public CoinWallet Wallet => Run?.Wallet;
        public UnitSummoner Summoner => Run?.Summoner;
        public UnitUpgrades Upgrades => Run?.Upgrades;
        public Battlefield Field => Run?.Field;
        public UnitDeployment Deployment => Run?.Deployment;

        private void Start()
        {
            // マップは BattleFieldView の Awake、財布は SlotCabinet の Awake で作られている。
            if (fieldView == null || fieldView.Map == null)
            {
                Debug.LogError("戦闘フィールドのマップがない", this);
                enabled = false;
                return;
            }

            if (slotCabinet == null || slotCabinet.Wallet == null)
            {
                Debug.LogError("財布を共有するスロット筐体がない", this);
                enabled = false;
                return;
            }

            try
            {
                _config = GameConfig.Load(Parse(GameConfig.FileName, gameConfigTable));
                Units = UnitTable.Load(Parse(UnitTable.FileName, unitsTable));
                Ranges = UnitRanges.Load(Parse(UnitRanges.FileName, unitRangesTable), Units);
                var upgradeTable = UnitUpgradeTable.Load(Parse(UnitUpgradeTable.FileName, unitUpgradesTable), Units);
                _enemies = EnemyTable.Load(Parse(EnemyTable.FileName, enemiesTable));
                _waves = Parse(WaveSchedule.FileName, wavesTable);
                _encounters = EncounterTable.Load(Parse(EncounterTable.FileName, encountersTable));
                _random = new SeededRandom((ulong)DateTime.UtcNow.Ticks);
                // コインは筐体と共有し、召喚・強化は所持金で払う（plan 16.2）。RunState はルートと流れ（T350）で RunController に移す。
                _state = new RunState(_config, upgradeTable, slotCabinet.Rules, slotCabinet.Setting);
                slotCabinet.UseWallet(_state.Coins);
                BeginNode(1);
                slotCabinet.Aligned += OnSlotAligned;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                enabled = false;
                return;
            }

            _clock = new SimulationClock();
        }

        /// <summary>一時停止中か（FR-012）。時間が止まり、配置・撤退・強化・スロットの操作を受け付けない。</summary>
        public bool IsPaused { get; private set; }

        /// <summary>リールのカスタム画面を開いているか。カスタムは廃止（2026-11-14）で常に false。参照の掃除は T354。</summary>
        public bool IsCustomizing => false;

        /// <summary>クリア（最終ウェーブを終えた）かゲームオーバーで、ゲームが終わったか（リザルトを出す: T402）。</summary>
        public bool IsFinished => Run != null && (Run.IsRunCleared || Run.Phase == WavePhase.GameOver);

        /// <summary>
        /// 戦闘側の操作（配置・撤退・強化・▶）を受け付けないか。一時停止中・ゲーム終了後。
        /// </summary>
        public bool IsInputBlocked => IsPaused || IsFinished;

        /// <summary>一時停止を切り替える。スロットのリールやウェイトも止めるため Time.timeScale を 0 にする。カスタム中・ゲーム終了後は何もしない。</summary>
        public void SetPaused(bool paused)
        {
            if (IsFinished)
            {
                return;
            }

            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (_clock != null)
            {
                _clock.IsPaused = paused;
            }

            UpdateSlotLock();
        }

        public void TogglePause() => SetPaused(!IsPaused);

        /// <summary>
        /// 文字なし説明（T403）を見せているあいだ true。戦闘の時間だけを止め、操作とスロット（Time.timeScale）は止めない。
        /// </summary>
        public bool IsHeldByGuide { get; set; }

        /// <summary>説明の撤退の題目を表示中は true（D-56）。このあいだの撤退は召喚コストの全額を返す。</summary>
        public bool FullRefundRetreat { get; set; }

        /// <summary>準備中なら戦闘を始める（▶、plan 4章）。一時停止中・カスタム中は受け付けない。</summary>
        public void StartWave()
        {
            if (!IsInputBlocked)
            {
                Run?.Start();
            }
        }

        private void UpdateSlotLock()
        {
            if (slotCabinet != null)
            {
                slotCabinet.InputLocked = IsPaused || IsFinished;
            }
        }

        private void OnDestroy()
        {
            // シーンを離れても時間が止まったままにならないようにする。
            if (IsPaused)
            {
                Time.timeScale = 1f;
            }

            if (Run != null && slotCabinet != null)
            {
                slotCabinet.Aligned -= OnSlotAligned;
            }
        }

        /// <summary>
        /// floor 階の戦闘マスを出現表から選んで準備に入り、フィールドの表示をそのマップに差し替える。
        /// 回数固定（<see cref="Encounter.FixedSpins"/>）を筐体へ渡す（0 ならコインで回す）。
        /// </summary>
        private void BeginNode(int floor)
        {
            var encounter = _encounters.Pick(Sequence[floor - 1], floor, _random);
            slotCabinet.SetFixedSpins(encounter.FixedSpins);
            var map = fieldView.LoadMap(encounter.MapId);
            Floor = floor;
            Run = new BattleNodeRun(_config, Units, Ranges, _state, encounter, map,
                wave => WaveSchedule.Load(_waves, encounter.Id, wave, map, _enemies), _random);
            fieldView.Show(map);
        }

        // 揃った図柄の効果を戦闘へ渡す（FR-042）。
        private void OnSlotAligned(SlotSymbol symbol) => Run.ApplySlot(symbol);

        private void Update()
        {
            if (autoStart)
            {
                Run.Start();
            }

            Run.Advance(_clock.Advance(IsHeldByGuide ? 0d : Time.deltaTime));
            if (Run.Phase == WavePhase.Completed && !Run.IsRunCleared && Floor < Sequence.Length)
            {
                BeginNode(Floor + 1);
                NodeBegan?.Invoke();
            }

            if (IsFinished && !slotCabinet.InputLocked)
            {
                UpdateSlotLock();
            }
        }

        private static CsvTable Parse(string fileName, TextAsset asset)
        {
            if (asset == null)
            {
                throw new InvalidOperationException($"{fileName} が設定されていない");
            }

            return CsvParser.Parse(fileName, asset.text);
        }
    }
}
