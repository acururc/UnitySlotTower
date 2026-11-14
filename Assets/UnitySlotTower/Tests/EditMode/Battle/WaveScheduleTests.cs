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
            var wave = BattleFixtures.LoadWave("1,5,ranged,S1,g,0\n1,0,melee,S1,g,0\n1,2.5,air,S2,a,0\n");

            Assert.That(wave.Spawns.Select(s => (s.Time, s.Enemy.Id)),
                Is.EqualTo(new[] { (0.0, "melee"), (2.5, "air"), (5.0, "ranged") }));
        }

        [Test]
        public void Same_Time_Keeps_Row_Order()
        {
            var wave = BattleFixtures.LoadWave("1,1,ranged,S1,g,0\n1,1,melee,S1,g,0\n");

            Assert.That(wave.Spawns.Select(s => s.Enemy.Id), Is.EqualTo(new[] { "ranged", "melee" }));
        }

        [Test]
        public void Only_Rows_Of_The_Wave_Are_Read()
        {
            var wave = BattleFixtures.LoadWave("1,0,melee,S1,g,0\n2,0,unknown,S9,nowhere,0\n");

            Assert.That(wave.Spawns.Select(s => s.Enemy.Id), Is.EqualTo(new[] { "melee" }));
        }

        [Test]
        [Description("FR-010: 戦闘マスの id とウェーブ番号の両方が合う行だけを読む")]
        public void Only_Rows_Of_The_Encounter_And_Wave_Are_Read()
        {
            var wave = BattleFixtures.LoadEncounterWave(
                "a,2,0,melee,S1,g,0\nb,1,0,ranged,S1,g,0\nb,2,0,air,S2,a,0\nb,2,1,rich,S1,g,0\n", "b", 2);

            Assert.That((wave.EncounterId, wave.Wave, string.Join(",", wave.Spawns.Select(s => s.Enemy.Id))),
                Is.EqualTo(("b", 2, "air,rich")));
        }

        [Test]
        public void Spawn_Resolves_The_Path_Of_The_Map()
        {
            var spawn = BattleFixtures.LoadWave("1,0,air,S2,a,1\n").Spawns.Single();

            Assert.That((spawn.Path.Id, spawn.Path.IsAir, spawn.IsBoss), Is.EqualTo(("a", true, true)));
        }

        [Test]
        public void Unknown_Enemy_Is_An_Error_At_Its_Cell()
        {
            var error = LoadError("1,0,melee,S1,g,0\n1,0,dragon,S1,g,0\n");

            Assert.That((error.Line, error.Column), Is.EqualTo((3, 4)));
        }

        [Test]
        public void Path_Missing_From_The_Map_Is_An_Error()
        {
            var error = LoadError("1,0,melee,S1,w1_g1,0\n");

            Assert.That((error.Line, error.Column), Is.EqualTo((2, 6)));
        }

        [Test]
        public void Spawn_Must_Be_Where_The_Path_Starts()
        {
            var error = LoadError("1,0,melee,S2,g,0\n");

            Assert.That((error.Line, error.Column), Is.EqualTo((2, 5)));
        }

        [Test]
        [Description("FR-031: 空中の敵は空中経路、地上の敵は地上経路を進む")]
        public void Air_Enemy_On_A_Ground_Path_Is_An_Error()
        {
            var error = LoadError("1,0,air,S1,g,0\n");

            Assert.That((error.Line, error.Column), Is.EqualTo((2, 6)));
        }

        [Test]
        public void Ground_Enemy_On_An_Air_Path_Is_An_Error()
        {
            var error = LoadError("1,0,melee,S2,a,0\n");

            Assert.That((error.Line, error.Column), Is.EqualTo((2, 6)));
        }

        [Test]
        [Description("FR-010: 出現表のどの戦闘マスも、2ウェーブともそのマップで敵が出る")]
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
                for (var wave = 1; wave <= WaveSchedule.WavesPerEncounter; wave++)
                {
                    counts.Add(WaveSchedule.Load(waves, encounter.Id, wave, map, enemies).Spawns.Count);
                }
            }

            Assert.That((counts.Count, counts.All(c => c > 0)),
                Is.EqualTo((encounters.All.Count * WaveSchedule.WavesPerEncounter, true)));
        }
    }
}
