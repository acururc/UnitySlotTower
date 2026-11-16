using System;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>ダメージ式（plan 5章、アークナイツ準拠）: max(攻撃力 − 防御力, 攻撃力 × 0.05)。</summary>
    public static class Damage
    {
        public const double MinimumRate = 0.05;

        /// <summary>遠距離耐性は防御・最低ダメージ計算の後に掛け、端数を四捨五入する（FR-075）。</summary>
        public static int Calculate(int atk, int def, UnitRole attackerRole, double rangedDamageRate) =>
            (int)Math.Round(Calculate(atk, def) * (attackerRole == UnitRole.Ranged ? rangedDamageRate : 1),
                MidpointRounding.AwayFromZero);

        /// <summary>最低ダメージの端数は四捨五入する。</summary>
        public static int Calculate(int atk, int def) =>
            Math.Max(atk - def, (int)Math.Round(atk * MinimumRate, MidpointRounding.AwayFromZero));
    }
}
