using System;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// リールのカスタム画面の中身（FR-046〜048、plan 5章 スロット）。マスを選び、図柄を6種で順に切り替え、
    /// 条件（D-46）を満たすときだけ確定できる。表示する%（ハズレ・当たり・当たり時の各図柄）は小数1桁に四捨五入した 1/10 % 単位で返す。
    /// </summary>
    public sealed class ReelCustomizer
    {
        private readonly SlotSymbol[] _cells;
        private readonly int[] _counts = new int[SlotSymbols.Count];
        private readonly int _reelLength;

        /// <param name="start">編集を始める配列（既定配列か、前回確定した配列）。</param>
        /// <param name="reelLength">確定に必要なマス数（slot_rules.csv の reel_length）。</param>
        /// <param name="missRate">現在の難易度のハズレ率（0〜1）。</param>
        public ReelCustomizer(ReelLayout start, int reelLength, double missRate)
        {
            _cells = new SlotSymbol[start.Length];
            for (var i = 0; i < _cells.Length; i++)
            {
                _cells[i] = start.At(i);
                _counts[(int)_cells[i]]++;
            }

            _reelLength = reelLength;
            MissTenths = ToTenths(missRate);
            HitTenths = ToTenths(1.0 - missRate);
        }

        /// <summary>配列が変わったとき（図柄の変更）に呼ぶ。</summary>
        public event Action Changed;

        public int Length => _cells.Length;

        /// <summary>選んでいるマス（0 始まり）。</summary>
        public int Selected { get; private set; }

        /// <summary>ハズレ率（1/10 % 単位。25.0% なら 250）。</summary>
        public int MissTenths { get; }

        /// <summary>当たり率（1/10 % 単位）。</summary>
        public int HitTenths { get; }

        public SlotSymbol At(int index) => _cells[index];

        public int Count(SlotSymbol symbol) => _counts[(int)symbol];

        /// <summary>当たり時にこの図柄が揃う確率（マス数 ÷ 総マス数、1/10 % 単位）。</summary>
        public int SymbolTenths(SlotSymbol symbol) => ToTenths((double)Count(symbol) / _cells.Length);

        /// <summary>この図柄の数が条件（D-46）を満たすか。満たさない図柄は画面で赤く点滅させる。</summary>
        public bool IsCountAllowed(SlotSymbol symbol) => SlotSymbols.IsCountAllowed(symbol, Count(symbol));

        /// <summary>確定できるか（マス数が reel_length と一致し、全図柄の数が条件を満たす）。</summary>
        public bool IsValid
        {
            get
            {
                if (_cells.Length != _reelLength)
                {
                    return false;
                }

                for (var s = 0; s < SlotSymbols.Count; s++)
                {
                    if (!IsCountAllowed((SlotSymbol)s))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>マスを選ぶ（範囲外は循環）。</summary>
        public void Select(int index) => Selected = ((index % _cells.Length) + _cells.Length) % _cells.Length;

        /// <summary>選んでいるマスを左右に動かす（端は循環）。</summary>
        public void MoveSelection(int delta) => Select(Selected + delta);

        /// <summary>選んでいるマスの図柄を <see cref="SlotSymbol"/> の並びで delta だけ進める（端は循環）。</summary>
        public void CycleSelected(int delta)
        {
            var old = _cells[Selected];
            var next = (SlotSymbol)((((int)old + delta) % SlotSymbols.Count + SlotSymbols.Count) % SlotSymbols.Count);
            if (next == old)
            {
                return;
            }

            _counts[(int)old]--;
            _counts[(int)next]++;
            _cells[Selected] = next;
            Changed?.Invoke();
        }

        /// <summary>確定できるときだけ今の配列を返す。満たさないときは null。</summary>
        public ReelLayout TryConfirm() => IsValid ? new ReelLayout(_cells) : null;

        /// <summary>0〜1 の値を、小数1桁に四捨五入した % の 10 倍の整数にする（33.33…% → 333）。</summary>
        public static int ToTenths(double fraction) =>
            (int)Math.Round(fraction * 1000.0, MidpointRounding.AwayFromZero);
    }
}
