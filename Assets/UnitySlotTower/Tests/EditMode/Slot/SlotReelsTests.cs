using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class SlotReelsTests
    {
        [Test]
        [Description("FR-041, plan 16.3: プロジェクトの並びは決まり（各図柄1つずつ・中段に揃えても上段と下段が揃わない）を満たす")]
        public void Project_Reels_Have_No_Problems()
        {
            var reels = SlotReels.Load(SlotTestRules.ProjectTable(SlotReels.FileName));

            Assert.That(reels.Problems(), Is.Empty);
        }

        [Test]
        [Description("plan 16.3: プロジェクトの右リールは 赤・バー・黄・緑・青・コイン")]
        public void Project_Right_Reel_Has_The_Planned_Order()
        {
            var reels = SlotReels.Load(SlotTestRules.ProjectTable(SlotReels.FileName));

            Assert.That(new[] { reels[2].At(0), reels[2].At(1), reels[2].At(2), reels[2].At(3), reels[2].At(4), reels[2].At(5) },
                Is.EqualTo(new[]
                {
                    SlotSymbol.Red7, SlotSymbol.Bar, SlotSymbol.Yellow7, SlotSymbol.Green7, SlotSymbol.Blue7, SlotSymbol.Coin,
                }));
        }

        [Test]
        public void Position_Of_A_Symbol_Is_Its_Index()
        {
            var reels = SlotTestRules.Reels();

            Assert.That((reels.PositionOf(0, SlotSymbol.Bar), reels.PositionOf(1, SlotSymbol.Bar)), Is.EqualTo((4, 2)));
        }

        [Test]
        [Description("D-62: 3本が同じ並びだと中段に揃えたとき上段・下段も揃う")]
        public void Identical_Reels_Are_A_Problem()
        {
            var reels = SlotTestRules.Reels(SlotTestRules.PlanLeft, SlotTestRules.PlanLeft, SlotTestRules.PlanLeft);

            Assert.That(reels.Problems(), Is.Not.Empty);
        }

        [Test]
        [Description("D-62: 図柄が重なるリール（コインが2つ・バーがない）は決まりに合わない")]
        public void Reel_With_A_Duplicate_Symbol_Is_A_Problem()
        {
            var reels = SlotTestRules.Reels("red7 blue7 green7 yellow7 coin coin");

            Assert.That(reels.Problems(), Is.Not.Empty);
        }

        [Test]
        [Description("FR-041: 赤7を中段に揃えると上段（コイン・バー・コイン）は揃わないが下段（青・青・青）が揃う並びは決まりに合わない")]
        public void Reels_Whose_Bottom_Row_Aligns_Are_A_Problem()
        {
            var reels = SlotTestRules.Reels(SlotTestRules.PlanLeft, "red7 blue7 green7 yellow7 coin bar", SlotTestRules.PlanLeft);

            Assert.That(reels.Problems(), Is.Not.Empty);
        }

        [Test]
        public void Missing_Cell_Is_Rejected()
        {
            var csv = SlotTestRules.ReelsCsv(SlotTestRules.PlanLeft, SlotTestRules.PlanCenter, "red7 bar yellow7 green7 blue7");

            Assert.Throws<CsvFormatException>(() => SlotReels.Load(CsvParser.Parse(SlotReels.FileName, csv)));
        }

        [Test]
        public void Reel_Out_Of_Range_Is_Rejected()
        {
            var csv = SlotTestRules.ReelsCsv(SlotTestRules.PlanLeft, SlotTestRules.PlanCenter, SlotTestRules.PlanRight) +
                "3,0,coin\n";

            Assert.Throws<CsvFormatException>(() => SlotReels.Load(CsvParser.Parse(SlotReels.FileName, csv)));
        }
    }
}
