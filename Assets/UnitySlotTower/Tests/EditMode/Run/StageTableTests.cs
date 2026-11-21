using System.Collections.Generic;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;

namespace UnitySlotTower.Tests.EditMode.Run
{
    [Description("T605 (FR-101〜FR-104): stages.csv をステージ番号で引く")]
    public sealed class StageTableTests
    {
        private const string Header = "stage,map_id,wave_mode,boss\n";

        private static StageTable Load(string rows) =>
            StageTable.Load(CsvParser.Parse(StageTable.FileName, Header + rows));

        [Test]
        public void Rows_Are_Read_With_Mode_Map_And_Boss()
        {
            var table = Load("1,first,clear,none\n2,elite_a,timed,mid_boss\n");

            var first = table.Get(1);
            var second = table.Get(2);
            Assert.That((table.Count, first.MapId, first.Mode, first.BossId), Is.EqualTo((2, "first", WaveMode.Clear, (string)null)));
            Assert.That((second.MapId, second.Mode, second.BossId), Is.EqualTo(("elite_a", WaveMode.Timed, "mid_boss")));
        }

        [Test]
        public void Find_By_Map_Returns_The_Stage_Or_Null()
        {
            var table = Load("1,first,clear,none\n2,battle_a,timed,none\n");

            Assert.That((table.FindByMap("battle_a").Number, table.FindByMap("boss_a")), Is.EqualTo((2, (StageDefinition)null)));
        }

        [Test]
        public void Unknown_Stage_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => Load("1,first,clear,none\n").Get(2));
        }

        [Test]
        public void Unknown_Wave_Mode_Is_An_Error_At_Its_Cell()
        {
            var error = Assert.Throws<CsvFormatException>(() => Load("1,first,rush,none\n")).Error;

            Assert.That((error.Line, error.Column), Is.EqualTo((2, 3)));
        }

        [Test]
        public void Duplicate_Stage_Is_An_Error_At_Its_Cell()
        {
            var error = Assert.Throws<CsvFormatException>(() => Load("1,first,clear,none\n1,battle_a,timed,none\n")).Error;

            Assert.That((error.Line, error.Column), Is.EqualTo((3, 1)));
        }
    }
}
