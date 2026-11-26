using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// ステージのウェーブの進行（FR-102・FR-103、E-24、plan 2.1）。1ステップに1回 <see cref="Step"/> を呼ぶ。
    /// <list type="bullet">
    /// <item>どちらの進み方でも、始まった全ウェーブを出し終わって盤上の敵が0になってから間隔だけたったら次を始める。</item>
    /// <item><see cref="WaveMode.Timed"/> では、前のウェーブの始まりから <see cref="WaveDefinition.StartAfter"/> 秒でも次を始める（早いほう）。</item>
    /// </list>
    /// ウェーブ1はステップ0で始まる。ウェーブが1つなら、時刻 t の出現はステップ round(t×60) に出る（001 と同じ）。
    /// </summary>
    public sealed class WaveProgress
    {
        private readonly long[] _startStep;
        private readonly int[] _emitted;
        private readonly long _gapSteps;
        private long _step;
        private long _clearSince = -1;

        public WaveProgress(WaveSchedule schedule, WaveMode mode, double gapSeconds)
        {
            Schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            if (gapSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gapSeconds), gapSeconds, "間隔は0以上");
            }

            Mode = mode;
            _gapSteps = StepsOf(gapSeconds);
            _startStep = new long[schedule.Waves.Count];
            _emitted = new int[schedule.Waves.Count];
            Unspawned = schedule.Spawns.Count;
        }

        public WaveSchedule Schedule { get; }
        public WaveMode Mode { get; }

        public int WaveCount => _startStep.Length;

        /// <summary>始まったウェーブの数。</summary>
        public int StartedWaves { get; private set; }

        /// <summary>まだ出ていない出現の数（始まっていないウェーブの分を含む）。</summary>
        public int Unspawned { get; private set; }

        /// <summary>全ウェーブが始まり、すべて出し終わった。</summary>
        public bool IsFinished => StartedWaves == WaveCount && Unspawned == 0;

        /// <summary>ウェーブ index（0始まり）が始まり、出現をすべて出し終わった。</summary>
        public bool IsWaveEmitted(int index) => index < StartedWaves && _emitted[index] == Schedule.Waves[index].Spawns.Count;

        /// <summary>
        /// 1ステップ進める。<paramref name="enemiesOnField"/> はこのステップの始めに盤上にいる敵の数。
        /// 次のウェーブを始めるかを決めてから、時刻になった出現を <paramref name="due"/> に足す（ウェーブの順・出現の順）。
        /// </summary>
        public void Step(int enemiesOnField, List<WaveSpawn> due)
        {
            if (StartedWaves == 0 && WaveCount > 0)
            {
                StartWave();
            }

            while (StartedWaves < WaveCount && ShouldStartNext(enemiesOnField))
            {
                StartWave();
            }

            for (var i = 0; i < StartedWaves; i++)
            {
                var spawns = Schedule.Waves[i].Spawns;
                while (_emitted[i] < spawns.Count && _startStep[i] + StepsOf(spawns[_emitted[i]].Time) <= _step)
                {
                    due.Add(spawns[_emitted[i]++]);
                    Unspawned--;
                }
            }

            _step++;
        }

        private bool ShouldStartNext(int enemiesOnField)
        {
            if (AllStartedEmitted() && enemiesOnField == 0)
            {
                if (_clearSince < 0)
                {
                    _clearSince = _step;
                }

                if (_step - _clearSince >= _gapSteps)
                {
                    return true;
                }
            }
            else
            {
                _clearSince = -1;
            }

            return Mode == WaveMode.Timed &&
                   _step - _startStep[StartedWaves - 1] >= StepsOf(Schedule.Waves[StartedWaves].StartAfter);
        }

        private bool AllStartedEmitted()
        {
            for (var i = 0; i < StartedWaves; i++)
            {
                if (_emitted[i] < Schedule.Waves[i].Spawns.Count)
                {
                    return false;
                }
            }

            return true;
        }

        private void StartWave()
        {
            _startStep[StartedWaves++] = _step;
            _clearSince = -1;
        }

        private static long StepsOf(double seconds) =>
            (long)Math.Round(seconds * SimulationClock.StepsPerSecond);
    }
}
