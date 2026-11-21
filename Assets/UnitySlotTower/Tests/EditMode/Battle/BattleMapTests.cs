using System.IO;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    public sealed class BattleMapTests
    {
        private const string Header = "map_id,path_id,is_air,step,x,y\n";
        private const string SmallMap = "S1,G,E\nH,X,G\n";

        private static BattleMap ProjectMap(string id) =>
            BattleMap.Load(id, File.ReadAllText(Path.Combine(TablePaths.Root, BattleMap.MapFileName(id))),
                CsvParser.Parse(BattleMap.PathsFileName,
                    File.ReadAllText(Path.Combine(TablePaths.Root, BattleMap.PathsFileName))));

        private static BattleMap Small(string pathRows) =>
            BattleMap.Load("m", SmallMap, CsvParser.Parse(BattleMap.PathsFileName, Header + pathRows));

        private static CsvError SmallError(string pathRows) =>
            Assert.Throws<CsvFormatException>(() => Small(pathRows)).Error;

        [TestCase("melee", 1)]
        [TestCase("heavy", 2)]
        [Description("AC-057: Either goal reduces the same life by life_damage, once per arrival")]
        public void Either_Goal_Reduces_The_Single_Life(string enemyId, int damage)
        {
            var map = BattleMap.Load("m", "S1,G,E\nH,H,H\nS2,G,E\n",
                CsvParser.Parse(BattleMap.PathsFileName, Header +
                    "m,upper,0,1,0,0\nm,upper,0,2,1,0\nm,upper,0,3,2,0\n" +
                    "m,lower,0,1,0,2\nm,lower,0,2,1,2\nm,lower,0,3,2,2\n"));
            var wave = WaveSchedule.Load(CsvParser.Parse(WaveSchedule.FileName, BattleFixtures.GroupWavesHeader +
                $"1,0,{enemyId},S1,upper,0,1,0\n1,5,{enemyId},S2,lower,0,1,0\n"),
                1, map, BattleFixtures.LoadEnemies());
            var field = new Battlefield(wave, BattleFixtures.LoadRanges(), 10, new SeededRandom(1));

            field.Advance(3 * SimulationClock.StepsPerSecond);
            Assert.That((field.Life, field.ArrivedCount), Is.EqualTo((10 - damage, 1)));
            field.Advance(5 * SimulationClock.StepsPerSecond);
            Assert.That((field.Life, field.ArrivedCount), Is.EqualTo((10 - 2 * damage, 2)));
            field.Advance(3 * SimulationClock.StepsPerSecond);
            Assert.That((field.Life, field.ArrivedCount), Is.EqualTo((10 - 2 * damage, 2)));
        }

        private static readonly string[] ProjectMapIds = { "first", "battle_a", "battle_b", "battle_c", "battle_d", "battle_e", "elite_a", "boss_a" };

        [TestCase("battle_d", 1)]
        [TestCase("battle_e", 2)]
        [Description("AC-057: Normal battle maps have two independent approaches and validated waves")]
        public void New_Battle_Maps_Have_Nonmerging_Paths_And_Validate(string id, int goalCount)
        {
            var files = TablePaths.ReadAll();
            Assert.That(GameDataValidator.ValidateAll(files).Errors, Is.Empty);
            var map = ProjectMap(id);
            var encounter = EncounterTable.Load(CsvParser.Parse(EncounterTable.FileName,
                files[EncounterTable.FileName])).Get(id);
            Assert.That((encounter.Kind, encounter.MapId), Is.EqualTo((EncounterKind.Battle, id)));
            var cells = Enumerable.Range(0, map.Width).SelectMany(x =>
                Enumerable.Range(0, map.Height).Select(y => new GridPoint(x, y))).ToArray();
            Assert.That(cells.Count(c => map.TileAt(c) == MapTile.Spawn), Is.EqualTo(2));
            Assert.That(cells.Count(c => map.TileAt(c) == MapTile.Goal), Is.EqualTo(goalCount));
            var ground = map.Paths.Where(p => !p.IsAir).ToArray();
            Assert.That(ground.Length, Is.EqualTo(2));
            Assert.That(ground.Select(p => p.Cells[0]).Distinct().Count(), Is.EqualTo(2));
            Assert.That(ground.Select(p => p.Cells.Last()).Distinct().Count(), Is.EqualTo(goalCount));
            if (id == "battle_d")
            {
                Assert.That(ground.Select(p => p.Cells[0].X), Is.EquivalentTo(new[] { 0, map.Width - 1 }));
                Assert.That(ground[0].Cells.Last(), Is.EqualTo(ground[1].Cells.Last()));
            }
            else
            {
                Assert.That(ground[0].Cells.Max(c => c.Y), Is.LessThan(ground[1].Cells.Min(c => c.Y)));
            }
            for (var i = 0; i < ground.Length; i++)
            {
                for (var j = i + 1; j < ground.Length; j++)
                {
                    Assert.That(ground[i].Cells.Intersect(ground[j].Cells)
                        .Where(c => map.TileAt(c) != MapTile.Spawn && map.TileAt(c) != MapTile.Goal), Is.Empty);
                }
            }
            var stage = StageTable.Load(CsvParser.Parse(StageTable.FileName, files[StageTable.FileName])).FindByMap(id);
            var wave = WaveSchedule.Load(CsvParser.Parse(WaveSchedule.FileName, files[WaveSchedule.FileName]),
                stage.Number, map, EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, files[EnemyTable.FileName])),
                CsvParser.Parse(WaveSchedule.StageWavesFileName, files[WaveSchedule.StageWavesFileName]));
            Assert.That(wave.Spawns.Select(s => s.Path.Id).Distinct(),
                Is.EquivalentTo(map.Paths.Select(p => p.Id)));
        }

        [TestCase("melee")]
        [TestCase("heavy")]
        [Description("AC-057: Both battle_e goals apply the same data-defined damage to one life pool")]
        public void Battle_E_Goals_Apply_Equal_Data_Damage(string enemyId)
        {
            var map = ProjectMap("battle_e");
            var enemies = EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName,
                File.ReadAllText(Path.Combine(TablePaths.Root, EnemyTable.FileName))));
            var ground = map.Paths.Where(p => !p.IsAir).ToArray();
            var rows = string.Join("\n", ground.Select((p, i) =>
                $"1,{i * 120},{enemyId},S{map.SpawnNumberAt(p.Cells[0])},{p.Id},0,1,0"));
            var wave = WaveSchedule.Load(CsvParser.Parse(WaveSchedule.FileName,
                BattleFixtures.GroupWavesHeader + rows), 1, map, enemies);
            var field = new Battlefield(wave, BattleFixtures.LoadRanges(), 100, new SeededRandom(1));
            var damage = enemies.Get(enemyId).LifeDamage;

            field.Advance(120 * SimulationClock.StepsPerSecond);
            Assert.That((field.Life, field.ArrivedCount), Is.EqualTo((100 - damage, 1)));
            field.Advance(120 * SimulationClock.StepsPerSecond);
            Assert.That((field.Life, field.ArrivedCount), Is.EqualTo((100 - 2 * damage, 2)));
        }

        [Test]
        [Description("D-71・plan 16.6: マップは横9〜10×縦4〜5")]
        public void Project_Maps_Are_9_To_10_By_4_To_5()
        {
            var sizes = ProjectMapIds.Select(ProjectMap).Select(m => (m.Width, m.Height));

            Assert.That(sizes, Is.EqualTo(new[] { (10, 5), (10, 5), (9, 5), (10, 4), (9, 5), (10, 5), (10, 5), (10, 5) }));
        }

        [Test]
        public void Project_First_Reads_Tiles_From_The_Grid()
        {
            var map = ProjectMap("first");

            Assert.That(new[] { (0, 0), (0, 1), (1, 2), (9, 3), (9, 4) }.Select(p => map.TileAt(new GridPoint(p.Item1, p.Item2))),
                Is.EqualTo(new[] { MapTile.HighGround, MapTile.Spawn, MapTile.Ground, MapTile.Goal, MapTile.HighGround }));
        }

        [Test]
        [Description("AC-014・D-71: 最初の戦闘は中3行が出現口からゴールまでまっすぐな地上（3列一直線）")]
        public void Project_First_Has_Three_Straight_Ground_Lanes()
        {
            var map = ProjectMap("first");
            var lanes = map.Paths.Select(p => (p.IsAir, map.SpawnNumberAt(p.Cells[0]), p.Cells.Select(c => c.Y).Distinct().Single(),
                p.Cells.Count, map.TileAt(p.Cells[p.Cells.Count - 1])));

            Assert.That(lanes, Is.EqualTo(new[]
            {
                (false, 1, 1, 10, MapTile.Goal), (false, 2, 2, 10, MapTile.Goal), (false, 3, 3, 10, MapTile.Goal),
            }));
        }

        [Test]
        [Description("plan 16.6: 最初の戦闘以外のマップは空中路が1本ずつ（地上路の数はマップごと）")]
        public void Project_Maps_After_First_Have_Ground_Paths_And_One_Air_Path()
        {
            var counts = ProjectMapIds.Skip(1).Select(ProjectMap)
                .Select(m => (m.Paths.Count(p => !p.IsAir), m.Paths.Count(p => p.IsAir)));

            Assert.That(counts, Is.EqualTo(new[] { (1, 1), (2, 1), (1, 1), (2, 1), (2, 1), (2, 1), (3, 1) }));
        }

        [Test]
        public void Path_Cells_Follow_Step_Order_Not_Row_Order()
        {
            var map = Small("m,p1,0,3,2,0\nm,p1,0,1,0,0\nm,p1,0,2,1,0\n");

            Assert.That(map.FindPath("p1").Cells, Is.EqualTo(new[] { new GridPoint(0, 0), new GridPoint(1, 0), new GridPoint(2, 0) }));
        }

        [Test]
        public void Paths_Of_Other_Maps_Are_Ignored()
        {
            var map = Small("m,p1,0,1,0,0\nm,p1,0,2,1,0\nm,p1,0,3,2,0\nother,q1,0,1,5,5\n");

            Assert.That(map.Paths.Select(p => p.Id), Is.EqualTo(new[] { "p1" }));
        }

        [Test]
        [Description("FR-023: 地上経路は高台・通行不可マスを通れない")]
        public void Ground_Path_Through_High_Ground_Is_An_Error()
        {
            var error = SmallError("m,p1,0,1,0,0\nm,p1,0,2,0,1\nm,p1,0,3,1,1\nm,p1,0,4,2,1\nm,p1,0,5,2,0\n");

            Assert.That((error.File, error.Line), Is.EqualTo(("map_paths.csv", 3)));
        }

        [Test]
        [Description("plan 1章: 空中敵は専用経路で、高台・通行不可マスの上も飛べる")]
        public void Air_Path_May_Cross_High_Ground_And_Blocked_Tiles()
        {
            var map = Small("m,a1,1,1,0,0\nm,a1,1,2,0,1\nm,a1,1,3,1,1\nm,a1,1,4,2,1\nm,a1,1,5,2,0\n");

            Assert.That(map.FindPath("a1").IsAir, Is.True);
        }

        [Test]
        public void Path_Outside_The_Grid_Is_An_Error()
        {
            var error = SmallError("m,p1,0,1,0,0\nm,p1,0,2,1,0\nm,p1,0,3,2,0\nm,p1,0,4,3,0\n");

            Assert.That(error.Line, Is.EqualTo(5));
        }

        [Test]
        public void Path_Not_Starting_At_A_Spawn_Is_An_Error()
        {
            var error = SmallError("m,p1,0,1,1,0\nm,p1,0,2,2,0\n");

            Assert.That(error.Line, Is.EqualTo(2));
        }

        [Test]
        public void Path_Not_Ending_At_The_Goal_Is_An_Error()
        {
            var error = SmallError("m,p1,0,1,0,0\nm,p1,0,2,1,0\n");

            Assert.That(error.Line, Is.EqualTo(3));
        }

        [Test]
        public void Map_Without_Paths_Is_An_Error()
        {
            var error = SmallError("other,q1,0,1,0,0\n");

            Assert.That(error.File, Is.EqualTo("map_paths.csv"));
        }

        [Test]
        [Description("C-1: 経路の不正は全CSV検証でも見つかる")]
        public void Project_Validation_Reports_A_Ground_Path_Over_High_Ground()
        {
            var files = TablePaths.ReadAll();
            files["map_paths.csv"] += "s1,bad,0,1,0,1\ns1,bad,0,2,1,1\ns1,bad,0,3,1,0\n";

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("map_paths.csv"));
        }
    }
}
