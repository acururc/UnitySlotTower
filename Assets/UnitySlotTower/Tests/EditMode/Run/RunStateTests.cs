using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Battle;
using UnitySlotTower.Tests.EditMode.Slot;

namespace UnitySlotTower.Tests.EditMode.Run
{
    [Description("plan 16.2 / FR-013, FR-033: ゲーム1回分の状態（コイン・所持金・ライフ・強化・ストック・難易度）")]
    public sealed class RunStateTests
    {
        private static RunState Create(int coins = 15, int money = 30, int life = 10, int setting = 4,
            int gaugeMax = 5)
        {
            var config = GameConfig.Load(CsvParser.Parse(GameConfig.FileName,
                $"key,value\ninitial_coins,{coins}\ninitial_money,{money}\nbet,3\nlife,{life}\n" +
                "deploy_limit,8\nretreat_refund_rate,0.5\nintro_seconds,2\nplacement_time_scale,0.1\nwave_gap_seconds,1\n"));
            return new RunState(config, BattleFixtures.LoadUpgrades(), SlotTestRules.Create(gaugeMax: gaugeMax),
                setting);
        }

        [Test]
        [Description("plan 16.4: スキルゲージは空で始まり、満タンの段数は slot_rules の gauge_max")]
        public void New_Run_Starts_With_Empty_Gauges_Of_The_Configured_Size()
        {
            var run = Create(gaugeMax: 4);

            Assert.That((run.Gauges.Level(SlotSymbol.Blue7), run.Gauges.Max), Is.EqualTo((0, 4)));
        }

        [Test]
        [Description("plan 16.2: コインと所持金は別々の初期値で始まる")]
        public void New_Run_Starts_With_Configured_Coins_And_Money()
        {
            var run = Create(coins: 15, money: 30);

            Assert.That((run.Coins.Coins, run.Money.Coins), Is.EqualTo((15, 30)));
        }

        [Test]
        public void New_Run_Starts_With_Full_Life()
        {
            var run = Create(life: 10);

            Assert.That((run.Life, run.MaxLife), Is.EqualTo((10, 10)));
        }

        [Test]
        public void New_Run_Keeps_The_Chosen_Setting()
        {
            var run = Create(setting: 4);

            Assert.That(run.Setting, Is.EqualTo(4));
        }

        [Test]
        [Description("plan 16.2: 強化は所持金で払い、コインは減らない（剣士の段階2は 20）")]
        public void Upgrades_Are_Paid_With_Money()
        {
            var run = Create(coins: 15, money: 30);

            var result = run.Upgrades.TryUpgrade("sword");

            Assert.That((result, run.Money.Coins, run.Coins.Coins), Is.EqualTo((UpgradeResult.Upgraded, 10, 15)));
        }

        [Test]
        [Description("plan 16.2: 所持金が足りない強化は拒否され、何も変わらない")]
        public void Upgrade_Without_Enough_Money_Changes_Nothing()
        {
            var run = Create(coins: 100, money: 19);

            var result = run.Upgrades.TryUpgrade("sword");

            Assert.That((result, run.Money.Coins, run.Coins.Coins, run.Upgrades.Level("sword")),
                Is.EqualTo((UpgradeResult.NotEnoughCoins, 19, 100, 1)));
        }

        [Test]
        public void New_Run_Has_Empty_Stock()
        {
            var run = Create();

            Assert.That(run.Stock.Count("sword"), Is.EqualTo(0));
        }

        [Test]
        [Description("FR-013: ライフは減るだけで、0 より下にならない")]
        public void Losing_Life_Stops_At_Zero()
        {
            var run = Create(life: 3);

            run.LoseLife(5);

            Assert.That((run.Life, run.IsGameOver), Is.EqualTo((0, true)));
        }

        [Test]
        public void Losing_Some_Life_Keeps_The_Run_Going()
        {
            var run = Create(life: 10);

            run.LoseLife(2);

            Assert.That((run.Life, run.MaxLife, run.IsGameOver), Is.EqualTo((8, 10, false)));
        }
    }
}
