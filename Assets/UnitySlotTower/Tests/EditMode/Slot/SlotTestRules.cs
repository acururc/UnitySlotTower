using System.Globalization;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    /// <summary>テスト用の slot_rules。既定値は plan 6章の初期値（コイン15枚、BIG 12G×10枚、REG 6G×8枚、効果 1.5倍・20秒、回復50%）。</summary>
    internal static class SlotTestRules
    {
        public static SlotRules Create(int coinPayout = 15, int bigGames = 12, int bigPayout = 10, int regGames = 6,
            int regPayout = 8, double atkRate = 1.5, double atkSeconds = 20, double speedRate = 1.5,
            double speedSeconds = 20, double healRate = 0.5, int earlyGames = 0, double earlyMissReduction = 0) =>
            SlotRules.Load(CsvParser.Parse(SlotRules.FileName,
                "key,value\nreel_length,12\nwait_seconds,2\n" +
                $"coin_payout,{coinPayout}\nbig_games,{bigGames}\nbig_payout,{bigPayout}\n" +
                $"reg_games,{regGames}\nreg_payout,{regPayout}\n" +
                $"atk_rate,{N(atkRate)}\natk_seconds,{N(atkSeconds)}\n" +
                $"speed_rate,{N(speedRate)}\nspeed_seconds,{N(speedSeconds)}\n" +
                $"early_games,{earlyGames}\nearly_miss_reduction,{N(earlyMissReduction)}\n" +
                $"heal_rate,{N(healRate)}\n"));

        private static string N(double value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>ボーナスが0ゲームで終わるルール。抽選と出目だけを見るテスト用（7・BAR を揃えても次のゲームは通常どおり抽選する）。</summary>
        public static SlotRules NoBonus() => Create(bigGames: 0, regGames: 0);
    }
}
