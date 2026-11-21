using NUnit.Framework;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("plan 5章: max(攻撃力 − 防御力, 攻撃力 × 0.05)")]
    public sealed class DamageTests
    {
        [TestCase(UnitRole.Ranged, 0.4, 100)]
        [TestCase(UnitRole.Melee, 0.4, 250)]
        [TestCase(UnitRole.Ranged, 1, 250)]
        [Description("AC-056: 防御計算後、遠距離職だけに倍率を掛ける")]
        public void Only_Ranged_Attacks_Use_Enemy_Ranged_Damage_Rate(UnitRole role, double rate, int expected)
        {
            Assert.That(Damage.Calculate(400, 150, role, rate), Is.EqualTo(expected));
        }

        [Test]
        public void Ranged_Reduction_Applies_After_Minimum_Damage_And_Rounds_Half_Up()
        {
            Assert.That(Damage.Calculate(150, 300, UnitRole.Ranged, 0.4), Is.EqualTo(3));
        }

        [Test]
        public void Damage_Is_Attack_Minus_Defense()
        {
            Assert.That(Damage.Calculate(400, 150), Is.EqualTo(250));
        }

        [Test]
        public void Damage_Is_At_Least_5_Percent_Of_Attack()
        {
            Assert.That(Damage.Calculate(200, 300), Is.EqualTo(10));
        }

        [Test]
        public void Fractional_Minimum_Damage_Is_Rounded_Half_Up()
        {
            Assert.That(Damage.Calculate(150, 300), Is.EqualTo(8));
        }

        [TestCase(1, 0, 1)]
        [TestCase(2, 5, 1)]
        public void Positive_Attack_Has_At_Least_One_Damage(int atk, int def, int expected)
        {
            Assert.That(Damage.Calculate(atk, def), Is.EqualTo(expected));
        }

        [Test]
        public void Ranged_Reduction_Still_Has_Minimum_One()
        {
            Assert.That(Damage.Calculate(1, 0, UnitRole.Ranged, 0.5), Is.EqualTo(1));
        }

        [TestCase(UnitRole.Melee, 1)]
        [TestCase(UnitRole.Ranged, 1)]
        public void Zero_Attack_Deals_Zero_Damage(UnitRole role, double rate)
        {
            Assert.That(Damage.Calculate(0, 0, role, rate), Is.EqualTo(0));
            Assert.That(Damage.Calculate(0, 0), Is.EqualTo(0));
        }
    }
}
