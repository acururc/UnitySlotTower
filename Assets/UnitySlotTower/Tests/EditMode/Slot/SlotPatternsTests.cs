using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class SlotPatternsTests
    {
        private static SlotPatterns Project() => SlotPatterns.Load(SlotTestRules.ProjectTable(SlotPatterns.FileName));

        private static string Csv(string rows)
        {
            // 設定2〜6は全部ハズレ（20）で埋め、rows で設定1を書く。
            var csv = "setting,outcome,count\n" + rows;
            for (var s = 2; s <= 6; s++)
            {
                csv += $"{s},miss,20\n";
            }

            return csv;
        }

        private static SlotPatterns Load(string csv) => SlotPatterns.Load(CsvParser.Parse(SlotPatterns.FileName, csv));

        [Test]
        [Description("plan 16.3: 設定1は 色7 各3・ハズレ4・バー2・コイン2")]
        public void Project_Setting_1_Has_The_Planned_Counts()
        {
            var patterns = Project();

            Assert.That(new[]
                {
                    patterns.Count(1, SlotSymbol.Red7), patterns.Count(1, SlotSymbol.Blue7), patterns.Count(1, SlotSymbol.Green7),
                    patterns.Count(1, SlotSymbol.Yellow7), patterns.Count(1, null), patterns.Count(1, SlotSymbol.Bar),
                    patterns.Count(1, SlotSymbol.Coin),
                },
                Is.EqualTo(new[] { 3, 3, 3, 3, 4, 2, 2 }));
        }

        [Test]
        [Description("plan 16.3: 設定6は 色7 各2・ハズレ10・バー1・コイン1")]
        public void Project_Setting_6_Has_The_Planned_Counts()
        {
            var patterns = Project();

            Assert.That(new[]
                {
                    patterns.Count(6, SlotSymbol.Red7), patterns.Count(6, SlotSymbol.Yellow7), patterns.Count(6, null),
                    patterns.Count(6, SlotSymbol.Bar), patterns.Count(6, SlotSymbol.Coin),
                },
                Is.EqualTo(new[] { 2, 2, 10, 1, 1 }));
        }

        [Test]
        [Description("D-60: 各設定の表は20パターン")]
        public void Every_Project_Setting_Has_20_Patterns()
        {
            var patterns = Project();

            Assert.That(Enumerable.Range(1, 6).Select(s => patterns.For(s).Count), Has.All.EqualTo(20));
        }

        [Test]
        public void Patterns_List_Each_Outcome_As_Many_Times_As_Its_Count()
        {
            var patterns = Load(Csv("1,coin,5\n1,miss,15\n"));

            Assert.That((patterns.For(1).Count(o => o == SlotSymbol.Coin), patterns.For(1).Count(o => o == null)),
                Is.EqualTo((5, 15)));
        }

        [Test]
        [Description("D-60: 合計が20でない設定は読めない")]
        public void Setting_Not_Totalling_20_Is_Rejected()
        {
            Assert.Throws<CsvFormatException>(() => Load(Csv("1,coin,5\n1,miss,14\n")));
        }

        [Test]
        public void Missing_Setting_Is_Rejected()
        {
            Assert.Throws<CsvFormatException>(() => Load("setting,outcome,count\n1,miss,20\n"));
        }

        [Test]
        public void Unknown_Outcome_Is_Rejected()
        {
            Assert.Throws<CsvFormatException>(() => Load(Csv("1,seven,5\n1,miss,15\n")));
        }

        [Test]
        public void Duplicate_Outcome_In_A_Setting_Is_Rejected()
        {
            Assert.Throws<CsvFormatException>(() => Load(Csv("1,coin,5\n1,coin,5\n1,miss,10\n")));
        }
    }
}
