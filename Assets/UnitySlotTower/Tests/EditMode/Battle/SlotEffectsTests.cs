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

        private static Battlefield CreatePoisonDelayed(string waveRows) =>
            new Battlefield(BattleFixtures.LoadWave(waveRows), BattleFixtures.LoadRanges(), 10, new SeededRandom(1),
                new SlotEffects(SlotTestRules.Create(yellowDelaySeconds: 1)));

        [Test]
        [Description("spec 006 AC-002: 黄7は待ち（1秒=60ステップ）の間はダメージが入らず、沼が弾ける瞬間に一度だけ通知して、そこから5秒（5回）毒が入る。ダメージごとに通知する")]
        public void Yellow_Poison_Starts_When_The_Swamp_Bursts()
        {
            var field = CreatePoisonDelayed("0,quaker,S1,g,0\n");
            field.Advance(1);
            var burst = 0;
            var ticks = 0;
            field.YellowBurst += () => burst++;
            field.PoisonTicked += () => ticks++;

            field.ApplySlot(SlotSymbol.Yellow7);
            field.Advance(59);
            var waiting = (field.Enemies[0].Hp, field.Effects.IsPoisoned, field.Effects.IsPoisonPending, burst);
            field.Advance(1);
            var bursting = (field.Effects.IsPoisoned, field.Effects.IsPoisonPending, burst, field.Effects.PoisonStepsLeft);
            field.Advance(60);
            var first = (field.Enemies[0].Hp, ticks);
            field.Advance(240);

            Assert.That((waiting, bursting, first, field.Enemies[0].Hp, ticks, field.Effects.IsPoisoned, burst),
                Is.EqualTo(((100000, false, true, 0), (true, false, 1, 300), (100000 - 1, 1), 100000 - 5, 5, false, 1)));
        }

        [Test]
        [Description("spec 006 AC-002: 戦闘を進めなければ（ステージが終わったら）毒は始まらない。一度に進めたステップ数（倍速）でも同じ時点で弾ける")]
        public void Yellow_Burst_Needs_The_Battle_To_Advance_And_Counts_Steps()
        {
            var idle = CreatePoisonDelayed("0,quaker,S1,g,0\n");
            idle.Advance(1);
            idle.ApplySlot(SlotSymbol.Yellow7);
            var fast = CreatePoisonDelayed("0,quaker,S1,g,0\n");
            fast.Advance(1);
            fast.ApplySlot(SlotSymbol.Yellow7);

            fast.Advance(61);

            Assert.That((idle.Effects.IsPoisoned, idle.Effects.IsPoisonPending, fast.Effects.IsPoisoned, fast.Effects.PoisonStepsLeft),
                Is.EqualTo((false, true, true, 299)));
        }

        [Test]
        [Description("spec 006 AC-002: 黄7の待ちの間も黄7は抽選から外れる")]
        public void Yellow_Cannot_Roll_While_Waiting_To_Burst()
        {
            var field = CreatePoisonDelayed("0,quaker,S1,g,0\n");
            field.Advance(1);
            var before = field.Effects.CanRoll(SlotSymbol.Yellow7);

            field.ApplySlot(SlotSymbol.Yellow7);

            Assert.That((before, field.Effects.CanRoll(SlotSymbol.Yellow7), field.Effects.IsActive(SlotSymbol.Yellow7)),
                Is.EqualTo((true, false, true)));
        }

        [Test]
        [Description("spec 006 FR-003: 待ちが0なら揃った瞬間に毒が始まり、弾けたことを通知する")]
        public void Yellow_Without_Delay_Bursts_At_Once()
        {
            var field = Create("0,quaker,S1,g,0\n");
            field.Advance(1);
            var burst = 0;
            field.YellowBurst += () => burst++;

            field.ApplySlot(SlotSymbol.Yellow7);

            Assert.That((field.Effects.IsPoisoned, field.Effects.PoisonStepsLeft, burst), Is.EqualTo((true, 300, 1)));
        }

        [Test]
        [Description("spec 006 FR-003: 正本の slot_rules.csv の黄7の待ちは1秒（60ステップ）")]
        public void Project_Yellow_Delay_Is_One_Second()
        {
            var rules = SlotRules.Load(SlotTestRules.ProjectTable(SlotRules.FileName));

            Assert.That(new SlotEffects(rules).YellowDelaySteps, Is.EqualTo(SimulationClock.StepsPerSecond));
        }

        private static Battlefield CreateBarDelayed(string waveRows) =>
            new Battlefield(BattleFixtures.LoadWave(waveRows), BattleFixtures.LoadRanges(), 10, new SeededRandom(1),
                new SlotEffects(SlotTestRules.Create(barDelaySeconds: 1)));

        [Test]
        [Description("spec 007 AC-002: バーは待ち（1秒=60ステップ）の間は倍率1のまま、闘気が爆発する瞬間に一度だけ通知して、そこから10秒（600ステップ）倍率2")]
        public void Bar_Attack_Up_Starts_When_The_Aura_Bursts()
        {
            var field = CreateBarDelayed("0,quaker,S1,g,0\n");
            field.Advance(1);
            var burst = 0;
            field.BarBurst += () => burst++;

            field.ApplySlot(SlotSymbol.Bar);
            field.Advance(59);
            var waiting = (field.Effects.AttackRate, field.Effects.IsAttackPending, burst);
            field.Advance(1);
            var bursting = (field.Effects.AttackRate, field.Effects.IsAttackPending, burst, field.Effects.AttackStepsLeft);
            field.Advance(599);
            var last = field.Effects.AttackRate;
            field.Advance(1);

            Assert.That((waiting, bursting, last, field.Effects.AttackRate, burst),
                Is.EqualTo(((1d, true, 0), (2d, false, 1, 600), 2d, 1d, 1)));
        }

        [Test]
        [Description("spec 007 AC-002: 戦闘を進めなければ（ステージが終わったら）攻撃力アップは始まらない。一度に進めたステップ数（倍速）でも同じ時点で爆発する")]
        public void Bar_Burst_Needs_The_Battle_To_Advance_And_Counts_Steps()
        {
            var idle = CreateBarDelayed("0,quaker,S1,g,0\n");
            idle.Advance(1);
            idle.ApplySlot(SlotSymbol.Bar);
            var fast = CreateBarDelayed("0,quaker,S1,g,0\n");
            fast.Advance(1);
            fast.ApplySlot(SlotSymbol.Bar);

            fast.Advance(61);

            Assert.That((idle.Effects.IsAttackUp, idle.Effects.IsAttackPending, fast.Effects.IsAttackUp, fast.Effects.AttackStepsLeft),
                Is.EqualTo((false, true, true, 599)));
        }

        [Test]
        [Description("spec 007 AC-002: バーの待ちの間も効果の間もバーは抽選から外れる")]
        public void Bar_Cannot_Roll_While_Waiting_Or_Active()
        {
            var field = CreateBarDelayed("0,quaker,S1,g,0\n");
            field.Advance(1);
            var before = field.Effects.CanRoll(SlotSymbol.Bar);

            field.ApplySlot(SlotSymbol.Bar);
            var waiting = field.Effects.CanRoll(SlotSymbol.Bar);
            field.Advance(60);

            Assert.That((before, waiting, field.Effects.CanRoll(SlotSymbol.Bar), field.Effects.IsAttackUp),
                Is.EqualTo((true, false, false, true)));
        }

        [Test]
        [Description("spec 007 FR-003: 待ちが0なら揃った瞬間に攻撃力アップが始まり、爆発したことを通知する")]
        public void Bar_Without_Delay_Bursts_At_Once()
        {
            var field = Create("0,quaker,S1,g,0\n");
            field.Advance(1);
            var burst = 0;
            field.BarBurst += () => burst++;

            field.ApplySlot(SlotSymbol.Bar);

            Assert.That((field.Effects.AttackRate, field.Effects.AttackStepsLeft, burst), Is.EqualTo((2d, 600, 1)));
        }

        [Test]
        [Description("spec 007 FR-003: 正本の slot_rules.csv のバーの待ちは1秒（60ステップ）")]
        public void Project_Bar_Delay_Is_One_Second()
        {
            var rules = SlotRules.Load(SlotTestRules.ProjectTable(SlotRules.FileName));

            Assert.That(new SlotEffects(rules).BarDelaySteps, Is.EqualTo(SimulationClock.StepsPerSecond));
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

        // 回復量を大きくして、回復した瞬間に必ず最大 HP になるようにする。brute は約1秒ごとに950を与え剣士は2回目で倒れるので、待ちは0.5秒（30ステップ）にする。
        private static Battlefield CreateHealDelayed(string waveRows) =>
            new Battlefield(BattleFixtures.LoadWave(waveRows), BattleFixtures.LoadRanges(), 10, new SeededRandom(1),
                new SlotEffects(SlotTestRules.Create(greenHealRate: 10, greenDelaySeconds: 0.5)));

        [Test]
        [Description("spec 005 AC-002: 緑7の回復は待ち時間（0.5秒=30ステップ）戦闘を進めてから一度だけ入り、そのとき通知する")]
        public void Green_Heal_Waits_For_The_Delay_Then_Heals_Once()
        {
            var field = CreateHealDelayed("0,brute,S1,g,0\n");
            var sword = SwordHitByBrute(field);
            var healed = 0;
            field.GreenHealed += () => healed++;

            field.ApplySlot(SlotSymbol.Green7);
            var applied = (sword.Hp < sword.MaxHp, field.PendingGreenHeals, healed);
            field.Advance(29);
            var before = (sword.Hp < sword.MaxHp, field.PendingGreenHeals, healed);
            field.Advance(1);
            var at = (sword.Hp, field.PendingGreenHeals, healed);
            field.Advance(15);

            Assert.That((applied, before, at, healed), Is.EqualTo(
                ((true, 1, 0), (true, 1, 0), (sword.MaxHp, 0, 1), 1)));
        }

        [Test]
        [Description("spec 005 AC-002: 待っている間に外した味方は回復せず、戦闘を進めなければ（ステージが終わったら）回復しない")]
        public void Green_Heals_Only_Units_On_The_Field_And_Needs_The_Battle_To_Advance()
        {
            var field = CreateHealDelayed("0,brute,S1,g,0\n");
            var sword = SwordHitByBrute(field);
            field.ApplySlot(SlotSymbol.Green7);
            field.RemoveUnit(sword);
            var hp = sword.Hp;

            var idle = field.PendingGreenHeals;
            field.Advance(30);

            Assert.That((idle, field.PendingGreenHeals, sword.Hp), Is.EqualTo((1, 0, hp)));
        }

        [Test]
        [Description("spec 005 AC-002: 一度に30ステップ進めても（倍速）回復する")]
        public void Green_Heals_When_The_Delay_Passes_In_One_Advance()
        {
            var field = CreateHealDelayed("0,brute,S1,g,0\n");
            var sword = SwordHitByBrute(field);
            var healed = 0;
            field.GreenHealed += () => healed++;
            field.ApplySlot(SlotSymbol.Green7);

            field.Advance(30);

            Assert.That((healed, field.PendingGreenHeals, sword.Hp), Is.EqualTo((1, 0, sword.MaxHp)));
        }

        [Test]
        [Description("spec 005 AC-002: 続けて揃えるとそれぞれの待ちで別々に回復する")]
        public void Each_Green_Heals_After_Its_Own_Delay()
        {
            var field = CreateHealDelayed("0,brute,S1,g,0\n");
            SwordHitByBrute(field);
            var healed = 0;
            field.GreenHealed += () => healed++;
            field.ApplySlot(SlotSymbol.Green7);
            field.Advance(15);
            field.ApplySlot(SlotSymbol.Green7);

            field.Advance(15);
            var first = (healed, field.PendingGreenHeals);
            field.Advance(15);

            Assert.That((first, healed, field.PendingGreenHeals), Is.EqualTo(((1, 1), 2, 0)));
        }

        [Test]
        [Description("spec 005 FR-002: 正本の slot_rules.csv の緑7の待ちは1秒（60ステップ）")]
        public void Project_Green_Delay_Is_One_Second()
        {
            var rules = SlotRules.Load(SlotTestRules.ProjectTable(SlotRules.FileName));

            Assert.That(new SlotEffects(rules).GreenDelaySteps, Is.EqualTo(SimulationClock.StepsPerSecond));
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

        private static Battlefield CreateCoinDelayed(string waveRows) =>
            new Battlefield(BattleFixtures.LoadWave(waveRows), BattleFixtures.LoadRanges(), 10, new SeededRandom(1),
                new SlotEffects(SlotTestRules.Create(coinDelaySeconds: 1.5)));

        [Test]
        [Description("spec 009 AC-002: コインは待ち（1.5秒=90ステップ）の間は前の倍率のまま、吸い込みの瞬間に1段上がって一度だけ通知する")]
        public void Coin_Rate_Rises_When_The_Coins_Arrive()
        {
            var field = CreateCoinDelayed("0,quaker,S1,g,0\n");
            field.Advance(1);
            var gained = 0;
            field.CoinGained += () => gained++;

            field.ApplySlot(SlotSymbol.Coin);
            field.Advance(89);
            var waiting = (field.Effects.MoneyRate, field.Effects.PendingCoins, gained);
            field.Advance(1);

            Assert.That((waiting, field.Effects.MoneyRate, field.Effects.PendingCoins, gained),
                Is.EqualTo(((1d, 1, 0), 1.1, 0, 1)));
        }

        [Test]
        [Description("spec 009 AC-002: 待っている間に倒した敵には前の倍率がかかる")]
        public void Kills_While_Waiting_Use_The_Old_Rate()
        {
            var field = CreateCoinDelayed("0,grunt,S1,g,0\n");
            field.Advance(1);

            field.ApplySlot(SlotSymbol.Coin);
            field.ApplySlot(SlotSymbol.Red7);

            Assert.That((field.Enemies.Count, field.DroppedMoney), Is.EqualTo((0, 30)));
        }

        [Test]
        [Description("spec 009 AC-002: 戦闘を進めなければ（ステージが終わったら）倍率は上がらない。一度に進めたステップ数（倍速）でも同じ時点で上がる")]
        public void Coin_Needs_The_Battle_To_Advance_And_Counts_Steps()
        {
            var idle = CreateCoinDelayed("0,quaker,S1,g,0\n");
            idle.Advance(1);
            idle.ApplySlot(SlotSymbol.Coin);
            var fast = CreateCoinDelayed("0,quaker,S1,g,0\n");
            fast.Advance(1);
            fast.ApplySlot(SlotSymbol.Coin);

            fast.Advance(91);

            Assert.That((idle.Effects.MoneyRate, idle.Effects.PendingCoins, fast.Effects.MoneyRate, fast.Effects.PendingCoins),
                Is.EqualTo((1d, 1, 1.1, 0)));
        }

        [Test]
        [Description("spec 009 AC-002: 続けて揃った分は順に上がり、待っている分を足して上限に届くとコインは抽選から外れる")]
        public void Pending_Coins_Count_Toward_The_Cap()
        {
            var field = CreateCoinDelayed("0,quaker,S1,g,0\n");
            field.Advance(1);
            for (var i = 0; i < 4; i++)
            {
                field.ApplySlot(SlotSymbol.Coin);
            }

            var beforeCap = field.Effects.CanRoll(SlotSymbol.Coin);
            field.Advance(30);
            field.ApplySlot(SlotSymbol.Coin);
            var atCap = (field.Effects.MoneyRate, field.Effects.PendingCoins, field.Effects.CanRoll(SlotSymbol.Coin));
            field.Advance(60);
            var first = (field.Effects.MoneyRate, field.Effects.PendingCoins);
            field.Advance(30);

            Assert.That((beforeCap, atCap, first, field.Effects.MoneyRate, field.Effects.CanRoll(SlotSymbol.Coin)),
                Is.EqualTo((true, (1d, 5, false), (1.4, 1), 1.5, false)));
        }

        [Test]
        [Description("spec 009 FR-003: 待ちが0なら揃った瞬間に倍率が上がり、上がったことを通知する")]
        public void Coin_Without_Delay_Rises_At_Once()
        {
            var field = Create("0,quaker,S1,g,0\n");
            field.Advance(1);
            var gained = 0;
            field.CoinGained += () => gained++;

            field.ApplySlot(SlotSymbol.Coin);

            Assert.That((field.Effects.MoneyRate, field.Effects.PendingCoins, gained), Is.EqualTo((1.1, 0, 1)));
        }

        [Test]
        [Description("spec 009 FR-005: 倒した敵ごとに通知する（赤7で倒した敵も）")]
        public void Defeated_Enemies_Are_Reported()
        {
            var field = Create("0,grunt,S1,g,0\n0,grunt,S1,g,0\n");
            field.Advance(1);
            var defeated = 0;
            field.EnemyDefeated += enemy => defeated += enemy.IsAlive ? 100 : 1;

            field.ApplySlot(SlotSymbol.Red7);

            Assert.That((field.Enemies.Count, defeated), Is.EqualTo((0, 2)));
        }

        [Test]
        [Description("spec 009 FR-003: 正本の slot_rules.csv のコインの待ちは1.5秒（90ステップ）")]
        public void Project_Coin_Delay_Is_One_And_A_Half_Seconds()
        {
            var rules = SlotRules.Load(SlotTestRules.ProjectTable(SlotRules.FileName));

            Assert.That(new SlotEffects(rules).CoinDelaySteps, Is.EqualTo(SimulationClock.StepsPerSecond * 3 / 2));
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
