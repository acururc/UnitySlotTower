using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Tests.EditMode.Data
{
    public sealed class TableValidationTests
    {
        private static readonly TableSchema UnitSchema = new TableSchema("units.csv", new[] { "id" },
            ColumnRule.Id("id"),
            ColumnRule.Int("cost", 0, 999),
            ColumnRule.Float("cooldown", 0, 600),
            ColumnRule.Enum("tile", "ground", "high"));

        private static ValidationReport Validate(string text)
        {
            var report = new ValidationReport();
            TableValidator.ValidateColumns(CsvParser.Parse("units.csv", text), UnitSchema, report);
            return report;
        }

        [Test]
        public void Valid_Table_Has_No_Errors()
        {
            Assert.That(Validate("id,cost,cooldown,tile\ntank,15,20,ground\n").IsValid, Is.True);
        }

        [Test]
        public void Wrong_Type_Reports_File_Line_And_Column()
        {
            var error = Validate("id,cost,cooldown,tile\ntank,15,20,ground\nsword,x,12,ground\n").Errors.Single();

            Assert.That((error.File, error.Line, error.Column), Is.EqualTo(("units.csv", 3, 2)));
        }

        [Test]
        public void Out_Of_Range_Value_Is_An_Error()
        {
            Assert.That(Validate("id,cost,cooldown,tile\ntank,-1,20,ground\n").IsValid, Is.False);
        }

        [Test]
        public void Unknown_Enum_Value_Is_An_Error()
        {
            Assert.That(Validate("id,cost,cooldown,tile\ntank,15,20,water\n").IsValid, Is.False);
        }

        [Test]
        public void Missing_Column_Is_An_Error()
        {
            Assert.That(Validate("id,cost,tile\ntank,15,ground\n").IsValid, Is.False);
        }

        [Test]
        public void Duplicate_Key_Is_An_Error()
        {
            Assert.That(Validate("id,cost,cooldown,tile\ntank,15,20,ground\ntank,12,12,ground\n").IsValid, Is.False);
        }

        [Test]
        public void Reference_To_Unknown_Id_Is_An_Error()
        {
            var units = CsvParser.Parse("units.csv", "id\ntank\n");
            var ranges = CsvParser.Parse("unit_ranges.csv", "unit_id\ntank\nbow\n");
            var report = new ValidationReport();

            TableValidator.ValidateReference(ranges, "unit_id", units, "id", report);

            Assert.That(report.Errors.Single().Line, Is.EqualTo(3));
        }

        [Test]
        public void Probability_Sum_Above_One_Is_An_Error()
        {
            var table = CsvParser.Parse("slot_probabilities.csv",
                "role,state,setting1\nbell,normal,1/2\nreplay,normal,1/1.5\nbell,kakuhen,1/2\n");
            var report = new ValidationReport();

            TableValidator.ValidateProbabilitySum(table, "state", new[] { "setting1" }, report);

            Assert.That(report.Errors.Count, Is.EqualTo(1));
        }

        [Test]
        public void Probability_Sum_Up_To_One_Is_Valid()
        {
            var table = CsvParser.Parse("slot_probabilities.csv",
                "role,state,setting1\nbell,normal,1/2\nreplay,normal,1/2\n");
            var report = new ValidationReport();

            TableValidator.ValidateProbabilitySum(table, "state", new[] { "setting1" }, report);

            Assert.That(report.IsValid, Is.True);
        }

        [Test]
        public void Path_With_A_Gap_Is_An_Error()
        {
            var table = CsvParser.Parse("map_paths.csv",
                "map_id,path_id,step,x,y\nw1,p1,1,0,2\nw1,p1,2,1,2\nw1,p1,3,3,2\n");
            var report = new ValidationReport();

            TableValidator.ValidatePathContinuity(table, report);

            Assert.That(report.Errors.Single().Line, Is.EqualTo(4));
        }

        [Test]
        public void Connected_Paths_Are_Valid_Regardless_Of_Row_Order()
        {
            var table = CsvParser.Parse("map_paths.csv",
                "map_id,path_id,step,x,y\nw1,p1,2,1,2\nw1,p2,1,5,5\nw1,p1,1,0,2\nw1,p1,3,1,3\nw1,p2,2,5,4\n");
            var report = new ValidationReport();

            TableValidator.ValidatePathContinuity(table, report);

            Assert.That(report.IsValid, Is.True);
        }

        [Test]
        public void Project_Tables_Are_All_Valid()
        {
            var report = GameDataValidator.ValidateAll(TablePaths.ReadAll());

            Assert.That(report.IsValid, Is.True, report.ToString());
        }

        [Test]
        public void Broken_Project_Table_Fails_With_Its_File_Name()
        {
            var files = TablePaths.ReadAll();
            files["game_config.csv"] = files["game_config.csv"].Replace("deploy_limit,8", "deploy_limit,eight");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("game_config.csv"));
        }

        [Test]
        public void Missing_Project_Table_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files.Remove("units.csv");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("units.csv"));
        }

        [Test]
        public void Unknown_Map_Tile_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["maps/s1.csv"] = "S1,G,Q\nX,G,E\n";

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => (e.File, e.Line, e.Column)), Does.Contain(("maps/s1.csv", 1, 3)));
        }

        [Test]
        public void Wave_Spawn_Not_On_Its_Path_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["waves.csv"] += "1,1,0,melee,S2,s1_g1,0\n";
            var line = files["waves.csv"].TrimEnd('\n').Split('\n').Length;

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => (e.File, e.Line, e.Column)), Does.Contain(("waves.csv", line, 5)));
        }

        [Test]
        public void Map_Without_Paths_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["map_paths.csv"] = WithoutLinesStartingWith(files["map_paths.csv"], "s3,");
            files["waves.csv"] = WithoutLinesStartingWith(files["waves.csv"], "3,");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("map_paths.csv"));
        }

        [Test]
        public void Wave_Without_Enemies_Is_An_Error()
        {
            // S3 の W1 だけを消す（ステージ内のウェーブごとに確かめる）。
            var files = TablePaths.ReadAll();
            files["waves.csv"] = WithoutLinesStartingWith(files["waves.csv"], "3,1,");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("waves.csv"));
        }

        [Test]
        public void Missing_Slot_Setting_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["slot_settings.csv"] = WithoutLinesStartingWith(files["slot_settings.csv"], "4,");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("slot_settings.csv"));
        }

        [Test]
        public void Miss_Rate_Above_One_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["slot_settings.csv"] = "setting,miss_rate\n1,1.2\n2,0.8\n3,0.8\n4,0.8\n5,0.8\n6,0.8\n";

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => (e.File, e.Line)), Does.Contain(("slot_settings.csv", 2)));
        }

        [Test]
        public void Unknown_Slot_Rule_Key_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["slot_rules.csv"] += "bonus_rate,2\n";

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("slot_rules.csv"));
        }

        [Test]
        public void Default_Reel_Length_Not_Matching_Reel_Length_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["slot_rules.csv"] = files["slot_rules.csv"].Replace("reel_length,12", "reel_length,13");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("slot_default_reel.csv"));
        }

        [Test]
        public void Default_Reel_With_Two_Sevens_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["slot_default_reel.csv"] = files["slot_default_reel.csv"].Replace("6,bar", "6,seven");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("slot_default_reel.csv"));
        }

        [Test]
        public void Default_Reel_With_An_Index_Gap_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["slot_default_reel.csv"] = files["slot_default_reel.csv"].Replace("11,coin", "12,coin");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("slot_default_reel.csv"));
        }

        [Test]
        public void Wave_Stage_Out_Of_Range_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["waves.csv"] += "4,1,0,melee,S1,s1_g1,0\n";
            var line = files["waves.csv"].TrimEnd('\n').Split('\n').Length;

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => (e.File, e.Line)), Does.Contain(("waves.csv", line)));
        }

        private static string WithoutLinesStartingWith(string text, string prefix) =>
            string.Join("\n", text.Split('\n').Where(line => !line.StartsWith(prefix)));
    }
}
