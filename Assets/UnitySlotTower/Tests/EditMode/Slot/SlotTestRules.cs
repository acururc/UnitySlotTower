using System.Globalization;
using System.IO;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    /// <summary>テスト用の slot_rules・リール・抽選表。</summary>
    internal static class SlotTestRules
    {
        /// <summary>plan 16.3 の仮の並び（左・中・右）。</summary>
        public const string PlanLeft = "red7 blue7 green7 yellow7 bar coin";
        public const string PlanCenter = "red7 green7 bar blue7 coin yellow7";
        public const string PlanRight = "red7 bar yellow7 green7 blue7 coin";

        public static SlotRules Create(double healRate = 0.5, int gaugeMax = 5, double barStep = 0.1,
            double barCap = 0.5, double coinStep = 0.1, double coinCap = 1.5) =>
            SlotRules.Load(CsvParser.Parse(SlotRules.FileName,
                "key,value\nfirst_stop_seconds,1\nstop_interval_seconds,0.4\n" +
                $"heal_rate,{N(healRate)}\ngauge_max,{gaugeMax}\n" +
                $"bar_step,{N(barStep)}\nbar_cap,{N(barCap)}\ncoin_step,{N(coinStep)}\ncoin_cap,{N(coinCap)}\n"));

        /// <summary>3本の並び（図柄 ID を空白区切り）から slot_reels を作る。</summary>
        public static SlotReels Reels(string left = PlanLeft, string center = PlanCenter, string right = PlanRight) =>
            SlotReels.Load(CsvParser.Parse(SlotReels.FileName, ReelsCsv(left, center, right)));

        public static string ReelsCsv(params string[] reels)
        {
            var csv = "reel,index,symbol\n";
            for (var reel = 0; reel < reels.Length; reel++)
            {
                var ids = reels[reel].Split(' ');
                for (var i = 0; i < ids.Length; i++)
                {
                    csv += $"{reel},{i},{ids[i]}\n";
                }
            }

            return csv;
        }

        public static CsvTable ProjectTable(string fileName) =>
            CsvParser.Parse(fileName, File.ReadAllText(Path.Combine(TablePaths.Root, fileName)));

        private static string N(double value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
