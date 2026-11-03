using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class ReelLayoutTests
    {
        private const int Length = 12;

        /// <summary>plan 5章の既定配列: 7・攻・コ・回・速・コ・BAR・攻・コ・回・速・コ。</summary>
        private static ReelLayout Default() => Layout("seven atk coin heal speed coin bar atk coin heal speed coin");

        private static ReelLayout Layout(string ids) =>
            new ReelLayout(ids.Split(' ').Select(id => (SlotSymbol)System.Array.IndexOf(SlotSymbols.Ids, id)).ToArray());

        [Test]
        [Description("AC-044: 7・BAR ちょうど1つ、他4図柄各1以上、総マス数一致なら確定できる")]
        public void Default_Layout_Is_Valid()
        {
            Assert.That(Default().IsValid(Length), Is.True);
        }

        [TestCase("seven seven coin heal speed coin bar atk coin heal speed coin", TestName = "Two_Sevens")]
        [TestCase("atk atk coin heal speed coin bar atk coin heal speed coin", TestName = "No_Seven")]
        [TestCase("seven atk coin heal speed coin bar bar coin heal speed coin", TestName = "Two_Bars")]
        [TestCase("seven atk coin heal coin coin bar atk coin heal coin coin", TestName = "No_Speed")]
        [TestCase("seven atk coin heal speed coin bar atk coin heal speed", TestName = "Eleven_Cells")]
        [TestCase("seven atk coin heal speed coin bar atk coin heal speed coin coin", TestName = "Thirteen_Cells")]
        [Description("AC-044: 条件を満たさない配列は確定できない")]
        public void Layout_Breaking_A_Condition_Is_Invalid(string ids)
        {
            Assert.That(Layout(ids).IsValid(Length), Is.False);
        }

        [Test]
        [Description("AC-045, D-43: 当たり時の各図柄の確率はマス数 ÷ 総マス数（既定配列のコイン 4/12）")]
        public void Ratio_Is_Cells_Over_Length()
        {
            var layout = Default();

            Assert.That((layout.Ratio(SlotSymbol.Coin), layout.Ratio(SlotSymbol.Seven), layout.Ratio(SlotSymbol.Atk)),
                Is.EqualTo((4.0 / 12, 1.0 / 12, 2.0 / 12)));
        }

        [Test]
        public void Cell_Index_Wraps_Around()
        {
            var layout = Default();

            Assert.That((layout.At(-1), layout.At(12), layout.At(6)),
                Is.EqualTo((SlotSymbol.Coin, SlotSymbol.Seven, SlotSymbol.Bar)));
        }

        [Test]
        public void Load_Reads_Index_And_Symbol_Rows_In_Index_Order()
        {
            var layout = ReelLayout.Load(CsvParser.Parse(ReelLayout.FileName, "index,symbol\n1,bar\n0,seven\n2,coin\n"));

            Assert.That(Enumerable.Range(0, layout.Length).Select(layout.At),
                Is.EqualTo(new[] { SlotSymbol.Seven, SlotSymbol.Bar, SlotSymbol.Coin }));
        }

        [Test]
        public void Load_Rejects_A_Missing_Index_With_The_File_Name()
        {
            var error = Assert.Throws<CsvFormatException>(() =>
                ReelLayout.Load(CsvParser.Parse(ReelLayout.FileName, "index,symbol\n0,seven\n2,coin\n")));

            Assert.That(error.Error.File, Is.EqualTo(ReelLayout.FileName));
        }
    }
}
