using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class ReelCustomizerTests
    {
        private const int Length = 12;

        /// <summary>plan 5章の既定配列: 7・攻・コ・回・速・コ・BAR・攻・コ・回・速・コ。</summary>
        private const string DefaultIds = "seven atk coin heal speed coin bar atk coin heal speed coin";

        private static ReelLayout Layout(string ids) =>
            new ReelLayout(ids.Split(' ').Select(id => (SlotSymbol)System.Array.IndexOf(SlotSymbols.Ids, id)).ToArray());

        private static ReelCustomizer Create(string ids = DefaultIds, double missRate = 0.25) =>
            new ReelCustomizer(Layout(ids), Length, missRate);

        [Test]
        [Description("AC-045: ハズレ率と当たり率は設定の値を小数1桁の%で表す")]
        public void Miss_And_Hit_Are_Percent_In_Tenths()
        {
            var customizer = Create(missRate: 0.25);

            Assert.That((customizer.MissTenths, customizer.HitTenths), Is.EqualTo((250, 750)));
        }

        [TestCase(1.0 / 3, 333)]
        [TestCase(2.0 / 3, 667)]
        [TestCase(0.0625, 63)]
        [TestCase(0.15, 150)]
        [TestCase(0.0, 0)]
        [TestCase(1.0, 1000)]
        [Description("AC-045, plan 5章: % は小数1桁に四捨五入")]
        public void ToTenths_Rounds_Half_Away_From_Zero(double fraction, int expected)
        {
            Assert.That(ReelCustomizer.ToTenths(fraction), Is.EqualTo(expected));
        }

        [Test]
        [Description("AC-045: 当たり時の各図柄の%はマス数 ÷ 総マス数（既定配列: コイン 4/12=33.3%、7 1/12=8.3%、攻 2/12=16.7%）")]
        public void Symbol_Percent_Is_Cells_Over_Length()
        {
            var customizer = Create();

            Assert.That(
                (customizer.SymbolTenths(SlotSymbol.Coin), customizer.SymbolTenths(SlotSymbol.Seven),
                    customizer.SymbolTenths(SlotSymbol.Atk)),
                Is.EqualTo((333, 83, 167)));
        }

        [Test]
        [Description("AC-045: 配列を変えると各図柄の%が即時に変わり、Changed が呼ばれる")]
        public void Cycling_A_Cell_Updates_Counts_And_Percent()
        {
            var customizer = Create();
            var changed = 0;
            customizer.Changed += () => changed++;

            customizer.Select(2); // コイン → 次は Seven（並びの先頭へ循環）
            customizer.CycleSelected(1);

            Assert.That(
                (customizer.At(2), customizer.Count(SlotSymbol.Coin), customizer.SymbolTenths(SlotSymbol.Coin),
                    customizer.Count(SlotSymbol.Seven), changed),
                Is.EqualTo((SlotSymbol.Seven, 3, 250, 2, 1)));
        }

        [Test]
        [Description("AC-044: 7 が2つになると確定できず、7 が条件を満たさない図柄として分かる")]
        public void Two_Sevens_Cannot_Be_Confirmed()
        {
            var customizer = Create();
            customizer.Select(2);
            customizer.CycleSelected(1);

            Assert.That(
                (customizer.IsValid, customizer.TryConfirm(), customizer.IsCountAllowed(SlotSymbol.Seven),
                    customizer.IsCountAllowed(SlotSymbol.Coin)),
                Is.EqualTo((false, (ReelLayout)null, false, true)));
        }

        [Test]
        [Description("AC-044: 図柄を1つ戻せば（下方向へ切り替え）再び確定できる")]
        public void Cycling_Back_Restores_Valid_Layout()
        {
            var customizer = Create();
            customizer.Select(2);
            customizer.CycleSelected(1);
            customizer.CycleSelected(-1);

            Assert.That((customizer.At(2), customizer.IsValid), Is.EqualTo((SlotSymbol.Coin, true)));
        }

        [Test]
        [Description("AC-044: 満たす配列は確定でき、確定した配列は編集内容と同じ")]
        public void Valid_Layout_Is_Confirmed_As_Edited()
        {
            var customizer = Create();
            customizer.Select(3); // 回 → コ（各図柄1つ以上は保たれる）
            customizer.CycleSelected(1);

            var layout = customizer.TryConfirm();

            Assert.That(layout, Is.Not.Null);
            var expected = Layout("seven atk coin coin speed coin bar atk coin heal speed coin");
            Assert.That(Enumerable.Range(0, Length).Select(layout.At),
                Is.EqualTo(Enumerable.Range(0, Length).Select(expected.At)));
            Assert.That(layout.IsValid(Length), Is.True);
        }

        [Test]
        [Description("AC-044: マス数が reel_length と違う配列は確定できない")]
        public void Wrong_Length_Cannot_Be_Confirmed()
        {
            var customizer = Create("seven atk coin heal speed coin bar atk coin heal speed");

            Assert.That(customizer.IsValid, Is.False);
        }

        [Test]
        [Description("plan 3章: 左右でマスを選ぶ（端は循環）")]
        public void Selection_Wraps_Around()
        {
            var customizer = Create();

            customizer.MoveSelection(-1);
            var left = customizer.Selected;
            customizer.MoveSelection(2);

            Assert.That((left, customizer.Selected), Is.EqualTo((Length - 1, 1)));
        }

        [Test]
        [Description("編集しても元の配列は変わらない")]
        public void Editing_Does_Not_Change_Start_Layout()
        {
            var start = Layout(DefaultIds);
            var customizer = new ReelCustomizer(start, Length, 0.25);

            customizer.CycleSelected(1);

            Assert.That(start.At(0), Is.EqualTo(SlotSymbol.Seven));
        }
    }
}
