using System;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// 色ごとのスキルゲージ（FR-042・FR-029、plan 16.4）。色7が揃うとその色が1段たまり、満タンなら増えない。
    /// 発動で0に戻る。<see cref="Run.RunState"/> が持ち、戦闘マスをまたいで残る。
    /// </summary>
    public sealed class SkillGauges
    {
        // 色7（赤・青・緑・黄）は SlotSymbol の先頭4つ。
        private const int ColorCount = 4;

        private readonly int[] _levels = new int[ColorCount];

        /// <param name="max">満タンの段数（slot_rules の <c>gauge_max</c>）。</param>
        public SkillGauges(int max)
        {
            if (max < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "ゲージの最大段数は1以上");
            }

            Max = max;
        }

        /// <summary>満タンの段数。</summary>
        public int Max { get; }

        /// <summary>その色の段数（0〜<see cref="Max"/>）。</summary>
        public int Level(SlotSymbol color) => _levels[IndexOf(color)];

        public bool IsFull(SlotSymbol color) => Level(color) >= Max;

        /// <summary>その色のゲージを1段上げる。満タンなら変わらない。</summary>
        public void Add(SlotSymbol color)
        {
            var index = IndexOf(color);
            if (_levels[index] < Max)
            {
                _levels[index]++;
            }
        }

        /// <summary>その色のゲージを0に戻す（スキルの発動）。</summary>
        public void Clear(SlotSymbol color) => _levels[IndexOf(color)] = 0;

        /// <summary>色7の図柄か。</summary>
        public static bool IsColor(SlotSymbol symbol) => (int)symbol < ColorCount;

        private static int IndexOf(SlotSymbol color)
        {
            if (!IsColor(color))
            {
                throw new ArgumentOutOfRangeException(nameof(color), color, "色7の図柄ではない");
            }

            return (int)color;
        }
    }
}
