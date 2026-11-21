using System.Text.RegularExpressions;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Slot;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("FR-010, FR-013〜016, FR-019: 戦闘マスの開始の演出・1ウェーブ・終了・報酬とマスをまたぐ状態、spec 002 の無料の1体と持ち越し（AC-010, AC-012, AC-016）")]
    public sealed class BattleNodeRunTests
    {
        // フィクスチャの地図（4×2）。地上マスは (1,0)(2,0)、高台マスは (1,1)(3,1)。
        //   S1 G G E
        //   S2 H X H
        // fodder（HP1・攻撃0・1マス/秒・コインだけ落とす）は 0 秒に出て約3秒でゴールに着く。行は e,time,enemy_id,spawn,path_id,is_boss（e は stage 列の 1 に置き換えて読む）。
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
            string waveRows = OneFodder, string encounterId = "e", double introSeconds = 1, int stage = 2)
        {
            _config = GameConfig.Load(CsvParser.Parse(GameConfig.FileName,
                $"key,value\ninitial_coins,{coins}\ninitial_money,{money}\nbet,3\nlife,{life}\n" +
                $"deploy_limit,{deployLimit}\nretreat_refund_rate,0.5\nintro_seconds,{introSeconds}\nplacement_time_scale,0.1\nwave_gap_seconds,1\n"));
            _state = new RunState(_config, BattleFixtures.LoadUpgrades(), SlotTestRules.Create(), 1);
            // 既定はステージ2（無料の1体なし）。無料の1体を調べるテストだけ stage: 1。
            for (var i = 1; i < stage; i++)
            {
                _state.AdvanceStage();
            }

            _waveRows = waveRows;
            return Next(encounterId);
        }

        /// <summary>同じ <see cref="RunState"/> で次の戦闘マスに入る（Runtime の流れと同じ）。</summary>
        private BattleNodeRun Next(string encounterId = "e")
        {
            var encounter = EncounterFixtures.Get(encounterId);
            // 出現はどの戦闘マスでもステージ1の行として読む（行の先頭の "e," を stage 列の "1," にする）。
            var rows = Regex.Replace(_waveRows, "^e,", "1,", RegexOptions.Multiline);
            return new BattleNodeRun(_config, BattleFixtures.LoadUnits(), BattleFixtures.LoadRanges(), _state, encounter,
                BattleFixtures.LoadMap(), BattleFixtures.LoadStageWave(rows, 1),
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

        /// <summary>剣士を1体置いて1つ目の戦闘マスを終え、次のマスに入った状態。</summary>
        private BattleNodeRun ClearFirstNodeWithSword(int money = 100, int deployLimit = 8)
        {
            var first = Create(money: money, deployLimit: deployLimit);
            Place(first, "sword", 1, 0);
            PlayNode(first);
            Assert.That(first.Phase, Is.EqualTo(WavePhase.Completed),
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
        [Description("spec 002 FR-106: 配置中だった味方はストックにならず、次の戦闘では無料で置けない")]
        public void Deployed_Units_Do_Not_Carry_Over_As_Free_Units()
        {
            var run = ClearFirstNodeWithSword(money: 12);

            Assert.That((run.Summoner.FreeSummons, Place(run, "sword", 1, 0)), Is.EqualTo((0, SummonResult.NotEnoughCoins)));
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

        [TestCase("tank", 1, 0)]
        [TestCase("sword", 1, 0)]
        [TestCase("archer", 1, 1)]
        [TestCase("buffer", 1, 1)]
        [Description("spec 002 AC-105: ステージ1では所持金0でも最初の1体をどの職でも置け、待ち時間が始まらない")]
        public void Stage_One_First_Unit_Is_Free_For_Any_Type(string unitId, int x, int y)
        {
            var run = Create(money: 0, stage: 1);

            Assert.That((Place(run, unitId, x, y), _state.Money.Coins, run.Summoner.CooldownRemaining(unitId)),
                Is.EqualTo((SummonResult.Summoned, 0, 0.0)));
        }

        [Test]
        [Description("spec 002 AC-105: ステージ1の2体目は費用が要る")]
        public void Stage_One_Second_Unit_Needs_The_Cost()
        {
            var run = Create(money: 0, stage: 1);
            Place(run, "sword", 1, 0);

            Assert.That(run.Deployment.Select("archer"), Is.EqualTo(SummonResult.NotEnoughCoins));
        }

        [Test]
        [Description("spec 002 AC-105: ステージ2以降は無料にならない")]
        public void Stage_Two_Has_No_Free_Unit()
        {
            var run = Create(money: 0, stage: 2);

            Assert.That((run.Summoner.FreeSummons, run.Deployment.Select("sword")), Is.EqualTo((0, SummonResult.NotEnoughCoins)));
        }

        [Test]
        [Description("spec 002 E-25: 無料の1体の撤退は返還がなく、有料の2体目は今までどおり半分返る")]
        public void Retreating_The_Free_Unit_Refunds_Nothing()
        {
            var run = Create(money: 100, stage: 1);
            Place(run, "sword", 1, 0);
            Place(run, "archer", 1, 1);
            var free = run.Field.Units[0];
            var paid = run.Field.Units[1];

            Assert.That((run.Deployment.RetreatRefund(free), run.Deployment.RetreatRefund(paid)), Is.EqualTo((0, 7)));
            run.Deployment.Retreat(free);
            run.Deployment.Retreat(paid);
            Assert.That((_state.Money.Coins, run.Summoner.DeployedCount), Is.EqualTo((100 - 14 + 7, 0)));
        }

        [Test]
        [Description("spec 002 AC-103: バーとコインの効果は次の戦闘に持ち越さない")]
        public void Slot_Effects_Do_Not_Carry_Over()
        {
            var first = Create();
            SkipIntro(first);
            first.ApplySlot(SlotSymbol.Bar);
            first.ApplySlot(SlotSymbol.Coin);
            Assert.That((first.Field.Effects.IsAttackUp, first.Field.Effects.Coins), Is.EqualTo((true, 1)));

            var next = Next();

            Assert.That((next.Field.Effects.IsAttackUp, next.Field.Effects.Coins, next.Field.Effects.MoneyRate),
                Is.EqualTo((false, 0, 1.0)));
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

        [Test]
        [Description("FR-112, E-14: 色7はその場の効果を出し、スキルゲージはためない")]
        public void Color_Seven_Applies_Its_Effect_Without_Filling_Gauges()
        {
            var run = Create();

            run.ApplySlot(SlotSymbol.Blue7);
            run.ApplySlot(SlotSymbol.Yellow7);

            Assert.That((run.Field.Effects.IsStopped, run.Field.Effects.IsPoisoned,
                _state.Gauges.Level(SlotSymbol.Blue7) + _state.Gauges.Level(SlotSymbol.Yellow7)),
                Is.EqualTo((true, true, 0)));
        }

        [Test]
        [Description("FR-112: バーは戦闘の攻撃力の倍率を上げ（bar_attack_rate=2）、所持金の倍率は変えない")]
        public void Bar_Raises_The_Attack_Rate_Of_The_Battle()
        {
            var run = Create();

            run.ApplySlot(SlotSymbol.Bar);

            Assert.That((run.Field.Effects.AttackRate, run.Field.Effects.MoneyRate), Is.EqualTo((2.0, 1.0)));
        }

        [Test]
        [Description("AC-042: コインは所持金の倍率を上げ、攻撃力の倍率は変えない")]
        public void Coin_Raises_The_Money_Rate_Of_The_Battle()
        {
            var run = Create();

            run.ApplySlot(SlotSymbol.Coin);

            Assert.That((run.Field.Effects.AttackRate, run.Field.Effects.MoneyRate), Is.EqualTo((1.0, 1.1)));
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
        [Description("E-16: バー・コインの効果は戦闘ごとに新しくなる（持ち越さない）")]
        public void Boosts_Reset_For_The_Next_Node()
        {
            var first = Create();
            first.ApplySlot(SlotSymbol.Bar);
            first.ApplySlot(SlotSymbol.Coin);
            PlayNode(first);

            var next = Next();

            Assert.That((next.Field.Effects.AttackRate, next.Field.Effects.MoneyRate), Is.EqualTo((1.0, 1.0)));
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
