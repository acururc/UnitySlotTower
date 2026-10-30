using System;
using UnityEngine;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// ウェーブの進行（<see cref="WaveRun"/>）を組み立てて実時間で進める。財布はスロット筐体と共有する（T301）。
    /// スロット操作中も止まらない（FR-011）。▶ の入力ができるまでは準備に入るとすぐ戦闘を始める（autoStart）。
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
        [Tooltip("最後のウェーブの番号。waves.csv にそのウェーブまでの行が要る（W2〜5 は T305）。")]
        [SerializeField] private int waveCount = 1;
        [Tooltip("準備に入ったらすぐ戦闘を始める（▶ の入力ができるまでの仮）。")]
        [SerializeField] private bool autoStart = true;

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
                // マップはまだ1枚だけ（ウェーブごとのマップは T305）。
                var map = fieldView.Map;
                Run = new WaveRun(config, Units, ranges, new UnitUpgrades(upgradeTable, wallet), wallet, waveCount,
                    wave => new WaveStage(map, WaveSchedule.Load(waves, wave, map, enemies)),
                    new SeededRandom((ulong)DateTime.UtcNow.Ticks));
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                enabled = false;
                return;
            }

            _clock = new SimulationClock();
        }

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
