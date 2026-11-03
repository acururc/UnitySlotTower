using System;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// スロットの効果のうち戦闘全体に掛かる倍率と残り時間（D-45、plan 5章 味方）。攻撃力アップは攻撃と回復に、攻撃速度アップは攻撃間隔に掛ける。
    /// 残り時間は戦闘のステップで数え、効果中に再び揃えたら残り時間に持続時間を足す。ステージをまたいで持ち越す。
    /// </summary>
    public sealed class SlotEffects
    {
        private double _attackRate = 1.0;
        private double _speedRate = 1.0;
        private int _attackSteps;
        private int _speedSteps;

        /// <summary>攻撃力・回復量の倍率。効果がなければ 1。</summary>
        public double AttackRate => _attackSteps > 0 ? _attackRate : 1.0;

        /// <summary>攻撃の速さの倍率（攻撃間隔をこれで割る）。効果がなければ 1。</summary>
        public double SpeedRate => _speedSteps > 0 ? _speedRate : 1.0;

        /// <summary>攻撃力アップの残り秒数。</summary>
        public double AttackSeconds => _attackSteps / (double)SimulationClock.StepsPerSecond;

        /// <summary>攻撃速度アップの残り秒数。</summary>
        public double SpeedSeconds => _speedSteps / (double)SimulationClock.StepsPerSecond;

        /// <summary>攻撃力アップを始める。効果中なら残り時間に <paramref name="seconds"/> を足す。</summary>
        public void GrantAttack(double rate, double seconds)
        {
            _attackRate = rate;
            _attackSteps += StepsOf(seconds);
        }

        /// <summary>攻撃速度アップを始める。効果中なら残り時間に <paramref name="seconds"/> を足す。</summary>
        public void GrantSpeed(double rate, double seconds)
        {
            _speedRate = rate;
            _speedSteps += StepsOf(seconds);
        }

        /// <summary>戦闘の1ステップぶん残り時間を減らす。</summary>
        internal void Step()
        {
            if (_attackSteps > 0)
            {
                _attackSteps--;
            }

            if (_speedSteps > 0)
            {
                _speedSteps--;
            }
        }

        private static int StepsOf(double seconds) =>
            (int)Math.Round(seconds * SimulationClock.StepsPerSecond, MidpointRounding.AwayFromZero);
    }
}
