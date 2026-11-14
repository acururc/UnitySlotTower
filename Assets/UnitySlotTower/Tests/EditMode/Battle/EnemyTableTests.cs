using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    public sealed class EnemyTableTests
    {
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
        [Description("plan 16.2: コインと所持金の率の和が 1 を超える行は読み込みエラー（その行を報告）")]
        public void Drop_Rates_Summing_Over_One_Are_A_Load_Error()
        {
            // 4 行目の air（0.5 + 0.3）の所持金率を 0.6 にして和を 1.1 にする。
            var text = BattleFixtures.Enemies.Replace("0.5,1,2,0.3,4,5", "0.5,1,2,0.6,4,5");

            var ex = Assert.Throws<CsvFormatException>(() => EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, text)));

            Assert.That(ex.Error.Line, Is.EqualTo(4));
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
        [Description("plan 16.2: ドロップは 近 コイン20%で3／所持金40%で2、遠・空 コイン25%で3／所持金40%で2〜3")]
        public void Project_Grunt_Drops_Follow_Plan()
        {
            var enemies = Project();
            var drops = new[] { "melee", "ranged", "air" };
            var actual = new (double, int, int, double, int, int)[drops.Length];
            for (var i = 0; i < drops.Length; i++)
            {
                var e = enemies.Get(drops[i]);
                actual[i] = (e.CoinRate, e.CoinMin, e.CoinMax, e.MoneyRate, e.MoneyMin, e.MoneyMax);
            }

            Assert.That(actual,
                Is.EqualTo(new[] { (0.2, 3, 3, 0.4, 2, 2), (0.25, 3, 3, 0.4, 2, 3), (0.25, 3, 3, 0.4, 2, 3) }));
        }
    }
}
