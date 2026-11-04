using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class ReelLayoutTests
    {
        private static ReelLayout Layout(string ids) =>
            new ReelLayout(ids.Split(' ').Select(id => (SlotSymbol)System.Array.IndexOf(SlotSymbols.Ids, id)).ToArray());

        private static ReelLayout Sample() => Layout("seven atk coin heal speed coin bar atk coin heal speed coin");

        [Test]
        [Description("AC-045, D-43: 当たり時の各図柄の確率はマス数 ÷ 総マス数（コイン 4/12）")]
        public void Ratio_Is_Cells_Over_Length()
        {
            var layout = Sample();

            Assert.That((layout.Ratio(SlotSymbol.Coin), layout.Ratio(SlotSymbol.Seven), layout.Ratio(SlotSymbol.Atk)),
                Is.EqualTo((4.0 / 12, 1.0 / 12, 2.0 / 12)));
        }

        [Test]
        public void Cell_Index_Wraps_Around()
        {
            var layout = Sample();

            Assert.That((layout.At(-1), layout.At(12), layout.At(6)),
                Is.EqualTo((SlotSymbol.Coin, SlotSymbol.Seven, SlotSymbol.Bar)));
        }
    }
}
