using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    public sealed class EnemyTableTests
    {
        [Test]
        public void Missing_Ranged_Damage_Rate_Defaults_To_One()
        {
            Assert.That(BattleFixtures.LoadEnemies().Get("heavy").RangedDamageRate, Is.EqualTo(1));
        }

        [TestCase("0")]
        [TestCase("-0.1")]
        [TestCase("1.01")]
        [TestCase("NaN")]
        [TestCase("Infinity")]
        public void Invalid_Ranged_Damage_Rate_Is_A_Load_Error(string rate)
        {
            var csv = BattleFixtures.Enemies.TrimEnd().Split('\n');
            csv[0] += ",ranged_damage_rate";
            for (var i = 1; i < csv.Length; i++) csv[i] += "," + rate;
            var ex = Assert.Throws<CsvFormatException>(() =>
                EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, string.Join("\n", csv))));
            Assert.That((ex.Error.File, ex.Error.Line, ex.Error.Column), Is.EqualTo(("enemies.csv", 2, 20)));
        }

        [TestCase("0.4", 0.4)]
        [TestCase("1", 1)]
        [TestCase("0.0001", 0.0001)]
        public void Reads_Ranged_Damage_Rate(string rate, double expected)
        {
            var csv = BattleFixtures.Enemies.TrimEnd().Split('\n');
            csv[0] += ",ranged_damage_rate";
            for (var i = 1; i < csv.Length; i++) csv[i] += "," + rate;
            Assert.That(EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, string.Join("\n", csv)))
                .Get("heavy").RangedDamageRate, Is.EqualTo(expected));
        }

        private static EnemyTable Project() =>
            EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName,
                File.ReadAllText(Path.Combine(TablePaths.Root, EnemyTable.FileName))));

        [Test]
        public void Reads_Movement_And_Attack_Columns()
        {
            var ranged = BattleFixtures.LoadEnemies().Get("ranged");

            Assert.That((ranged.MaxHp, ranged.Atk, ranged.AttackInterval, ranged.MoveSpeed, ranged.Range, ranged.IsAir),
                Is.EqualTo((700, 300, 2.0, 0.5, 1.5, false)));
        }

        [Test]
        public void Reads_Coin_Drop_Columns()
        {
            var air = BattleFixtures.LoadEnemies().Get("air");

            Assert.That((air.IsAir, air.BlockCost, air.LifeDamage, air.CoinRate, air.CoinMin, air.CoinMax),
                Is.EqualTo((true, 1, 1, 0.5, 1, 2)));
        }

        [Test]
        [Description("plan 16.2: 所持金のドロップは別の列")]
        public void Reads_Money_Drop_Columns()
        {
            var air = BattleFixtures.LoadEnemies().Get("air");

            Assert.That((air.MoneyRate, air.MoneyMin, air.MoneyMax), Is.EqualTo((0.3, 4, 5)));
        }

        [Test]
        [Description("FR-107: コインと所持金は別々の抽選なので、率の和が 1 を超える行も読み込める")]
        public void Drop_Rates_Summing_Over_One_Load()
        {
            var grunt = BattleFixtures.LoadEnemies().Get("grunt");

            Assert.That((grunt.CoinRate, grunt.MoneyRate), Is.EqualTo((0.15, 1.0)));
        }

        [Test]
        public void Duplicate_Id_Is_A_Load_Error_At_Its_Line()
        {
            // 3 行目の ranged を melee に書き換えて、2 行目と重複させる。
            var text = BattleFixtures.Enemies.Replace("ranged,", "melee,");

            var ex = Assert.Throws<CsvFormatException>(() => EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, text)));

            Assert.That(ex.Error.Line, Is.EqualTo(3));
        }

        [Test]
        [Description("FR-030 / M2: 近距離・遠距離・空中の雑魚3種（ボス2種は M3）")]
        public void Project_Has_Melee_Ranged_And_Air_Grunts()
        {
            var enemies = Project();

            Assert.That((enemies.Get("melee").Range, enemies.Get("ranged").Range > 0, enemies.Get("air").IsAir),
                Is.EqualTo((0.0, true, true)));
        }

        [Test]
        [Description("FR-107、002 plan 2.4: 雑魚（近・遠・空・重）はコイン15%で1枚、所持金100%で50円（T617 で 30 から上げた）")]
        public void Project_Grunt_Drops_Follow_Plan()
        {
            var enemies = Project();
            var drops = new[] { "melee", "ranged", "air", "heavy" };
            var actual = new (double, int, int, double, int, int)[drops.Length];
            for (var i = 0; i < drops.Length; i++)
            {
                var e = enemies.Get(drops[i]);
                actual[i] = (e.CoinRate, e.CoinMin, e.CoinMax, e.MoneyRate, e.MoneyMin, e.MoneyMax);
            }

            Assert.That(actual,
                Is.EqualTo(new[] { (0.15, 1, 1, 1.0, 50, 50), (0.15, 1, 1, 1.0, 50, 50), (0.15, 1, 1, 1.0, 50, 50), (0.15, 1, 1, 1.0, 50, 50) }));
        }
    }
}
