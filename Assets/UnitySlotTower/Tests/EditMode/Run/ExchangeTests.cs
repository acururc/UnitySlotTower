using NUnit.Framework;
using UnitySlotTower.Core.Run;

namespace UnitySlotTower.Tests.EditMode.Run
{
    [Description("AC-013 / FR-017, plan 16.2: 両替所（掛け金3枚単位、コイン1枚 = 所持金2）")]
    public sealed class ExchangeTests
    {
        private static (RunState run, Exchange exchange) Create(int coins, int money)
        {
            var run = RunFixtures.CreateRun(coins: coins, money: money);
            return (run, new Exchange(run, 3, 2));
        }

        [Test]
        [Description("AC-013: 所持金6 → コイン3")]
        public void Buying_Coins_Trades_Money_At_The_Rate()
        {
            var (run, exchange) = Create(15, 30);

            Assert.That((exchange.TryBuyCoins(), run.Coins.Coins, run.Money.Coins), Is.EqualTo((true, 18, 24)));
        }

        [Test]
        [Description("AC-013: コイン3 → 所持金6")]
        public void Selling_Coins_Trades_Coins_At_The_Rate()
        {
            var (run, exchange) = Create(15, 30);

            Assert.That((exchange.TrySellCoins(), run.Coins.Coins, run.Money.Coins), Is.EqualTo((true, 12, 36)));
        }

        [Test]
        [Description("AC-013: 所持金が足りなければ拒否し、何も変えない")]
        public void Buying_Without_Enough_Money_Is_Refused()
        {
            var (run, exchange) = Create(15, 5);

            Assert.That((exchange.CanBuyCoins, exchange.TryBuyCoins(), run.Coins.Coins, run.Money.Coins),
                Is.EqualTo((false, false, 15, 5)));
        }

        [Test]
        [Description("AC-013: コインが足りなければ拒否し、何も変えない")]
        public void Selling_Without_Enough_Coins_Is_Refused()
        {
            var (run, exchange) = Create(2, 30);

            Assert.That((exchange.CanSellCoins, exchange.TrySellCoins(), run.Coins.Coins, run.Money.Coins),
                Is.EqualTo((false, false, 2, 30)));
        }

        [Test]
        public void Exactly_Enough_Allows_Both_Ways()
        {
            var (_, exchange) = Create(3, 6);

            Assert.That((exchange.CanBuyCoins, exchange.CanSellCoins), Is.EqualTo((true, true)));
        }
    }
}
