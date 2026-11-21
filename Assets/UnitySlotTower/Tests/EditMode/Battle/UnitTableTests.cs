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
        private const string Header = "id,max_hp,atk,def,attack_interval,block,tile,cost,cooldown,role,buff_rate\n";

        private static UnitTable Load(string text) => UnitTable.Load(CsvParser.Parse(UnitTable.FileName, text));

        [Test]
        public void Row_Is_Read_Into_A_Definition()
        {
            var unit = Load(Header + "tank,1800,220,300,1.2,3,ground,15,20,melee,1\n").Get("tank");

            Assert.That((unit.MaxHp, unit.Atk, unit.Def, unit.AttackInterval, unit.Block, unit.Tile, unit.Cost,
                    unit.Cooldown),
                Is.EqualTo((1800, 220, 300, 1.2, 3, UnitTile.Ground, 15, 20.0)));
        }

        [Test]
        public void High_Tile_Is_Read()
        {
            Assert.That(Load(Header + "archer,700,350,50,1,0,high,14,15,ranged,1\n").Get("archer").Tile,
                Is.EqualTo(UnitTile.High));
        }

        [Test]
        [Description("FR-028: バッファーは役割 buff と攻撃力の倍率を持つ")]
        public void Role_And_Buff_Rate_Are_Read()
        {
            var unit = Load(Header + "buffer,800,0,60,2.5,0,high,18,25,buff,1.3\n").Get("buffer");

            Assert.That((unit.Role, unit.BuffRate), Is.EqualTo((UnitRole.Buff, 1.3)));
        }

        [Test]
        [Description("AC-022: ヒーラーはいないので、役割 heal は読めない")]
        public void Heal_Role_Is_Rejected()
        {
            Assert.Throws<CsvFormatException>(() =>
                Load(Header + "healer,800,250,60,2.5,0,high,18,25,heal,1\n"));
        }

        [Test]
        public void Unknown_Role_Reports_Its_Line()
        {
            var ex = Assert.Throws<CsvFormatException>(() =>
                Load(Header + "tank,1800,220,300,1.2,3,ground,15,20,melee,1\nmage,700,350,50,1,0,high,14,15,magic,1\n"));
            Assert.That(ex.Error.Line, Is.EqualTo(3));
        }

        [Test]
        public void Unknown_Tile_Reports_Its_Line()
        {
            var ex = Assert.Throws<CsvFormatException>(() =>
                Load(Header + "tank,1800,220,300,1.2,3,ground,15,20,melee,1\narcher,700,350,50,1,0,sky,14,15,ranged,1\n"));
            Assert.That(ex.Error.Line, Is.EqualTo(3));
        }

        [Test]
        public void Duplicate_Id_Is_Rejected()
        {
            Assert.Throws<CsvFormatException>(() =>
                Load(Header + "tank,1800,220,300,1.2,3,ground,15,20,melee,1\ntank,1,1,1,1,1,ground,1,1,melee,1\n"));
        }

        [Test]
        public void Unknown_Id_Is_Reported_By_Name()
        {
            var ex = Assert.Throws<CsvFormatException>(() =>
                Load(Header + "tank,1800,220,300,1.2,3,ground,15,20,melee,1\n").Get("dragon"));
            Assert.That(ex.Error.Message, Does.Contain("dragon"));
        }

        [Test]
        [Description("FR-020, AC-022 / plan 5章: 味方は4種（ヒーラーはいない、バッファーは倍率 2、spec 002 plan 4章）。コスト 130/100/120/150 円（plan 17.1）、待ち時間 20/12/15/25、近距離は地上・遠距離は高台")]
        public void Project_Table_Holds_Four_Units_With_Planned_Cost_Cooldown_And_Tile()
        {
            var text = File.ReadAllText(Path.Combine(TablePaths.Root, UnitTable.FileName));

            var units = Load(text).All.Select(u => (u.Id, u.Cost, u.Cooldown, u.Tile, u.BuffRate)).ToArray();

            Assert.That(units, Is.EqualTo(new[]
            {
                ("tank", 130, 20.0, UnitTile.Ground, 1.0),
                ("sword", 100, 12.0, UnitTile.Ground, 1.0),
                ("archer", 120, 15.0, UnitTile.High, 1.0),
                ("buffer", 150, 25.0, UnitTile.High, 2.0),
            }));
        }
    }
}
