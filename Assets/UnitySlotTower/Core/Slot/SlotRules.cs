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

        /// <summary>赤7で敵全員に与えるダメージ。</summary>
        public int RedDamage { get; private set; }

        /// <summary>赤7が揃ってからダメージが入る（息吹が爆発する）までの戦闘の秒数（spec 003 FR-005）。0 ならすぐ。</summary>
        public double RedDelaySeconds { get; private set; }
        public double BlueStopSeconds { get; private set; }
        public double GreenHealRate { get; private set; }
        public int YellowPoisonDamage { get; private set; }
        public double YellowPoisonTickSeconds { get; private set; }
        public double YellowPoisonSeconds { get; private set; }
        public double BarAttackRate { get; private set; }
        public double BarSeconds { get; private set; }

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
                RedDamage = values.GetInt("red_damage"),
                RedDelaySeconds = values.GetDouble("red_delay_seconds"),
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
