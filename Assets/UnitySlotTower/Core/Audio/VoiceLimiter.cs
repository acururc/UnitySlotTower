using System.Collections.Generic;

namespace UnitySlotTower.Core.Audio
{
    /// <summary>
    /// 同じ効果音が同時に鳴る数を数え、上限を超える要求を断る（spec 014 FR-005）。
    /// 鳴り終わる時刻で数えるので、止めた音を知らせる必要はない。
    /// </summary>
    public sealed class VoiceLimiter
    {
        private readonly Dictionary<int, List<double>> _endTimes = new Dictionary<int, List<double>>();

        /// <summary>
        /// now の時点で id の鳴っている数が max 未満なら、長さ length の音として数えて true。
        /// max が 0 以下なら上限なし。
        /// </summary>
        public bool TryStart(int id, double now, double length, int max)
        {
            if (!_endTimes.TryGetValue(id, out var ends))
            {
                ends = new List<double>(4);
                _endTimes.Add(id, ends);
            }

            for (var i = ends.Count - 1; i >= 0; i--)
            {
                if (ends[i] <= now)
                {
                    ends.RemoveAt(i);
                }
            }

            if (max > 0 && ends.Count >= max)
            {
                return false;
            }

            ends.Add(now + (length > 0 ? length : 0));
            return true;
        }

        /// <summary>now の時点で鳴っている数。</summary>
        public int Playing(int id, double now)
        {
            if (!_endTimes.TryGetValue(id, out var ends))
            {
                return 0;
            }

            var count = 0;
            foreach (var end in ends)
            {
                if (end > now) count++;
            }

            return count;
        }

        /// <summary>全部鳴り終わったことにする（一時停止で効果音を止めたとき）。</summary>
        public void Clear()
        {
            foreach (var ends in _endTimes.Values)
            {
                ends.Clear();
            }
        }
    }
}
