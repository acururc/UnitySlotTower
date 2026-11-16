using System;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("D-71, D-87, AC-014: カメラにマップ全体が収まる距離と注視点")]
    public sealed class BattleCameraFitTests
    {
        private static readonly double[] SideHeights = { 0.0, BattleCameraFit.DefaultSideHeight };

        private static (double X, double Z)[] Corners(int columns, int rows)
        {
            var hx = columns * 0.5;
            var hz = rows * 0.5;
            return new[] { (-hx, -hz), (hx, -hz), (-hx, hz), (hx, hz) };
        }

        private static bool AnyCornerOutside(BattleCameraFit.Pose pose, int columns, int rows)
        {
            foreach (var y in SideHeights)
            {
                foreach (var (x, z) in Corners(columns, rows))
                {
                    if (!pose.IsVisible(x, y, z))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static BattleCameraFit.Pose Closer(BattleCameraFit.Pose pose, double by) =>
            BattleCameraFit.Pose.Create(pose.Distance - by, pose.TargetZ, pose.PitchDegrees, pose.VerticalFovDegrees, pose.Aspect);

        [TestCase(9, 4)]
        [TestCase(9, 5)]
        [TestCase(10, 4)]
        [TestCase(10, 5)]
        [TestCase(9, 6)]
        [Description("AC-014: 9×4〜10×5 のどのマップでも四隅（地面と高台の上面）が視野に入り、空中の高さの四隅も上下は視野に入る")]
        public void All_Corners_Are_In_View(int columns, int rows)
        {
            var pose = BattleCameraFit.Fit(columns, rows);

            Assert.That(AnyCornerOutside(pose, columns, rows), Is.False);
            foreach (var (_, z) in Corners(columns, rows))
            {
                Assert.That(pose.IsVisible(0, BattleCameraFit.DefaultHeight, z), Is.True, $"{columns}×{rows} の空中の z={z} が上下の外");
            }
        }

        [Test]
        [Description("D-87（T368）: 左右の余白はない。既定の距離から 0.05 近づくと、地面か高台の上面の四隅のどれかが外れる")]
        public void Fit_Is_Tight()
        {
            var pose = BattleCameraFit.Fit(10, 5);

            Assert.That(AnyCornerOutside(Closer(pose, 0.05), 10, 5), Is.True);
        }

        [Test]
        [Description("D-87（T368）: 戦闘の領域は 1440×960（上半分）で、横の画角は 6:4 のとき（縦 45°・1440:1152）と同じ")]
        public void Horizontal_View_Keeps_The_Old_Look()
        {
            var pose = BattleCameraFit.Fit(10, 5);
            var tanH = Math.Tan(pose.VerticalFovDegrees * Math.PI / 360) * pose.Aspect;

            Assert.That(new[] { pose.Aspect, tanH },
                Is.EqualTo(new[] { 1440.0 / 960.0, Math.Tan(22.5 * Math.PI / 180) * 1440.0 / 1152.0 }).Within(1e-6));
        }

        [Test]
        public void Wider_Map_Needs_A_Farther_Camera()
        {
            Assert.That(BattleCameraFit.Fit(10, 5).Distance, Is.GreaterThan(BattleCameraFit.Fit(9, 5).Distance));
        }

        [Test]
        [Description("plan 16.6・T357: 俯角 55° で、カメラはマップの手前（-z）上方にあり注視点を向く")]
        public void Camera_Looks_Down_From_The_Near_Side()
        {
            var pose = BattleCameraFit.Fit(10, 5);

            Assert.That((pose.PitchDegrees, pose.PositionY > 0, pose.PositionZ < pose.TargetZ),
                Is.EqualTo((55.0, true, true)));
        }

        [Test]
        public void Point_Behind_The_Camera_Is_Not_Visible()
        {
            var pose = BattleCameraFit.Fit(10, 5);

            Assert.That(pose.IsVisible(0, pose.PositionY, pose.PositionZ - 1), Is.False);
        }
    }
}
