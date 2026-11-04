using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    /// <summary>一時停止中の説明に出す確率（T404、FR-012）。</summary>
    public sealed class SlotOddsTests
    {
        private static ReelLayout Layout(string ids) =>
            new ReelLayout(ids.Split(' ').Select(id => (SlotSymbol)System.Array.IndexOf(SlotSymbols.Ids, id)).ToArray());

        [Test]
        public void Miss_Is_Shown_In_Tenths_Of_Percent()
        {
            Assert.That(SlotOdds.MissTenths(0.79), Is.EqualTo(790));
        }

        [TestCase(SlotSymbol.Seven, 18, TestName = "Seven_Is_21_Percent_Of_One_Twelfth")]
        [TestCase(SlotSymbol.Coin, 70, TestName = "Coin_Is_21_Percent_Of_Four_Twelfths")]
        [TestCase(SlotSymbol.Atk, 35, TestName = "Atk_Is_21_Percent_Of_Two_Twelfths")]
        public void Chance_Is_Hit_Rate_Times_Symbol_Ratio(SlotSymbol symbol, int tenths)
        {
            // 既定配列（7・BAR 各1、攻・速・回 各2、コイン 4）で設定3（ハズレ 79%）。0.21 × 1/12 = 1.75% → 1.8%。
            var layout = Layout("seven atk coin heal speed coin bar atk coin heal speed coin");

            Assert.That(SlotOdds.ChanceTenths(layout, 0.79, symbol), Is.EqualTo(tenths));
        }

        [Test]
        public void Chance_Follows_The_Customized_Layout()
        {
            var layout = Layout("seven coin coin coin coin coin bar atk coin heal speed coin");

            // コイン 7 マス: 0.3 × 7/12 = 17.5%。
            Assert.That(SlotOdds.ChanceTenths(layout, 0.7, SlotSymbol.Coin), Is.EqualTo(175));
        }
    }
}
