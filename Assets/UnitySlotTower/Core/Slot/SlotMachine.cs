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

        /// <summary>コインが0。</summary>
        NotEnoughCoins,
    }

    /// <summary>1本のラインの揃い（spec 011 FR-010）。row は上段 -1・中段 0・下段 1。</summary>
    public readonly struct SlotLineHit
    {
        public SlotLineHit(int row, SlotSymbol symbol)
        {
            Row = row;
            Symbol = symbol;
        }

        public int Row { get; }
        public SlotSymbol Symbol { get; }

        public override string ToString() => $"{Row}:{Symbol}";
    }

    /// <summary>
    /// 1台のスロット（spec 011）。賭け枚数（1〜3）が有効ラインの数（中段→上段→下段の順）で、効果の強さの倍率にもなる。
    /// レバーで賭け枚数ぶんのコインを払い（コインが足りなければ持っている枚数まで下げる、FR-003）、
    /// 有効ラインごとに <c>line_hit_rate</c> で揃うかを決め、揃うラインには出せる図柄（発動中でなく、同じ回の別のラインで使っていない）を
    /// 抽選表の個数を重みに選ぶ（FR-010）。見える3×3の図柄はこのクラスが決める: 揃う行は3本とも同じ図柄、揃わない行は3本がそろわず、
    /// 各リールの縦3マスは別々の図柄（FR-011）。リールの帯（<see cref="Reels"/>）は回転中の見た目だけに使う。
    /// 表示側が3本を止め終えたら <see cref="Settle"/> を呼び、揃ったラインを中段→上段→下段の順に通知する。
    /// </summary>
    public sealed class SlotMachine
    {
        public const int ReelCount = SlotReels.ReelCount;
        public const int RowCount = 3;
        public const int MinBet = 1;
        public const int MaxBet = 3;

        /// <summary>有効ラインの順（賭け1枚目が中段、2枚目が上段、3枚目が下段、spec 011 D2）。</summary>
        public static readonly int[] LineRows = { 0, -1, 1 };

        // 図柄の重み（抽選表の個数。ハズレの個数は使わない: 揃うかは line_hit_rate で決める）。
        private readonly int[] _weights = new int[SlotSymbols.Count];
        private readonly CoinWallet _coins;
        private readonly IRandom _random;

        // [reel, row+1]
        private readonly SlotSymbol[,] _grid = new SlotSymbol[ReelCount, RowCount];
        private readonly List<SlotLineHit> _hits = new List<SlotLineHit>();
        private readonly bool[] _used = new bool[SlotSymbols.Count];
        private readonly bool[] _rowAligned = new bool[RowCount];
        private readonly int[] _candidates = new int[SlotSymbols.Count];
        private int _selectedBet = MinBet;

        public SlotMachine(SlotReels reels, IReadOnlyList<SlotSymbol?> patterns, CoinWallet coins, IRandom random, double lineHitRate)
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

            foreach (var pattern in patterns)
            {
                if (pattern.HasValue)
                {
                    _weights[(int)pattern.Value]++;
                }
            }

            if (lineHitRate < 0 || lineHitRate > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(lineHitRate), lineHitRate, "0〜1");
            }

            _coins = coins ?? throw new ArgumentNullException(nameof(coins));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            LineHitRate = lineHitRate;

            // 最初の見た目: 帯の先頭の3マス（表示側は自分の初期位置で描くので、テスト以外では使わない）。
            for (var reel = 0; reel < ReelCount; reel++)
            {
                for (var row = -1; row <= 1; row++)
                {
                    _grid[reel, row + 1] = reels[reel].At(row + 1);
                }
            }
        }

        public SlotReels Reels { get; }

        /// <summary>有効ライン1本が揃う確率。</summary>
        public double LineHitRate { get; }

        /// <summary>プレイヤーが選んだ賭け枚数（1〜3）。</summary>
        public int SelectedBet
        {
            get => _selectedBet;
            set
            {
                if (value < MinBet || value > MaxBet)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "1〜3");
                }

                if (!IsSpinning)
                {
                    _selectedBet = value;
                }
            }
        }

        /// <summary>次のレバーで実際に賭ける枚数: 選んだ枚数を持っているコインまで下げる（最低1、FR-003）。</summary>
        public int EffectiveBet => Math.Max(MinBet, Math.Min(_selectedBet, _coins.Coins));

        /// <summary>直近のレバーで払った枚数（効果の強さの倍率）。</summary>
        public int SpinBet { get; private set; } = MinBet;

        /// <summary>レバーから <see cref="Settle"/> まで true。</summary>
        public bool IsSpinning { get; private set; }

        /// <summary>直近のレバーで揃うと決まったライン（中段→上段→下段の順）。</summary>
        public IReadOnlyList<SlotLineHit> Hits => _hits;

        /// <summary>止まったあとに揃ったラインを1本ずつ通知する（中段→上段→下段の順）。</summary>
        public event Action<SlotLineHit> LineAligned;

        /// <summary>賭け枚数を 1→2→3→1 と切り替える。回転中は変えない（FR-001）。</summary>
        public void CycleBet()
        {
            if (!IsSpinning)
            {
                _selectedBet = _selectedBet >= MaxBet ? MinBet : _selectedBet + 1;
            }
        }

        /// <summary>止まったときに見える図柄。row は上段 -1・中段 0・下段 1。</summary>
        public SlotSymbol Visible(int reel, int row) => _grid[reel, row + 1];

        /// <summary>行 row がこの回の有効ラインか（賭け枚数で決まる）。</summary>
        public static bool IsLineActive(int row, int bet)
        {
            for (var i = 0; i < bet && i < LineRows.Length; i++)
            {
                if (LineRows[i] == row)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// レバー。<paramref name="canAppear"/> が false の図柄は揃えない（発動中の図柄、E-11）。
        /// <paramref name="forced"/> があれば抽選せずその揃いにする（ステージ1の見せ場、FR-032）。行と図柄はそれぞれ重ならないこと。
        /// </summary>
        public LeverResult TryLever(Func<SlotSymbol, bool> canAppear = null, IReadOnlyList<SlotLineHit> forced = null)
        {
            if (IsSpinning)
            {
                return LeverResult.Spinning;
            }

            var bet = EffectiveBet;
            if (_coins.Coins <= 0 || !_coins.TrySpend(bet))
            {
                return LeverResult.NotEnoughCoins;
            }

            SpinBet = bet;
            _hits.Clear();
            Array.Clear(_used, 0, _used.Length);
            if (forced != null)
            {
                foreach (var hit in forced)
                {
                    if (hit.Row < -1 || hit.Row > 1 || _used[(int)hit.Symbol] || ContainsRow(hit.Row))
                    {
                        throw new ArgumentException("決め打ちの行か図柄が重なっている", nameof(forced));
                    }

                    _used[(int)hit.Symbol] = true;
                    _hits.Add(hit);
                }

                SortHits();
            }
            else
            {
                for (var line = 0; line < bet; line++)
                {
                    if (_random.NextDouble() >= LineHitRate)
                    {
                        continue;
                    }

                    var symbol = Draw(canAppear);
                    if (symbol.HasValue)
                    {
                        _used[(int)symbol.Value] = true;
                        _hits.Add(new SlotLineHit(LineRows[line], symbol.Value));
                    }
                }
            }

            FillGrid();
            IsSpinning = true;
            return LeverResult.Started;
        }

        /// <summary>3本とも止まった（表示側）ときに呼ぶ。回転を終え、揃ったラインを順に通知する。</summary>
        public void Settle()
        {
            if (!IsSpinning)
            {
                throw new InvalidOperationException("回転中ではない");
            }

            IsSpinning = false;
            foreach (var hit in _hits)
            {
                LineAligned?.Invoke(hit);
            }
        }

        private bool ContainsRow(int row)
        {
            foreach (var hit in _hits)
            {
                if (hit.Row == row)
                {
                    return true;
                }
            }

            return false;
        }

        // 中段→上段→下段の順に並べる。
        private void SortHits()
        {
            _hits.Sort((a, b) => Array.IndexOf(LineRows, a.Row).CompareTo(Array.IndexOf(LineRows, b.Row)));
        }

        // 出せる図柄から重みで1つ。なければ null（そのラインは揃わない）。
        private SlotSymbol? Draw(Func<SlotSymbol, bool> canAppear)
        {
            var count = 0;
            var total = 0;
            for (var s = 0; s < SlotSymbols.Count; s++)
            {
                if (_weights[s] > 0 && !_used[s] && (canAppear == null || canAppear((SlotSymbol)s)))
                {
                    _candidates[count++] = s;
                    total += _weights[s];
                }
            }

            if (count == 0)
            {
                return null;
            }

            var pick = _random.NextInt(0, total);
            for (var i = 0; i < count; i++)
            {
                pick -= _weights[_candidates[i]];
                if (pick < 0)
                {
                    return (SlotSymbol)_candidates[i];
                }
            }

            return (SlotSymbol)_candidates[count - 1];
        }

        // 揃う行を置き、残りのマスを「縦3マスは別々・揃わない行は3本がそろわない」で埋める。
        private void FillGrid()
        {
            Array.Clear(_rowAligned, 0, _rowAligned.Length);
            foreach (var hit in _hits)
            {
                _rowAligned[hit.Row + 1] = true;
                for (var reel = 0; reel < ReelCount; reel++)
                {
                    _grid[reel, hit.Row + 1] = hit.Symbol;
                }
            }

            for (var reel = 0; reel < ReelCount; reel++)
            {
                for (var r = 0; r < RowCount; r++)
                {
                    if (!_rowAligned[r])
                    {
                        _grid[reel, r] = PickFree(reel, r, null);
                    }
                }
            }

            for (var r = 0; r < RowCount; r++)
            {
                if (!_rowAligned[r] && _grid[0, r] == _grid[1, r] && _grid[1, r] == _grid[2, r])
                {
                    _grid[ReelCount - 1, r] = PickFree(ReelCount - 1, r, _grid[0, r]);
                }
            }
        }

        // リール reel の縦で、行 row 以外に使われていない図柄（avoid も除く）を一様に選ぶ。
        private SlotSymbol PickFree(int reel, int row, SlotSymbol? avoid)
        {
            var count = 0;
            for (var s = 0; s < SlotSymbols.Count; s++)
            {
                var free = avoid != (SlotSymbol)s;
                for (var r = 0; r < RowCount && free; r++)
                {
                    // まだ埋めていない下の行は前回の値が残っているので、揃う行と上の行だけを見る。
                    if (r != row && (_rowAligned[r] || r < row || avoid.HasValue))
                    {
                        free = _grid[reel, r] != (SlotSymbol)s;
                    }
                }

                if (free)
                {
                    _candidates[count++] = s;
                }
            }

            return (SlotSymbol)_candidates[_random.NextInt(0, count)];
        }
    }
}
