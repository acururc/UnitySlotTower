using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("FR-029, plan 16.4: skills.csv の読み込み（色と職の割り当て・効果の値）")]
    public sealed class SkillTableTests
    {
        [Test]
        public void Each_Color_Has_Its_Unit_And_Values()
        {
            var skill = BattleFixtures.LoadSkills().For(SlotSymbol.Blue7);

            Assert.That((skill.UnitId, skill.DurationSteps, skill.IntervalRate), Is.EqualTo(("archer", 480, 0.4)));
        }

        [Test]
        public void Duplicate_Color_Is_An_Error()
        {
            var csv = BattleFixtures.Skills + "red7,tank,1,0,1,0,1,0\n";

            Assert.Throws<CsvFormatException>(() => BattleFixtures.LoadSkills(csv));
        }

        [Test]
        public void Non_Color_Symbol_Is_An_Error()
        {
            var csv = BattleFixtures.Skills.Replace("red7,sword", "bar,sword");

            Assert.Throws<CsvFormatException>(() => BattleFixtures.LoadSkills(csv));
        }

        [Test]
        public void Missing_Color_Is_An_Error()
        {
            var csv = BattleFixtures.Skills.Replace("green7,buffer,10,0,1,0,1,0.3\n", "");

            Assert.Throws<CsvFormatException>(() => BattleFixtures.LoadSkills(csv));
        }
    }
}
