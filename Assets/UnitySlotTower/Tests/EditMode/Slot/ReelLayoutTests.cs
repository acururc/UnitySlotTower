using NUnit.Framework;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class ReelLayoutTests
    {
        [Test]
        public void Cell_Index_Wraps_Around()
        {
            var layout = new ReelLayout(new[]
            {
                SlotSymbol.Red7, SlotSymbol.Blue7, SlotSymbol.Green7, SlotSymbol.Yellow7, SlotSymbol.Bar, SlotSymbol.Coin,
            });

            Assert.That((layout.At(-1), layout.At(6), layout.At(4)),
                Is.EqualTo((SlotSymbol.Coin, SlotSymbol.Red7, SlotSymbol.Bar)));
        }
    }
}
