using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>レバーの結果（FR-040）。</summary>
    public enum LeverResult
    {
        Started,

        /// <summary>回転中（<see cref="SlotMachine.Settle"/> の前）。</summary>
        Spinning,

        /// <summary>コインが掛け金未満。</summary>
        NotEnoughCoins,
    }

    /// <summary>
    /// 1台のスロット（FR-040・FR-041、plan 16.3）。レバーで掛け金を払い、許可されたパターンから出目を1つ一様に選んで3本の停止位置を決める。
    /// 揃いはその図柄が中段に来る位置、ハズレはどの段（上・中・下）も揃わない組み合わせから一様に選ぶ。
    /// 表示側が3本を止め終えたら <see cref="Settle"/> を呼び、揃った図柄を <see cref="Aligned"/> で通知する。
    /// 一時停止中のレバーの拒否は表示側（Runtime）が受け持つ。
    /// </summary>
    public sealed class SlotMachine
    {
        public const int ReelCount = SlotReels.ReelCount;

        private readonly SlotSymbol?[] _patterns;
        private readonly int[] _allowedIndices;
        private readonly CoinWallet _coins;
        private readonly IRandom _random;
        private readonly List<int[]> _missStops = new List<int[]>();
        private readonly int[] _stops = new int[ReelCount];

        public SlotMachine(SlotReels reels, IReadOnlyList<SlotSymbol?> patterns, CoinWallet coins, int bet, IRandom random)
        {
            Reels = reels ?? throw new ArgumentNullException(nameof(reels));
            var problems = reels.Problems();
            if (problems.Count > 0)
            {
                throw new ArgumentException($"リールの並びが決まりに合わない: {string.Join(" / ", problems)}", nameof(reels));
            }

            if (patterns == null || patterns.Count == 0)
            {
                throw new ArgumentException("抽選表が空", nameof(patterns));
            }

            _allowedIndices = new int[patterns.Count];
            _patterns = new SlotSymbol?[patterns.Count];
            for (var i = 0; i < _patterns.Length; i++)
            {
                _patterns[i] = patterns[i];
            }

            _coins = coins ?? throw new ArgumentNullException(nameof(coins));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            Bet = bet;
            CollectMissStops();
        }

        public SlotReels Reels { get; }

        /// <summary>1回の掛け金。</summary>
        public int Bet { get; }

        /// <summary>レバーから <see cref="Settle"/> まで true。</summary>
        public bool IsSpinning { get; private set; }

        /// <summary>直近のレバーで決まった出目。揃う図柄、ハズレなら null。</summary>
        public SlotSymbol? Outcome { get; private set; }

        /// <summary>止まったあとに揃った図柄を通知する（ハズレでは呼ばない）。</summary>
        public event Action<SlotSymbol> Aligned;

        /// <summary>リールの停止位置（中段のマス）。</summary>
        public int StopPosition(int reel) => _stops[reel];

        /// <summary>止まったときに見える図柄。row は上段 -1・中段 0・下段 1。</summary>
        public SlotSymbol Visible(int reel, int row) => Reels[reel].At(_stops[reel] + row);

        /// <summary>出せるパターンから等確率で選ぶ。ハズレは常に候補とし、候補がなければ表全体から選ぶ。</summary>
        public LeverResult TryLever(Func<SlotSymbol, bool> canAppear = null)
        {
            if (IsSpinning)
            {
                return LeverResult.Spinning;
            }

            if (_coins.Coins < Bet)
            {
                return LeverResult.NotEnoughCoins;
            }

            var allowedCount = 0;
            if (canAppear != null)
            {
                for (var i = 0; i < _patterns.Length; i++)
                {
                    var pattern = _patterns[i];
                    if (!pattern.HasValue || canAppear(pattern.Value))
                    {
                        _allowedIndices[allowedCount++] = i;
                    }
                }
            }

            if (!_coins.TrySpend(Bet))
            {
                return LeverResult.NotEnoughCoins;
            }

            var index = allowedCount == 0
                ? _random.NextInt(0, _patterns.Length)
                : _allowedIndices[_random.NextInt(0, allowedCount)];
            Outcome = _patterns[index];
            if (Outcome.HasValue)
            {
                for (var reel = 0; reel < ReelCount; reel++)
                {
                    _stops[reel] = Reels.PositionOf(reel, Outcome.Value);
                }
            }
            else
            {
                var stops = _missStops[_random.NextInt(0, _missStops.Count)];
                Array.Copy(stops, _stops, ReelCount);
            }

            IsSpinning = true;
            return LeverResult.Started;
        }

        /// <summary>3本とも止まった（表示側）ときに呼ぶ。回転を終え、揃いなら <see cref="Aligned"/> を通知する。</summary>
        public void Settle()
        {
            if (!IsSpinning)
            {
                throw new InvalidOperationException("回転中ではない");
            }

            IsSpinning = false;
            if (Outcome.HasValue)
            {
                Aligned?.Invoke(Outcome.Value);
            }
        }

        // ハズレの止まり方: 3本の停止位置の全組み合わせのうち、上・中・下のどの段も揃わないもの。
        private void CollectMissStops()
        {
            int length0 = Reels[0].Length, length1 = Reels[1].Length, length2 = Reels[2].Length;
            for (var a = 0; a < length0; a++)
            {
                for (var b = 0; b < length1; b++)
                {
                    for (var c = 0; c < length2; c++)
                    {
                        var aligned = false;
                        for (var row = -1; row <= 1 && !aligned; row++)
                        {
                            var symbol = Reels[0].At(a + row);
                            aligned = Reels[1].At(b + row) == symbol && Reels[2].At(c + row) == symbol;
                        }

                        if (!aligned)
                        {
                            _missStops.Add(new[] { a, b, c });
                        }
                    }
                }
            }
        }
    }
}
