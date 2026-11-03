using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class SlotTableTests
    {
        private static CsvTable ProjectTable(string fileName) =>
            CsvParser.Parse(fileName, File.ReadAllText(Path.Combine(TablePaths.Root, fileName)));

        [Test]
        [Description("plan 5章: ハズレ率（設定1〜6）は 85 / 82 / 79 / 76 / 73 / 70%")]
        public void Project_Settings_Have_The_Planned_Miss_Rates()
        {
            var settings = SlotSettings.Load(ProjectTable(SlotSettings.FileName));

            Assert.That(new[] { 1, 2, 3, 4, 5, 6 }, Has.All.Matches<int>(
                s => System.Math.Abs(settings.MissRate(s) - (0.85 - 0.03 * (s - 1))) < 1e-9));
        }

        [Test]
        public void Settings_Without_Every_Setting_Are_Rejected()
        {
            var table = CsvParser.Parse(SlotSettings.FileName, "setting,miss_rate\n1,0.8\n2,0.7\n");

            Assert.Throws<CsvFormatException>(() => SlotSettings.Load(table));
        }

        [Test]
        [Description("plan 5章: 12コマ、コイン15枚、BIG 12G×10枚・REG 6G×8枚、1.5倍・20秒、回復50%")]
        public void Project_Rules_Have_The_Planned_Values()
        {
            var rules = SlotRules.Load(ProjectTable(SlotRules.FileName));

            Assert.That(new object[]
                {
                    rules.ReelLength, rules.WaitSeconds, rules.CoinPayout, rules.BigGames, rules.BigPayout, rules.RegGames,
                    rules.RegPayout, rules.AtkRate, rules.AtkSeconds, rules.SpeedRate, rules.SpeedSeconds, rules.HealRate,
                },
                Is.EqualTo(new object[] { 12, 2.0, 15, 12, 10, 6, 8, 1.5, 20.0, 1.5, 20.0, 0.5 }));
        }

        [Test]
        [Description("AC-043, D-40: REG の払い出しの合計（ゲーム数×1ゲームの払い出し）は BIG より小さい")]
        public void Project_Reg_Pays_Less_Than_Big()
        {
            var rules = SlotRules.Load(ProjectTable(SlotRules.FileName));

            Assert.That(rules.RegGames * rules.RegPayout, Is.LessThan(rules.BigGames * rules.BigPayout));
        }

        [Test]
        [Description("D-46: 既定の配列はルールのマス数と配列の条件を満たす")]
        public void Project_Default_Reel_Is_Valid_For_The_Rules()
        {
            var rules = SlotRules.Load(ProjectTable(SlotRules.FileName));
            var layout = ReelLayout.Load(ProjectTable(ReelLayout.FileName));

            Assert.That(layout.IsValid(rules.ReelLength), Is.True);
        }
    }
}
