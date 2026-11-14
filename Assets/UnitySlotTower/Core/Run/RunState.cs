using System;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;

namespace UnitySlotTower.Core.Run
{
    /// <summary>
    /// ゲーム1回分の状態（plan 16.2）。コイン（スロット用）と所持金（召喚・強化・撤退の返還）は別の財布で、
    /// ライフ・強化・ストックとともに戦闘マスをまたいで残る。リトライでは作り直す（AC-004）。
    /// </summary>
    public sealed class RunState
    {
        /// <param name="setting">難易度（スロットの設定 1〜6）。</param>
        public RunState(GameConfig config, UnitUpgradeTable upgradeTable, int setting)
        {
            Coins = new CoinWallet(config.InitialCoins);
            Money = new CoinWallet(config.InitialMoney);
            MaxLife = config.Life;
            Life = config.Life;
            Setting = setting;
            Upgrades = new UnitUpgrades(upgradeTable, Money);
        }

        /// <summary>スロットに賭けるコイン。</summary>
        public CoinWallet Coins { get; }

        /// <summary>召喚・強化に使い、撤退で返る所持金。</summary>
        public CoinWallet Money { get; }

        public int Life { get; private set; }
        public int MaxLife { get; }
        public int Setting { get; }
        public UnitUpgrades Upgrades { get; }
        public UnitStock Stock { get; } = new UnitStock();

        /// <summary>ライフが 0 になった（FR-013）。</summary>
        public bool IsGameOver => Life <= 0;

        /// <summary>ライフを減らす。0 より下にはならない。</summary>
        public void LoseLife(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "ライフの減少が負");
            }

            Life = Math.Max(0, Life - amount);
        }
    }
}
