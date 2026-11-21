using System;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Core.Run
{
    /// <summary>
    /// ゲーム1回分の状態（plan 16.2）。コイン（スロット用）と所持金（召喚・強化・撤退の返還）は別の財布で、
    /// ライフ・強化・ストックとともに戦闘マスをまたいで残る。リトライでは作り直す（AC-004）。
    /// </summary>
    public sealed class RunState
    {
        /// <param name="slotRules">スロットの値（スキルゲージの段数など）。</param>
        /// <param name="setting">難易度（スロットの設定 1〜6）。</param>
        public RunState(GameConfig config, UnitUpgradeTable upgradeTable, SlotRules slotRules, int setting)
        {
            SlotRules = slotRules ?? throw new ArgumentNullException(nameof(slotRules));
            Gauges = new SkillGauges(slotRules.GaugeMax);
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

        /// <summary>スロットの値。戦闘マスごとの <see cref="SlotEffects"/> もこれで作る。</summary>
        public SlotRules SlotRules { get; }

        /// <summary>色ごとのスキルゲージ（plan 16.4）。戦闘マスをまたいで残る。</summary>
        public SkillGauges Gauges { get; }

        /// <summary>ライフが 0 になった（FR-013）。</summary>
        public bool IsGameOver => Life <= 0;

        /// <summary>このゲームのルート（<see cref="BeginRoute"/> まで null）。</summary>
        public RouteMap Route { get; private set; }

        /// <summary>いまいるマス。ルートに入る前は null。</summary>
        public RouteNode Current { get; private set; }

        /// <summary>いまの階（ルートに入る前は 0）。</summary>
        public int Floor => Current?.Floor ?? 0;

        /// <summary>ルートを決める。現在地はルートの外（次に入れるのは1階の最初の戦闘だけ）。</summary>
        public void BeginRoute(RouteMap route)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
            Current = null;
        }

        /// <summary>D-89: 直前の戦闘マスのマップの id（まだ戦っていなければ null）。</summary>
        public string LastBattleMapId { get; private set; }

        /// <summary>
        /// D-89: 戦闘マスの出現を、直前の戦闘マスと同じマップを避けて（ほかに選べるときだけ）選び、そのマップを <see cref="LastBattleMapId"/> として覚える。
        /// </summary>
        public Encounter PickEncounter(EncounterTable table, EncounterKind kind, int floor, IRandom random)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            var encounter = table.Pick(kind, floor, random, LastBattleMapId);
            LastBattleMapId = encounter.MapId;
            return encounter;
        }

        /// <summary>そのマスに進めるか（ルートの外からは1階、以後はいまのマスにつながった上の階のマス）。</summary>
        public bool CanEnter(RouteNode node)
        {
            if (Route == null || node == null)
            {
                return false;
            }

            return Current == null ? node == Route.Start : Route.CanMove(Current, node);
        }

        /// <summary>そのマスへ進む。進めなければ false で何も変えない。</summary>
        public bool Enter(RouteNode node)
        {
            if (!CanEnter(node))
            {
                return false;
            }

            Current = node;
            return true;
        }

        /// <summary>ライフを回復する。最大値で止まる（AC-012）。実際に増えた量を返す。</summary>
        public int Heal(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "回復量が負");
            }

            var healed = Math.Min(amount, MaxLife - Life);
            Life += healed;
            return healed;
        }

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
