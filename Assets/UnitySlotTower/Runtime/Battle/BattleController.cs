using System;
using UnityEngine;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
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
                var ranges = UnitRanges.Load(Parse(UnitRanges.FileName, unitRangesTable), Units);
                var upgradeTable = UnitUpgradeTable.Load(Parse(UnitUpgradeTable.FileName, unitUpgradesTable), Units);
                var enemies = EnemyTable.Load(Parse(EnemyTable.FileName, enemiesTable));
                var waves = Parse(WaveSchedule.FileName, wavesTable);
                var wallet = slotCabinet.Wallet;
                Run = new WaveRun(config, Units, ranges, new UnitUpgrades(upgradeTable, wallet), wallet,
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

        /// <summary>一時停止を切り替える。スロットのリールやウェイトも止めるため Time.timeScale を 0 にする。</summary>
        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (_clock != null)
            {
                _clock.IsPaused = paused;
            }

            if (slotCabinet != null)
            {
                slotCabinet.InputLocked = paused;
            }
        }

        public void TogglePause() => SetPaused(!IsPaused);

        /// <summary>準備中なら戦闘を始める（▶、plan 4章）。一時停止中は受け付けない。</summary>
        public void StartWave()
        {
            if (!IsPaused)
            {
                Run?.Start();
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

        private void OnStageBegan() => fieldView.Show(Run.Map);

        // 揃った図柄の効果（攻撃力アップ・攻撃速度アップ・全体HP回復）を戦闘へ渡す（FR-042、T314）。
        private void OnSlotAligned(SlotSymbol symbol) => Run.ApplySlot(symbol, slotCabinet.Rules);

        private void Update()
        {
            if (autoStart)
            {
                Run.Start();
            }

            Run.Advance(_clock.Advance(Time.deltaTime));
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
