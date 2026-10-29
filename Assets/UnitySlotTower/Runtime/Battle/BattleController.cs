using System;
using UnityEngine;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 1 ウェーブの戦闘を組み立てて実時間で進める（T208 時点の仮の進行）。財布は戦闘専用で、スロットとの統合は T301、
    /// ウェーブの準備フェーズと開始は T304 で扱う。
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class BattleController : MonoBehaviour
    {
        [Header("Tables")]
        [SerializeField] private TextAsset gameConfigTable;
        [SerializeField] private TextAsset unitsTable;
        [SerializeField] private TextAsset unitRangesTable;
        [SerializeField] private TextAsset enemiesTable;
        [SerializeField] private TextAsset wavesTable;

        [SerializeField] private BattleFieldView fieldView;
        [Tooltip("waves.csv の wave。M3 でウェーブ進行から渡す。")]
        [SerializeField] private int wave = 1;

        private SimulationClock _clock;

        public UnitTable Units { get; private set; }
        public CoinWallet Wallet { get; private set; }
        public UnitSummoner Summoner { get; private set; }
        public Battlefield Field { get; private set; }
        public UnitDeployment Deployment { get; private set; }

        private void Start()
        {
            // マップは BattleFieldView の Awake で読み込み済み。
            if (fieldView == null || fieldView.Map == null)
            {
                Debug.LogError("戦闘フィールドのマップがない", this);
                enabled = false;
                return;
            }

            try
            {
                var config = GameConfig.Load(Parse(GameConfig.FileName, gameConfigTable));
                Units = UnitTable.Load(Parse(UnitTable.FileName, unitsTable));
                var ranges = UnitRanges.Load(Parse(UnitRanges.FileName, unitRangesTable), Units);
                var enemies = EnemyTable.Load(Parse(EnemyTable.FileName, enemiesTable));
                var schedule = WaveSchedule.Load(Parse(WaveSchedule.FileName, wavesTable), wave, fieldView.Map, enemies);
                Wallet = new CoinWallet(config.InitialCoins);
                Summoner = new UnitSummoner(Units, Wallet, config.DeployLimit);
                Field = new Battlefield(schedule, ranges, config.Life, new SeededRandom((ulong)DateTime.UtcNow.Ticks));
                Deployment = new UnitDeployment(fieldView.Map, Units, ranges, Summoner, Field);
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
            var steps = _clock.Advance(Time.deltaTime);
            Field.Advance(steps);
            Summoner.Advance(steps);
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
