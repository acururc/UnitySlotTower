using System.Collections.Generic;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Slot;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("FR-010, FR-013〜016: ステージとウェーブの準備・戦闘・終了とストック（AC-010, AC-012）")]
    public sealed class WaveRunTests
    {
        // フィクスチャの地図（4×2）。地上マスは (1,0)(2,0)、高台マスは (1,1)(3,1)。
        //   S1 G G E
        //   S2 H X H
        // fodder（HP1・攻撃0・1マス/秒）は 0 秒に出て約3秒でゴールに着く。行は stage,wave,time,enemy_id,spawn,path_id,is_boss。
        private const string OneFodderPerWave =
            "1,1,0,fodder,S1,g,0\n1,2,0,fodder,S1,g,0\n" +
            "2,1,0,fodder,S1,g,0\n2,2,0,fodder,S1,g,0\n" +
            "3,1,0,fodder,S1,g,0\n3,2,0,fodder,S1,g,0\n";

        // 10 秒あればどのウェーブも終わる。
        private const int WaveSteps = 600;

        private RunState _state;
        private Dictionary<int, BattleMap> _maps;

        private WaveRun Create(int money = 100, int coins = 50, int stageCount = 3, int wavesPerStage = 2,
            int life = 10, int deployLimit = 8, string waveRows = OneFodderPerWave)
        {
            _maps = new Dictionary<int, BattleMap>();
            var config = GameConfig.Load(CsvParser.Parse(GameConfig.FileName,
                $"key,value\ninitial_coins,{coins}\ninitial_money,{money}\nbet,3\nlife,{life}\n" +
                $"deploy_limit,{deployLimit}\nretreat_refund_rate,0.5\n"));
            _state = new RunState(config, BattleFixtures.LoadUpgrades(), 1);
            var units = BattleFixtures.LoadUnits();
            return new WaveRun(config, units, BattleFixtures.LoadRanges(), _state, stageCount, wavesPerStage,
                stage => _maps[stage] = BattleFixtures.LoadMap(),
                (stage, wave, map) => BattleFixtures.LoadStageWave(waveRows, stage, wave), new SeededRandom(1));
        }

        private static SummonResult Place(WaveRun run, string unitId, int x, int y)
        {
            var result = run.Deployment.Select(unitId);
            if (result != SummonResult.Summoned)
            {
                return result;
            }

            run.Deployment.SetCursor(new GridPoint(x, y));
            Assert.That(run.Deployment.ChooseCell(), Is.True, $"{unitId} を ({x},{y}) に置けない");
            return run.Deployment.Confirm();
        }

        private static void PlayWave(WaveRun run)
        {
            run.Start();
            run.Advance(WaveSteps);
        }

        private static void PlayStage(WaveRun run)
        {
            PlayWave(run);
            PlayWave(run);
        }

        /// <summary>S1 で剣士を1体置いて終え、S2 の準備に入った状態（剣士のストック1）。</summary>
        private WaveRun ClearStage1WithSword(int money = 100, int deployLimit = 8)
        {
            var run = Create(money: money, deployLimit: deployLimit);
            Place(run, "sword", 1, 0);
            PlayStage(run);
            Assert.That((run.Stage, run.Stock.Count("sword")), Is.EqualTo((2, 1)), "S1 を剣士1体で終えられない");
            return run;
        }

        [Test]
        public void Run_Starts_In_Preparation_Of_Stage_1_Wave_1()
        {
            var run = Create();

            Assert.That((run.Stage, run.Wave, run.Phase), Is.EqualTo((1, 1, WavePhase.Preparing)));
        }

        [Test]
        public void Enemies_Do_Not_Appear_During_Preparation()
        {
            var run = Create();

            run.Advance(WaveSteps);

            Assert.That((run.Field.Enemies.Count, run.Field.Tick), Is.EqualTo((0, 0L)));
        }

        [Test]
        public void Starting_Begins_The_Fight()
        {
            var run = Create();

            run.Start();
            run.Advance(1);

            Assert.That((run.Phase, run.Field.Enemies.Count), Is.EqualTo((WavePhase.Fighting, 1)));
        }

        [Test]
        [Description("plan C-3: 準備中は召喚の待ち時間を無視する")]
        public void Same_Type_Can_Be_Summoned_Again_During_Preparation()
        {
            var run = Create();
            Place(run, "sword", 1, 0);

            Assert.That(Place(run, "sword", 2, 0), Is.EqualTo(SummonResult.Summoned));
        }

        [Test]
        public void Preparation_Summons_Leave_No_Cooldown_For_The_Fight()
        {
            var run = Create();
            Place(run, "sword", 1, 0);

            run.Start();

            Assert.That(run.Summoner.CooldownRemaining("sword"), Is.EqualTo(0));
        }

        [Test]
        public void Cooldown_Applies_During_The_Fight()
        {
            var run = Create();
            run.Start();
            Place(run, "sword", 1, 0);

            Assert.That(run.Deployment.Select("sword"), Is.EqualTo(SummonResult.CoolingDown));
        }

        [Test]
        [Description("FR-010: W1 を終えると同じステージの W2 の準備に入る")]
        public void Clearing_Wave_1_Moves_To_Wave_2_Of_The_Same_Stage()
        {
            var run = Create();
            var map = run.Map;

            PlayWave(run);

            Assert.That((run.Stage, run.Wave, run.Phase, run.Map), Is.EqualTo((1, 2, WavePhase.Preparing, map)));
        }

        [Test]
        [Description("AC-010: 同じステージ内のウェーブ切り替えで配置が保たれる（同じ個体なので HP も残る）")]
        public void Deployed_Units_Stay_On_The_Field_Into_Wave_2()
        {
            var run = Create();
            Place(run, "sword", 1, 0);
            var sword = run.Field.Units[0];

            PlayWave(run);

            Assert.That((run.Field.Units.Count, run.Field.Units[0], run.Summoner.DeployedCount, run.Stock.Count("sword")),
                Is.EqualTo((1, sword, 1, 0)));
        }

        [Test]
        [Description("plan 4章: W1 の終わりではライフボーナスもライフ回復もしない")]
        public void Clearing_Wave_1_Keeps_The_Life_And_Gives_No_Bonus()
        {
            // fodder がゴールに着いてライフ 9。ゴールに着いた敵は落とさない。
            var run = Create(money: 100, life: 10);

            PlayWave(run);

            Assert.That((run.Field.Life, _state.Money.Coins), Is.EqualTo((9, 100)));
        }

        [Test]
        [Description("plan 4章: W2 の準備でも召喚の待ち時間を無視し、戦闘開始時の待ち時間は 0")]
        public void Wave_2_Preparation_Clears_Cooldowns()
        {
            var run = Create();
            run.Start();
            Place(run, "sword", 1, 0);
            run.Advance(WaveSteps);

            Assert.That((run.Wave, run.Summoner.CooldownRemaining("sword"), Place(run, "sword", 2, 0)),
                Is.EqualTo((2, 0.0, SummonResult.Summoned)));
        }

        [Test]
        public void Wave_2_Spawns_Its_Own_Enemies_From_Time_Zero()
        {
            var run = Create();
            PlayWave(run);

            run.Start();
            run.Advance(1);

            Assert.That((run.Field.Tick, run.Field.Enemies.Count), Is.EqualTo((1L, 1)));
        }

        [Test]
        [Description("plan 16.2: ライフボーナスは廃止。ステージを終えても残りライフ分のお金は入らない")]
        public void Clearing_A_Stage_Adds_No_Life_Bonus()
        {
            // 2 体の fodder がゴールに着いてライフ 8。ゴールに着いた敵は落とさない。
            var run = Create(money: 100);

            PlayStage(run);

            Assert.That(_state.Money.Coins, Is.EqualTo(100));
        }

        [Test]
        public void Clearing_A_Stage_Moves_To_Preparation_Of_The_Next_On_Its_Map()
        {
            var run = Create();

            PlayStage(run);

            Assert.That((run.Stage, run.Wave, run.Phase, run.Map), Is.EqualTo((2, 1, WavePhase.Preparing, _maps[2])));
        }

        [Test]
        [Description("FR-013: ライフは各ステージ開始時に満タンに戻る")]
        public void Next_Stage_Starts_With_Full_Life()
        {
            var run = Create(life: 10);

            PlayStage(run);

            Assert.That(run.Field.Life, Is.EqualTo(10));
        }

        [Test]
        public void Stage_And_Wave_Began_Are_Raised()
        {
            var run = Create();
            var stages = 0;
            var waves = 0;
            run.StageBegan += () => stages++;
            run.WaveBegan += () => waves++;

            PlayWave(run);
            var afterWave1 = (stages, waves);
            PlayWave(run);

            Assert.That((afterWave1, (stages, waves)), Is.EqualTo(((0, 1), (1, 2))));
        }

        [Test]
        public void Losing_All_Life_Ends_The_Run()
        {
            var run = Create(life: 1);

            PlayWave(run);

            Assert.That((run.Stage, run.Wave, run.Phase), Is.EqualTo((1, 1, WavePhase.GameOver)));
        }

        [Test]
        [Description("AC-012: 配置中だった数と同数の無料ストックができる")]
        public void Deployed_Units_Become_Free_Stock_By_Type()
        {
            // 剣士が fodder を止めて倒し、弓2体は高台に居るだけ。
            var run = Create();
            Place(run, "sword", 1, 0);
            Place(run, "archer", 1, 1);
            Place(run, "archer", 3, 1);

            PlayStage(run);

            Assert.That((run.Stock.Count("sword"), run.Stock.Count("archer"), run.Stock.Count("tank")),
                Is.EqualTo((1, 2, 0)));
        }

        [Test]
        public void Next_Stage_Starts_With_An_Empty_Field()
        {
            var run = Create();
            Place(run, "sword", 1, 0);

            PlayStage(run);

            Assert.That((run.Field.Units.Count, run.Summoner.DeployedCount), Is.EqualTo((0, 0)));
        }

        [Test]
        [Description("FR-016: 無料ストックはコインなしで配置できる")]
        public void Placing_From_Stock_Costs_No_Coins()
        {
            var run = ClearStage1WithSword();
            var coins = _state.Money.Coins;

            Place(run, "sword", 1, 0);

            Assert.That((_state.Money.Coins, run.Stock.Count("sword")), Is.EqualTo((coins, 0)));
        }

        [Test]
        public void Stock_Can_Be_Placed_Without_Enough_Coins()
        {
            // 剣士 12 枚をちょうど払う。fodder のドロップ（40%で1枚）があっても 12 枚には届かない。
            var run = ClearStage1WithSword(money: 12);

            Assert.That(Place(run, "sword", 1, 0), Is.EqualTo(SummonResult.Summoned));
        }

        [Test]
        [Description("plan C-3: ストックからの配置も配置上限に数える")]
        public void Stock_Placement_Counts_Toward_The_Deploy_Limit()
        {
            var run = ClearStage1WithSword(deployLimit: 1);
            Place(run, "sword", 1, 0);

            Assert.That(run.Deployment.Select("archer"), Is.EqualTo(SummonResult.DeployLimitReached));
        }

        [Test]
        [Description("AC-012: 強化が残り、ストックから置いた個体は HP 全快")]
        public void Stock_Unit_Has_Full_Hp_At_The_Carried_Upgrade()
        {
            // 剣士は段階2で HP×1.5 → 1800。
            var run = Create();
            Place(run, "sword", 1, 0);
            run.Deployment.Upgrade("sword");
            PlayStage(run);

            Place(run, "sword", 1, 0);

            var unit = run.Field.Units[0];
            Assert.That((unit.MaxHp, unit.Hp), Is.EqualTo((1800, 1800)));
        }

        [Test]
        public void Unused_Stock_Carries_Over_To_Later_Stages()
        {
            var run = ClearStage1WithSword();

            PlayStage(run);

            Assert.That((run.Stage, run.Stock.Count("sword")), Is.EqualTo((3, 1)));
        }

        [Test]
        [Description("plan C-3: ストック由来の味方の撤退も召喚コストを基準に返す")]
        public void Retreating_A_Stock_Unit_Refunds_Half_Its_Cost()
        {
            var run = ClearStage1WithSword();
            Place(run, "sword", 1, 0);
            var money = _state.Money.Coins;

            run.Deployment.Retreat(run.Field.Units[0]);

            Assert.That(_state.Money.Coins - money, Is.EqualTo(6));
        }

        [Test]
        [Description("plan 16.2: 召喚は所持金で払い、コインは減らない（剣士 12）")]
        public void Summoning_Pays_With_Money_Not_Coins()
        {
            var run = Create(money: 100, coins: 50);

            Place(run, "sword", 1, 0);

            Assert.That((_state.Money.Coins, _state.Coins.Coins), Is.EqualTo((88, 50)));
        }

        [Test]
        [Description("plan 16.2: 撤退の返還は所持金に入る（剣士 12 の半分）")]
        public void Retreat_Refunds_Into_Money()
        {
            var run = Create(money: 100, coins: 50);
            Place(run, "sword", 1, 0);

            run.Deployment.Retreat(run.Field.Units[0]);

            Assert.That((_state.Money.Coins, _state.Coins.Coins), Is.EqualTo((94, 50)));
        }

        [Test]
        [Description("plan 16.2: 強化は所持金で払う（剣士の段階2は 20）")]
        public void Upgrading_Pays_With_Money()
        {
            var run = Create(money: 100, coins: 50);

            run.Deployment.Upgrade("sword");

            Assert.That((_state.Money.Coins, _state.Coins.Coins), Is.EqualTo((80, 50)));
        }

        [Test]
        [Description("FR-033: コインのドロップは戦闘中にコインへ入り、所持金は変わらない")]
        public void Coin_Drops_Go_Into_The_Coins_During_The_Fight()
        {
            // rich は倒されると必ずコインを 2〜3 枚落とす。
            var run = Create(waveRows: "1,1,0,rich,S1,g,0\n1,1,5,fodder,S1,g,0\n");
            Place(run, "sword", 1, 0);
            var coins = _state.Coins.Coins;
            var money = _state.Money.Coins;
            run.Start();

            run.Advance(120);

            Assert.That((_state.Coins.Coins - coins is >= 2 and <= 3, _state.Money.Coins - money),
                Is.EqualTo((true, 0)));
        }

        [Test]
        [Description("plan 16.2: 所持金のドロップは戦闘中に所持金へ入り、コインは変わらない")]
        public void Money_Drops_Go_Into_The_Money_During_The_Fight()
        {
            // banker は倒されると必ず所持金を 2〜3 落とす。
            var run = Create(waveRows: "1,1,0,banker,S1,g,0\n1,1,5,fodder,S1,g,0\n");
            Place(run, "sword", 1, 0);
            var coins = _state.Coins.Coins;
            var money = _state.Money.Coins;
            run.Start();

            run.Advance(120);

            Assert.That((_state.Money.Coins - money is >= 2 and <= 3, _state.Coins.Coins - coins),
                Is.EqualTo((true, 0)));
        }

        [Test]
        [Description("FR-033: W2 の撃破ドロップも二重に数えず入る")]
        public void Drops_Of_Wave_2_Are_Added_Once()
        {
            var run = Create(waveRows: "1,1,0,rich,S1,g,0\n1,2,0,banker,S1,g,0\n");
            Place(run, "sword", 1, 0);
            PlayWave(run);
            var coins = _state.Coins.Coins;
            var money = _state.Money.Coins;

            PlayWave(run);

            Assert.That((_state.Coins.Coins - coins, _state.Money.Coins - money is >= 2 and <= 3),
                Is.EqualTo((0, true)));
        }

        [Test]
        [Description("AC-010: 最後のステージの最終ウェーブを終えるとクリア")]
        public void Clearing_The_Last_Stage_Completes_The_Run()
        {
            var run = Create(stageCount: 1);

            PlayStage(run);

            Assert.That((run.Stage, run.Wave, run.Phase), Is.EqualTo((1, 2, WavePhase.Completed)));
        }

        [Test]
        [Description("AC-010: 最終ボスを倒しても、残りの敵を全滅させるまでクリアにならない（2026-10-30 ユーザー決定）")]
        public void Defeating_The_Final_Boss_Does_Not_Complete_The_Run_While_Enemies_Remain()
        {
            // ボス役の fodder は約 0.5 秒で剣士に倒される。雑魚は 5 秒に出る。
            var run = Create(stageCount: 1, wavesPerStage: 1, waveRows: "1,1,0,fodder,S1,g,1\n1,1,5,fodder,S1,g,0\n");
            Place(run, "sword", 1, 0);
            run.Start();

            run.Advance(120);

            Assert.That((run.Field.Enemies.Count, run.Phase), Is.EqualTo((0, WavePhase.Fighting)));
        }

        // T342（出目の効果）までのつなぎ: バーだけが旧効果の攻撃力アップを足す。
        [TestCase(SlotSymbol.Bar, 1.5)]
        [TestCase(SlotSymbol.Coin, 1.0)]
        [TestCase(SlotSymbol.Red7, 1.0)]
        [Description("FR-042（つなぎ）: バーの図柄だけが戦闘の攻撃力の倍率を上げる")]
        public void Slot_Symbol_Grants_Its_Effect(SlotSymbol symbol, double attackRate)
        {
            var run = Create();

            run.ApplySlot(symbol, SlotTestRules.Create(atkRate: 1.5));

            Assert.That((run.Field.Effects.AttackRate, run.Field.Effects.SpeedRate), Is.EqualTo((attackRate, 1.0)));
        }

        [Test]
        [Description("plan 5章: 効果の残り時間は準備中は減らない")]
        public void Effects_Do_Not_Wear_Off_While_Preparing()
        {
            var run = Create();
            run.ApplySlot(SlotSymbol.Bar, SlotTestRules.Create(atkSeconds: 20));

            run.Advance(600);

            Assert.That(run.Field.Effects.AttackSeconds, Is.EqualTo(20.0));
        }

        [Test]
        [Description("plan 5章: 効果はステージをまたいで持ち越す")]
        public void Effects_Carry_Over_To_The_Next_Stage()
        {
            var run = Create();
            Place(run, "sword", 1, 0);
            run.ApplySlot(SlotSymbol.Bar, SlotTestRules.Create(atkRate: 1.5, atkSeconds: 100));

            PlayStage(run);

            Assert.That((run.Stage, run.Field.Effects.AttackRate), Is.EqualTo((2, 1.5)));
        }
    }
}
