using System;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("FR-042, plan 16.4: 色7でその色のスキルゲージが1段たまる（5段で満タン）")]
    public sealed class SkillGaugesTests
    {
        [Test]
        public void Gauges_Start_Empty()
        {
            var gauges = new SkillGauges(5);

            Assert.That(gauges.Level(SlotSymbol.Red7), Is.EqualTo(0));
        }

        [Test]
        [Description("AC-042: 色7はその色のゲージだけを1段上げる")]
        public void Adding_A_Color_Raises_Only_That_Gauge()
        {
            var gauges = new SkillGauges(5);

            gauges.Add(SlotSymbol.Blue7);
            gauges.Add(SlotSymbol.Blue7);

            Assert.That((gauges.Level(SlotSymbol.Blue7), gauges.Level(SlotSymbol.Red7)), Is.EqualTo((2, 0)));
        }

        [Test]
        [Description("plan 16.4: 満タン（5段）ならそれ以上増えない")]
        public void A_Full_Gauge_Stays_At_Max()
        {
            var gauges = new SkillGauges(5);
            for (var i = 0; i < 7; i++)
            {
                gauges.Add(SlotSymbol.Green7);
            }

            Assert.That((gauges.Level(SlotSymbol.Green7), gauges.IsFull(SlotSymbol.Green7)), Is.EqualTo((5, true)));
        }

        [Test]
        public void A_Gauge_Below_Max_Is_Not_Full()
        {
            var gauges = new SkillGauges(5);
            for (var i = 0; i < 4; i++)
            {
                gauges.Add(SlotSymbol.Yellow7);
            }

            Assert.That(gauges.IsFull(SlotSymbol.Yellow7), Is.False);
        }

        [Test]
        [Description("plan 16.4: 発動でゲージは0に戻る")]
        public void Clearing_Empties_The_Gauge()
        {
            var gauges = new SkillGauges(5);
            gauges.Add(SlotSymbol.Red7);
            gauges.Add(SlotSymbol.Red7);

            gauges.Clear(SlotSymbol.Red7);

            Assert.That(gauges.Level(SlotSymbol.Red7), Is.EqualTo(0));
        }

        [TestCase(SlotSymbol.Bar)]
        [TestCase(SlotSymbol.Coin)]
        public void Non_Color_Symbols_Have_No_Gauge(SlotSymbol symbol)
        {
            var gauges = new SkillGauges(5);

            Assert.That(() => gauges.Add(symbol), Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
