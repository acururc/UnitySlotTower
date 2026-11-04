using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    /// <summary>ロック12マス＋空き6マスのカスタム（D-46・D-50、FR-046・FR-048、AC-044・AC-045）。</summary>
    public sealed class ReelCustomizerTests
    {
        /// <summary>plan 5章の初期のロック配置（_ は空き: 6〜8 と 15〜17）。</summary>
        public const string LockIds = "atk speed coin bar coin seven _ _ _ bar speed heal atk seven heal _ _ _";

        public static ReelLocks Locks(string ids = LockIds) =>
            new ReelLocks(ids.Split(' ')
                .Select(id => id == "_" ? (SlotSymbol?)null : (SlotSymbol)System.Array.IndexOf(SlotSymbols.Ids, id)).ToArray());

        private static ReelCustomizer Create(double missRate = 0.25) => new ReelCustomizer(Locks(), missRate);

        private static readonly int[] Empties = { 6, 7, 8, 15, 16, 17 };

        [Test]
        [Description("AC-045: ハズレ率と当たり率は設定の値を小数1桁の%で表す")]
        public void Miss_And_Hit_Are_Percent_In_Tenths()
        {
            Assert.That((Create(0.25).MissTenths, Create(0.25).HitTenths), Is.EqualTo((250, 750)));
        }

        [TestCase(1.0 / 3, 333)]
        [TestCase(2.0 / 3, 667)]
        [TestCase(0.0625, 63)]
        [TestCase(0.0, 0)]
        [TestCase(1.0, 1000)]
        [Description("AC-045, plan 5章: % は小数1桁に四捨五入")]
        public void ToTenths_Rounds_Half_Away_From_Zero(double fraction, int expected)
        {
            Assert.That(ReelCustomizer.ToTenths(fraction), Is.EqualTo(expected));
        }

        [Test]
        [Description("D-46: 開始時はロック以外が空きで、確定できない")]
        public void Starts_With_Empty_Cells_And_Cannot_Confirm()
        {
            var customizer = Create();

            Assert.That(Empties.All(i => customizer.At(i) == null && !customizer.IsLocked(i)), Is.True);
            Assert.That((customizer.EmptyCount, customizer.IsValid, customizer.TryConfirm()), Is.EqualTo((6, false, (ReelLayout)null)));
        }

        [Test]
        [Description("D-46: ロックのマスには入れられず、外せない")]
        public void Locked_Cells_Refuse_Place_And_Clear()
        {
            var customizer = Create();

            Assert.That((customizer.Place(0, SlotSymbol.Coin), customizer.Clear(0), customizer.At(0)),
                Is.EqualTo((false, false, (SlotSymbol?)SlotSymbol.Atk)));
        }

        [TestCase(SlotSymbol.Seven)]
        [TestCase(SlotSymbol.Bar)]
        [Description("D-46: 空きに入れられるのは攻撃力・攻撃速度・回復・コインだけ")]
        public void Seven_And_Bar_Cannot_Be_Placed(SlotSymbol symbol)
        {
            var customizer = Create();

            Assert.That((customizer.Place(6, symbol), customizer.At(6)), Is.EqualTo((false, (SlotSymbol?)null)));
        }

        [Test]
        [Description("D-46: 確定前は上書きと外しができ、変わるたびに通知する")]
        public void Placed_Cells_Can_Be_Overwritten_And_Cleared()
        {
            var customizer = Create();
            var changes = 0;
            customizer.Changed += () => changes++;

            customizer.Place(6, SlotSymbol.Coin);
            customizer.Place(6, SlotSymbol.Heal);
            var cleared = customizer.Clear(6);

            Assert.That((cleared, customizer.At(6), customizer.Clear(6), changes),
                Is.EqualTo((true, (SlotSymbol?)null, false, 3)));
        }

        [Test]
        [Description("D-46, AC-044: 空きがなくなれば確定でき、ロックと入れた図柄の配列になる")]
        public void Filling_All_Empties_Confirms_The_Layout()
        {
            var customizer = Create();
            foreach (var i in Empties)
            {
                customizer.Place(i, SlotSymbol.Coin);
            }

            var layout = customizer.TryConfirm();

            Assert.That(layout, Is.Not.Null);
            Assert.That((layout.Length, layout.At(5), layout.At(6), layout.Count(SlotSymbol.Coin)),
                Is.EqualTo((18, SlotSymbol.Seven, SlotSymbol.Coin, 8)));
        }

        [Test]
        [Description("FR-048: 空きがあるあいだは埋まったマスだけで当たり時の%を計算する")]
        public void Symbol_Percent_Uses_Filled_Cells_Only()
        {
            var customizer = Create();
            Assert.That(customizer.SymbolTenths(SlotSymbol.Seven), Is.EqualTo(167), "2/12");

            foreach (var i in Empties)
            {
                customizer.Place(i, SlotSymbol.Coin);
            }

            Assert.That((customizer.SymbolTenths(SlotSymbol.Seven), customizer.SymbolTenths(SlotSymbol.Coin)),
                Is.EqualTo((111, 444)), "2/18 と 8/18");
        }

        [Test]
        [Description("D-50: 左右の移動はロックを飛ばして空き（入れたマス）だけを回る")]
        public void Selection_Skips_Locked_Cells()
        {
            var customizer = Create();
            Assert.That(customizer.Selected, Is.EqualTo(6), "最初の空き");

            var visited = new int[7];
            for (var i = 0; i < visited.Length; i++)
            {
                visited[i] = customizer.Selected;
                customizer.MoveSelection(1);
            }

            Assert.That(visited, Is.EqualTo(new[] { 6, 7, 8, 15, 16, 17, 6 }));
            customizer.MoveSelection(-1); // 7 → 6
            customizer.MoveSelection(-1); // 端を越えて 17
            Assert.That(customizer.Selected, Is.EqualTo(17));
        }

        [Test]
        [Description("D-50: 上下で入れる図柄を4種で回し、Space で選んだマスに入れ、Backspace で外す")]
        public void Brush_Cycles_Fillable_Symbols_And_Places_On_Selected()
        {
            var customizer = Create();
            var brushes = new SlotSymbol[5];
            for (var i = 0; i < brushes.Length; i++)
            {
                brushes[i] = customizer.Brush;
                customizer.CycleBrush(1);
            }

            Assert.That(brushes, Is.EqualTo(new[] { SlotSymbol.Atk, SlotSymbol.Speed, SlotSymbol.Heal, SlotSymbol.Coin, SlotSymbol.Atk }));
            customizer.CycleBrush(-2); // 速 → 攻 → 端を越えてコイン
            customizer.MoveSelection(1);
            Assert.That((customizer.PlaceSelected(), customizer.At(7)), Is.EqualTo((true, (SlotSymbol?)SlotSymbol.Coin)));
            Assert.That((customizer.ClearSelected(), customizer.At(7)), Is.EqualTo((true, (SlotSymbol?)null)));
        }
    }
}
