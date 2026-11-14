using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Tests.EditMode.Slot;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("FR-042, plan 16.4: バーで攻撃力、コインで所持金の倍率が上がる（上限あり）")]
    public sealed class BattleBoostsTests
    {
        private static BattleBoosts Create() => new BattleBoosts(SlotTestRules.Create(
            barStep: 0.1, barCap: 0.5, coinStep: 0.1, coinCap: 1.5));

        private static BattleBoosts After(int bars, int coins)
        {
            var boosts = Create();
            for (var i = 0; i < bars; i++)
            {
                boosts.AddBar();
            }

            for (var i = 0; i < coins; i++)
            {
                boosts.AddCoin();
            }

            return boosts;
        }

        [Test]
        public void New_Boosts_Change_Nothing()
        {
            var boosts = Create();

            Assert.That((boosts.AttackRate, boosts.MoneyRate), Is.EqualTo((1.0, 1.0)));
        }

        [TestCase(1, 1.1)]
        [TestCase(3, 1.3)]
        [TestCase(5, 1.5)]
        [Description("AC-042: バーは1回ごとに攻撃力 +10%")]
        public void Each_Bar_Raises_The_Attack_Rate(int bars, double expected)
        {
            var boosts = After(bars, 0);

            Assert.That(boosts.AttackRate, Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        [Description("plan 16.4: バーの上がり幅は +50% で止まる")]
        public void Attack_Rate_Stops_At_The_Cap()
        {
            var boosts = After(8, 0);

            Assert.That(boosts.AttackRate, Is.EqualTo(1.5).Within(1e-9));
        }

        [TestCase(1, 1.1)]
        [TestCase(4, 1.4)]
        [Description("AC-042: コインは1回ごとに所持金の倍率 +0.1（1回目で 1.1）")]
        public void Each_Coin_Raises_The_Money_Rate(int coins, double expected)
        {
            var boosts = After(0, coins);

            Assert.That(boosts.MoneyRate, Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        [Description("plan 16.4: 所持金の倍率は 1.5 で止まる")]
        public void Money_Rate_Stops_At_The_Cap()
        {
            var boosts = After(0, 9);

            Assert.That(boosts.MoneyRate, Is.EqualTo(1.5).Within(1e-9));
        }

        [Test]
        public void Bars_And_Coins_Are_Independent()
        {
            var boosts = After(2, 0);

            Assert.That(boosts.MoneyRate, Is.EqualTo(1.0));
        }

        [TestCase(0, 3, 3)]
        [TestCase(1, 3, 3)]
        [TestCase(1, 5, 6)]
        [TestCase(5, 15, 23)]
        [Description("plan 16.2: 所持金のドロップは 量×倍率 を四捨五入（3×1.1=3.3→3、5×1.1=5.5→6、15×1.5=22.5→23）")]
        public void Money_Drop_Is_Multiplied_And_Rounded(int coins, int amount, int expected)
        {
            var boosts = After(0, coins);

            Assert.That(boosts.ApplyMoneyRate(amount), Is.EqualTo(expected));
        }
    }
}
