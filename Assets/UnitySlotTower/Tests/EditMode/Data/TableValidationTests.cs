using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Slot;

namespace UnitySlotTower.Tests.EditMode.Data
{
    public sealed class TableValidationTests
    {
        [TestCase("battle_a")]
        [TestCase("battle_b")]
        [TestCase("battle_c")]
        [TestCase("battle_d")]
        [TestCase("battle_e")]
        [TestCase("elite_a")]
        [TestCase("boss_a")]
        public void Later_Battles_Mix_Heavy_Into_Ground_Groups(string encounter)
        {
            var rows = CsvParser.Parse("waves.csv", TablePaths.ReadAll()["waves.csv"]).Rows;
            Assert.That(rows.Any(row => row.Get("encounter_id") == encounter && row.Get("enemy_id") == "heavy"),
                Is.True);
        }

        [Test]
        [Description("AC-056: first のIDを変えても、最初の戦闘種別には重装を出せない")]
        public void Heavy_In_First_Battle_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["encounters.csv"] = files["encounters.csv"].Replace("first,first,", "tutorial,first,");
            files["waves.csv"] = files["waves.csv"].Replace("first,", "tutorial,") +
                "tutorial,70,heavy,S2,first_g2,0,1,0\n";
            var line = files["waves.csv"].TrimEnd('\n').Split('\n').Length;
            Assert.That(GameDataValidator.ValidateAll(files).Errors.Select(e => (e.File, e.Line, e.Column)),
                Does.Contain(("waves.csv", line, 3)));
        }

