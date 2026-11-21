using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class SlotPatternsTests
    {
        private static SlotPatterns Project() => SlotPatterns.Load(SlotTestRules.ProjectTable(SlotPatterns.FileName));

        private static SlotPatterns Load(string rows) =>
            SlotPatterns.Load(CsvParser.Parse(SlotPatterns.FileName, "outcome,count\n" + rows));

        [Test]
        public void Project_Table_Has_The_Planned_Counts()
        {
            var table = Project();
            Assert.That(table.Patterns.Count, Is.EqualTo(20));
            Assert.That(Enumerable.Range(0, SlotSymbols.Count).Select(i => table.Count((SlotSymbol)i)),
                Is.EqualTo(new[] { 3, 3, 3, 3, 4, 4 }));
            Assert.That(table.Count(null), Is.Zero);
        }

        [Test]
        public void Patterns_Start_With_Misses_Then_Follow_Symbol_Order()
        {
            var table = Load("coin,1\nred7,1\nmiss,18\n");
            Assert.That(table.Patterns, Is.EqualTo(Enumerable.Repeat<SlotSymbol?>(null, 18)
                .Concat(new SlotSymbol?[] { SlotSymbol.Red7, SlotSymbol.Coin })));
        }

        [TestCase("coin,5\ncoin,15")]
        [TestCase("coin,-1\nred7,21")]
        [TestCase("miss,19")]
        [TestCase("seven,20")]
        public void Invalid_Table_Is_Rejected(string rows)
        {
            Assert.Throws<CsvFormatException>(() => Load(rows));
        }
    }
}
