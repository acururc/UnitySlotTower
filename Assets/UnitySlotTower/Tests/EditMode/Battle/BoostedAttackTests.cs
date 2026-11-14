using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Tests.EditMode.Slot;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("FR-042, plan 16.4: バーの攻撃力の倍率が戦闘のダメージに効く（攻撃速度は変わらない）")]
    public sealed class BoostedAttackTests
    {
        private static Battlefield Create(string waveRows, int bars)
        {
            var boosts = new BattleBoosts(SlotTestRules.Create(barStep: 0.1, barCap: 0.5));
            for (var i = 0; i < bars; i++)
            {
                boosts.AddBar();
            }

            return new Battlefield(BattleFixtures.LoadWave(waveRows), BattleFixtures.LoadRanges(), 10,
                new SeededRandom(1), boosts);
        }

        private static void PlaceArcher(Battlefield field) =>
            field.AddUnit(BattleFixtures.LoadUnits().Get("archer"), new GridPoint(1, 1), Facing.Right);

        [TestCase(1, 1000 - (385 - 50))]
        [TestCase(3, 1000 - (455 - 50))]
        [Description("AC-042: 弓（攻撃力350、敵の防御50）の1発目。バー1回で 350×1.1=385、3回で 350×1.3=455")]
        public void Bar_Boost_Multiplies_The_Damage(int bars, int expectedHp)
        {
            var field = Create("1,0,melee,S1,g,0\n", bars);
            PlaceArcher(field);

            field.Advance(45);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(expectedHp));
        }

        [Test]
        [Description("plan 16.4: 掛け順は 強化後の攻撃力 × バッファー × バー（弓 350 × 1.5 × 1.2 = 630）")]
        public void Buffer_And_Bar_Multiply_Together()
        {
            var field = Create("1,0,melee,S1,g,0\n", 2);
            PlaceArcher(field);
            field.AddUnit(BattleFixtures.LoadUnits().Get("buffer"), new GridPoint(0, 1), Facing.Right);

            field.Advance(45);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - (630 - 50)));
        }

        [Test]
        [Description("plan 16.4: 攻撃速度はスキルだけが変える（バーがあっても弓は75ステップで1回だけ撃つ）")]
        public void Bar_Boost_Does_Not_Change_The_Attack_Interval()
        {
            var field = Create("1,0,melee,S1,g,0\n", 5);
            PlaceArcher(field);

            field.Advance(75);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - (525 - 50)));
        }
    }
}
