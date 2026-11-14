using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("FR-029, plan 16.4: スキルの効果（AC-023 の Core 部分）")]
    public sealed class SkillTests
    {
        private static readonly UnitTable Units = BattleFixtures.LoadUnits();
        private static readonly SkillTable Skills = BattleFixtures.LoadSkills();

        private static BattleUnit Place(Battlefield field, string unitId, int x, int y) =>
            field.AddUnit(Units.Get(unitId), new GridPoint(x, y), Facing.Right);

        [Test]
        [Description("タンク（ブロック3）がスキルで4になり、ブロック消費2の敵を2体止める")]
        public void Tank_Skill_Adds_A_Block()
        {
            var field = BattleFixtures.CreateField("1,0,heavy,S1,g,0\n1,0.2,heavy,S1,g,0\n");
            var tank = Place(field, "tank", 1, 0);
            field.ActivateSkill(Skills.For(SlotSymbol.Yellow7));

            field.Advance(60);

            Assert.That(tank.BlockUsed, Is.EqualTo(4));
        }

        [Test]
        [Description("brute の一撃（1000 − 防御300 = 700）がスキル中は半分の350")]
        public void Tank_Skill_Halves_The_Damage_Taken()
        {
            var field = BattleFixtures.CreateField("1,0,brute,S1,g,0\n");
            var tank = Place(field, "tank", 1, 0);
            field.ActivateSkill(Skills.For(SlotSymbol.Yellow7));

            field.Advance(35);

            Assert.That(tank.Hp, Is.EqualTo(1800 - 350));
        }

        [Test]
        [Description("剣士（攻撃力400）が範囲（自マスと前1）の敵全員に 400×3 − 防御50 = 1150 を1回与え、範囲外の敵には当たらない")]
        public void Sword_Skill_Hits_Every_Enemy_In_Range_Once()
        {
            // 80 ステップ後: 1体目は 1.33 マス・2体目は 0.83 マス（範囲内）、3体目は 0.13 マス（範囲外のマス0）。
            var field = BattleFixtures.CreateField(
                "1,0,heavy,S1,g,0\n1,0.5,heavy,S1,g,0\n1,1.2,heavy,S1,g,0\n");
            Place(field, "sword", 1, 0);
            field.Advance(80);
            var before = field.Enemies.Select(e => e.Hp).ToArray();

            field.ActivateSkill(Skills.For(SlotSymbol.Red7));

            Assert.That(field.Enemies.Select(e => e.Hp),
                Is.EqualTo(new[] { before[0] - 1150, before[1] - 1150, before[2] }));
        }

        [Test]
        [Description("弓の攻撃間隔 60 ステップがスキル中は24ステップになり、75ステップで2発（350 − 50 = 300 ずつ）")]
        public void Archer_Skill_Shortens_The_Attack_Interval()
        {
            var field = BattleFixtures.CreateField("1,0,melee,S1,g,0\n");
            Place(field, "archer", 1, 1);
            field.ActivateSkill(Skills.For(SlotSymbol.Blue7));

            field.Advance(75);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - 2 * 300));
        }

        [Test]
        [Description("バッファーの倍率 1.5 がスキル中は1.8になり、弓は 350×1.8 = 630")]
        public void Buffer_Skill_Raises_The_Buff_Rate()
        {
            var field = BattleFixtures.CreateField("1,0,melee,S1,g,0\n");
            Place(field, "archer", 1, 1);
            Place(field, "buffer", 0, 1);
            field.ActivateSkill(Skills.For(SlotSymbol.Green7));

            field.Advance(45);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - (630 - 50)));
        }

        [Test]
        [Description("plan 16.4: 発動時にいたタンクだけに付き、あとから置いたタンクは brute の一撃700をそのまま受ける")]
        public void Skill_Does_Not_Apply_To_Units_Placed_Afterwards()
        {
            var field = BattleFixtures.CreateField("1,0,brute,S1,g,0\n");
            Place(field, "tank", 0, 1);
            field.ActivateSkill(Skills.For(SlotSymbol.Yellow7));
            var later = Place(field, "tank", 1, 0);

            field.Advance(35);

            Assert.That(later.Hp, Is.EqualTo(1800 - 700));
        }

        [Test]
        public void Skill_Applies_To_Every_Placed_Unit_Of_That_Kind()
        {
            var field = BattleFixtures.CreateField("1,0,fodder,S1,g,0\n");
            var first = Place(field, "tank", 1, 0);
            var second = Place(field, "tank", 2, 0);

            field.ActivateSkill(Skills.For(SlotSymbol.Yellow7));

            Assert.That((first.SkillStepsLeft, second.SkillStepsLeft), Is.EqualTo((600, 600)));
        }

        [TestCase(599, 1)]
        [TestCase(600, 0)]
        [Description("効果時間10秒 = 600ステップ。戦闘ステップだけで減る")]
        public void Skill_Lasts_For_Its_Duration_In_Battle_Steps(int steps, int expectedLeft)
        {
            var field = BattleFixtures.CreateField("1,100,fodder,S1,g,0\n");
            var tank = Place(field, "tank", 1, 0);
            field.ActivateSkill(Skills.For(SlotSymbol.Yellow7));

            field.Advance(steps);

            Assert.That(tank.SkillStepsLeft, Is.EqualTo(expectedLeft));
        }

        [Test]
        [Description("効果時間が切れたタンクは被ダメージが元に戻る（brute の2発目は700）")]
        public void Expired_Skill_Has_No_Effect()
        {
            var shortSkill = BattleFixtures.LoadSkills(BattleFixtures.Skills.Replace("yellow7,tank,10,", "yellow7,tank,1,"));
            var field = BattleFixtures.CreateField("1,0,brute,S1,g,0\n");
            var tank = Place(field, "tank", 1, 0);
            field.ActivateSkill(shortSkill.For(SlotSymbol.Yellow7));

            // 効果時間1秒。1発目（約30ステップ目、350）は効果中、2発目（約90ステップ目）は切れたあと。
            field.Advance(95);

            Assert.That(tank.Hp, Is.EqualTo(1800 - 350 - 700));
        }

        [Test]
        public void Skill_Without_That_Unit_Is_Refused_And_Changes_Nothing()
        {
            var field = BattleFixtures.CreateField("1,0,fodder,S1,g,0\n");
            var tank = Place(field, "tank", 1, 0);

            var activated = field.ActivateSkill(Skills.For(SlotSymbol.Blue7));

            Assert.That((activated, tank.SkillStepsLeft), Is.EqualTo((false, 0)));
        }
    }
}
