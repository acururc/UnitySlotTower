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
    /// ステージとウェーブの進行（<see cref="WaveRun"/>）を組み立てて実時間で進める。財布はスロット筐体と共有する（T301）。
    /// スロット操作中も止まらない（FR-011）。戦闘は ▶（<see cref="StartWave"/>）で始める。autoStart なら準備に入るとすぐ始める。
    /// ステージ n はマップ s{n} で戦い、ステージが始まるとフィールドの表示を差し替える（T311）。
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

        [SerializeField] private BattleFieldView fieldView;
        [Tooltip("所持コインを共有するスロット筐体。")]
        [SerializeField] private SlotCabinet slotCabinet;
        [Tooltip("準備に入ったらすぐ戦闘を始める（確認用。通常は ▶ で始める）。")]
        [SerializeField] private bool autoStart;

        private SimulationClock _clock;

        public UnitTable Units { get; private set; }

        /// <summary>味方の攻撃範囲（一時停止中の説明の範囲図にも使う: T404）。</summary>
        public UnitRanges Ranges { get; private set; }

        public WaveRun Run { get; private set; }
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
                var config = GameConfig.Load(Parse(GameConfig.FileName, gameConfigTable));
                Units = UnitTable.Load(Parse(UnitTable.FileName, unitsTable));
                Ranges = UnitRanges.Load(Parse(UnitRanges.FileName, unitRangesTable), Units);
                var upgradeTable = UnitUpgradeTable.Load(Parse(UnitUpgradeTable.FileName, unitUpgradesTable), Units);
                var enemies = EnemyTable.Load(Parse(EnemyTable.FileName, enemiesTable));
                var waves = Parse(WaveSchedule.FileName, wavesTable);
                // コインは筐体と共有し、召喚・強化は所持金で払う（plan 16.2）。ルートと流れ（T350）までは戦闘ごとに作る。
                var state = new RunState(config, upgradeTable, slotCabinet.Rules, slotCabinet.Setting);
                slotCabinet.UseWallet(state.Coins);
                Run = new WaveRun(config, Units, Ranges, state,
                    WaveSchedule.StageCount, WaveSchedule.WavesPerStage,
                    stage => stage == 1 ? fieldView.Map : fieldView.LoadStageMap(stage),
                    (stage, wave, map) => WaveSchedule.Load(waves, stage, wave, map, enemies),
                    new SeededRandom((ulong)DateTime.UtcNow.Ticks));
                Run.StageBegan += OnStageBegan;
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
        public bool IsFinished => Run != null && (Run.Phase == WavePhase.Completed || Run.Phase == WavePhase.GameOver);

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

            if (Run != null)
            {
                Run.StageBegan -= OnStageBegan;
                if (slotCabinet != null)
                {
                    slotCabinet.Aligned -= OnSlotAligned;
                }
            }
        }

        // ステージ1 の StageBegan は WaveRun を作る中で呼ばれ、まだ購読していない（開始時のカスタムは Start で開く）。
        // ステージ間ではカスタムを開かない（D-48、2026-11-04）。
        private void OnStageBegan()
        {
            fieldView.Show(Run.Map);
        }

        // 揃った図柄の効果を戦闘へ渡す（FR-042。T342 までのつなぎはバーの攻撃力アップだけ）。
        private void OnSlotAligned(SlotSymbol symbol) => Run.ApplySlot(symbol);

        private void Update()
        {
            if (autoStart)
            {
                Run.Start();
            }

            Run.Advance(_clock.Advance(IsHeldByGuide ? 0d : Time.deltaTime));
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
