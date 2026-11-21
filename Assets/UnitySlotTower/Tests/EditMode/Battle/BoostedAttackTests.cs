using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Slot;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("FR-112: バーの攻撃力の倍率（bar_attack_rate=2）が戦闘のダメージに効く（攻撃速度は変わらない）")]
    public sealed class BoostedAttackTests
    {
        private static Battlefield Create(string waveRows, bool bar)
        {
            var effects = new SlotEffects(SlotTestRules.Create());
            var field = new Battlefield(BattleFixtures.LoadWave(waveRows), BattleFixtures.LoadRanges(), 10,
                new SeededRandom(1), effects);
            if (bar)
            {
                field.ApplySlot(SlotSymbol.Bar);
            }

            return field;
        }

        private static void PlaceArcher(Battlefield field) =>
            field.AddUnit(BattleFixtures.LoadUnits().Get("archer"), new GridPoint(1, 1), Facing.Right);

        [TestCase(false, 1000 - (350 - 50))]
        [TestCase(true, 1000 - (700 - 50))]
        [Description("AC-109: 弓（攻撃力350、敵の防御50）の1発目。バーの効果中は 350×2=700")]
        public void Bar_Multiplies_The_Damage(bool bar, int expectedHp)
        {
            var field = Create("0,melee,S1,g,0\n", bar);
            PlaceArcher(field);

            field.Advance(45);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(expectedHp));
        }

        [Test]
        [Description("掛け順は 強化後の攻撃力 × バッファー × バー（弓 350 × 1.2 × 2 = 840）")]
        public void Buffer_And_Bar_Multiply_Together()
        {
            var field = Create("0,melee,S1,g,0\n", true);
            PlaceArcher(field);
            field.AddUnit(BattleFixtures.LoadUnits().Get("weak_buffer"), new GridPoint(0, 1), Facing.Right);

            field.Advance(45);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - (840 - 50)));
        }

        [Test]
        [Description("バーがあっても攻撃速度は変わらない（弓は75ステップで1回だけ撃つ）")]
        public void Bar_Does_Not_Change_The_Attack_Interval()
        {
            var field = Create("0,melee,S1,g,0\n", true);
            PlaceArcher(field);

            field.Advance(75);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - (700 - 50)));
        }

        [Test]
        [Description("AC-109: バーの効果は bar_seconds（10秒=600ステップ）で切れ、攻撃力が元に戻る")]
        public void Bar_Ends_After_Its_Duration()
        {
            var field = Create("0,quaker,S1,g,0\n", true);

            field.Advance(599);
            var during = (field.Effects.AttackRate, field.Effects.IsActive(SlotSymbol.Bar));
            field.Advance(1);

            Assert.That((during, field.Effects.AttackRate, field.Effects.IsActive(SlotSymbol.Bar)),
                Is.EqualTo(((2.0, true), 1.0, false)));
        }
    }
}
