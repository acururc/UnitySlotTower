using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Tests.EditMode.Data
{
    /// <summary>spec 013: ステージ5〜7の主役の敵、spec 015: ステージ4の分かれ道・ステージ5の飛行の弱め・ステージ6の序盤（実データ）。</summary>
    public sealed class StageThemeTests
    {
        private sealed class Group
        {
            public int Wave;
            public double Time;
            public string Enemy;
            public string Path;
            public int Count;
            public double Interval;
        }

        private static List<Group> Groups(int stage)
        {
            return CsvParser.Parse("waves.csv", TablePaths.ReadAll()["waves.csv"]).Rows
                .Where(row => row.GetInt("stage") == stage)
                .Select(row => new Group
                {
                    Wave = row.GetInt("wave"),
                    Time = row.GetDouble("time"),
                    Enemy = row.Get("enemy_id"),
                    Path = row.Get("path_id"),
                    Count = row.GetInt("count"),
                    Interval = row.GetDouble("interval"),
                })
                .ToList();
        }

        private static int Total(List<Group> groups, string enemy = null) =>
            groups.Where(g => enemy == null || g.Enemy == enemy).Sum(g => g.Count);

        [Test]
        [Description("FR-001: ステージ5は半分以上が飛行で、飛行6体以上が短い間隔でまとまって来るウェーブがある")]
        public void Stage_5_Is_Led_By_Air()
        {
            var groups = Groups(5);
            Assert.That(Total(groups, "air") * 2, Is.GreaterThanOrEqualTo(Total(groups)));
            // 群れは3〜5体（WaveScheduleTests）なので、間隔1秒以下の飛行の群れをウェーブごとに足す
            var swarm = groups.Where(g => g.Enemy == "air" && g.Interval <= 1)
                .GroupBy(g => g.Wave)
                .Max(w => w.Sum(g => g.Count));
            Assert.That(swarm, Is.GreaterThanOrEqualTo(6));
        }

        [Test]
        [Description("spec 015 FR-031: ステージ5の飛行は1回3体まで、同じウェーブの次の飛行まで6秒以上あく")]
        public void Stage_5_Air_Comes_In_Small_Spaced_Groups()
        {
            var air = Groups(5).Where(g => g.Enemy == "air").ToList();
            Assert.That(air.All(g => g.Count <= 3), Is.True, "飛行の群れが4体以上");
            foreach (var wave in air.GroupBy(g => g.Wave))
            {
                var times = wave.Select(g => g.Time).OrderBy(t => t).ToArray();
                for (var i = 1; i < times.Length; i++)
                {
                    Assert.That(times[i] - times[i - 1], Is.GreaterThanOrEqualTo(6), $"wave {wave.Key}");
                }
            }
        }

        [Test]
        [Description("spec 015 FR-032: ステージ6のウェーブ1・2は両側が同時に来ない（反対側は遅れて来る）")]
        public void Stage_6_Opening_Waves_Stagger_The_Two_Sides()
        {
            var groups = Groups(6).Where(g => g.Enemy == "melee" && g.Wave <= 2).ToList();
            foreach (var wave in groups.GroupBy(g => g.Wave))
            {
                var left = wave.Where(g => g.Path == "battle_d_g1").Min(g => g.Time);
                var right = wave.Where(g => g.Path == "battle_d_g2").Min(g => g.Time);
                Assert.That(System.Math.Abs(left - right), Is.GreaterThanOrEqualTo(6), $"wave {wave.Key}");
            }
        }

        [Test]
        [Description("spec 015 FR-030: ステージ4は出現口1つから上下2本に分かれる道（両方を使い、中ボスがいる）。ステージ3（出現口2つ）と形が違う")]
        public void Stage_4_Splits_One_Spawn_Into_Two_Roads()
        {
            var rows = CsvParser.Parse("waves.csv", TablePaths.ReadAll()["waves.csv"]).Rows.Where(r => r.GetInt("stage") == 4).ToList();
            Assert.That(rows.Select(r => r.Get("spawn")).Distinct(), Is.EqualTo(new[] { "S1" }));
            var paths = rows.Select(r => r.Get("path_id")).Distinct().ToList();
            Assert.That(paths, Does.Contain("elite_a_g1").And.Contain("elite_a_g2"));
            Assert.That(rows.Any(r => r.Get("enemy_id") == "mid_boss"), Is.True);

            string Map(string id) => System.IO.File.ReadAllText(System.IO.Path.Combine(TablePaths.Root, "maps", id + ".csv"));
            int Spawns(string map) => map.Split(',', '\n', '\r').Count(cell => cell.StartsWith("S"));
            Assert.That((Spawns(Map("elite_a")), Spawns(Map("battle_b"))), Is.EqualTo((1, 2)));
        }

        [Test]
        [Description("FR-002: ステージ6は半分以上が近接で、両方の地上経路から同時に5体以上ずつ来るウェーブが2つ以上ある")]
        public void Stage_6_Sends_Melee_Hordes_From_Both_Sides()
        {
            var groups = Groups(6);
            Assert.That(Total(groups, "melee") * 2, Is.GreaterThanOrEqualTo(Total(groups)));
            var bothSides = groups
                .Where(g => g.Enemy == "melee" && g.Count >= 5 && g.Path == "battle_d_g1")
                .Count(g => groups.Any(o => o.Enemy == "melee" && o.Count >= 5 && o.Path == "battle_d_g2"
                    && o.Wave == g.Wave && o.Time == g.Time));
            Assert.That(bothSides, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        [Description("FR-003: ステージ7は重装が6体以上で、重装が出るウェーブにはすべて遠距離も出る")]
        public void Stage_7_Sends_Heavies_With_Ranged_Cover()
        {
            var groups = Groups(7);
            Assert.That(Total(groups, "heavy"), Is.GreaterThanOrEqualTo(6));
            var heavyWaves = groups.Where(g => g.Enemy == "heavy").Select(g => g.Wave).Distinct();
            foreach (var wave in heavyWaves)
            {
                Assert.That(groups.Any(g => g.Wave == wave && g.Enemy == "ranged"), Is.True, $"wave {wave}");
            }
        }

        [Test]
        [Description("FR-004: ステージ5・6・7の出方（経路を除く）は互いに異なる")]
        public void Stages_5_To_7_Differ()
        {
            string Signature(int stage) => string.Join(";", Groups(stage)
                .Select(g => $"{g.Wave},{g.Time},{g.Enemy},{g.Count},{g.Interval}")
                .OrderBy(s => s, System.StringComparer.Ordinal));
            var signatures = new[] { Signature(5), Signature(6), Signature(7) };
            Assert.That(signatures.Distinct().Count(), Is.EqualTo(3));
        }
    }
}
