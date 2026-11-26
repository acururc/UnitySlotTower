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

        /// <summary>赤7で敵全員に与えるダメージの率（最大 HP に掛ける、賭け1枚あたり、spec 011 FR-021）。</summary>
        public double RedHpRate { get; private set; }

        /// <summary>赤7が揃ってからダメージが入る（息吹が爆発する）までの戦闘の秒数（spec 003 FR-005）。0 ならすぐ。</summary>
        public double RedDelaySeconds { get; private set; }
        public double BlueStopSeconds { get; private set; }

        /// <summary>青7が揃ってから停止が始まる（盤面が凍りつく）までの戦闘の秒数（spec 004 FR-003）。0 ならすぐ。</summary>
        public double BlueDelaySeconds { get; private set; }

        public double GreenHealRate { get; private set; }

        /// <summary>緑7が揃ってから回復する（光の柱が立つ）までの戦闘の秒数（spec 005 FR-002）。0 ならすぐ。</summary>
        public double GreenDelaySeconds { get; private set; }

        /// <summary>毒の1回のダメージの率（最大 HP に掛ける、賭け1枚あたり、spec 011 FR-021）。</summary>
        public double YellowHpRate { get; private set; }
        public double YellowPoisonTickSeconds { get; private set; }
        public double YellowPoisonSeconds { get; private set; }

        /// <summary>黄7が揃ってから毒が始まる（毒の沼が弾ける）までの戦闘の秒数（spec 006 FR-003）。0 ならすぐ。</summary>
        public double YellowDelaySeconds { get; private set; }

        /// <summary>バーの攻撃力の倍率の上げ幅（倍率は 1 + 幅 × 賭け枚数、spec 011 D6）。</summary>
        public double BarAttackStep { get; private set; }
        public double BarSeconds { get; private set; }

        /// <summary>バーが揃ってから攻撃力アップが始まる（闘気が爆発する）までの戦闘の秒数（spec 007 FR-003）。0 ならすぐ。</summary>
        public double BarDelaySeconds { get; private set; }

        /// <summary>コイン1回ぶんの所持金の倍率の上がり幅。</summary>
        public double CoinStep { get; private set; }

        /// <summary>所持金の倍率の上限（倍率そのもの。1.5 なら最大 1.5 倍）。</summary>
        public double CoinCap { get; private set; }

        /// <summary>コインが揃ってから所持金の倍率が上がる（金貨が所持金の表示に吸い込まれる）までの戦闘の秒数（spec 009 FR-003）。0 ならすぐ。</summary>
        public double CoinDelaySeconds { get; private set; }

        /// <summary>赤7・毒のボスへのダメージの倍率（spec 011 FR-021）。</summary>
        public double BossDamageRate { get; private set; }

        /// <summary>有効ライン1本が揃う確率（spec 011 FR-010）。</summary>
        public double LineHitRate { get; private set; }

        /// <summary>二重・三重のとき、揃ったラインの通知をずらす秒数（spec 011 FR-012、表示の時間）。</summary>
        public double LineGapSeconds { get; private set; }

        /// <summary>ステージ1の見せ場で補うコインの枚数（spec 011 FR-031）。</summary>
        public int ShowcaseCoins { get; private set; }

        public static SlotRules Load(CsvTable table)
        {
            var values = new KeyValueTable(table);
            return new SlotRules
            {
                FirstStopSeconds = values.GetDouble("first_stop_seconds"),
                StopIntervalSeconds = values.GetDouble("stop_interval_seconds"),
                RedHpRate = values.GetDouble("red_hp_rate"),
                RedDelaySeconds = values.GetDouble("red_delay_seconds"),
                BlueStopSeconds = values.GetDouble("blue_stop_seconds"),
                BlueDelaySeconds = values.GetDouble("blue_delay_seconds"),
                GreenHealRate = values.GetDouble("green_heal_rate"),
                GreenDelaySeconds = values.GetDouble("green_delay_seconds"),
                YellowDelaySeconds = values.GetDouble("yellow_delay_seconds"),
                YellowHpRate = values.GetDouble("yellow_hp_rate"),
                YellowPoisonTickSeconds = values.GetDouble("yellow_poison_tick_seconds"),
                YellowPoisonSeconds = values.GetDouble("yellow_poison_seconds"),
                BarAttackStep = values.GetDouble("bar_attack_step"),
                BarSeconds = values.GetDouble("bar_seconds"),
                BarDelaySeconds = values.GetDouble("bar_delay_seconds"),
                CoinStep = values.GetDouble("coin_step"),
                CoinCap = values.GetDouble("coin_cap"),
                CoinDelaySeconds = values.GetDouble("coin_delay_seconds"),
                BossDamageRate = values.GetDouble("boss_damage_rate"),
                LineHitRate = values.GetDouble("line_hit_rate"),
                LineGapSeconds = values.GetDouble("line_gap_seconds"),
                ShowcaseCoins = values.GetInt("showcase_coins"),
            };
        }
    }
}
