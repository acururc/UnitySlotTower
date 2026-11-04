using System;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// リールのカスタム画面の中身（FR-046〜048、D-46・D-50、plan 5章 スロット）。ロックのマスは変えられず、
    /// 空きマスと入れたマスに <see cref="ReelLocks.Fillable"/> の図柄を入れる（上書き）・外すだけを受け付ける。空きがなくなれば確定できる。
    /// 表示する%（ハズレ・当たり・当たり時の各図柄）は小数1桁に四捨五入した 1/10 % 単位で返し、各図柄の%は埋まったマスだけで数える。
    /// </summary>
    public sealed class ReelCustomizer
    {
        private readonly ReelLocks _locks;
        private readonly SlotSymbol?[] _cells;
        private readonly int[] _counts = new int[SlotSymbols.Count];
        private int _brush;

        /// <param name="locks">ロック配置（slot_locked_reel.csv）。空きマスはすべて空から始める。</param>
        /// <param name="missRate">現在の難易度のハズレ率（0〜1）。</param>
        public ReelCustomizer(ReelLocks locks, double missRate)
        {
            _locks = locks ?? throw new ArgumentNullException(nameof(locks));
            _cells = new SlotSymbol?[locks.Length];
            for (var i = 0; i < _cells.Length; i++)
            {
                _cells[i] = locks.LockedAt(i);
                if (_cells[i] != null)
                {
                    _counts[(int)_cells[i].Value]++;
                }
            }

            EmptyCount = locks.EmptyCount;
            MissTenths = ToTenths(missRate);
            HitTenths = ToTenths(1.0 - missRate);
            Selected = NextEditable(-1, 1);
        }

        /// <summary>マスの図柄が変わったとき（入れる・上書き・外す）に呼ぶ。</summary>
        public event Action Changed;

        public int Length => _cells.Length;

        /// <summary>残っている空きマスの数。</summary>
        public int EmptyCount { get; private set; }

        /// <summary>キーボードで選んでいるマス（ロック以外。編集できるマスがなければ -1）。</summary>
        public int Selected { get; private set; }

        /// <summary>キーボードで入れる図柄（<see cref="ReelLocks.Fillable"/> のどれか）。</summary>
        public SlotSymbol Brush => ReelLocks.Fillable[_brush];

        /// <summary>ハズレ率（1/10 % 単位。25.0% なら 250）。</summary>
        public int MissTenths { get; }

        /// <summary>当たり率（1/10 % 単位）。</summary>
        public int HitTenths { get; }

        /// <summary>マスの図柄（空きなら null）。</summary>
        public SlotSymbol? At(int index) => _cells[index];

        public bool IsLocked(int index) => _locks.IsLocked(index);

        /// <summary>埋まっているマスのうちこの図柄のマス数。</summary>
        public int Count(SlotSymbol symbol) => _counts[(int)symbol];

        /// <summary>当たり時にこの図柄が揃う確率（マス数 ÷ 埋まっているマス数、1/10 % 単位）。</summary>
        public int SymbolTenths(SlotSymbol symbol)
        {
            var filled = _cells.Length - EmptyCount;
            return filled == 0 ? 0 : ToTenths((double)Count(symbol) / filled);
        }

        /// <summary>確定できるか（空きマスがない）。</summary>
        public bool IsValid => EmptyCount == 0;

        /// <summary>空きマスか入れたマスに図柄を入れる（上書き）。ロック・7・BAR・同じ図柄のときは何もせず false（同じ図柄は true）。</summary>
        public bool Place(int index, SlotSymbol symbol)
        {
            if (index < 0 || index >= _cells.Length || IsLocked(index) || !ReelLocks.IsFillable(symbol))
            {
                return false;
            }

            var old = _cells[index];
            if (old == symbol)
            {
                return true;
            }

            if (old == null)
            {
                EmptyCount--;
            }
            else
            {
                _counts[(int)old.Value]--;
            }

            _cells[index] = symbol;
            _counts[(int)symbol]++;
            Changed?.Invoke();
            return true;
        }

        /// <summary>入れたマスを空きに戻す。ロック・空きのときは false。</summary>
        public bool Clear(int index)
        {
            if (index < 0 || index >= _cells.Length || IsLocked(index) || _cells[index] == null)
            {
                return false;
            }

            _counts[(int)_cells[index].Value]--;
            _cells[index] = null;
            EmptyCount++;
            Changed?.Invoke();
            return true;
        }

        public bool PlaceSelected() => Place(Selected, Brush);

        public bool ClearSelected() => Clear(Selected);

        /// <summary>ロックでないマスを選ぶ。ロックなら何もしない。</summary>
        public void Select(int index)
        {
            if (index >= 0 && index < _cells.Length && !IsLocked(index))
            {
                Selected = index;
            }
        }

        /// <summary>選んでいるマスを、ロックを飛ばして左右に動かす（端は循環）。</summary>
        public void MoveSelection(int delta)
        {
            if (Selected >= 0 && delta != 0)
            {
                Selected = NextEditable(Selected, Math.Sign(delta));
            }
        }

        /// <summary>入れる図柄を <see cref="ReelLocks.Fillable"/> の並びで delta だけ進める（端は循環）。</summary>
        public void CycleBrush(int delta)
        {
            var count = ReelLocks.Fillable.Length;
            _brush = ((_brush + delta) % count + count) % count;
        }

        /// <summary>空きがなければ今の配列を返す。空きがあれば null。</summary>
        public ReelLayout TryConfirm()
        {
            if (!IsValid)
            {
                return null;
            }

            var cells = new SlotSymbol[_cells.Length];
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = _cells[i].Value;
            }

            return new ReelLayout(cells);
        }

        /// <summary>0〜1 の値を、小数1桁に四捨五入した % の 10 倍の整数にする（33.33…% → 333）。</summary>
        public static int ToTenths(double fraction) =>
            (int)Math.Round(fraction * 1000.0, MidpointRounding.AwayFromZero);

        // from の次（step 方向）から循環して、最初のロックでないマス。なければ -1。
        private int NextEditable(int from, int step)
        {
            for (var k = 1; k <= _cells.Length; k++)
            {
                var i = ((from + step * k) % _cells.Length + _cells.Length) % _cells.Length;
                if (!IsLocked(i))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
