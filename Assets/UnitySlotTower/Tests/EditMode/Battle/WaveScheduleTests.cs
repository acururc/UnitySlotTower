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
        [Description("FR-010, D-78: 戦闘マスの id が合う行だけを読む（戦闘マスは1ウェーブ）")]
        public void Only_Rows_Of_The_Encounter_Are_Read()
        {
            var wave = BattleFixtures.LoadEncounterWave(
                "a,0,melee,S1,g,0\nb,0,air,S2,a,0\na,1,ranged,S1,g,0\nb,1,rich,S1,g,0\n", "b");

            Assert.That((wave.EncounterId, string.Join(",", wave.Spawns.Select(s => s.Enemy.Id))),
                Is.EqualTo(("b", "air,rich")));
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
        [Description("D-78, FR-034: waves.csv に wave 列はなく（戦闘マスは1ウェーブ）、群れの count・interval 列がある")]
        public void Project_Waves_Have_Group_Columns_And_No_Wave_Column()
        {
            var header = TablePaths.ReadAll()[WaveSchedule.FileName].Split('\n')[0].Trim();

            Assert.That(header, Is.EqualTo("encounter_id,time,enemy_id,spawn,path_id,is_boss,count,interval"));
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
        [Description("FR-034, plan 17.3: 群れは3〜5体のまとまり（データの書き直しで1体だけの行を残さない。ボスは1体）")]
        public void Project_Groups_Have_Three_To_Five_Enemies_Except_Bosses()
        {
            var waves = CsvParser.Parse(WaveSchedule.FileName, TablePaths.ReadAll()[WaveSchedule.FileName]);
            var wrong = waves.Rows
                .Where(r => r.GetBool("is_boss") ? r.GetInt("count") != 1 : r.GetInt("count") < 3 || r.GetInt("count") > 5)
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
        [Description("FR-010: 出現表のどの戦闘マスも、そのマップで敵が出る")]
        public void Project_Every_Encounter_Wave_Loads_On_Its_Map()
        {
            var tables = TablePaths.ReadAll();
            var paths = CsvParser.Parse(BattleMap.PathsFileName, tables[BattleMap.PathsFileName]);
            var waves = CsvParser.Parse(WaveSchedule.FileName, tables[WaveSchedule.FileName]);
            var enemies = EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, tables[EnemyTable.FileName]));
            var encounters = EncounterTable.Load(CsvParser.Parse(EncounterTable.FileName, tables[EncounterTable.FileName]));
            var counts = new System.Collections.Generic.List<int>();

            foreach (var encounter in encounters.All)
            {
                var map = BattleMap.Load(encounter.MapId, tables[BattleMap.MapFileName(encounter.MapId)], paths);
                counts.Add(WaveSchedule.Load(waves, encounter.Id, map, enemies).Spawns.Count);
            }

            Assert.That((counts.Count, counts.All(c => c > 0)), Is.EqualTo((encounters.All.Count, true)));
        }
    }
}