        [TestCase("0")]
        [TestCase("-0.1")]
        [TestCase("1.01")]
        [TestCase("NaN")]
        [TestCase("Infinity")]
        public void Invalid_Enemy_Ranged_Damage_Rate_Is_Reported(string rate)
        {
            var files = TablePaths.ReadAll();
            files["enemies.csv"] = files["enemies.csv"].Replace(",0.4\n", "," + rate + "\n");
            Assert.That(GameDataValidator.ValidateAll(files).Errors.Any(e =>
                e.File == "enemies.csv" && e.Column == 20), Is.True);
        }

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
        [Description("FR-074: 配置中の時間の倍率は 0 より大きく 1 以下（0 だと止まり、1 より大きいと速くなる）")]
        public void Placement_Time_Scale_Outside_Zero_To_One_Fails()
        {
            var errors = new List<string>();
            foreach (var value in new[] { "0", "1.5" })
            {
                var files = TablePaths.ReadAll();
                files["game_config.csv"] = files["game_config.csv"].Replace("placement_time_scale,0.2", "placement_time_scale," + value);
                if (GameDataValidator.ValidateAll(files).Errors.Any(e => e.File == "game_config.csv"))
                {
                    errors.Add(value);
                }
            }

            Assert.That(errors, Is.EqualTo(new[] { "0", "1.5" }));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Nonpositive_Encounter_Weight_Fails_Validation(int weight)
        {
            var files = TablePaths.ReadAll();
            files[EncounterTable.FileName] = files[EncounterTable.FileName].Replace("battle_a,battle,battle_a,2,7,0,80,6,7",
                $"battle_a,battle,battle_a,2,7,0,80,6,{weight}");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => (e.File, e.Column)), Does.Contain((EncounterTable.FileName, 9)));
        }

        [Test]
        public void Ui_Texts_With_A_Duplicate_Id_Or_Empty_Text_Fail()
        {
            // 文字あり版の文言（T352）。
            var files = TablePaths.ReadAll();
            files["ui_texts.csv"] += "life,命\n";
            var duplicate = GameDataValidator.ValidateAll(files);
            files = TablePaths.ReadAll();
            files["ui_texts.csv"] += "extra_text, \n";
            var empty = GameDataValidator.ValidateAll(files);

            Assert.That((duplicate.Errors.Any(e => e.File == "ui_texts.csv"), empty.Errors.Any(e => e.File == "ui_texts.csv")),
                Is.EqualTo((true, true)), duplicate + "\n" + empty);
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
            files["maps/first.csv"] = "S1,G,Q\nX,G,E\n";

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => (e.File, e.Line, e.Column)), Does.Contain(("maps/first.csv", 1, 3)));
        }

        [Test]
        public void Wave_Spawn_Not_On_Its_Path_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["waves.csv"] += "first,0,melee,S1,first_g2,0,3,1\n";
            var line = files["waves.csv"].TrimEnd('\n').Split('\n').Length;

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => (e.File, e.Line, e.Column)), Does.Contain(("waves.csv", line, 4)));
        }

        [Test]
        [Description("FR-034, AC-031: 最初の戦闘の敵が中央のレーン以外から出るとエラー")]
        public void First_Battle_Enemy_Off_The_Center_Lane_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["waves.csv"] += "first,70,melee,S1,first_g1,0,3,1\n";
            var line = files["waves.csv"].TrimEnd('\n').Split('\n').Length;

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => (e.File, e.Line, e.Column)), Does.Contain(("waves.csv", line, 5)));
        }

        [Test]
        [Description("FR-034: 中央のレーン以外から出てよいのは最初の戦闘ではない戦闘マス")]
        public void Normal_Battle_Enemy_Off_The_Center_Lane_Is_Allowed()
        {
            var files = TablePaths.ReadAll();
            files["waves.csv"] += "boss_a,70,melee,S1,boss_a_g1,0,3,1\n";

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Where(e => e.File == "waves.csv"), Is.Empty);
        }

        [Test]
        [Description("FR-034: 群れの数は1以上")]
        public void Group_Of_Zero_Is_A_Validation_Error()
        {
            var files = TablePaths.ReadAll();
            files["waves.csv"] += "battle_a,70,melee,S1,battle_a_g1,0,0,1\n";
            var line = files["waves.csv"].TrimEnd('\n').Split('\n').Length;

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => (e.File, e.Line, e.Column)), Does.Contain(("waves.csv", line, 7)));
        }

        [Test]
        public void Map_Without_Paths_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["map_paths.csv"] = WithoutLinesStartingWith(files["map_paths.csv"], "boss_a,");
            files["waves.csv"] = WithoutLinesStartingWith(files["waves.csv"], "boss_a,");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("map_paths.csv"));
        }

        [Test]
        public void Encounter_Without_Enemies_Is_An_Error()
        {
            // 通常の戦闘マス battle_a の行だけを消す（マップと出現表の行は残る）。
            var files = TablePaths.ReadAll();
            files["waves.csv"] = WithoutLinesStartingWith(files["waves.csv"], "battle_a,");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Where(e => e.File == "waves.csv").Select(e => e.Message), Has.Some.Contains("'battle_a'"));
        }

        [Test]
        [Description("D-63: 設定が欠けた抽選表はエラー")]
        public void Missing_Slot_Setting_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files[SlotPatterns.FileName] = WithoutLinesStartingWith(files[SlotPatterns.FileName], "4,");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain(SlotPatterns.FileName));
        }

        [Test]
        [Description("D-60: 合計が20でない設定はエラー（設定1のハズレ 4 → 5 で21）")]
        public void Slot_Setting_Not_Totalling_20_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files[SlotPatterns.FileName] = files[SlotPatterns.FileName].Replace("1,miss,4", "1,miss,5");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain(SlotPatterns.FileName));
        }

        [Test]
        public void Unknown_Slot_Outcome_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files[SlotPatterns.FileName] = files[SlotPatterns.FileName].Replace("1,bar,2", "1,seven,2");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain(SlotPatterns.FileName));
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
        [Description("D-62: 3本を同じ並びにすると、中段に揃えたとき上段か下段も揃うのでエラー")]
        public void Reels_Whose_Rows_Align_Are_An_Error()
        {
            var files = TablePaths.ReadAll();
            files[SlotReels.FileName] = SlotTestRules.ReelsCsv(SlotTestRules.PlanLeft, SlotTestRules.PlanLeft,
                SlotTestRules.PlanLeft);

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain(SlotReels.FileName));
        }

        [Test]
        public void Reel_With_A_Missing_Cell_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files[SlotReels.FileName] = WithoutLinesStartingWith(files[SlotReels.FileName], "2,5,");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain(SlotReels.FileName));
        }

        [Test]
        [Description("plan 16.4: スキルの職が units.csv にないとエラー")]
        public void Skill_With_Unknown_Unit_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["skills.csv"] = files["skills.csv"].Replace("red7,sword,", "red7,healer,");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("skills.csv"));
        }

        [Test]
        [Description("plan 16.4: 色7のどれかが欠けたスキル表はエラー")]
        public void Skill_Table_Missing_A_Color_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["skills.csv"] = WithoutLinesStartingWith(files["skills.csv"], "green7,");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("skills.csv"));
        }

        [Test]
        public void Wave_Of_An_Unknown_Encounter_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["waves.csv"] += "nowhere,0,melee,S1,first_g1,0,1,0\n";
            var line = files["waves.csv"].TrimEnd('\n').Split('\n').Length;

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => (e.File, e.Line)), Does.Contain(("waves.csv", line)));
        }

        [Test]
        [Description("plan 16.6: 出現表の種類は first/battle/elite/boss のどれか")]
        public void Encounter_Unknown_Kind_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files[EncounterTable.FileName] = files[EncounterTable.FileName].Replace("battle_a,battle,", "battle_a,shop,");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => (e.File, e.Column)), Does.Contain((EncounterTable.FileName, 2)));
        }

        [Test]
        [Description("plan 16.6: 出現表が使うマップのファイルがなければエラー")]
        public void Encounter_With_A_Missing_Map_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files[EncounterTable.FileName] = files[EncounterTable.FileName].Replace("battle_a,battle,battle_a,", "battle_a,battle,s9,");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("maps/s9.csv"));
        }

        [Test]
        [Description("plan 16.6: 出現表の行に出現がなければエラー")]
        public void Encounter_Without_Waves_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files[EncounterTable.FileName] += "extra,battle,battle_a,2,7,0,10,3,1\n";

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.Message), Has.Some.Contains("'extra'"));
        }

        [Test]
        [Description("D-69: 登録表の種類の重みが route_rules にない")]
        public void Route_Rules_Missing_A_Registered_Weight_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["route_rules.csv"] = WithoutLinesStartingWith(files["route_rules.csv"], "weight_shop,");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("route_rules.csv"));
        }

        [Test]
        [Description("D-88, plan 17.7: route_rules の columns は 3 だけ")]
        public void Route_Rules_With_Columns_Other_Than_Three_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["route_rules.csv"] = WithoutLinesStartingWith(files["route_rules.csv"], "columns,") + "columns,4\n";

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("route_rules.csv"));
        }

        [Test]
        [Description("D-69: 登録表にない種類の重みが route_rules にある")]
        public void Route_Rules_With_An_Unregistered_Weight_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["route_rules.csv"] += "weight_casino,5\n";

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("route_rules.csv"));
        }

        [Test]
        public void Shop_Item_Of_An_Unknown_Unit_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["shop_items.csv"] += "stock_ghost,stock,ghost,20,1\n";

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("shop_items.csv"));
        }

        [Test]
        [Description("plan 16.2: ゲージの品の職にはスキルが要る")]
        public void Shop_Gauge_Item_Of_A_Unit_Without_A_Skill_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["skills.csv"] = files["skills.csv"].Replace("green7,buffer,", "green7,tank,");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("shop_items.csv"));
        }

        [Test]
        [Description("D-71・plan 16.6: 横9〜10×縦4〜5 を外れるマップはエラー（6行目を足す）")]
        public void Map_Outside_The_Size_Range_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["maps/first.csv"] += "H,H,H,H,H,H,H,H,H,H\n";

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => e.File), Does.Contain("maps/first.csv"));
        }

        [Test]
        [Description("plan 16.6: ルートの階（10階なら通常・強敵は8階まで）に合う戦闘マスがなければエラー")]
        public void Encounters_Not_Covering_Every_Route_Floor_Are_An_Error()
        {
            var files = TablePaths.ReadAll();
            files[RouteRules.FileName] = files[RouteRules.FileName].Replace("floors,8", "floors,10");

            var report = GameDataValidator.ValidateAll(files);

            var messages = report.Errors.Where(e => e.File == EncounterTable.FileName).Select(e => e.Message).ToList();
            Assert.That(messages, Has.Some.Contains("battle の 8 階"));
            Assert.That(messages, Has.Some.Contains("elite の 8 階"));
        }

        [Test]
        [Description("plan 16.6: 強敵の重みが正なのに強敵の戦闘マスがなければエラー")]
        public void Missing_Elite_Encounter_Is_An_Error()
        {
            var files = WithoutElite(TablePaths.ReadAll());

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Where(e => e.File == EncounterTable.FileName).Select(e => e.Message),
                Has.Some.Contains("elite"));
        }

        [Test]
        [Description("plan 16.6: 強敵の重みが0なら強敵の戦闘マスはなくてよい")]
        public void Elite_Encounter_Is_Not_Needed_When_Its_Weight_Is_0()
        {
            var files = WithoutElite(TablePaths.ReadAll());
            files[RouteRules.FileName] = files[RouteRules.FileName].Replace("weight_elite,14", "weight_elite,0");

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.IsValid, Is.True, report.ToString());
        }

        [Test]
        [Description("FR-010・plan 16.6: 強敵マスには中ボス（is_boss）が要る")]
        public void Elite_Without_A_Boss_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["waves.csv"] = string.Join("\n", files["waves.csv"].Split('\n').Where(line => !line.Contains(",mid_boss,")));

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Where(e => e.File == "waves.csv").Select(e => e.Message), Has.Some.Contains("'elite_a'"));
        }

        [Test]
        [Description("plan 16.6: ボス（is_boss）が出るのは強敵・ボスマスだけ")]
        public void Boss_In_A_Normal_Battle_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files["waves.csv"] += "battle_a,70,mid_boss,S1,battle_a_g1,1,1,0\n";
            var line = files["waves.csv"].TrimEnd('\n').Split('\n').Length;

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => (e.File, e.Line)), Does.Contain(("waves.csv", line)));
        }

        [Test]
        [Description("FR-018・AC-014: 強敵・ボスのマップは専用（最初の戦闘・通常の戦闘で使うとエラー）")]
        public void Elite_Or_Boss_Map_Used_By_A_Normal_Battle_Is_An_Error()
        {
            var files = TablePaths.ReadAll();
            files[EncounterTable.FileName] += "battle_d,battle,boss_a,2,7,0,10,3,1\n";
            var line = files[EncounterTable.FileName].TrimEnd('\n').Split('\n').Length;
            files["waves.csv"] += "battle_d,0,melee,S1,boss_a_g1,0,3,1\n";

            var report = GameDataValidator.ValidateAll(files);

            Assert.That(report.Errors.Select(e => (e.File, e.Line)), Does.Contain((EncounterTable.FileName, line)));
        }

        private static System.Collections.Generic.Dictionary<string, string> WithoutElite(
            System.Collections.Generic.Dictionary<string, string> files)
        {
            files[EncounterTable.FileName] = WithoutLinesStartingWith(files[EncounterTable.FileName], "elite_a,");
            files["waves.csv"] = WithoutLinesStartingWith(files["waves.csv"], "elite_a,");
            files["map_paths.csv"] = WithoutLinesStartingWith(files["map_paths.csv"], "elite_a,");
            return files;
        }

        private static string WithoutLinesStartingWith(string text, string prefix) =>
            string.Join("\n", text.Split('\n').Where(line => !line.StartsWith(prefix)));
    }
}
