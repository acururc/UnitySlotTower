using System.IO;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    public sealed class UnitTableTests
    {
        private const string Header = "id,max_hp,atk,def,attack_interval,block,tile,cost,cooldown\n";

        private static UnitTable Load(string text) => UnitTable.Load(CsvParser.Parse(UnitTable.FileName, text));

        [Test]
        public void Row_Is_Read_Into_A_Definition()
        {
            var unit = Load(Header + "tank,1800,220,300,1.2,3,ground,15,20\n").Get("tank");

            Assert.That((unit.MaxHp, unit.Atk, unit.Def, unit.AttackInterval, unit.Block, unit.Tile, unit.Cost,
                    unit.Cooldown),
                Is.EqualTo((1800, 220, 300, 1.2, 3, UnitTile.Ground, 15, 20.0)));
        }

        [Test]
        public void High_Tile_Is_Read()
        {
            Assert.That(Load(Header + "archer,700,350,50,1,0,high,14,15\n").Get("archer").Tile,
                Is.EqualTo(UnitTile.High));
        }

        [Test]
        public void Unknown_Tile_Reports_Its_Line()
        {
            var ex = Assert.Throws<CsvFormatException>(() =>
                Load(Header + "tank,1800,220,300,1.2,3,ground,15,20\narcher,700,350,50,1,0,sky,14,15\n"));
            Assert.That(ex.Error.Line, Is.EqualTo(3));
        }

        [Test]
        public void Duplicate_Id_Is_Rejected()
        {
            Assert.Throws<CsvFormatException>(() =>
                Load(Header + "tank,1800,220,300,1.2,3,ground,15,20\ntank,1,1,1,1,1,ground,1,1\n"));
        }

        [Test]
        public void Unknown_Id_Is_Reported_By_Name()
        {
            var ex = Assert.Throws<CsvFormatException>(() =>
                Load(Header + "tank,1800,220,300,1.2,3,ground,15,20\n").Get("dragon"));
            Assert.That(ex.Error.Message, Does.Contain("dragon"));
        }

        [Test]
        [Description("plan 5章: コスト 15/12/14/18/18、待ち時間 20/12/15/25/25、近距離は地上・遠距離は高台")]
        public void Project_Table_Holds_Five_Units_With_Planned_Cost_Cooldown_And_Tile()
        {
            var text = File.ReadAllText(Path.Combine(TablePaths.Root, UnitTable.FileName));

            var units = Load(text).All.Select(u => (u.Id, u.Cost, u.Cooldown, u.Tile)).ToArray();

            Assert.That(units, Is.EqualTo(new[]
            {
                ("tank", 15, 20.0, UnitTile.Ground),
                ("sword", 12, 12.0, UnitTile.Ground),
                ("archer", 14, 15.0, UnitTile.High),
                ("healer", 18, 25.0, UnitTile.High),
                ("buffer", 18, 25.0, UnitTile.High),
            }));
        }
    }
}
