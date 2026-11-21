using System;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// ダメージ式（001 plan 5章）: max(攻撃力 − 防御力, 攻撃力 × 0.05, 1)。
    /// 002 の小さい桁では端数が0になるので、攻撃力1以上なら最低1、攻撃力0以下なら0（FR-109）。
    /// </summary>
    public static class Damage
    {
        public const double MinimumRate = 0.05;

        /// <summary>遠距離耐性は防御・最低ダメージ計算の後に掛け、端数を四捨五入する（FR-075）。掛けた後も最低1。</summary>
        public static int Calculate(int atk, int def, UnitRole attackerRole, double rangedDamageRate)
        {
            if (atk <= 0) return 0;
            return Math.Max(1, (int)Math.Round(Calculate(atk, def) *
                (attackerRole == UnitRole.Ranged ? rangedDamageRate : 1),
                MidpointRounding.AwayFromZero));
        }

        /// <summary>最低ダメージの端数は四捨五入する。</summary>
        public static int Calculate(int atk, int def)
        {
            if (atk <= 0) return 0;
            return Math.Max(1, Math.Max(atk - def,
                (int)Math.Round(atk * MinimumRate, MidpointRounding.AwayFromZero)));
        }
    }
}
