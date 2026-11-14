using System.IO;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
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

        private static readonly string[] ProjectMapIds = { "first", "battle_a", "battle_b", "battle_c", "elite_a", "boss_a" };

        [Test]
        [Description("D-71・plan 16.6: マップは横9〜10×縦4〜5")]
        public void Project_Maps_Are_9_To_10_By_4_To_5()
        {
            var sizes = ProjectMapIds.Select(ProjectMap).Select(m => (m.Width, m.Height));

            Assert.That(sizes, Is.EqualTo(new[] { (10, 5), (10, 5), (9, 5), (10, 4), (10, 5), (10, 5) }));
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

            Assert.That(counts, Is.EqualTo(new[] { (1, 1), (2, 1), (1, 1), (2, 1), (3, 1) }));
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
