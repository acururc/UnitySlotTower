using System.IO;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    public sealed class WaveScheduleTests
    {
        private static CsvError LoadError(string rows) =>
            Assert.Throws<CsvFormatException>(() => BattleFixtures.LoadWave(rows)).Error;

        [Test]
        public void Spawns_Are_Sorted_By_Time()
        {
            var wave = BattleFixtures.LoadWave("5,ranged,S1,g,0\n0,melee,S1,g,0\n2.5,air,S2,a,0\n");

            Assert.That(wave.Spawns.Select(s => (s.Time, s.Enemy.Id)),
                Is.EqualTo(new[] { (0.0, "melee"), (2.5, "air"), (5.0, "ranged") }));
        }

        [Test]
        public void Same_Time_Keeps_Row_Order()
        {
            var wave = BattleFixtures.LoadWave("1,ranged,S1,g,0\n1,melee,S1,g,0\n");

            Assert.That(wave.Spawns.Select(s => s.Enemy.Id), Is.EqualTo(new[] { "ranged", "melee" }));
        }

        [Test]
        [Description("FR-102: ステージ番号が合う行だけを読む")]
        public void Only_Rows_Of_The_Stage_Are_Read()
        {
            var wave = BattleFixtures.LoadStageWave(
                "1,0,melee,S1,g,0\n2,0,air,S2,a,0\n1,1,ranged,S1,g,0\n2,1,rich,S1,g,0\n", 2);

            Assert.That((wave.Stage, string.Join(",", wave.Spawns.Select(s => s.Enemy.Id))),
                Is.EqualTo((2, "air,rich")));
        }

        [Test]
        [Description("FR-102, plan 2.1: 行は wave 列でウェーブに分かれ、時刻はウェーブの始まりから。start_after は stage_waves.csv から読む")]
        public void Rows_Are_Grouped_By_Wave_With_Start_After()
        {
            var wave = BattleFixtures.LoadStagedWave(
                "2,1,ranged,S1,g,0,1,0\n1,3,melee,S1,g,0,1,0\n2,0,air,S2,a,0,1,0\n1,0,rich,S1,g,0,1,0\n", 0, 7.5);

            Assert.That(wave.Waves.Select(w => (w.Number, w.StartAfter,
                    string.Join(",", w.Spawns.Select(s => $"{s.Wave}:{s.Time}:{s.Enemy.Id}")))),
                Is.EqualTo(new[] { (1, 0.0, "1:0:rich,1:3:melee"), (2, 7.5, "2:0:air,2:1:ranged") }));
            Assert.That(wave.Spawns.Select(s => s.Enemy.Id), Is.EqualTo(new[] { "rich", "melee", "air", "ranged" }));
        }

        [Test]
        [Description("plan 2.1: stage_waves.csv にないウェーブの行はその行の wave のセルのエラー")]
        public void Wave_Missing_From_Stage_Waves_Is_An_Error_At_Its_Cell()
        {
            var error = Assert.Throws<CsvFormatException>(() =>
                BattleFixtures.LoadStagedWave("1,0,melee,S1,g,0,1,0\n2,0,melee,S1,g,0,1,0\n", 0)).Error;

            Assert.That((error.File, error.Line, error.Column), Is.EqualTo((WaveSchedule.FileName, 3, 2)));
        }

        [Test]
        [Description("plan 2.1: 出現のないウェーブの stage_waves.csv の行は読み飛ばす（データ検証でエラーにする）")]
        public void Stage_Wave_Without_Spawns_Is_Skipped_On_Load()
        {
            var wave = BattleFixtures.LoadStagedWave("1,0,melee,S1,g,0,1,0\n", 0, 5);

            Assert.That(wave.Waves.Select(w => w.Number), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void Wave_Below_One_Is_An_Error_At_Its_Cell()
        {
            var error = Assert.Throws<CsvFormatException>(() =>
                BattleFixtures.LoadStagedWave("0,0,melee,S1,g,0,1,0\n", 0)).Error;

            Assert.That((error.Line, error.Column), Is.EqualTo((2, 2)));
        }

        [Test]
        public void Spawn_Resolves_The_Path_Of_The_Map()
        {
            var spawn = BattleFixtures.LoadWave("0,air,S2,a,1\n").Spawns.Single();

            Assert.That((spawn.Path.Id, spawn.Path.IsAir, spawn.IsBoss), Is.EqualTo(("a", true, true)));
        }

        [Test]
        public void Unknown_Enemy_Is_An_Error_At_Its_Cell()
        {
            var error = LoadError("0,melee,S1,g,0\n0,dragon,S1,g,0\n");

            Assert.That((error.Line, error.Column), Is.EqualTo((3, 3)));
        }

        [Test]
        [Description("FR-102, FR-034: waves.csv はステージとウェーブの列で始まり、群れの count・interval 列がある")]
        public void Project_Waves_Have_Stage_Wave_And_Group_Columns()
        {
            var header = TablePaths.ReadAll()[WaveSchedule.FileName].Split('\n')[0].Trim();

            Assert.That(header, Is.EqualTo("stage,wave,time,enemy_id,spawn,path_id,is_boss,count,interval"));
        }

        [Test]
        [Description("FR-034, AC-031: 1行の群れは count 体を interval 秒ごとに出す")]
        public void Group_Row_Spawns_Count_Enemies_At_The_Interval()
        {
            var wave = BattleFixtures.LoadGroupWave("10,melee,S1,g,0,3,1.5\n");

            Assert.That(wave.Spawns.Select(s => (s.Time, s.Enemy.Id, s.Path.Id)),
                Is.EqualTo(new[] { (10.0, "melee", "g"), (11.5, "melee", "g"), (13.0, "melee", "g") }));
        }

        [Test]
        [Description("FR-034: 重なった群れは出る時刻の順に混ざる")]
        public void Overlapping_Groups_Interleave_By_Time()
        {
            var wave = BattleFixtures.LoadGroupWave("0,melee,S1,g,0,3,2\n1,air,S2,a,0,2,2\n");

            Assert.That(wave.Spawns.Select(s => (s.Time, s.Enemy.Id)),
                Is.EqualTo(new[] { (0.0, "melee"), (1.0, "air"), (2.0, "melee"), (3.0, "air"), (4.0, "melee") }));
        }

        [Test]
        [Description("plan 17.3: count・interval の列がなければ1行1体")]
        public void Rows_Without_Group_Columns_Spawn_One_Enemy_Each()
        {
            Assert.That(BattleFixtures.LoadWave("0,melee,S1,g,0\n3,ranged,S1,g,0\n").Spawns.Count, Is.EqualTo(2));
        }

        [Test]
        public void Group_Of_Zero_Is_An_Error_At_Its_Cell()
        {
            var error = Assert.Throws<CsvFormatException>(() => BattleFixtures.LoadGroupWave("0,melee,S1,g,0,0,1\n")).Error;

            Assert.That((error.Line, error.Column), Is.EqualTo((2, 7)));
        }

        [Test]
        public void Negative_Interval_Is_An_Error_At_Its_Cell()
        {
            var error = Assert.Throws<CsvFormatException>(() => BattleFixtures.LoadGroupWave("0,melee,S1,g,0,2,-1\n")).Error;

            Assert.That((error.Line, error.Column), Is.EqualTo((2, 8)));
        }

        [Test]
        [Description("FR-034 / FR-075: ステージ1（チュートリアル）以外の群れは3〜5体。重装1体は同じウェーブの3〜5体の群れの直後に続く。ボスは1体")]
        public void Project_Groups_Have_Three_To_Five_Enemies_Except_Bosses()
        {
            var waves = CsvParser.Parse(WaveSchedule.FileName, TablePaths.ReadAll()[WaveSchedule.FileName]);
            var wrong = waves.Rows
                .Where(r => r.GetInt("stage") != 1)
                .Where(r => r.GetBool("is_boss") ? r.GetInt("count") != 1 :
                    r.Get("enemy_id") == "heavy" ? r.GetInt("count") != 1 || !waves.Rows.Any(group =>
                        group.Get("stage") == r.Get("stage") && group.Get("wave") == r.Get("wave") &&
                        group.Get("enemy_id") == "melee" && group.GetInt("count") >= 3 && group.GetInt("count") <= 5 &&
                        System.Math.Abs(group.GetDouble("time") +
                            group.GetInt("count") * group.GetDouble("interval") - r.GetDouble("time")) < 0.0001) :
                    r.GetInt("count") < 3 || r.GetInt("count") > 5)
                .Select(r => r.Line)
                .ToArray();

            Assert.That(wrong, Is.Empty);
        }

        [Test]
        public void Path_Missing_From_The_Map_Is_An_Error()
        {
            var error = LoadError("0,melee,S1,w1_g1,0\n");

            Assert.That((error.Line, error.Column), Is.EqualTo((2, 5)));
        }

        [Test]
        public void Spawn_Must_Be_Where_The_Path_Starts()
        {
            var error = LoadError("0,melee,S2,g,0\n");

            Assert.That((error.Line, error.Column), Is.EqualTo((2, 4)));
        }

        [Test]
        [Description("FR-031: 空中の敵は空中経路、地上の敵は地上経路を進む")]
        public void Air_Enemy_On_A_Ground_Path_Is_An_Error()
        {
            var error = LoadError("0,air,S1,g,0\n");

            Assert.That((error.Line, error.Column), Is.EqualTo((2, 5)));
        }

        [Test]
        public void Ground_Enemy_On_An_Air_Path_Is_An_Error()
        {
            var error = LoadError("0,melee,S2,a,0\n");

            Assert.That((error.Line, error.Column), Is.EqualTo((2, 5)));
        }

        [Test]
        [Description("FR-102: どのステージも、そのマップで stage_waves.csv のウェーブどおりに敵が出る")]
        public void Project_Every_Stage_Wave_Loads_On_Its_Map()
        {
            var tables = TablePaths.ReadAll();
            var paths = CsvParser.Parse(BattleMap.PathsFileName, tables[BattleMap.PathsFileName]);
            var waves = CsvParser.Parse(WaveSchedule.FileName, tables[WaveSchedule.FileName]);
            var stageWaves = CsvParser.Parse(WaveSchedule.StageWavesFileName, tables[WaveSchedule.StageWavesFileName]);
            var enemies = EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, tables[EnemyTable.FileName]));
            var stages = StageTable.Load(CsvParser.Parse(StageTable.FileName, tables[StageTable.FileName]));
            var empty = new System.Collections.Generic.List<string>();

            foreach (var stage in stages.Stages)
            {
                var map = BattleMap.Load(stage.MapId, tables[BattleMap.MapFileName(stage.MapId)], paths);
                var schedule = WaveSchedule.Load(waves, stage.Number, map, enemies, stageWaves);
                empty.AddRange(schedule.Waves.Where(w => w.Spawns.Count == 0).Select(w => $"{stage.Number}-{w.Number}"));
                if (schedule.Waves.Count == 0)
                {
                    empty.Add($"{stage.Number}");
                }
            }

            Assert.That(empty, Is.Empty);
        }
    }
}
