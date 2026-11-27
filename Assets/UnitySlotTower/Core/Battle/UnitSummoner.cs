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

        /// <summary>いまは置けない（ステージ1の見せ場が終わるまで、spec 015 FR-006）。</summary>
        Locked,
    }

    /// <summary>
    /// 召喚の可否判定と支払い（FR-020〜FR-022）。待ち時間は種類ごとで、所持金で召喚した瞬間から数える。
    /// 無料の1回（spec 002 FR-108）はどの職にも使え、所持金を使わず待ち時間も始めない。撤退しても返還はない。
    /// 時間は <see cref="SimulationClock"/> の固定ステップで進める。
    /// </summary>
    public sealed class UnitSummoner
    {
        private readonly UnitTable _units;
        private readonly CoinWallet _wallet;
        private readonly int _deployLimit;
        private readonly double _refundRate;
        private readonly Dictionary<string, int> _indices = new Dictionary<string, int>();

        // 種類ごとの残り待ちステップ数（UnitTable.All と同じ並び）。
        private readonly int[] _cooldownSteps;

        /// <param name="refundRate">撤退で召喚コストのうち返す割合（FR-025）。</param>
        /// <param name="freeSummons">どの職にも使える無料の召喚の回数（spec 002 FR-108。ステージ1の開始時だけ 1）。</param>
        public UnitSummoner(UnitTable units, CoinWallet wallet, int deployLimit, double refundRate, int freeSummons = 0)
        {
            _units = units;
            _wallet = wallet;
            _deployLimit = deployLimit;
            _refundRate = refundRate;
            if (freeSummons < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(freeSummons), freeSummons, "無料の回数が負");
            }

            FreeSummons = freeSummons;
            _cooldownSteps = new int[units.All.Count];
            for (var i = 0; i < units.All.Count; i++)
            {
                _indices.Add(units.All[i].Id, i);
            }
        }

        /// <summary>true を返す間は召喚させない（<see cref="SummonResult.Locked"/>、spec 015 FR-006 の見せ場）。</summary>
        public Func<bool> Lock { get; set; }

        /// <summary>フィールド上の味方の総数。</summary>
        public int DeployedCount { get; private set; }

        /// <summary>残りの無料の召喚の回数（FR-108）。</summary>
        public int FreeSummons { get; private set; }

        /// <summary>直前の <see cref="TrySummon"/> の成功が無料の1回だったか。</summary>
        public bool LastSummonWasFree { get; private set; }

        /// <summary>いま召喚できるかだけを調べる（何も変えない）。召喚できるなら <see cref="SummonResult.Summoned"/>。</summary>
        public SummonResult Check(string unitId)
        {
            if (Lock != null && Lock())
            {
                return SummonResult.Locked;
            }

            var unit = _units.Get(unitId);
            var free = FreeSummons > 0;
            if (!free && _cooldownSteps[_indices[unitId]] > 0)
            {
                return SummonResult.CoolingDown;
            }

            if (DeployedCount >= _deployLimit)
            {
                return SummonResult.DeployLimitReached;
            }

            return !free && _wallet.Coins < unit.Cost
                ? SummonResult.NotEnoughCoins
                : SummonResult.Summoned;
        }

        /// <summary>拒否したときは何も変えない（所持金・無料の回数・待ち時間・配置数）。無料の回数があれば先に使う。</summary>
        public SummonResult TrySummon(string unitId)
        {
            var result = Check(unitId);
            if (result != SummonResult.Summoned)
            {
                return result;
            }

            var unit = _units.Get(unitId);
            LastSummonWasFree = FreeSummons > 0;
            if (LastSummonWasFree)
            {
                FreeSummons--;
            }
            else
            {
                _wallet.TrySpend(unit.Cost);
                _cooldownSteps[_indices[unitId]] = (int)Math.Round(unit.Cooldown * SimulationClock.StepsPerSecond);
            }

            DeployedCount++;
            return SummonResult.Summoned;
        }

        /// <summary>
        /// 撤退した味方の分の返還（FR-025）。召喚コスト × 返還率を切り捨てて財布に戻し、返した額を返す。
        /// 無料の1回で出した味方（<paramref name="wasFree"/>）は返還しない（spec 002 E-25）。
        /// </summary>
        public int Retreat(string unitId, bool wasFree = false)
        {
            var refund = RefundFor(unitId, wasFree);
            _wallet.Add(refund);
            DeployedCount--;
            return refund;
        }

        /// <summary>撤退したときに戻る額（Retreat と同じ計算。何も変えない）。詳細の板に出す（T384）。</summary>
        public int RefundFor(string unitId, bool wasFree = false)
        {
            var cost = _units.Get(unitId).Cost;
            return wasFree ? 0 : (int)Math.Floor(cost * _refundRate);
        }

        /// <summary>倒された味方の分の配置枠を空ける（返還はない）。</summary>
        public void ReleaseFallen()
        {
            DeployedCount--;
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
