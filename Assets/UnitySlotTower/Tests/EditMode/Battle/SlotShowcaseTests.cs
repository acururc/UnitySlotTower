using System.Text.RegularExpressions;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Tests.EditMode.Data;
using UnitySlotTower.Tests.EditMode.Slot;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    /// <summary>spec 011 AC-005 の Core 部分: 見せ場のウェーブを出し終わると戦闘が止まり、解くまで進まない。</summary>
    public sealed class SlotShowcaseTests
    {
        private const int Second = 60;

        // ウェーブ1: melee 1体。ウェーブ2（見せ場）: melee 3体を0.5秒おき。
        // enemy を変えると両方のウェーブがその敵になる（rich: HP1・必ずコイン2〜3枚）。sword なら (1,0) 右向きに剣士を置く。
        private static Battlefield Create(bool showcase = true, string enemy = null, bool sword = false)
        {
            var stageWaves = "stage,wave,start_after,slot_showcase\n1,1,0,0\n" + $"1,2,0,{(showcase ? 1 : 0)}\n";
            var schedule = WaveSchedule.Load(
                CsvParser.Parse(WaveSchedule.FileName, BattleFixtures.StagedWavesHeader +
                    Regex.Replace($"1,0,{enemy ?? "fodder"},S1,g,0,1,0\n2,0,{enemy ?? "melee"},S1,g,0,3,0.5\n", "^(?=.)", "1,", RegexOptions.Multiline)),
                1, BattleFixtures.LoadMap(), BattleFixtures.LoadEnemies(),
                CsvParser.Parse(WaveSchedule.StageWavesFileName, stageWaves));
            var field = new Battlefield(new WaveProgress(schedule, WaveMode.Clear, 1), BattleFixtures.LoadRanges(), 10,
                new SeededRandom(1), new SlotEffects(SlotTestRules.Create()));
            if (sword)
            {
                field.AddUnit(BattleFixtures.LoadUnits().Get("sword"), new GridPoint(1, 0), Facing.Right);
            }

            return field;
        }

        [Test]
        [Description("FR-031: 見せ場のウェーブを出し終わると止まり、通知は1回。止まっている間は Advance しても何も動かない")]
        public void Battle_Holds_After_The_Showcase_Wave_Is_Emitted()
        {
            var field = Create();
            var held = 0;
            field.ShowcaseHeld += () => held++;

            for (var i = 0; i < 20 * Second && !field.IsShowcaseHolding; i++)
            {
                field.Advance(1);
            }

            var tick = field.Tick;
            var distance = field.Enemies[0].Distance;
            field.Advance(5 * Second);

            Assert.That((field.IsShowcaseHolding, held, field.Enemies.Count, field.Waves.Unspawned),
                Is.EqualTo((true, 1, 3, 0)));
            Assert.That((field.Tick, field.Enemies[0].Distance), Is.EqualTo((tick, distance)));
        }

        [Test]
        [Description("FR-031/033: 解くと進み、同じ戦闘ではもう止まらない")]
        public void Release_Resumes_And_Does_Not_Hold_Again()
        {
            var field = Create();
            for (var i = 0; i < 20 * Second && !field.IsShowcaseHolding; i++)
            {
                field.Advance(1);
            }

            var tick = field.Tick;
            field.ReleaseShowcase();
            field.Advance(Second);

            Assert.That((field.IsShowcaseHolding, field.ShowcaseStarted, field.Tick), Is.EqualTo((false, true, tick + Second)));
        }

        private static void AdvanceUntilHeld(Battlefield field)
        {
            for (var i = 0; i < 20 * Second && !field.IsShowcaseHolding; i++)
            {
                field.Advance(1);
            }
        }


        [Test]
        [Description("spec 015 FR-005, FR-006, AC-008: 見せ場の群れは落とさず、全滅するまで見せ場は終わらない（配置の止め）。普通のウェーブの敵は落とす")]
        public void Showcase_Enemies_Drop_Nothing_And_Pending_Until_All_Defeated()
        {
            var field = Create(enemy: "rich", sword: true);
            var drops = new System.Collections.Generic.List<(bool, int)>();
            field.EnemyDefeated += (enemy, money, coins) => drops.Add((enemy.FromShowcase, money + coins));

            AdvanceUntilHeld(field);
            Assert.That((field.IsShowcasePending, field.Enemies.Count > 0), Is.EqualTo((true, true)), "見せ場の群れが場にいない");
            Assert.That(field.Enemies, Has.All.Matches<BattleEnemy>(e => e.FromShowcase));
            field.ReleaseShowcase();
            Assert.That(field.IsShowcasePending, Is.True, "群れが残っているのに終わった");
            field.Advance(20 * Second);

            // ウェーブ1（見せ場でない）の1体は落とし、見せ場の3体は落とさない。
            Assert.That(drops.Count, Is.EqualTo(4), "4体とも剣士に倒されていない");
            Assert.That((drops[0].Item1, drops[0].Item2 > 0), Is.EqualTo((false, true)));
            Assert.That(drops.GetRange(1, 3), Has.All.EqualTo((true, 0)));
            Assert.That((field.IsShowcasePending, field.ShowcaseFinished), Is.EqualTo((false, true)));
        }

        [Test]
        [Description("spec 015 FR-007, AC-008: 見せ場の群れが全滅すると、青7の停止と黄7の毒が発動待ちも含めて消える（ほかの効果は残る）")]
        public void Showcase_End_Clears_Stop_And_Poison()
        {
            // 群れ（HP1）は止まったまま毒で倒れる。
            var field = Create(enemy: "rich");
            var ended = 0;
            field.ShowcaseEnded += () => ended++;
            AdvanceUntilHeld(field);
            field.ReleaseShowcase();
            field.Effects.StartStop(3);
            field.Effects.QueueStop(3);
            field.Effects.StartPoison(3);
            field.Effects.QueuePoison(3);
            field.Effects.StartAttack(3);
            for (var i = 0; i < 20 * Second && ended == 0; i++)
            {
                field.Advance(1);
            }

            Assert.That(field.Enemies.Count, Is.EqualTo(0), "群れが全滅しない");

            Assert.That((ended, field.Effects.IsStopped, field.Effects.IsStopPending, field.Effects.IsPoisoned, field.Effects.IsPoisonPending, field.Effects.IsAttackUp),
                Is.EqualTo((1, false, false, false, false, true)));
        }

        [Test]
        [Description("テスト用に見せ場を無効にすると止まらず、配置も止めず、見せ場の群れは普通の敵（落とし、ライフを減らす）")]
        public void Disabled_Showcase_Never_Holds_Or_Locks()
        {
            var field = Create();
            field.DisableShowcase();

            field.Advance(20 * Second);

            Assert.That((field.IsShowcaseHolding, field.IsShowcasePending), Is.EqualTo((false, false)));
            Assert.That((field.Waves.StartedWaves, field.Life < 9), Is.EqualTo((2, true)), "ウェーブが進まない、または見せ場の群れが本拠地に着かない");
            Assert.That(field.Enemies, Has.None.Matches<BattleEnemy>(e => e.FromShowcase));
        }

        [Test]
        [Description("見せ場のないステージは止まらない")]
        public void Battle_Without_Showcase_Never_Holds()
        {
            var field = Create(showcase: false);

            field.Advance(20 * Second);

            Assert.That((field.IsShowcaseHolding, field.ShowcaseStarted), Is.EqualTo((false, false)));
        }

        [Test]
        [Description("spec 015 FR-001: 正本のステージ1はウェーブ1が見せ場で、ほかのステージに見せ場はない")]
        public void Project_Stage_One_Wave_One_Is_The_Only_Showcase()
        {
            var stageWaves = CsvParser.Parse(WaveSchedule.StageWavesFileName,
                System.IO.File.ReadAllText(System.IO.Path.Combine(TablePaths.Root, WaveSchedule.StageWavesFileName)));
            var showcases = 0;
            foreach (var row in stageWaves.Rows)
            {
                if (row.GetBool(WaveSchedule.SlotShowcaseColumn))
                {
                    showcases++;
                    Assert.That((row.GetInt("stage"), row.GetInt(WaveSchedule.WaveColumn)), Is.EqualTo((1, 1)));
                }
            }

            Assert.That(showcases, Is.EqualTo(1));
        }
    }
}
