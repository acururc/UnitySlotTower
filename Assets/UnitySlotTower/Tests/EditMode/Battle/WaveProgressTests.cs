using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("T605 (FR-102, FR-103, E-24, AC-102): ウェーブの進行（clear は全滅を待つ、timed は決めた時刻か全滅から間隔の早いほう）")]
    public sealed class WaveProgressTests
    {
        private const int Second = SimulationClock.StepsPerSecond;

        // ウェーブ1: melee 1体（0秒）。ウェーブ2: ranged（0秒）と air（2秒）。
        private const string TwoWaves =
            "1,0,melee,S1,g,0,1,0\n" +
            "2,0,ranged,S1,g,0,1,0\n" +
            "2,2,air,S2,a,0,1,0\n";

        private readonly List<WaveSpawn> _due = new List<WaveSpawn>();

        private static WaveProgress Create(WaveMode mode, double gapSeconds = 1, double startAfter = 10, string rows = TwoWaves) =>
            new WaveProgress(BattleFixtures.LoadStagedWave(rows, 0, startAfter), mode, gapSeconds);

        /// <summary>
        /// <paramref name="steps"/> ステップ進め、出た出現を (ステップ, 敵) で返す。盤上の敵の数は <paramref name="field"/> が決める
        /// （引数はそのステップの番号と、それまでに出た数）。
        /// </summary>
        private List<(int step, string enemy)> Drive(WaveProgress progress, int steps, System.Func<int, int, int> field,
            int firstStep = 0)
        {
            var emitted = new List<(int, string)>();
            for (var step = firstStep; step < firstStep + steps; step++)
            {
                _due.Clear();
                progress.Step(field(step, emitted.Count), _due);
                emitted.AddRange(_due.Select(s => (step, s.Enemy.Id)));
            }

            return emitted;
        }

        [Test]
        [Description("FR-102, FR-103: clear では、ウェーブ1の敵が盤上に残っている間はウェーブ2が来ない")]
        public void Clear_Mode_Waits_While_Enemies_Remain()
        {
            var progress = Create(WaveMode.Clear);

            var emitted = Drive(progress, 600, (_, _) => 1);

            Assert.That(emitted, Is.EqualTo(new[] { (0, "melee") }));
            Assert.That((progress.StartedWaves, progress.Unspawned, progress.IsFinished), Is.EqualTo((1, 2, false)));
        }

        [Test]
        [Description("FR-102, plan 2.1: clear では、盤上が0になってから間隔（1秒）ちょうどで次のウェーブが来る")]
        public void Clear_Mode_Starts_The_Next_Wave_After_The_Gap()
        {
            var progress = Create(WaveMode.Clear);

            // ステップ100で盤上が0になる。
            var emitted = Drive(progress, 400, (step, _) => step < 100 ? 1 : 0);

            Assert.That(emitted, Is.EqualTo(new[] { (0, "melee"), (100 + Second, "ranged"), (100 + 3 * Second, "air") }));
        }

        [Test]
        [Description("E-24: timed では、前のウェーブの敵が残っていても start_after で次が来る")]
        public void Timed_Mode_Starts_At_Start_After_Even_With_Enemies()
        {
            var progress = Create(WaveMode.Timed, startAfter: 5);

            var emitted = Drive(progress, 600, (_, _) => 3);

            Assert.That(emitted, Is.EqualTo(new[] { (0, "melee"), (5 * Second, "ranged"), (7 * Second, "air") }));
        }

        [Test]
        [Description("E-24: timed でも、出し終わって盤上が0になれば、間隔の後に start_after より早く次が来る")]
        public void Timed_Mode_Starts_Early_When_Cleared()
        {
            var progress = Create(WaveMode.Timed, startAfter: 10);

            var emitted = Drive(progress, 400, (step, _) => step < 120 ? 1 : 0);

            Assert.That(emitted.Select(e => e.step).Take(2), Is.EqualTo(new[] { 0, 120 + Second }));
        }

        [Test]
        [Description("plan 2.1: 出現の時刻はウェーブの始まりからの秒数")]
        public void Spawn_Times_Are_Relative_To_The_Wave_Start()
        {
            var progress = Create(WaveMode.Timed, startAfter: 4);

            var emitted = Drive(progress, 600, (_, _) => 1);

            Assert.That(emitted.Single(e => e.enemy == "air").step, Is.EqualTo(4 * Second + 2 * Second));
        }

        [Test]
        [Description("FR-102: 残りの出現は始まっていないウェーブの分を含み、最後の出現で終わる")]
        public void Unspawned_Counts_Unstarted_Waves_And_Finishes_At_The_Last_Spawn()
        {
            var progress = Create(WaveMode.Timed, startAfter: 5);
            Assert.That((progress.Unspawned, progress.WaveCount, progress.IsFinished), Is.EqualTo((3, 2, false)));

            Drive(progress, 5 * Second, (_, _) => 1);
            Assert.That((progress.Unspawned, progress.StartedWaves, progress.IsFinished), Is.EqualTo((2, 1, false)));

            Drive(progress, 2 * Second, (_, _) => 1, 5 * Second);
            Assert.That((progress.Unspawned, progress.StartedWaves, progress.IsFinished), Is.EqualTo((1, 2, false)));

            Drive(progress, 1, (_, _) => 1, 7 * Second);
            Assert.That((progress.Unspawned, progress.IsFinished), Is.EqualTo((0, true)));
        }

        [Test]
        [Description("plan 2.1: 間隔0なら、盤上が0と分かったステップで次のウェーブが始まる")]
        public void Zero_Gap_Starts_On_The_Step_The_Field_Is_Empty()
        {
            var progress = Create(WaveMode.Clear, gapSeconds: 0);

            var emitted = Drive(progress, 200, (step, _) => step < 50 ? 1 : 0);

            Assert.That(emitted.Select(e => e.step).Take(2), Is.EqualTo(new[] { 0, 50 }));
        }

        [Test]
        [Description("001 互換: 1ウェーブの timed は、時刻 t の出現を round(t×60) のステップに出す")]
        public void Single_Wave_Emits_At_Rounded_Steps()
        {
            var progress = Create(WaveMode.Timed, rows: "1,0,melee,S1,g,0,1,0\n1,1.5,ranged,S1,g,0,1,0\n1,2.51,air,S2,a,0,1,0\n");

            var emitted = Drive(progress, 300, (_, _) => 0);

            Assert.That(emitted, Is.EqualTo(new[] { (0, "melee"), (90, "ranged"), (151, "air") }));
        }

        [Test]
        [Description("FR-102: clear のステージは、ウェーブの間に盤上が0でもクリアにならず、残りの敵は次のウェーブの数")]
        public void Battlefield_Is_Not_Cleared_Between_Waves()
        {
            // fodder は約3秒でゴールに着いて盤上から消える。
            var schedule = BattleFixtures.LoadStagedWave(
                "1,0,fodder,S1,g,0,1,0\n2,0,fodder,S1,g,0,2,1\n", 0, 0);
            var field = new Battlefield(new WaveProgress(schedule, WaveMode.Clear, 1), BattleFixtures.LoadRanges(), 10,
                new SeededRandom(1));

            field.Advance(4 * Second);
            Assert.That((field.ArrivedCount, field.IsCleared, field.RemainingEnemies), Is.EqualTo((1, false, 2)));

            field.Advance(10 * Second);
            Assert.That((field.ArrivedCount, field.IsCleared, field.RemainingEnemies), Is.EqualTo((3, true, 0)));
        }
    }
}
