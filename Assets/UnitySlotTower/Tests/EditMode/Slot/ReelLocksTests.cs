using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    /// <summary>ロック配置 slot_locked_reel.csv（D-46・D-54、plan 5章）。</summary>
    public sealed class ReelLocksTests
    {
        private static ReelLocks Locks(string ids = ReelCustomizerTests.LockIds) => ReelCustomizerTests.Locks(ids);

        [Test]
        public void Load_Reads_Symbols_And_Empty_In_Index_Order()
        {
            var locks = ReelLocks.Load(CsvParser.Parse(ReelLocks.FileName, "index,symbol\n1,empty\n0,seven\n2,coin\n"));

            Assert.That(Enumerable.Range(0, locks.Length).Select(locks.LockedAt),
                Is.EqualTo(new SlotSymbol?[] { SlotSymbol.Seven, null, SlotSymbol.Coin }));
            Assert.That((locks.IsLocked(0), locks.IsLocked(1), locks.EmptyCount), Is.EqualTo((true, false, 1)));
        }

        [Test]
        public void Load_Rejects_A_Missing_Index_With_The_File_Name()
        {
            var error = Assert.Throws<CsvFormatException>(() =>
                ReelLocks.Load(CsvParser.Parse(ReelLocks.FileName, "index,symbol\n0,seven\n2,coin\n")));

            Assert.That(error.Error.File, Is.EqualTo(ReelLocks.FileName));
        }

        [Test]
        [Description("D-46: 初期のロック配置は 18 マスで、6図柄がちょうど2つずつ")]
        public void Plan_Locks_Have_No_Problems()
        {
            Assert.That(Locks().Problems(18), Is.Empty);
        }

        [TestCase("atk speed coin bar coin seven _ _ _ bar speed heal atk seven heal _ _", TestName = "Seventeen_Cells")]
        [TestCase("atk speed coin bar coin seven _ _ _ _ speed heal atk seven heal _ _ _", TestName = "One_Bar")]
        [TestCase("atk speed coin bar coin seven _ _ _ bar speed heal atk seven heal seven _ _", TestName = "Three_Sevens")]
        [Description("D-46: マス数がルールと違うか、ロックが2つずつでないと問題")]
        public void Locks_Breaking_A_Rule_Have_Problems(string ids)
        {
            Assert.That(Locks(ids).Problems(18), Is.Not.Empty);
        }

        [Test]
        [Description("D-46: 確定した配列はロックが一致し、空きに4図柄のどれかが入っているときだけ受け付ける")]
        public void Accepts_Only_Layouts_Matching_The_Locks()
        {
            var locks = Locks();
            var coin = locks.Fill(SlotSymbol.Coin);
            var changedLock = new ReelLayout(Enumerable.Range(0, 18).Select(i => i == 0 ? SlotSymbol.Coin : coin.At(i)).ToArray());
            var sevenInEmpty = new ReelLayout(Enumerable.Range(0, 18).Select(i => i == 6 ? SlotSymbol.Seven : coin.At(i)).ToArray());

            Assert.That((locks.Accepts(coin), locks.Accepts(changedLock), locks.Accepts(sevenInEmpty)),
                Is.EqualTo((true, false, false)));
        }
    }
}
