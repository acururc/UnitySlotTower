using NUnit.Framework;
using UnitySlotTower.Core.Layout;

namespace UnitySlotTower.Tests.EditMode.Layout
{
    [Description("FR-001 縦長 3:4、上部 60% が戦闘、余白は黒帯")]
    public sealed class PortraitLayoutTests
    {
        private static (double, double, double, double) Tuple(NormalizedRect r) => (r.X, r.Y, r.Width, r.Height);

        [TestCase(1440, 1920)]
        [TestCase(1920, 1080)]
        [TestCase(1080, 2400)]
        public void BattleView_Reserves_The_Top_Band(int width, int height)
        {
            var battle = PortraitLayout.Battle(width, height);
            var view = PortraitLayout.BattleView(width, height);
            var band = battle.Height * PortraitLayout.TopBandRatio;
            Assert.That(new[] { view.X, view.Y, view.Width, view.Y + view.Height, view.Height + band },
                Is.EqualTo(new[] { battle.X, battle.Y, battle.Width, battle.Y + battle.Height - band, battle.Height }).Within(1e-9));
        }

        [Test]
        [Description("T614: 外枠の内側で、上端の帯（96 px の 0.94 倍）を除く")]
        public void Reference_BattleView_Leaves_The_Frame_And_Hud()
        {
            var view = PortraitLayout.BattleView(1440, 1920);
            Assert.That(new[] { view.Height * 1920, (1 - view.Y - view.Height) * 1920, view.X * 1440, view.Width * 1440 },
                Is.EqualTo(new[] { 864.0 * 0.94, 57.6 + 96.0 * 0.94, 43.2, 1440.0 * 0.94 }).Within(1e-6));
        }

        [Test]
        [Description("T614（FR-116）: 中身は 3:4 の領域の中央に同じ比のまま縮め、周りを外枠に空ける")]
        public void Content_Is_Centered_Inside_The_Frame()
        {
            var content = PortraitLayout.Content(1080, 2400);
            var scale = PortraitLayout.ContentScale;

            Assert.That(Tuple(content), Is.EqualTo(((1 - scale) / 2, 0.2 + 0.6 * (1 - scale) / 2, scale, 0.6 * scale))
                .Using(new Tolerance()));
        }

        [Test]
        public void Window_Of_960x1280_Uses_Whole_Screen()
        {
            Assert.That(Tuple(PortraitLayout.Letterbox(960, 1280)), Is.EqualTo((0.0, 0.0, 1.0, 1.0)));
        }

        [Test]
        public void Landscape_1080p_Gets_Side_Bars()
        {
            // 高さ 1080 に合わせた幅 810 px を中央に置く。
            var rect = PortraitLayout.Letterbox(1920, 1080);

            Assert.That(Tuple(rect), Is.EqualTo((555.0 / 1920, 0.0, 810.0 / 1920, 1.0)).Using(new Tolerance()));
        }

        [Test]
        public void Landscape_1440p_Gets_Side_Bars()
        {
            var rect = PortraitLayout.Letterbox(2560, 1440);

            Assert.That(Tuple(rect), Is.EqualTo((740.0 / 2560, 0.0, 1080.0 / 2560, 1.0)).Using(new Tolerance()));
        }

        [Test]
        public void Taller_Than_3x4_Gets_Top_And_Bottom_Bars()
        {
            // 幅 1080 に合わせた高さ 1440 px を、高さ 2400 の中央に置く。
            var rect = PortraitLayout.Letterbox(1080, 2400);

            Assert.That(Tuple(rect), Is.EqualTo((0.0, 480.0 / 2400, 1.0, 1440.0 / 2400)).Using(new Tolerance()));
        }

        [Test]
        [Description("D-87（T368）: 戦闘は外枠の内側（T614）の上半分")]
        public void Battle_View_Is_Top_Half_Of_Content()
        {
            var battle = PortraitLayout.Battle(1080, 2400);
            var content = PortraitLayout.Content(1080, 2400);

            Assert.That(Tuple(battle), Is.EqualTo((content.X, content.Y + content.Height * 0.5, content.Width, content.Height * 0.5))
                .Using(new Tolerance()));
        }

        [Test]
        [Description("D-87（T368）: 操作バーは戦闘の下端と筐体の上端の間（240 px）、筐体は 720 px")]
        public void Operation_Bar_Sits_Between_Battle_And_Slot()
        {
            var bar = (1 - PortraitLayout.BattleHeightRatio - PortraitLayout.SlotAreaTop) * PortraitLayout.ReferenceHeight;
            var slot = PortraitLayout.SlotAreaTop * PortraitLayout.ReferenceHeight;

            Assert.That(new[] { bar, slot }, Is.EqualTo(new[] { 240.0, 720.0 }).Within(1e-6));
        }

        [Test]
        public void Ui_Matches_Height_On_Wide_Screens()
        {
            Assert.That(PortraitLayout.MatchHeight(1920, 1080), Is.True);
        }

        [Test]
        public void Ui_Matches_Width_On_Tall_Screens()
        {
            Assert.That(PortraitLayout.MatchHeight(1080, 2400), Is.False);
        }

        private sealed class Tolerance : System.Collections.IComparer
        {
            public int Compare(object x, object y)
            {
                var a = ((double, double, double, double))x;
                var b = ((double, double, double, double))y;
                const double eps = 1e-6;
                var same = System.Math.Abs(a.Item1 - b.Item1) < eps && System.Math.Abs(a.Item2 - b.Item2) < eps
                    && System.Math.Abs(a.Item3 - b.Item3) < eps && System.Math.Abs(a.Item4 - b.Item4) < eps;
                return same ? 0 : 1;
            }
        }
    }
}
