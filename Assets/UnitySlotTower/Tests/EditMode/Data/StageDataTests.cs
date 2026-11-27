using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;

namespace UnitySlotTower.Tests.EditMode.Data
{
    [Description("T605 (FR-102〜FR-104, AC-102 の Core 部分): 8ステージの仮データ")]
    public sealed class StageDataTests
    {
        private static StageTable Stages() =>
            StageTable.Load(CsvParser.Parse(StageTable.FileName, TablePaths.ReadAll()[StageTable.FileName]));

        private static WaveSchedule Schedule(int stage)
        {
            var tables = TablePaths.ReadAll();
            var definition = Stages().Get(stage);
            var paths = CsvParser.Parse(BattleMap.PathsFileName, tables[BattleMap.PathsFileName]);
            var map = BattleMap.Load(definition.MapId, tables[BattleMap.MapFileName(definition.MapId)], paths);
            return WaveSchedule.Load(CsvParser.Parse(WaveSchedule.FileName, tables[WaveSchedule.FileName]), stage, map,
                EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, tables[EnemyTable.FileName])),
                CsvParser.Parse(WaveSchedule.StageWavesFileName, tables[WaveSchedule.StageWavesFileName]));
        }

        [Test]
        [Description("plan 2.1: 8ステージ、マップは固定（FR-104）、ステージ1だけ clear")]
        public void Eight_Stages_Use_Fixed_Maps_And_Modes()
        {
            var stages = Stages().Stages;

            Assert.That(stages.Select(s => (s.Number, s.MapId, s.Mode)), Is.EqualTo(new[]
            {
                (1, "first", WaveMode.Clear), (2, "battle_a", WaveMode.Timed), (3, "battle_b", WaveMode.Timed),
                (4, "elite_a", WaveMode.Timed), (5, "battle_c", WaveMode.Timed), (6, "battle_d", WaveMode.Timed),
                (7, "battle_e", WaveMode.Timed), (8, "boss_a", WaveMode.Timed),
            }));
        }

        [Test]
        [Description("T617: 敵の HP の倍率はステージ1・2が1で、後ろのステージほど下がらない（値はプレイで調整）")]
        public void Enemy_Hp_Rate_Is_One_For_Stages_1_And_2_And_Never_Drops()
        {
            var rates = Stages().Stages.Select(s => s.EnemyHpRate).ToArray();

            Assert.That((rates[0], rates[1]), Is.EqualTo((1.0, 1.0)));
            Assert.That(rates, Is.Ordered.Ascending);
        }

        [Test]
        [Description("FR-103: ステージ1は最初に見せ場の群れ（spec 015 FR-001、8体）、続いて 1体 → 2体 → 3体 → 長め（6体以上）")]
        public void Stage_1_Waves_Grow_One_Two_Three_Then_Long()
        {
            var counts = Schedule(1).Waves.Select(w => w.Spawns.Count).ToArray();

            Assert.That((counts.Length, counts[0], counts[1], counts[2], counts[3], counts[4] >= 6), Is.EqualTo((5, 8, 1, 2, 3, true)));
        }

        [Test]
        [Description("FR-103: ステージ1の2体のウェーブ（spec 015 でウェーブ3）は同時に来る（間隔1秒未満）")]
        public void Stage_1_Second_Wave_Comes_Together()
        {
            var spawns = Schedule(1).Waves[2].Spawns;

            Assert.That(spawns[1].Time - spawns[0].Time, Is.LessThan(1));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [Description("plan 4章: ステージ2〜8は4〜6ウェーブ")]
        public void Later_Stages_Have_Four_To_Six_Waves(int stage)
        {
            Assert.That(Schedule(stage).Waves.Count, Is.InRange(4, 6));
        }

        [Test]
        [Description("FR-104: ステージ4の最後のウェーブに中ボス、ステージ8の最後のウェーブに最終ボス。ほかのステージにボスはいない")]
        public void Bosses_Are_In_The_Last_Wave_Of_Stages_4_And_8()
        {
            var bosses = Stages().Stages.Select(stage =>
            {
                var waves = Schedule(stage.Number).Waves;
                var last = waves[waves.Count - 1].Number;
                return (stage.Number, string.Join(",", waves.SelectMany(w => w.Spawns)
                    .Where(s => s.IsBoss).Select(s => $"{s.Wave == last}:{s.Enemy.Id}")));
            }).ToArray();

            Assert.That(bosses, Is.EqualTo(new[]
            {
                (1, ""), (2, ""), (3, ""), (4, "True:mid_boss"), (5, ""), (6, ""), (7, ""), (8, "True:final_boss"),
            }));
        }

        [Test]
        [Description("plan 4章: 後ろのステージほど敵が多い（ステージ5〜7はステージ3以上、ステージ8が最多）")]
        public void Later_Stages_Have_More_Enemies()
        {
            var counts = Enumerable.Range(1, 8).Select(stage => Schedule(stage).Spawns.Count).ToArray();

            Assert.That(counts.Skip(4).Take(3).All(c => c >= counts[2]), Is.True, string.Join(",", counts));
            Assert.That(counts[7], Is.EqualTo(counts.Max()), string.Join(",", counts));
        }

        [Test]
        [Description("AC-102: データ検証で各ステージにマップとウェーブがある（今のデータはエラーなし）")]
        public void Project_Tables_Are_Valid()
        {
            var report = GameDataValidator.ValidateAll(TablePaths.ReadAll());

            Assert.That(report.IsValid, Is.True, report.ToString());
        }
    }
}
