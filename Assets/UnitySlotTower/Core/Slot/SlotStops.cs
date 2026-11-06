namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// 3リール×3段の出目の条件（D-52〜D-54・D-58、plan 5章 スロット）。停止位置 s のリールは上段に s−1、中段に s、下段に s+1 を見せる。
    /// 有効ラインは上・中・下と斜め2本の5本。当たりは結果の図柄がちょうど1本に3つ揃い、残りのラインは揃わない。ハズレはどのラインも揃わない。
    /// 配列によっては厳密な条件を満たせないので、そのときの緩い条件（当たりは結果の図柄がどこかのラインに揃う）も持つ。
    /// </summary>
    public static class SlotStops
    {
        public const int Rows = 3;

        /// <summary>有効ライン（D-52・D-58）。各リールで中段から何段ずれたマスを使うか。</summary>
        private static readonly int[][] Lines =
        {
            new[] { -1, -1, -1 }, new[] { 0, 0, 0 }, new[] { 1, 1, 1 }, new[] { -1, 0, 1 }, new[] { 1, 0, -1 },
        };

        /// <summary>3本の停止位置が結果どおりの最終形か。</summary>
        public static bool IsFinal(ReelLayout layout, int s0, int s1, int s2, SlotSymbol? result, bool strict = true)
        {
            var aligned = 0;
            var resultAligned = false;
            foreach (var line in Lines)
            {
                var a = layout.At(s0 + line[0]);
                if (a != layout.At(s1 + line[1]) || a != layout.At(s2 + line[2]))
                {
                    continue;
                }

                aligned++;
                if (result.HasValue && a == result.Value)
                {
                    resultAligned = true;
                }
                else if (strict || !result.HasValue)
                {
                    return false;
                }
            }

            return result.HasValue ? resultAligned && (!strict || aligned == 1) : aligned == 0;
        }

        /// <summary>
        /// 止まっているリール（<paramref name="stopped"/> が true の <paramref name="stops"/>）はそのままに、
        /// 残りのリールの位置を総当たりして最終形が作れるか。
        /// </summary>
        public static bool CanComplete(ReelLayout layout, int[] stops, bool[] stopped, SlotSymbol? result, bool strict)
        {
            var n = layout.Length;
            var r0 = stopped[0] ? 1 : n;
            var r1 = stopped[1] ? 1 : n;
            var r2 = stopped[2] ? 1 : n;
            for (var a = 0; a < r0; a++)
            {
                var s0 = stopped[0] ? stops[0] : a;
                for (var b = 0; b < r1; b++)
                {
                    var s1 = stopped[1] ? stops[1] : b;
                    for (var c = 0; c < r2; c++)
                    {
                        if (IsFinal(layout, s0, s1, stopped[2] ? stops[2] : c, result, strict))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>どのリールも止まっていない状態から、当たり（6図柄）とハズレのすべての最終形を厳密な条件で作れるか。</summary>
        public static bool CanShowEveryResult(ReelLayout layout)
        {
            var stops = new int[SlotMachine.ReelCount];
            var stopped = new bool[SlotMachine.ReelCount];
            if (!CanComplete(layout, stops, stopped, null, true))
            {
                return false;
            }

            for (var s = 0; s < SlotSymbols.Count; s++)
            {
                if (!CanComplete(layout, stops, stopped, (SlotSymbol)s, true))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// ロック配置の空きの埋め方（<see cref="ReelLocks.Fillable"/> の全組み合わせ）のうち、
        /// <see cref="CanShowEveryResult"/> を満たさない最初の配列。すべて満たせば null（D-54 のデータ検証）。
        /// </summary>
        public static ReelLayout FirstUnsolvableFill(ReelLocks locks)
        {
            var fillable = ReelLocks.Fillable;
            var fill = new SlotSymbol[locks.EmptyCount];
            var digits = new int[fill.Length];
            while (true)
            {
                for (var i = 0; i < fill.Length; i++)
                {
                    fill[i] = fillable[digits[i]];
                }

                var layout = locks.Fill(fill);
                if (!CanShowEveryResult(layout))
                {
                    return layout;
                }

                var d = 0;
                while (d < digits.Length && ++digits[d] == fillable.Length)
                {
                    digits[d] = 0;
                    d++;
                }

                if (d == digits.Length)
                {
                    return null;
                }
            }
        }
    }
}
