using System;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Slot;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    /// <summary>
    /// 出目の効果（FR-112、AC-109 の Core 部分）。値は slot_rules.csv の仮値（赤5・青3秒・緑50%・黄 1秒ごとに1を5秒・バー×2を10秒・コイン +0.1 上限1.5）。
    /// 地図と敵は <see cref="BattleFixtures"/>。1 ステップ = 1/60 秒。
    /// </summary>
    public sealed class SlotEffectsTests
    {
        private static Battlefield Create(string waveRows) =>
            new Battlefield(BattleFixtures.LoadWave(waveRows), BattleFixtures.LoadRanges(), 10, new SeededRandom(1),
                new SlotEffects(SlotTestRules.Create()));

        private static BattleUnit Place(Battlefield field, string unitId, int x, int y) =>
            field.AddUnit(BattleFixtures.LoadUnits().Get(unitId), new GridPoint(x, y), Facing.Right);

        /// <summary>剣士（(1,0)）を brute にブロックさせ、最初の攻撃を受けるまで進める。</summary>
        private static BattleUnit SwordHitByBrute(Battlefield field)
        {
            var sword = Place(field, "sword", 1, 0);
            for (var i = 0; i < 120 && sword.Hp == sword.MaxHp; i++)
            {
                field.Advance(1);
            }

            Assume.That(sword.Hp, Is.LessThan(sword.MaxHp));
            return sword;
        }

        [Test]
        [Description("AC-109: 赤7は空中を含む全ての敵に、防御を無視した固定ダメージ")]
        public void Red_Damages_All_Enemies_Including_Air_Ignoring_Defence()
        {
            var field = Create("0,melee,S1,g,0\n0,air,S2,a,0\n");
            field.Advance(1);

            field.ApplySlot(SlotSymbol.Red7);

            Assert.That((field.Enemies[0].Hp, field.Enemies[1].Hp), Is.EqualTo((1000 - 5, 600 - 5)));
        }

        [Test]
        [Description("AC-109: 赤7で倒した敵もドロップする")]
        public void Enemy_Killed_By_Red_Drops()
        {
            var field = Create("0,rich,S1,g,0\n");
            field.Advance(1);

            field.ApplySlot(SlotSymbol.Red7);

            Assert.That((field.Enemies.Count, field.DroppedCoins is >= 2 and <= 3), Is.EqualTo((0, true)));
        }

        private static Battlefield CreateDelayed(string waveRows) =>
            new Battlefield(BattleFixtures.LoadWave(waveRows), BattleFixtures.LoadRanges(), 10, new SeededRandom(1),
                new SlotEffects(SlotTestRules.Create(redDelaySeconds: 1)));

        [Test]
        [Description("spec 003 AC-005: 赤7のダメージは待ち時間（1秒=60ステップ）戦闘を進めてから一度だけ入り、そのとき通知する")]
        public void Red_Damage_Waits_For_The_Delay_Then_Strikes_Once()
        {
            var field = CreateDelayed("0,melee,S1,g,0\n");
            field.Advance(1);
            var struck = 0;
            field.RedStruck += () => struck++;

            field.ApplySlot(SlotSymbol.Red7);
            var applied = (field.Enemies[0].Hp, field.PendingRedStrikes, struck);
            field.Advance(59);
            var before = (field.Enemies[0].Hp, field.PendingRedStrikes, struck);
            field.Advance(1);
            var at = (field.Enemies[0].Hp, field.PendingRedStrikes, struck);
            field.Advance(30);

            Assert.That((applied, before, at, field.Enemies[0].Hp, struck), Is.EqualTo(
                ((1000, 1, 0), (1000, 1, 0), (995, 0, 1), 995, 1)));
        }

        [Test]
        [Description("spec 003 AC-005: 待っている間に出た敵にも当たり、戦闘を進めなければ（ステージが終わったら）入らない")]
        public void Red_Hits_Enemies_Spawned_While_Waiting_And_Needs_The_Battle_To_Advance()
        {
            var field = CreateDelayed("0,melee,S1,g,0\n0.5,air,S2,a,0\n");
            field.Advance(1);
            field.ApplySlot(SlotSymbol.Red7);
            var idle = field.Enemies[0].Hp;

            field.Advance(60);

            Assert.That((idle, field.Enemies.Count, field.Enemies[0].Hp, field.Enemies[1].Hp),
                Is.EqualTo((1000, 2, 995, 595)));
        }

        [Test]
        [Description("spec 003 AC-005: 続けて揃えるとそれぞれの待ちで別々に当たる")]
        public void Each_Red_Strikes_After_Its_Own_Delay()
        {
            var field = CreateDelayed("0,melee,S1,g,0\n");
            field.Advance(1);
            field.ApplySlot(SlotSymbol.Red7);
            field.Advance(30);
            field.ApplySlot(SlotSymbol.Red7);

            field.Advance(30);
            var first = field.Enemies[0].Hp;
            field.Advance(30);

            Assert.That((first, field.Enemies[0].Hp, field.PendingRedStrikes), Is.EqualTo((995, 990, 0)));
        }

        [Test]
        [Description("spec 003 FR-005: 正本の slot_rules.csv の待ちは1秒（60ステップ）")]
        public void Project_Red_Delay_Is_One_Second()
        {
            var rules = SlotRules.Load(SlotTestRules.ProjectTable(SlotRules.FileName));

            Assert.That(new SlotEffects(rules).RedDelaySteps, Is.EqualTo(SimulationClock.StepsPerSecond));
        }

        private static Battlefield CreateFreezeDelayed(string waveRows) =>
            new Battlefield(BattleFixtures.LoadWave(waveRows), BattleFixtures.LoadRanges(), 10, new SeededRandom(1),
                new SlotEffects(SlotTestRules.Create(blueDelaySeconds: 1)));

        [Test]
        [Description("spec 004 AC-002: 青7は待ち（1秒=60ステップ）の間は敵が動き、凍結の瞬間に一度だけ通知して、そこから180ステップ止まる")]
        public void Blue_Stop_Starts_When_It_Freezes_And_Lasts_Its_Full_Length()
        {
            var field = CreateFreezeDelayed("0,melee,S1,g,0\n");
            field.Advance(1);
            var froze = 0;
            field.BlueFroze += () => froze++;

            field.ApplySlot(SlotSymbol.Blue7);
            var start = field.Enemies[0].Distance;
            field.Advance(59);
            var waiting = (field.Enemies[0].Distance > start, field.Effects.IsStopped, field.Effects.IsStopPending, froze);
            field.Advance(1);
            var frozen = (field.Effects.IsStopped, field.Effects.IsStopPending, froze, field.Effects.StopStepsLeft);
            var at = field.Enemies[0].Distance;
            field.Advance(179);
            var still = (field.Enemies[0].Distance == at, field.Effects.IsStopped);
            field.Advance(2);

            Assert.That((waiting, frozen, still, field.Effects.IsStopped, field.Enemies[0].Distance > at, froze),
                Is.EqualTo(((true, false, true, 0), (true, false, 1, 180), (true, true), false, true, 1)));
        }

        [Test]
        [Description("spec 004 AC-002: 戦闘を進めなければ（ステージが終わったら）停止は始まらない。一度に進めたステップ数（倍速）でも同じ時点で凍る")]
        public void Blue_Freeze_Needs_The_Battle_To_Advance_And_Counts_Steps()
        {
            var idle = CreateFreezeDelayed("0,melee,S1,g,0\n");
            idle.Advance(1);
            idle.ApplySlot(SlotSymbol.Blue7);
            var fast = CreateFreezeDelayed("0,melee,S1,g,0\n");
            fast.Advance(1);
            fast.ApplySlot(SlotSymbol.Blue7);

            fast.Advance(61);

            Assert.That((idle.Effects.IsStopped, idle.Effects.IsStopPending, fast.Effects.IsStopped, fast.Effects.StopStepsLeft),
                Is.EqualTo((false, true, true, 179)));
        }

        [Test]
        [Description("spec 004 AC-003: 青7の待ちの間も青7は抽選から外れる")]
        public void Blue_Cannot_Roll_While_Waiting_To_Freeze()
        {
            var field = CreateFreezeDelayed("0,melee,S1,g,0\n");
            field.Advance(1);
            var before = field.Effects.CanRoll(SlotSymbol.Blue7);

            field.ApplySlot(SlotSymbol.Blue7);

            Assert.That((before, field.Effects.CanRoll(SlotSymbol.Blue7), field.Effects.IsActive(SlotSymbol.Blue7)),
                Is.EqualTo((true, false, true)));
        }

        [Test]
        [Description("spec 004 FR-003: 待ちが0なら揃った瞬間に止まり、凍結を通知する")]
        public void Blue_Without_Delay_Freezes_At_Once()
        {
            var field = Create("0,melee,S1,g,0\n");
            field.Advance(1);
            var froze = 0;
            field.BlueFroze += () => froze++;

            field.ApplySlot(SlotSymbol.Blue7);

            Assert.That((field.Effects.IsStopped, field.Effects.StopStepsLeft, froze), Is.EqualTo((true, 180, 1)));
        }

        [Test]
        [Description("spec 004 FR-003: 正本の slot_rules.csv の青7の待ちは1秒（60ステップ）")]
        public void Project_Blue_Delay_Is_One_Second()
        {
            var rules = SlotRules.Load(SlotTestRules.ProjectTable(SlotRules.FileName));

            Assert.That(new SlotEffects(rules).BlueDelaySteps, Is.EqualTo(SimulationClock.StepsPerSecond));
        }

        [Test]
        [Description("AC-109: 青7の間（3秒=180ステップ）敵は進まず、時間が切れると再び進む")]
        public void Blue_Stops_Enemies_Until_It_Ends()
        {
            var field = Create("0,melee,S1,g,0\n");
            field.Advance(1);
            var distance = field.Enemies[0].Distance;

            field.ApplySlot(SlotSymbol.Blue7);
            field.Advance(180);
            var stopped = (field.Enemies[0].Distance, field.Effects.IsActive(SlotSymbol.Blue7));
            field.Advance(1);

            Assert.That((stopped, field.Enemies[0].Distance > distance), Is.EqualTo(((distance, false), true)));
        }

        [Test]
        [Description("AC-109: 停止中に出た敵も止まっている")]
        public void Enemies_Spawned_While_Stopped_Stay_Still()
        {
            var field = Create("0.5,melee,S1,g,0\n");

            field.ApplySlot(SlotSymbol.Blue7);
            field.Advance(180);

            Assert.That((field.Enemies.Count, field.Enemies[0].Distance), Is.EqualTo((1, 0.0)));
        }

        [Test]
        [Description("AC-109: 停止中は射程のある敵も攻撃しない（タンクの HP が減らない）")]
        public void Stopped_Ranged_Enemy_Does_Not_Attack()
        {
            var field = Create("0,ranged,S1,g,0\n");
            var tank = Place(field, "tank", 1, 0);

            field.ApplySlot(SlotSymbol.Blue7);
            field.Advance(180);

            Assert.That((tank.Hp, field.Enemies[0].Distance), Is.EqualTo((tank.MaxHp, 0.0)));
        }

        [Test]
        [Description("AC-109: 停止中はブロック中の近距離の敵も攻撃しない")]
        public void Stopped_Blocked_Enemy_Does_Not_Attack()
        {
            var field = Create("0,brute,S1,g,0\n");
            var sword = SwordHitByBrute(field);
            var hp = sword.Hp;

            field.ApplySlot(SlotSymbol.Blue7);
            field.Advance(179);

            Assert.That(sword.Hp, Is.EqualTo(hp));
        }

        [Test]
        [Description("AC-109: 黄7は1秒ごとに1ダメージを5秒（5回）。防御を無視し、切れた後は減らない")]
        public void Yellow_Poisons_For_Its_Duration()
        {
            var field = Create("0,quaker,S1,g,0\n");
            field.ApplySlot(SlotSymbol.Yellow7);

            field.Advance(299);
            var before = field.Enemies[0].Hp;
            field.Advance(1);
            var end = (field.Enemies[0].Hp, field.Effects.IsActive(SlotSymbol.Yellow7));
            field.Advance(300);

            Assert.That((before, end, field.Enemies[0].Hp), Is.EqualTo((100000 - 4, (100000 - 5, false), 100000 - 5)));
        }

        [Test]
        [Description("AC-109, E-27: 毒は停止中も減る")]
        public void Poison_Works_While_Stopped()
        {
            var field = Create("0,melee,S1,g,0\n");
            field.ApplySlot(SlotSymbol.Blue7);
            field.ApplySlot(SlotSymbol.Yellow7);

            field.Advance(180);

            Assert.That((field.Enemies[0].Hp, field.Enemies[0].Distance), Is.EqualTo((1000 - 3, 0.0)));
        }

        [Test]
        [Description("AC-109: 毒で倒した敵もドロップする")]
        public void Enemy_Killed_By_Poison_Drops()
        {
            var field = Create("0,rich,S1,g,0\n");
            field.ApplySlot(SlotSymbol.Yellow7);

            field.Advance(59);
            var alive = field.Enemies.Count;
            field.Advance(1);

            Assert.That((alive, field.Enemies.Count, field.DroppedCoins is >= 2 and <= 3), Is.EqualTo((1, 0, true)));
        }

        [Test]
        [Description("AC-109: 緑7は味方の HP を最大 HP の50%回復し、最大値で止まる。ライフは回復しない")]
        public void Green_Heals_Units_Up_To_Max_But_Not_Life()
        {
            var field = Create("0,brute,S1,g,0\n");
            var sword = SwordHitByBrute(field);
            var hp = sword.Hp;
            var life = field.Life;

            field.ApplySlot(SlotSymbol.Green7);
            var once = sword.Hp;
            field.ApplySlot(SlotSymbol.Green7);

            Assert.That((once, sword.Hp, field.Life),
                Is.EqualTo((Math.Min(sword.MaxHp, hp + 600), sword.MaxHp, life)));
        }

        [Test]
        [Description("AC-109: コインは1回ごとに +0.1 で 1.5 で止まり、上限で抽選から外れる（E-11）")]
        public void Coin_Rate_Rises_And_Stops_At_The_Cap()
        {
            var field = Create("0,quaker,S1,g,0\n");
            var rates = new double[6];
            var active = new bool[6];
            for (var i = 0; i < 6; i++)
            {
                field.ApplySlot(SlotSymbol.Coin);
                rates[i] = field.Effects.MoneyRate;
                active[i] = field.Effects.IsActive(SlotSymbol.Coin);
            }

            Assert.That(rates, Is.EqualTo(new[] { 1.1, 1.2, 1.3, 1.4, 1.5, 1.5 }));
            Assert.That(active, Is.EqualTo(new[] { false, false, false, false, true, true }));
            Assert.That(field.Effects.CanRoll(SlotSymbol.Coin), Is.False);
        }

        [Test]
        [Description("E-11: 時間の効果は発動中だけ抽選から外れる。赤7・緑7は外れない")]
        public void Only_Running_Effects_Are_Excluded_From_The_Draw()
        {
            var field = Create("0,quaker,S1,g,0\n");
            foreach (var symbol in new[] { SlotSymbol.Red7, SlotSymbol.Green7, SlotSymbol.Blue7, SlotSymbol.Yellow7, SlotSymbol.Bar })
            {
                field.ApplySlot(symbol);
            }

            var effects = field.Effects;
            Assert.That(new[]
            {
                effects.CanRoll(SlotSymbol.Red7), effects.CanRoll(SlotSymbol.Green7), effects.CanRoll(SlotSymbol.Blue7),
                effects.CanRoll(SlotSymbol.Yellow7), effects.CanRoll(SlotSymbol.Bar), effects.CanRoll(SlotSymbol.Coin),
            }, Is.EqualTo(new[] { true, true, false, false, false, true }));
        }

        [Test]
        [Description("E-26: 効果の残り時間は戦闘のステップでだけ減る（一時停止中は減らない）")]
        public void Timers_Count_Only_Battle_Steps()
        {
            var field = Create("0,quaker,S1,g,0\n");
            field.ApplySlot(SlotSymbol.Blue7);
            field.ApplySlot(SlotSymbol.Bar);
            var start = (field.Effects.StopStepsLeft, field.Effects.AttackStepsLeft);

            field.Advance(7);

            Assert.That((start, field.Effects.StopStepsLeft, field.Effects.AttackStepsLeft),
                Is.EqualTo(((180, 600), 173, 593)));
        }

        [Test]
        public void ApplySlot_Without_Effects_Throws()
        {
            var field = BattleFixtures.CreateField("0,quaker,S1,g,0\n");

            Assert.Throws<InvalidOperationException>(() => field.ApplySlot(SlotSymbol.Red7));
        }
    }
}
