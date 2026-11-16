using System;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// 戦闘マップ全体が収まるカメラの距離と注視点（D-71、plan 16.6）。座標はフィールドのローカル（グリッドの中心が原点、
    /// x が列の右、z が奥〔CSV の上の行〕、y が上。マスは1単位）。カメラは x 方向に傾けず、手前の上から俯角で注視点を向く。
    /// </summary>
    public static class BattleCameraFit
    {
        public const double DefaultPitchDegrees = 55;

        /// <summary>戦闘領域の縦横比（1440:960、D-87・T368 で上半分）。</summary>
        public const double DefaultAspect = 1440.0 / 960.0;

        /// <summary>
        /// 縦の画角。横の画角を 6:4 のとき（縦 45°・1440:1152）と同じにする値: 2·atan(tan 22.5° × 1.25 ÷ 1.5)。
        /// </summary>
        public const double DefaultVerticalFovDegrees = 38.08709224;

        /// <summary>マップの外側に足す余白（マス）。D-87（T368）で 0。</summary>
        public const double DefaultMargin = 0;

        /// <summary>上下に収める高さ（空中の敵の高さ）。</summary>
        public const double DefaultHeight = 1.2;

        /// <summary>左右に収める高さ（高台の上面）。空中の敵は手前の角で左右がわずかに切れてよい（D-87、T368）。</summary>
        public const double DefaultSideHeight = 0.5;

        /// <summary>カメラの置き方。<see cref="IsVisible"/> で点が視野に入るかを確かめられる。</summary>
        public readonly struct Pose
        {
            private Pose(double distance, double targetZ, double pitchDegrees, double verticalFovDegrees, double aspect)
            {
                Distance = distance;
                TargetZ = targetZ;
                PitchDegrees = pitchDegrees;
                VerticalFovDegrees = verticalFovDegrees;
                Aspect = aspect;
            }

            public static Pose Create(double distance, double targetZ, double pitchDegrees, double verticalFovDegrees,
                double aspect) => new Pose(distance, targetZ, pitchDegrees, verticalFovDegrees, aspect);

            /// <summary>注視点からカメラまでの距離。</summary>
            public double Distance { get; }

            /// <summary>注視点（x=0, y=0）の z。</summary>
            public double TargetZ { get; }

            public double PitchDegrees { get; }
            public double VerticalFovDegrees { get; }
            public double Aspect { get; }

            private double Pitch => PitchDegrees * Math.PI / 180;

            public double PositionY => Distance * Math.Sin(Pitch);
            public double PositionZ => TargetZ - Distance * Math.Cos(Pitch);

            /// <summary>点が視野（左右・上下とも画角の内側、カメラの前）に入るか。境界上は入るとみなす。</summary>
            public bool IsVisible(double x, double y, double z)
            {
                const double epsilon = 1e-9;
                var sin = Math.Sin(Pitch);
                var cos = Math.Cos(Pitch);
                var dy = y - PositionY;
                var dz = z - PositionZ;
                var depth = -dy * sin + dz * cos;
                if (depth <= 0)
                {
                    return false;
                }

                var up = dy * cos + dz * sin;
                var tanV = Math.Tan(VerticalFovDegrees * Math.PI / 360);
                return Math.Abs(up) <= depth * tanV + epsilon && Math.Abs(x) <= depth * tanV * Aspect + epsilon;
            }
        }

        /// <summary>
        /// 列数×行数のマップの四隅（外側に <paramref name="margin"/> の余白）が視野に入る最小の距離を、注視点をマップの中心にして求める。
        /// 上下は地面と <paramref name="height"/>、左右は地面と <paramref name="sideHeight"/> の高さで収める。
        /// </summary>
        public static Pose Fit(int columns, int rows, double pitchDegrees = DefaultPitchDegrees,
            double verticalFovDegrees = DefaultVerticalFovDegrees, double aspect = DefaultAspect,
            double margin = DefaultMargin, double height = DefaultHeight, double sideHeight = DefaultSideHeight)
        {
            if (columns <= 0 || rows <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columns), $"マップの大きさが不正: {columns}×{rows}");
            }

            var pitch = pitchDegrees * Math.PI / 180;
            var sin = Math.Sin(pitch);
            var cos = Math.Cos(pitch);
            var tanV = Math.Tan(verticalFovDegrees * Math.PI / 360);
            var tanH = tanV * aspect;
            var hx = columns * 0.5 + margin;
            var hz = rows * 0.5 + margin;

            // 注視点から見た点 Q で、カメラは注視点 − 前方×距離。深さ = Q·前方 + 距離、上下 = Q·上、左右 = Q.x。
            var distance = 0.0;
            foreach (var z in new[] { -hz, hz })
            {
                foreach (var y in new[] { 0.0, height })
                {
                    var up = y * cos + z * sin;
                    distance = Math.Max(distance, Math.Abs(up) / tanV - (-y * sin + z * cos));
                }

                foreach (var y in new[] { 0.0, sideHeight })
                {
                    distance = Math.Max(distance, hx / tanH - (-y * sin + z * cos));
                }
            }

            return Pose.Create(distance, 0, pitchDegrees, verticalFovDegrees, aspect);
        }
    }
}
