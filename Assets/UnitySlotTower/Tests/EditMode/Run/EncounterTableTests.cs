using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Run
{
    [Description("FR-018: 戦闘マスの出現表（種類と階で選ぶ）。AC-014 の選び方")]
    public sealed class EncounterTableTests
    {
        private const string Header = "id,kind,map_id,min_floor,max_floor,fixed_spins,reward_money,reward_coins\n";

        private const string Rows =
            "first_a,first,first,1,1,6,10,3\n" +
            "battle_low,battle,m1,2,4,0,10,3\n" +
            "battle_any,battle,m2,2,8,0,10,3\n" +
            "battle_high,battle,m3,5,8,0,12,3\n" +
            "elite_a,elite,elite,3,8,0,25,9\n" +
            "boss_a,boss,boss,1,99,0,0,0\n";

        private static EncounterTable Load(string rows = Rows) =>
            EncounterTable.Load(CsvParser.Parse(EncounterTable.FileName, Header + rows));

        private static CsvError LoadError(string rows) =>
            Assert.Throws<CsvFormatException>(() => Load(rows)).Error;

        [Test]
        public void Row_Is_Read_Into_An_Encounter()
        {
            var e = Load().Get("first_a");

            Assert.That((e.Kind, e.MapId, e.MinFloor, e.MaxFloor, e.FixedSpins, e.RewardMoney, e.RewardCoins),
                Is.EqualTo((EncounterKind.First, "first", 1, 1, 6, 10, 3)));
        }

        [Test]
        [Description("AC-014: 種類と階が合う行だけから選ぶ")]
        public void Pick_Uses_Only_Rows_Of_The_Kind_And_Floor()
        {
            var table = Load();
            var random = new SeededRandom(7);
            var picked = new HashSet<string>();

            for (var i = 0; i < 200; i++)
            {
                picked.Add(table.Pick(EncounterKind.Battle, 3, random).Id);
            }

            Assert.That(picked, Is.EquivalentTo(new[] { "battle_low", "battle_any" }));
        }

        [Test]
        [Description("AC-014: 合う行から一様に選ぶ（固定シード 3000 回で各 1/3 ± 5%）")]
        public void Pick_Is_Uniform_Among_Matches()
        {
            var table = Load();
            var random = new SeededRandom(11);
            var counts = new Dictionary<string, int>();

            for (var i = 0; i < 3000; i++)
            {
                var id = table.Pick(EncounterKind.Battle, 6, random).Id;
                counts[id] = counts.TryGetValue(id, out var n) ? n + 1 : 1;
            }

            Assert.That(counts.Keys, Is.EquivalentTo(new[] { "battle_any", "battle_high" }));
            Assert.That(counts.Values.All(n => n > 1500 - 150 && n < 1500 + 150), Is.True,
                string.Join(",", counts.Select(p => $"{p.Key}={p.Value}")));
        }

        [Test]
        [Description("AC-014: 最初・強敵・ボスはそれぞれ専用の行から選ぶ")]
        public void First_Elite_And_Boss_Use_Their_Own_Rows()
        {
            var table = Load();
            var random = new SeededRandom(1);

            Assert.That((table.Pick(EncounterKind.First, 1, random).MapId, table.Pick(EncounterKind.Elite, 4, random).MapId,
                table.Pick(EncounterKind.Boss, 10, random).MapId), Is.EqualTo(("first", "elite", "boss")));
        }

        [Test]
        public void Pick_Without_A_Match_Throws()
        {
            var table = Load();

            Assert.Throws<System.InvalidOperationException>(() => table.Pick(EncounterKind.Elite, 2, new SeededRandom(1)));
        }

        [Test]
        public void Unknown_Kind_Is_An_Error_At_Its_Cell()
        {
            var error = LoadError("x,shop,m1,1,1,0,0,0\n");

            Assert.That((error.Line, error.Column), Is.EqualTo((2, 2)));
        }

        [Test]
        public void Duplicate_Id_Is_An_Error()
        {
            var error = LoadError("a,battle,m1,1,2,0,0,0\na,battle,m2,1,2,0,0,0\n");

            Assert.That((error.Line, error.Column), Is.EqualTo((3, 1)));
        }

        [Test]
        public void Max_Floor_Below_Min_Floor_Is_An_Error()
        {
            var error = LoadError("a,battle,m1,4,3,0,0,0\n");

            Assert.That((error.Line, error.Column), Is.EqualTo((2, 5)));
        }

        [Test]
        public void Negative_Reward_Is_An_Error()
        {
            var error = LoadError("a,battle,m1,1,3,0,-1,0\n");

            Assert.That((error.Line, error.Column), Is.EqualTo((2, 7)));
        }

        [Test]
        [Description("FR-018: プロジェクトの出現表は読めて、最初の戦闘は first・回数固定あり")]
        public void Project_Table_Loads_With_A_First_Encounter()
        {
            var tables = TablePaths.ReadAll();
            var table = EncounterTable.Load(CsvParser.Parse(EncounterTable.FileName, tables[EncounterTable.FileName]));

            var first = table.Pick(EncounterKind.First, 1, new SeededRandom(1));

            Assert.That(first.FixedSpins, Is.GreaterThan(0));
        }
    }
}
