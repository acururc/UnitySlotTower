using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>slot_rules.csv の値（plan 5章 スロット・6章）。掛け金は game_config.csv の <c>bet</c>。</summary>
    public sealed class SlotRules
    {
        public const string FileName = "slot_rules.csv";

        /// <summary>リール1本のマス数（D-46）。</summary>
        public int ReelLength { get; private set; }

        public double WaitSeconds { get; private set; }

        /// <summary>コイン揃いの払い出し枚数。</summary>
        public int CoinPayout { get; private set; }

        public int BigGames { get; private set; }
        public int BigPayout { get; private set; }
        public int RegGames { get; private set; }
        public int RegPayout { get; private set; }

        /// <summary>攻撃力アップの倍率と秒数。</summary>
        public double AtkRate { get; private set; }
        public double AtkSeconds { get; private set; }

        /// <summary>攻撃速度アップの倍率（攻撃間隔をこれで割る）と秒数。</summary>
        public double SpeedRate { get; private set; }
        public double SpeedSeconds { get; private set; }

        /// <summary>全体HP回復で足す最大HPの割合。</summary>
        public double HealRate { get; private set; }

        public static SlotRules Load(CsvTable table)
        {
            var values = new KeyValueTable(table);
            return new SlotRules
            {
                ReelLength = values.GetInt("reel_length"),
                WaitSeconds = values.GetDouble("wait_seconds"),
                CoinPayout = values.GetInt("coin_payout"),
                BigGames = values.GetInt("big_games"),
                BigPayout = values.GetInt("big_payout"),
                RegGames = values.GetInt("reg_games"),
                RegPayout = values.GetInt("reg_payout"),
                AtkRate = values.GetDouble("atk_rate"),
                AtkSeconds = values.GetDouble("atk_seconds"),
                SpeedRate = values.GetDouble("speed_rate"),
                SpeedSeconds = values.GetDouble("speed_seconds"),
                HealRate = values.GetDouble("heal_rate"),
            };
        }
    }
}
