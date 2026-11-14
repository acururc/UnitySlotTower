using System;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// 1つの戦闘マスの間だけ効く出目の効果（FR-042、plan 16.4）。バーは攻撃力、コインは所持金のドロップの倍率を上げる。
    /// 戦闘マスごとに新しく作る（持ち越さない）。値は slot_rules.csv。
    /// </summary>
    public sealed class BattleBoosts
    {
        private readonly SlotRules _rules;

        public BattleBoosts(SlotRules rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        /// <summary>バーが揃った回数。</summary>
        public int Bars { get; private set; }

        /// <summary>コインが揃った回数。</summary>
        public int Coins { get; private set; }

        /// <summary>攻撃力の倍率: 1 + min(bar_step × 回数, bar_cap)。</summary>
        public double AttackRate => Tidy(1 + Math.Min(_rules.BarStep * Bars, _rules.BarCap));

        /// <summary>所持金のドロップの倍率: min(1 + coin_step × 回数, coin_cap)。</summary>
        public double MoneyRate => Tidy(Math.Min(1 + _rules.CoinStep * Coins, _rules.CoinCap));

        public void AddBar() => Bars++;

        public void AddCoin() => Coins++;

        /// <summary>所持金のドロップ量に倍率を掛けて四捨五入する（plan 16.2）。</summary>
        public int ApplyMoneyRate(int amount) =>
            (int)Math.Round(amount * MoneyRate, MidpointRounding.AwayFromZero);

        // 0.1 刻みの和の誤差（1.1000000000000001 など）で四捨五入がずれないよう、倍率を小数6桁にそろえる。
        private static double Tidy(double rate) => Math.Round(rate, 6);
    }
}
