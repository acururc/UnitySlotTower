using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Battle
{
    public enum SummonResult
    {
        Summoned,
        CoolingDown,
        DeployLimitReached,
        NotEnoughCoins,
    }

    /// <summary>
    /// 召喚の可否判定と支払い（FR-020〜FR-022）。待ち時間は種類ごとで、召喚した瞬間から数える。
    /// 時間は <see cref="SimulationClock"/> の固定ステップで進める。
    /// </summary>
    public sealed class UnitSummoner
    {
        private readonly UnitTable _units;
        private readonly CoinWallet _wallet;
        private readonly int _deployLimit;
        private readonly Dictionary<string, int> _indices = new Dictionary<string, int>();

        // 種類ごとの残り待ちステップ数（UnitTable.All と同じ並び）。
        private readonly int[] _cooldownSteps;

        public UnitSummoner(UnitTable units, CoinWallet wallet, int deployLimit)
        {
            _units = units;
            _wallet = wallet;
            _deployLimit = deployLimit;
            _cooldownSteps = new int[units.All.Count];
            for (var i = 0; i < units.All.Count; i++)
            {
                _indices.Add(units.All[i].Id, i);
            }
        }

        /// <summary>フィールド上の味方の総数。</summary>
        public int DeployedCount { get; private set; }

        /// <summary>拒否したときは何も変えない（コイン・待ち時間・配置数）。</summary>
        public SummonResult TrySummon(string unitId)
        {
            var unit = _units.Get(unitId);
            var index = _indices[unitId];
            if (_cooldownSteps[index] > 0)
            {
                return SummonResult.CoolingDown;
            }

            if (DeployedCount >= _deployLimit)
            {
                return SummonResult.DeployLimitReached;
            }

            if (!_wallet.TrySpend(unit.Cost))
            {
                return SummonResult.NotEnoughCoins;
            }

            _cooldownSteps[index] = (int)Math.Round(unit.Cooldown * SimulationClock.StepsPerSecond);
            DeployedCount++;
            return SummonResult.Summoned;
        }

        /// <summary>その種類を再び召喚できるまでの残り秒数。召喚できるなら 0。</summary>
        public double CooldownRemaining(string unitId)
        {
            _units.Get(unitId);
            return _cooldownSteps[_indices[unitId]] * SimulationClock.StepSeconds;
        }

        public void Advance(int steps)
        {
            for (var i = 0; i < _cooldownSteps.Length; i++)
            {
                _cooldownSteps[i] = Math.Max(0, _cooldownSteps[i] - steps);
            }
        }
    }
}
