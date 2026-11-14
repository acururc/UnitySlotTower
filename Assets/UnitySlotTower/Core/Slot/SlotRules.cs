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

        // 以下は旧効果（攻撃力・攻撃速度アップ、全体HP回復）の値。出目の効果（T342）とバッファー（T343）で置き換える。
        public double AtkRate { get; private set; }
        public double AtkSeconds { get; private set; }
        public double SpeedRate { get; private set; }
        public double SpeedSeconds { get; private set; }
        public double HealRate { get; private set; }

        public static SlotRules Load(CsvTable table)
        {
            var values = new KeyValueTable(table);
            return new SlotRules
            {
                FirstStopSeconds = values.GetDouble("first_stop_seconds"),
                StopIntervalSeconds = values.GetDouble("stop_interval_seconds"),
                AtkRate = values.GetDouble("atk_rate"),
                AtkSeconds = values.GetDouble("atk_seconds"),
                SpeedRate = values.GetDouble("speed_rate"),
                SpeedSeconds = values.GetDouble("speed_seconds"),
                HealRate = values.GetDouble("heal_rate"),
            };
        }
    }
}
