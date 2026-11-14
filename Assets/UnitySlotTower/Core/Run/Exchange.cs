using System;

namespace UnitySlotTower.Core.Run
{
    /// <summary>
    /// 両替所（FR-017、D-69、plan 16.2）。1回の操作でコインを掛け金1回分（<c>bet</c> 枚）単位で売買する。
    /// レートは双方向で同じ（コイン1枚 = <c>exchange_money_per_coin</c> の所持金）。足りなければ何も変えない（AC-013）。
    /// </summary>
    public sealed class Exchange
    {
        private readonly RunState _run;

        public Exchange(RunState run, int bet, int moneyPerCoin)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            CoinsPerTrade = bet > 0 ? bet : throw new ArgumentOutOfRangeException(nameof(bet), bet, "掛け金が0以下");
            MoneyPerTrade = moneyPerCoin > 0
                ? bet * moneyPerCoin
                : throw new ArgumentOutOfRangeException(nameof(moneyPerCoin), moneyPerCoin, "レートが0以下");
        }

        /// <summary>1回で動くコイン。</summary>
        public int CoinsPerTrade { get; }

        /// <summary>1回で動く所持金。</summary>
        public int MoneyPerTrade { get; }

        /// <summary>所持金でコインを買えるか。</summary>
        public bool CanBuyCoins => _run.Money.Coins >= MoneyPerTrade;

        /// <summary>コインを売って所持金にできるか。</summary>
        public bool CanSellCoins => _run.Coins.Coins >= CoinsPerTrade;

        /// <summary>所持金 <see cref="MoneyPerTrade"/> → コイン <see cref="CoinsPerTrade"/>。足りなければ false で何も変えない。</summary>
        public bool TryBuyCoins()
        {
            if (!_run.Money.TrySpend(MoneyPerTrade))
            {
                return false;
            }

            _run.Coins.Add(CoinsPerTrade);
            return true;
        }

        /// <summary>コイン <see cref="CoinsPerTrade"/> → 所持金 <see cref="MoneyPerTrade"/>。足りなければ false で何も変えない。</summary>
        public bool TrySellCoins()
        {
            if (!_run.Coins.TrySpend(CoinsPerTrade))
            {
                return false;
            }

            _run.Money.Add(MoneyPerTrade);
            return true;
        }
    }
}
