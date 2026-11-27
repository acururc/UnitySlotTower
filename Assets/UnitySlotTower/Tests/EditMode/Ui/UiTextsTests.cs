using System;
using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Ui;
using UnitySlotTower.Flow;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Ui
{
    [Description("T352 (FR-006, plan 16.9): 文字あり版の文言表 ui_texts.csv")]
    public sealed class UiTextsTests
    {
        private static UiTexts ProjectTexts() =>
            UiTexts.Parse(File.ReadAllText(Path.Combine(TablePaths.Root, UiTexts.FileName)));

        [Test]
        public void Reads_Ids_And_Texts()
        {
            var texts = UiTexts.Parse("id,ja\nlife,ライフ\nback,もどる\n");

            Assert.That((texts.Count, texts.Get("life"), texts.TryGet("none", out _)), Is.EqualTo((2, "ライフ", false)));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => texts.Get("none"));
        }

        [TestCase("id,ja\nlife,ライフ\nlife,命\n", "重複")]
        [TestCase("id,ja\nlife, \n", "空")]
        [TestCase("id,ja\n,ライフ\n", "ID が空")]
        [TestCase("id\nlife\n", "ja")]
        public void Broken_Table_Is_Rejected(string csv, string message)
        {
            var ex = Assert.Throws<CsvFormatException>(() => UiTexts.Parse(csv));
            Assert.That(ex.Message, Does.Contain(message));
        }

        [Test]
        public void Characters_Lists_Each_Letter_Once_Without_Line_Breaks()
        {
            var texts = UiTexts.Parse("id,ja\na,ライフ\nb,\"ライ\nン\"\n");

            Assert.That(texts.Characters(), Is.EqualTo("ライフン"));
        }

        [Test]
        public void Project_Table_Has_Texts_For_Every_Id_The_Game_Builds()
        {
            var texts = ProjectTexts();
            var missing = new System.Collections.Generic.List<string>();
            void Need(string id)
            {
                if (!texts.TryGet(id, out _))
                {
                    missing.Add(id);
                }
            }

            foreach (var kind in RouteNodeKinds.All)
            {
                Need("node_" + kind);
            }

            var units = CsvParser.Parse("units.csv", File.ReadAllText(Path.Combine(TablePaths.Root, "units.csv")));
            foreach (var row in units.Rows)
            {
                Need("unit_" + row.Get("id"));
                Need("help2_unit_" + row.Get("id"));
            }

            foreach (var symbol in SlotSymbols.Ids)
            {
                Need("help2_" + symbol);
            }

            foreach (var id in new[] { "help2_seconds", "help2_bet_spin", "detail_hp", "detail_atk", "detail_block", "battle_end" })
            {
                Need(id);
            }

            Assert.That(missing, Is.Empty);
        }
    }
}
