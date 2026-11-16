using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Slot;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("FR-010, FR-013〜016, FR-019: 戦闘マスの開始の演出・1ウェーブ・終了・ストック・報酬とマスをまたぐ状態（AC-010, AC-012, AC-016）")]
    public sealed class BattleNodeRunTests
    {
        // フィクスチャの地図（4×2）。地上マスは (1,0)(2,0)、高台マスは (1,1)(3,1)。
        //   S1 G G E
        //   S2 H X H
        // fodder（HP1・攻撃0・1マス/秒・コインだけ落とす）は 0 秒に出て約3秒でゴールに着く。行は encounter_id,time,enemy_id,spawn,path_id,is_boss。
        private const string OneFodder = "e,0,fodder,S1,g,0\n";

        // 通常 e は報酬 所持金10・コイン3、ボス boss は報酬なし、最初 first は回数固定6。
        private const string Encounters =
            "id,kind,map_id,min_floor,max_floor,fixed_spins,reward_money,reward_coins\n" +
            "e,battle,m,1,8,0,10,3\n" +
            "boss,boss,m,1,99,0,0,0\n" +
            "first,first,m,1,1,6,10,3\n";

        // 10 秒あればどの戦闘も終わる。
        private const int WaveSteps = 600;

        // 開始の演出は既定 1 秒。
        private const int IntroSteps = SimulationClock.StepsPerSecond;

        private static readonly EncounterTable EncounterFixtures =
            EncounterTable.Load(CsvParser.Parse(EncounterTable.FileName, Encounters));

        private static readonly SkillTable Skills = BattleFixtures.LoadSkills();

        private GameConfig _config;
        private RunState _state;
        private string _waveRows;

        private BattleNodeRun Create(int money = 100, int coins = 50, int life = 10, int deployLimit = 8,
            string waveRows = OneFodder, string encounterId = "e", double introSeconds = 1)
        {
            _config = GameConfig.Load(CsvParser.Parse(GameConfig.FileName,
                $"key,value\ninitial_coins,{coins}\ninitial_money,{money}\nbet,3\nlife,{life}\n" +
                $"deploy_limit,{deployLimit}\nretreat_refund_rate,0.5\nintro_seconds,{introSeconds}\n"));
            _state = new RunState(_config, BattleFixtures.LoadUpgrades(), SlotTestRules.Create(), 1);
            _waveRows = waveRows;
            return Next(encounterId);
        }

        /// <summary>同じ <see cref="RunState"/> で次の戦闘マスに入る（Runtime の流れと同じ）。</summary>
        private BattleNodeRun Next(string encounterId = "e")
        {
            var encounter = EncounterFixtures.Get(encounterId);
            var rows = _waveRows.Replace("e,", encounterId + ",");
            return new BattleNodeRun(_config, BattleFixtures.LoadUnits(), BattleFixtures.LoadRanges(), _state, encounter,
                BattleFixtures.LoadMap(), BattleFixtures.LoadEncounterWave(rows, encounterId),
                new SeededRandom(1));
        }

        private static SummonResult Place(BattleNodeRun run, string unitId, int x, int y)
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

        private static void SkipIntro(BattleNodeRun run) => run.Advance(IntroSteps);

        private static void PlayNode(BattleNodeRun run) => run.Advance(IntroSteps + WaveSteps);

        /// <summary>剣士を1体置いて1つ目の戦闘マスを終え、次のマスに入った状態（剣士のストック1）。</summary>
        private BattleNodeRun ClearFirstNodeWithSword(int money = 100, int deployLimit = 8)
        {
            var first = Create(money: money, deployLimit: deployLimit);
            Place(first, "sword", 1, 0);
            PlayNode(first);
            Assert.That((first.Phase, first.Stock.Count("sword")), Is.EqualTo((WavePhase.Completed, 1)),
                "剣士1体で戦闘マスを終えられない");
            return Next();
        }

        [Test]
        [Description("AC-016: 戦闘マスは準備を経ずに開始の演出から始まる")]
        public void Node_Starts_With_The_Intro()
        {
            var run = Create();

            Assert.That(run.Phase, Is.EqualTo(WavePhase.Intro));
        }

        [Test]
        [Description("AC-016: 開始の演出のあいだは戦闘の時間が進まない")]
        public void Battle_Time_Does_Not_Advance_During_The_Intro()
        {
            var run = Create();

            run.Advance(IntroSteps - 1);

            Assert.That((run.Phase, run.Field.Tick, run.Field.Enemies.Count), Is.EqualTo((WavePhase.Intro, 0L, 0)));
        }

        [Test]
        [Description("AC-016: 開始の演出が intro_seconds で終わると戦闘が始まり、残りのステップで時間が進む")]
        public void Fight_Begins_When_The_Intro_Ends()
        {
            var run = Create();

            run.Advance(IntroSteps + 1);

            Assert.That((run.Phase, run.Field.Tick, run.Field.Enemies.Count), Is.EqualTo((WavePhase.Fighting, 1L, 1)));
        }

        [Test]
        public void Intro_Seconds_Set_The_Intro_Length()
        {
            var run = Create(introSeconds: 2);

            run.Advance(2 * SimulationClock.StepsPerSecond - 1);

            Assert.That(run.Phase, Is.EqualTo(WavePhase.Intro));
        }

        [Test]
        [Description("plan 17.2: 開始の演出の進み具合（表示用）は 0 から 1 へ進む")]
        public void Intro_Progress_Goes_From_Zero_To_One()
        {
            var run = Create(introSeconds: 2);
            var start = run.IntroProgress;

            run.Advance(SimulationClock.StepsPerSecond);
            var half = run.IntroProgress;
            run.Advance(SimulationClock.StepsPerSecond);

            Assert.That((start, half, run.IntroProgress), Is.EqualTo((0.0, 0.5, 1.0)));
        }

        [Test]
        public void Zero_Intro_Seconds_Start_The_Fight_At_Once()
        {
            Assert.That(Create(introSeconds: 0).Phase, Is.EqualTo(WavePhase.Fighting));
        }

        [Test]
        [Description("D-41: 所持金での召喚は戦闘中に待ち時間を始める")]
        public void Cooldown_Applies_During_The_Fight()
        {
            var run = Create();
            SkipIntro(run);
            Place(run, "sword", 1, 0);

            Assert.That(run.Deployment.Select("sword"), Is.EqualTo(SummonResult.CoolingDown));
        }

        [Test]
        [Description("FR-022, D-79: 待ち時間中でも無料ストックからは配置できる")]
        public void Stock_Can_Be_Placed_While_The_Type_Cools_Down()
        {
            var run = ClearFirstNodeWithSword();
            SkipIntro(run);
            _state.Stock.TryTake("sword");
            Place(run, "sword", 1, 0);
            _state.Stock.Add("sword", 1);

            Assert.That(Place(run, "sword", 2, 0), Is.EqualTo(SummonResult.Summoned));
        }

        [Test]
        [Description("FR-013: ゴールに着いた敵の分だけ RunState のライフが減る")]
        public void Goal_Reduces_The_Run_Life()
        {
            var run = Create(life: 10);

            PlayNode(run);

            Assert.That((_state.Life, run.Field.Life), Is.EqualTo((9, 9)));
        }

        [Test]
        [Description("AC-010: ライフが 0 でゲームオーバーになり、終了を知らせる")]
        public void Losing_All_Life_Ends_The_Run()
        {
            var run = Create(life: 1);
            var ended = 0;
            run.Ended += () => ended++;

            PlayNode(run);

            Assert.That((run.Phase, _state.IsGameOver, ended), Is.EqualTo((WavePhase.GameOver, true, 1)));
        }

        [Test]
        [Description("AC-010, D-78: 1ウェーブを終えると戦闘マスが終わる（ボスでないのでクリアではない）")]
        public void Clearing_The_Wave_Completes_The_Node()
        {
            var run = Create();
            var ended = 0;
            run.Ended += () => ended++;

            PlayNode(run);

            Assert.That((run.Phase, run.IsRunCleared, ended), Is.EqualTo((WavePhase.Completed, false, 1)));
        }

        [Test]
        [Description("AC-010: ボスマスを終えるとクリア")]
        public void Clearing_The_Boss_Node_Clears_The_Run()
        {
            var run = Create(encounterId: "boss");

            PlayNode(run);

            Assert.That((run.Phase, run.IsRunCleared), Is.EqualTo((WavePhase.Completed, true)));
        }

        [Test]
        [Description("AC-010: 最終ボスを倒しても、残りの敵を全滅させるまで終わらない（2026-10-30 ユーザー決定）")]
        public void Defeating_The_Boss_Does_Not_End_The_Node_While_Enemies_Remain()
        {
            // ボス役の fodder は約 0.5 秒で剣士に倒される。雑魚は 5 秒に出る。
            var run = Create(encounterId: "boss",
                waveRows: "e,0,fodder,S1,g,1\ne,5,fodder,S1,g,0\n");
            Place(run, "sword", 1, 0);

            run.Advance(IntroSteps + 120);

            Assert.That((run.Field.Enemies.Count, run.Phase), Is.EqualTo((0, WavePhase.Fighting)));
        }

        [Test]
        [Description("plan 16.2: 戦闘マスを終えると報酬（所持金・コイン）が入る")]
        public void Completing_The_Node_Adds_The_Reward()
        {
            // 誰も置かないので fodder はゴールに着き、何も落とさない。
            var run = Create(money: 100, coins: 50);

            PlayNode(run);

            Assert.That((_state.Money.Coins, _state.Coins.Coins), Is.EqualTo((110, 53)));
        }

        [Test]
        [Description("plan 16.2: ゲームオーバーでは報酬は入らない。ライフボーナスもない")]
        public void Game_Over_Gives_No_Reward()
        {
            var run = Create(money: 100, coins: 50, life: 1);

            PlayNode(run);

            Assert.That((_state.Money.Coins, _state.Coins.Coins), Is.EqualTo((100, 50)));
        }

        [Test]
        [Description("D-68: 回数固定の回数は出現表の行から読む")]
        public void Fixed_Spins_Come_From_The_Encounter()
        {
            Assert.That((Create(encounterId: "first").Encounter.FixedSpins, Create().Encounter.FixedSpins),
                Is.EqualTo((6, 0)));
        }

        [Test]
        [Description("AC-012: 配置中だった数と同数の無料ストックができる")]
        public void Deployed_Units_Become_Free_Stock_By_Type()
        {
            // 剣士が fodder を止めて倒し、弓とバッファーは高台に居るだけ（同じ種類の2体目は待ち時間に当たるので別の種類）。
            var run = Create(money: 1000);
            Place(run, "sword", 1, 0);
            Place(run, "archer", 1, 1);
            Place(run, "buffer", 3, 1);

            PlayNode(run);

            Assert.That((run.Stock.Count("sword"), run.Stock.Count("archer"), run.Stock.Count("buffer"), run.Stock.Count("tank")),
                Is.EqualTo((1, 1, 1, 0)));
        }

        [Test]
        public void Next_Node_Starts_With_An_Empty_Field()
        {
            var run = ClearFirstNodeWithSword();

            Assert.That((run.Field.Units.Count, run.Summoner.DeployedCount, run.Phase),
                Is.EqualTo((0, 0, WavePhase.Intro)));
        }

        [Test]
        [Description("AC-012, FR-013: ライフは戦闘マスをまたいで引き継ぐ（満タンに戻らない）")]
        public void Life_Carries_Over_To_The_Next_Node()
        {
            // fodder がゴールに着いてライフ 9。
            var first = Create(life: 10);
            PlayNode(first);

            var next = Next();

            Assert.That((_state.Life, next.Field.Life), Is.EqualTo((9, 9)));
        }

        [Test]
        [Description("AC-012: 次のマスで残りライフ分の敵が着くとゲームオーバー")]
        public void Carried_Life_Can_Run_Out_In_The_Next_Node()
        {
            var first = Create(life: 2);
            PlayNode(first);
            var next = Next();

            PlayNode(next);

            Assert.That((next.Phase, _state.Life), Is.EqualTo((WavePhase.GameOver, 0)));
        }

        [Test]
        [Description("AC-012: 強化・所持金・コインは次のマスに残る")]
        public void Upgrades_Money_And_Coins_Carry_Over()
        {
            var first = Create(money: 100, coins: 50);
            first.Deployment.Upgrade("sword");
            PlayNode(first);

            var next = Next();

            Assert.That((next.Upgrades.Level("sword"), next.Wallet.Coins, _state.Coins.Coins),
                Is.EqualTo((2, 90, 53)));
        }

        [Test]
        [Description("FR-016: 無料ストックは所持金なしで配置できる")]
        public void Placing_From_Stock_Costs_No_Money()
        {
            var run = ClearFirstNodeWithSword();
            var money = _state.Money.Coins;

            Place(run, "sword", 1, 0);

            Assert.That((_state.Money.Coins, run.Stock.Count("sword")), Is.EqualTo((money, 0)));
        }

        [Test]
        public void Stock_Can_Be_Placed_Without_Enough_Money()
        {
            // 剣士 12 をちょうど払い、報酬 10 が入っても 12 には届かない（fodder は所持金を落とさない）。
            var run = ClearFirstNodeWithSword(money: 12);

            Assert.That((_state.Money.Coins < 12, Place(run, "sword", 1, 0)), Is.EqualTo((true, SummonResult.Summoned)));
        }

        [Test]
        [Description("plan C-3: ストックからの配置も配置上限に数える")]
        public void Stock_Placement_Counts_Toward_The_Deploy_Limit()
        {
            var run = ClearFirstNodeWithSword(deployLimit: 1);
            Place(run, "sword", 1, 0);

            Assert.That(run.Deployment.Select("archer"), Is.EqualTo(SummonResult.DeployLimitReached));
        }

        [Test]
        [Description("AC-012: 強化が残り、ストックから置いた個体は HP 全快")]
        public void Stock_Unit_Has_Full_Hp_At_The_Carried_Upgrade()
        {
            // 剣士は段階2で HP×1.5 → 1800。
            var first = Create();
            Place(first, "sword", 1, 0);
            first.Deployment.Upgrade("sword");
            PlayNode(first);
            var next = Next();

            Place(next, "sword", 1, 0);

            var unit = next.Field.Units[0];
            Assert.That((unit.MaxHp, unit.Hp), Is.EqualTo((1800, 1800)));
        }

        [Test]
        public void Unused_Stock_Carries_Over_To_Later_Nodes()
        {
            var second = ClearFirstNodeWithSword();
            PlayNode(second);

            Next();

            Assert.That(_state.Stock.Count("sword"), Is.EqualTo(1));
        }

        [Test]
        [Description("plan C-3: ストック由来の味方の撤退も召喚コストを基準に返す")]
        public void Retreating_A_Stock_Unit_Refunds_Half_Its_Cost()
        {
            var run = ClearFirstNodeWithSword();
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
            var run = Create(waveRows: "e,0,rich,S1,g,0\ne,5,fodder,S1,g,0\n");
            Place(run, "sword", 1, 0);
            var coins = _state.Coins.Coins;
            var money = _state.Money.Coins;

            run.Advance(IntroSteps + 120);

            Assert.That((_state.Coins.Coins - coins is >= 2 and <= 3, _state.Money.Coins - money),
                Is.EqualTo((true, 0)));
        }

        [Test]
        [Description("plan 16.2: 所持金のドロップは戦闘中に所持金へ入り、コインは変わらない")]
        public void Money_Drops_Go_Into_The_Money_During_The_Fight()
        {
            // banker は倒されると必ず所持金を 2〜3 落とす。
            var run = Create(waveRows: "e,0,banker,S1,g,0\ne,5,fodder,S1,g,0\n");
            Place(run, "sword", 1, 0);
            var coins = _state.Coins.Coins;
            var money = _state.Money.Coins;

            run.Advance(IntroSteps + 120);

            Assert.That((_state.Money.Coins - money is >= 2 and <= 3, _state.Coins.Coins - coins),
                Is.EqualTo((true, 0)));
        }

        [TestCase(SlotSymbol.Red7)]
        [TestCase(SlotSymbol.Yellow7)]
        [Description("AC-042: 色7が揃うとその色のスキルゲージが1段たまる")]
        public void Color_Seven_Fills_Its_Skill_Gauge(SlotSymbol color)
        {
            var run = Create();

            run.ApplySlot(color);

            Assert.That(_state.Gauges.Level(color), Is.EqualTo(1));
        }

        [Test]
        [Description("AC-042: バーは戦闘の攻撃力の倍率を上げ、所持金の倍率は変えない")]
        public void Bar_Raises_The_Attack_Rate_Of_The_Battle()
        {
            var run = Create();

            run.ApplySlot(SlotSymbol.Bar);

            Assert.That((run.Field.Boosts.AttackRate, run.Field.Boosts.MoneyRate), Is.EqualTo((1.1, 1.0)));
        }

        [Test]
        [Description("AC-042: コインは所持金の倍率を上げ、攻撃力の倍率は変えない")]
        public void Coin_Raises_The_Money_Rate_Of_The_Battle()
        {
            var run = Create();

            run.ApplySlot(SlotSymbol.Coin);

            Assert.That((run.Field.Boosts.AttackRate, run.Field.Boosts.MoneyRate), Is.EqualTo((1.0, 1.1)));
        }

        [Test]
        [Description("AC-042: バーとコインはゲージをためない")]
        public void Bar_And_Coin_Do_Not_Fill_Gauges()
        {
            var run = Create();

            run.ApplySlot(SlotSymbol.Bar);
            run.ApplySlot(SlotSymbol.Coin);

            Assert.That(_state.Gauges.Level(SlotSymbol.Red7) + _state.Gauges.Level(SlotSymbol.Blue7) +
                _state.Gauges.Level(SlotSymbol.Green7) + _state.Gauges.Level(SlotSymbol.Yellow7), Is.EqualTo(0));
        }

        [Test]
        [Description("AC-012, plan 16.4: バー・コインの倍率は戦闘マスごとに新しくなる")]
        public void Boosts_Reset_For_The_Next_Node()
        {
            var first = Create();
            first.ApplySlot(SlotSymbol.Bar);
            first.ApplySlot(SlotSymbol.Coin);
            PlayNode(first);

            var next = Next();

            Assert.That((next.Field.Boosts.AttackRate, next.Field.Boosts.MoneyRate), Is.EqualTo((1.0, 1.0)));
        }

        [Test]
        [Description("AC-012, plan 16.4: スキルゲージは戦闘マスをまたいで残る")]
        public void Skill_Gauges_Carry_Over_To_The_Next_Node()
        {
            var first = Create();
            first.ApplySlot(SlotSymbol.Green7);
            PlayNode(first);

            Next();

            Assert.That(_state.Gauges.Level(SlotSymbol.Green7), Is.EqualTo(1));
        }

        private void FillGauge(SlotSymbol color)
        {
            while (!_state.Gauges.IsFull(color))
            {
                _state.Gauges.Add(color);
            }
        }

        [Test]
        [Description("AC-023: 満タンで配置1体以上なら発動し、ゲージが0になり、配置中のタンクに効果が付く")]
        public void Full_Gauge_Activates_The_Skill_And_Empties_The_Gauge()
        {
            var run = Create();
            Place(run, "tank", 1, 0);
            SkipIntro(run);
            FillGauge(SlotSymbol.Yellow7);

            var result = run.TryActivateSkill(Skills.For(SlotSymbol.Yellow7));

            Assert.That((result, _state.Gauges.Level(SlotSymbol.Yellow7), run.Field.Units[0].SkillStepsLeft),
                Is.EqualTo((SkillResult.Activated, 0, 600)));
        }

        [Test]
        public void Skill_Can_Be_Activated_While_Fighting()
        {
            var run = Create();
            Place(run, "tank", 1, 0);
            run.Advance(IntroSteps + 10);
            FillGauge(SlotSymbol.Yellow7);

            Assert.That(run.TryActivateSkill(Skills.For(SlotSymbol.Yellow7)), Is.EqualTo(SkillResult.Activated));
        }

        [Test]
        [Description("AC-016, plan 17.2: 開始の演出のあいだはスキルを拒否し、ゲージは満タンのまま")]
        public void Skill_During_The_Intro_Is_Refused()
        {
            var run = Create();
            Place(run, "tank", 1, 0);
            FillGauge(SlotSymbol.Yellow7);

            var result = run.TryActivateSkill(Skills.For(SlotSymbol.Yellow7));

            Assert.That((result, _state.Gauges.IsFull(SlotSymbol.Yellow7)), Is.EqualTo((SkillResult.NotInBattle, true)));
        }

        [Test]
        [Description("AC-023: 満タンでなければ発動せず、ゲージも効果も変わらない")]
        public void Gauge_Not_Full_Refuses_And_Changes_Nothing()
        {
            var run = Create();
            Place(run, "tank", 1, 0);
            SkipIntro(run);
            _state.Gauges.Add(SlotSymbol.Yellow7);

            var result = run.TryActivateSkill(Skills.For(SlotSymbol.Yellow7));

            Assert.That((result, _state.Gauges.Level(SlotSymbol.Yellow7), run.Field.Units[0].SkillStepsLeft),
                Is.EqualTo((SkillResult.NotFull, 1, 0)));
        }

        [Test]
        [Description("AC-023: その職が配置されていなければ発動せず、ゲージは満タンのまま")]
        public void No_Placed_Unit_Refuses_And_Keeps_The_Gauge()
        {
            var run = Create();
            Place(run, "sword", 1, 0);
            SkipIntro(run);
            FillGauge(SlotSymbol.Yellow7);

            var result = run.TryActivateSkill(Skills.For(SlotSymbol.Yellow7));

            Assert.That((result, _state.Gauges.IsFull(SlotSymbol.Yellow7)), Is.EqualTo((SkillResult.NoUnits, true)));
        }

        [Test]
        [Description("plan 16.4: 戦闘中でない（ゲームオーバー後）なら発動せず、ゲージは満タンのまま")]
        public void Skill_After_Game_Over_Is_Refused()
        {
            var run = Create(life: 1);
            Place(run, "buffer", 1, 1);
            PlayNode(run);
            Assert.That(run.Phase, Is.EqualTo(WavePhase.GameOver), "ゲームオーバーにならない");
            FillGauge(SlotSymbol.Green7);

            var result = run.TryActivateSkill(Skills.For(SlotSymbol.Green7));

            Assert.That((result, _state.Gauges.IsFull(SlotSymbol.Green7)), Is.EqualTo((SkillResult.NotInBattle, true)));
        }

        [Test]
        [Description("plan 16.4: 戦闘マスを終えた後も発動しない")]
        public void Skill_After_The_Node_Is_Refused()
        {
            var run = Create();
            Place(run, "tank", 1, 0);
            PlayNode(run);
            FillGauge(SlotSymbol.Yellow7);

            Assert.That(run.TryActivateSkill(Skills.For(SlotSymbol.Yellow7)), Is.EqualTo(SkillResult.NotInBattle));
        }

        // 0 秒に1体、5 秒に2体の fodder（計3体）。
        private const string ThreeFodders = "e,0,fodder,S1,g,0\ne,5,fodder,S1,g,0\ne,5,fodder,S1,g,0\n";

        [Test]
        [Description("AC-017: 残りの敵の数はウェーブの敵の総数から始まる（まだ出ていない敵も数える）")]
        public void Remaining_Enemies_Start_At_The_Wave_Total()
        {
            var run = Create(waveRows: ThreeFodders);

            Assert.That(run.RemainingEnemies, Is.EqualTo(3));
        }

        [Test]
        [Description("AC-017: 撃破で残りの敵の数が1減る")]
        public void Defeating_An_Enemy_Reduces_The_Remaining_Count()
        {
            // 0 秒の fodder は約 0.5 秒で剣士に倒される。残りの2体は 5 秒まで出ない。
            var run = Create(waveRows: ThreeFodders);
            Place(run, "sword", 1, 0);

            run.Advance(IntroSteps + 120);

            Assert.That((run.Field.Enemies.Count, run.RemainingEnemies), Is.EqualTo((0, 2)));
        }

        [Test]
        [Description("AC-017: ゴールでも残りの敵の数が1減る")]
        public void An_Enemy_Reaching_The_Goal_Reduces_The_Remaining_Count()
        {
            // 0 秒の fodder は約 3 秒でゴールに着く。
            var run = Create(waveRows: ThreeFodders, life: 10);

            run.Advance(IntroSteps + 240);

            Assert.That((_state.Life, run.RemainingEnemies), Is.EqualTo((9, 2)));
        }

        [Test]
        [Description("AC-017: 戦闘マスを終えると残りの敵の数は 0")]
        public void Remaining_Enemies_Reach_Zero_When_The_Node_Ends()
        {
            var run = Create(waveRows: ThreeFodders);

            PlayNode(run);

            Assert.That((run.Phase, run.RemainingEnemies), Is.EqualTo((WavePhase.Completed, 0)));
        }
    }
}
