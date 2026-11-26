using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Core.Run
{
    /// <summary>
    /// ゲーム1回分の状態（plan 16.2）。コイン（スロット用）と所持金（召喚・強化・撤退の返還）は別の財布で、
    /// ライフ・強化とともにステージをまたいで残る（spec 002 FR-105）。配置とスロットの効果はステージごとの
    /// <see cref="BattleNodeRun"/> が持つので持ち越さない。リトライでは作り直す（AC-004）。
    /// </summary>
    public sealed class RunState
    {
        /// <param name="slotRules">スロットの値。</param>
        public RunState(GameConfig config, UnitUpgradeTable upgradeTable, SlotRules slotRules)
        {
            SlotRules = slotRules ?? throw new ArgumentNullException(nameof(slotRules));
            Coins = new CoinWallet(config.InitialCoins);
            Money = new CoinWallet(config.InitialMoney);
            MaxLife = config.Life;
            Life = config.Life;
            Upgrades = new UnitUpgrades(upgradeTable, Money);
            _firstStageFreeSummons = config.FirstStageFreeSummons;
        }

        /// <summary>スロットに賭けるコイン。</summary>
        public CoinWallet Coins { get; }

        /// <summary>召喚・強化に使い、撤退で返る所持金。</summary>
        public CoinWallet Money { get; }

        public int Life { get; private set; }
        public int MaxLife { get; }
        public UnitUpgrades Upgrades { get; }

        /// <summary>いまのステージ（1 から。spec 002 FR-101）。</summary>
        public int StageIndex { get; private set; } = 1;

        private readonly int _firstStageFreeSummons;

        /// <summary>このステージの開始時の無料の召喚の回数。ステージ1だけ game_config.csv の値、ほかは 0（spec 002 FR-108）。</summary>
        public int FreeSummons => StageIndex == 1 ? _firstStageFreeSummons : 0;

        /// <summary>次のステージへ進む。ライフ・コイン・所持金・強化はそのまま残る（FR-105）。</summary>
        public void AdvanceStage()
        {
            StageIndex++;
        }

        private StageStart? _stageStart;

        // ステージ開始時の値（spec 010 FR-201）。
        private readonly struct StageStart
        {
            public readonly int Life;
            public readonly int Money;
            public readonly int Coins;
            public readonly IReadOnlyDictionary<string, int> Upgrades;

            public StageStart(int life, int money, int coins, IReadOnlyDictionary<string, int> upgrades)
            {
                Life = life;
                Money = money;
                Coins = coins;
                Upgrades = upgrades;
            }
        }

        /// <summary>いまのライフ・所持金・コイン・強化を、このステージの開始時の値として記録する（spec 010 FR-201）。</summary>
        public void RecordStageStart()
        {
            _stageStart = new StageStart(Life, Money.Coins, Coins.Coins, Upgrades.Snapshot());
        }

        /// <summary>
        /// ライフ・所持金・コイン・強化を <see cref="RecordStageStart"/> の値に戻す。ステージ番号は変えない。何度戻しても同じ値。
        /// 記録がなければ例外。
        /// </summary>
        public void RestoreStageStart()
        {
            if (_stageStart == null)
            {
                throw new InvalidOperationException("ステージ開始時の値が記録されていない");
            }

            var start = _stageStart.Value;
            Life = start.Life;
            Money.Reset(start.Money);
            Coins.Reset(start.Coins);
            Upgrades.Restore(start.Upgrades);
        }

        /// <summary>スロットの値。戦闘マスごとの <see cref="SlotEffects"/> もこれで作る。</summary>
        public SlotRules SlotRules { get; }

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
