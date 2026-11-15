using NUnit.Framework;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("D-71, AC-014: カメラにマップ全体が収まる距離と注視点")]
    public sealed class BattleCameraFitTests
    {
        private static void AssertCornersVisible(BattleCameraFit.Pose pose, int columns, int rows)
        {
            var hx = columns * 0.5;
            var hz = rows * 0.5;
            foreach (var y in new[] { 0.0, BattleCameraFit.DefaultHeight })
            {
                foreach (var (x, z) in new[] { (-hx, -hz), (hx, -hz), (-hx, hz), (hx, hz) })
                {
                    Assert.That(pose.IsVisible(x, y, z), Is.True, $"{columns}×{rows} の角 ({x}, {y}, {z}) が視野の外");
                }
            }
        }

        [TestCase(9, 4)]
        [TestCase(9, 5)]
        [TestCase(10, 4)]
        [TestCase(10, 5)]
        [TestCase(9, 6)]
        [Description("AC-014: 9×4〜10×5 のどのマップでも四隅（地面と空中の高さ）が視野に入る")]
        public void All_Corners_Are_In_View(int columns, int rows)
        {
            var pose = BattleCameraFit.Fit(columns, rows);

            AssertCornersVisible(pose, columns, rows);
        }

        [Test]
        [Description("余白を足しても大きく引きすぎない: 余白なしなら少し近づくだけで角が外れる")]
        public void Fit_Is_Tight()
        {
            var pose = BattleCameraFit.Fit(10, 5, margin: 0, height: 0);
            var closer = BattleCameraFit.Pose.Create(pose.Distance - 0.05, pose.TargetZ, pose.PitchDegrees,
                pose.VerticalFovDegrees, pose.Aspect);

            var anyOutside = false;
            foreach (var (x, z) in new[] { (-5.0, -2.5), (5.0, -2.5), (-5.0, 2.5), (5.0, 2.5) })
            {
                anyOutside |= !closer.IsVisible(x, 0, z);
            }

            Assert.That(anyOutside, Is.True);
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
