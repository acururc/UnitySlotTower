using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Tests.EditMode.Battle;
using UnitySlotTower.Tests.EditMode.Slot;

namespace UnitySlotTower.Tests.EditMode.Run
{
    /// <summary>ルート・両替・ショップのテスト用の小さなデータ。</summary>
    internal static class RunFixtures
    {
        public const string RulesHeader = "key,value\n";

        // plan 16.7 の仮値。
        public const string RulesBody =
            "floors,8\ncolumns,7\npaths,4\n" +
            "weight_battle,50\nweight_elite,14\nweight_shop,22\nweight_rest,14\n" +
            "elite_min_floor,3\nrest_heal,3\nshop_slots,3\nexchange_money_per_coin,2\n";

        public static RouteRules LoadRules(string body = RulesBody) =>
            RouteRules.Load(CsvParser.Parse(RouteRules.FileName, RulesHeader + body));

        /// <summary>仮値の1行を差し替える（例: <c>("weight_elite", "0")</c>）。</summary>
        public static RouteRules LoadRulesWith(params (string key, string value)[] changes)
        {
            var body = RulesBody;
            foreach (var (key, value) in changes)
            {
                var start = body.IndexOf(key + ",", System.StringComparison.Ordinal);
                var end = body.IndexOf('\n', start);
                body = body.Substring(0, start) + key + "," + value + body.Substring(end);
            }

            return LoadRules(body);
        }

        /// <summary>強化は <see cref="BattleFixtures.Upgrades"/>（sword は3段階、tank は強化なし）、ゲージは5段。</summary>
        public static RunState CreateRun(int coins = 15, int money = 30, int life = 10, int gaugeMax = 5)
        {
            var config = GameConfig.Load(CsvParser.Parse(GameConfig.FileName,
                $"key,value\ninitial_coins,{coins}\ninitial_money,{money}\nbet,3\nlife,{life}\n" +
                "deploy_limit,8\nretreat_refund_rate,0.5\n"));
            return new RunState(config, BattleFixtures.LoadUpgrades(), SlotTestRules.Create(gaugeMax: gaugeMax), 4);
        }
    }
}
