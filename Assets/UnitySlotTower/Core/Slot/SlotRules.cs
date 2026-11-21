using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>slot_rules.csv の値（plan 16.3）。掛け金は game_config.csv の <c>bet</c>。</summary>
    public sealed class SlotRules
    {
        public const string FileName = "slot_rules.csv";

        /// <summary>レバーから左リールが止まるまでの秒数（自動停止）。</summary>
        public double FirstStopSeconds { get; private set; }

        /// <summary>リールが1本止まってから次が止まるまでの秒数。</summary>
        public double StopIntervalSeconds { get; private set; }

        /// <summary>スキルゲージの満タンの段数（plan 16.4）。</summary>
        public int GaugeMax { get; private set; }

        /// <summary>バー1回ぶんの攻撃力の上がり幅（0.1 なら +10%）。</summary>
        public int RedDamage { get; private set; }
        public double BlueStopSeconds { get; private set; }
        public double GreenHealRate { get; private set; }
        public int YellowPoisonDamage { get; private set; }
        public double YellowPoisonTickSeconds { get; private set; }
        public double YellowPoisonSeconds { get; private set; }
        public double BarAttackRate { get; private set; }
        public double BarSeconds { get; private set; }

        /// <summary>バーによる攻撃力の上がり幅の上限（0.5 なら最大 1.5 倍）。</summary>

        /// <summary>コイン1回ぶんの所持金の倍率の上がり幅。</summary>
        public double CoinStep { get; private set; }

        /// <summary>所持金の倍率の上限（倍率そのもの。1.5 なら最大 1.5 倍）。</summary>
        public double CoinCap { get; private set; }

        public static SlotRules Load(CsvTable table)
        {
            var values = new KeyValueTable(table);
            return new SlotRules
            {
                FirstStopSeconds = values.GetDouble("first_stop_seconds"),
                StopIntervalSeconds = values.GetDouble("stop_interval_seconds"),
                GaugeMax = values.GetInt("gauge_max"),
                RedDamage = values.GetInt("red_damage"),
                BlueStopSeconds = values.GetDouble("blue_stop_seconds"),
                GreenHealRate = values.GetDouble("green_heal_rate"),
                YellowPoisonDamage = values.GetInt("yellow_poison_damage"),
                YellowPoisonTickSeconds = values.GetDouble("yellow_poison_tick_seconds"),
                YellowPoisonSeconds = values.GetDouble("yellow_poison_seconds"),
                BarAttackRate = values.GetDouble("bar_attack_rate"),
                BarSeconds = values.GetDouble("bar_seconds"),
                CoinStep = values.GetDouble("coin_step"),
                CoinCap = values.GetDouble("coin_cap"),
            };
        }
    }
}
