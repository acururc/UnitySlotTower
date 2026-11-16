using System.Collections.Generic;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Ui;

namespace UnitySlotTower.Core.Data
{
    /// <summary>
    /// 全データテーブル（plan 6章）の型・範囲・ID参照・確率合計・経路の連続性を検証する。
    /// files のキーは Data/Tables からの相対パス（区切りは '/'）。
    /// </summary>
    public static class GameDataValidator
    {
        private static readonly ColumnRule[] GameConfigKeys =
        {
            ColumnRule.Int("initial_coins", 0, 100000),
            ColumnRule.Int("initial_money", 0, 100000),
            ColumnRule.Int("bet", 1, 100),
            ColumnRule.Int("life", 1, 1000),
            ColumnRule.Int("deploy_limit", 1, 100),
            ColumnRule.Float("retreat_refund_rate", 0, 1),
            ColumnRule.Float("intro_seconds", 0, 10),
        };

        private static readonly ColumnRule[] SlotRuleKeys =
        {
            ColumnRule.Float("first_stop_seconds", 0, 60),
            ColumnRule.Float("stop_interval_seconds", 0, 60),
            ColumnRule.Int("gauge_max", 1, 100),
            ColumnRule.Float("bar_step", 0, 10),
            ColumnRule.Float("bar_cap", 0, 10),
            ColumnRule.Float("coin_step", 0, 10),
            ColumnRule.Float("coin_cap", 1, 10),
        };

        // Schemas より前に置く（静的フィールドは書いた順に初期化される）。
        private static readonly string[] OutcomeIds =
            { "red7", "blue7", "green7", "yellow7", "bar", "coin", SlotSymbols.MissId };

        private static readonly TableSchema[] Schemas =
        {
            new TableSchema("units.csv", new[] { "id" },
                ColumnRule.Id("id"),
                ColumnRule.Int("max_hp", 1, 1000000),
                ColumnRule.Int("atk", 0, 1000000),
                ColumnRule.Int("def", 0, 1000000),
                ColumnRule.Float("attack_interval", 0.01, 60),
                ColumnRule.Int("block", 0, 10),
                ColumnRule.Enum("tile", "ground", "high"),
                ColumnRule.Int("cost", 0, 10000),
                ColumnRule.Float("cooldown", 0, 600),
                ColumnRule.Enum("role", "melee", "ranged", "buff"),
                ColumnRule.Float("buff_rate", 1, 10)),
            new TableSchema("unit_ranges.csv", new[] { "unit_id", "dx", "dy" },
                ColumnRule.Id("unit_id"),
                ColumnRule.Int("dx", -9, 9),
                ColumnRule.Int("dy", -9, 9)),
            new TableSchema("unit_upgrades.csv", new[] { "unit_id", "level" },
                ColumnRule.Id("unit_id"),
                ColumnRule.Int("level", 1, 5),
                ColumnRule.Float("hp_mult", 0, 100),
                ColumnRule.Float("atk_mult", 0, 100),
                ColumnRule.Float("def_mult", 0, 100),
                ColumnRule.Int("cost", 0, 100000)),
            new TableSchema("enemies.csv", new[] { "id" },
                ColumnRule.Id("id"),
                ColumnRule.Int("max_hp", 1, 10000000),
                ColumnRule.Int("atk", 0, 1000000),
                ColumnRule.Int("def", 0, 1000000),
                ColumnRule.Float("attack_interval", 0.01, 60),
                ColumnRule.Float("move_speed", 0, 100),
                ColumnRule.Float("range", 0, 20),
                ColumnRule.Bool("is_air"),
                ColumnRule.Int("block_cost", 1, 10),
                ColumnRule.Int("life_damage", 0, 1000),
                ColumnRule.Probability("coin_rate"),
                ColumnRule.Int("coin_min", 0, 10000),
                ColumnRule.Int("coin_max", 0, 10000),
                ColumnRule.Probability("money_rate"),
                ColumnRule.Int("money_min", 0, 10000),
                ColumnRule.Int("money_max", 0, 10000),
                ColumnRule.Int("area_atk", 0, 1000000),
                ColumnRule.Float("area_radius", 0, 20),
                ColumnRule.Float("area_interval", 0, 60)),
            new TableSchema("map_paths.csv", new[] { "map_id", "path_id", "step" },
                ColumnRule.Id("map_id"),
                ColumnRule.Id("path_id"),
                ColumnRule.Bool("is_air"),
                ColumnRule.Int("step", 1, 1000),
                ColumnRule.Int("x", 0, 99),
                ColumnRule.Int("y", 0, 99)),
            new TableSchema(EncounterTable.FileName, new[] { "id" },
                ColumnRule.Id("id"),
                ColumnRule.Enum("kind", EncounterTable.KindIds),
                ColumnRule.Id("map_id"),
                ColumnRule.Int("min_floor", 1, 99),
                ColumnRule.Int("max_floor", 1, 99),
                ColumnRule.Int("fixed_spins", 0, 100),
                ColumnRule.Int("reward_money", 0, 100000),
                ColumnRule.Int("reward_coins", 0, 100000)),
            new TableSchema("waves.csv", null,
                ColumnRule.Id("encounter_id"),
                ColumnRule.Float("time", 0, 3600),
                ColumnRule.Id("enemy_id"),
                ColumnRule.Id("spawn"),
                ColumnRule.Id("path_id"),
                ColumnRule.Bool("is_boss"),
                ColumnRule.Int(WaveSchedule.CountColumn, 1, 50),
                ColumnRule.Float(WaveSchedule.IntervalColumn, 0, 60)),
            new TableSchema(SlotPatterns.FileName, new[] { "setting", "outcome" },
                ColumnRule.Int("setting", 1, SlotPatterns.SettingCount),
                ColumnRule.Enum("outcome", OutcomeIds),
                ColumnRule.Int("count", 0, SlotPatterns.PatternCount)),
            new TableSchema(SlotReels.FileName, new[] { "reel", "index" },
                ColumnRule.Int("reel", 0, SlotReels.ReelCount - 1),
                ColumnRule.Int("index", 0, SlotReels.ReelLength - 1),
                ColumnRule.Enum("symbol", SlotSymbols.Ids)),
            new TableSchema(SkillTable.FileName, new[] { "color" },
                ColumnRule.Enum("color", SkillTable.ColorIds),
                ColumnRule.Id("unit_id"),
                ColumnRule.Float("duration", 0, 60),
                ColumnRule.Int("block_add", 0, 10),
                ColumnRule.Float("damage_taken_rate", 0.05, 1),
                ColumnRule.Float("damage_rate", 0, 10),
                ColumnRule.Float("interval_rate", 0.1, 1),
                ColumnRule.Float("buff_add", 0, 5)),
            new TableSchema(ShopTable.FileName, new[] { "id" },
                ColumnRule.Id("id"),
                ColumnRule.Enum("kind", ShopTable.KindIds),
                ColumnRule.Id("unit_id"),
                ColumnRule.Int("price", 0, 100000),
                ColumnRule.Int("weight", 0, 10000)),
            new TableSchema(UiTexts.FileName, new[] { "id" },
                ColumnRule.Id("id"),
                ColumnRule.Text("ja")),
        };

        /// <summary>マップの大きさの範囲（D-71、plan 16.6）。</summary>
        public const int MinMapWidth = 9;
        public const int MaxMapWidth = 10;
        public const int MinMapHeight = 4;
        public const int MaxMapHeight = 5;

        private static readonly HashSet<string> MapTiles = new HashSet<string>
            { "G", "H", "X", "E", "S1", "S2", "S3", "S4" };

        public static ValidationReport ValidateAll(IReadOnlyDictionary<string, string> files)
        {
            var report = new ValidationReport();
            var tables = new Dictionary<string, CsvTable>();

            ValidateKeyValueFile(files, GameConfig.FileName, GameConfigKeys, report);
            ValidateKeyValueFile(files, SlotRules.FileName, SlotRuleKeys, report);
            var routeTable = ValidateKeyValueFile(files, RouteRules.FileName, RouteRuleKeys(), report);

            foreach (var schema in Schemas)
            {
                var table = Parse(files, schema.FileName, report);
                if (table != null)
                {
                    tables.Add(schema.FileName, table);
                    TableValidator.ValidateColumns(table, schema, report);
                }
            }

            // 検証するマップは出現表（encounters.csv）が使うもの（plan 16.6）。
            var maps = new List<string>();
            if (tables.TryGetValue(EncounterTable.FileName, out var encounterRows))
            {
                foreach (var row in encounterRows.Rows)
                {
                    var name = row.Get("map_id");
                    if (!maps.Contains(name) && ValidateMap(files, BattleMap.MapFileName(name), report))
                    {
                        maps.Add(name);
                    }
                }
            }

            if (tables.Count != Schemas.Length)
            {
                return report;
            }

            TableValidator.ValidateReference(tables["unit_ranges.csv"], "unit_id", tables["units.csv"], "id", report);
            TableValidator.ValidateReference(tables["unit_upgrades.csv"], "unit_id", tables["units.csv"], "id", report);
            TableValidator.ValidateReference(tables["waves.csv"], "encounter_id", tables[EncounterTable.FileName], "id",
                report);
            TableValidator.ValidateReference(tables["waves.csv"], "enemy_id", tables["enemies.csv"], "id", report);
            TableValidator.ValidateReference(tables["waves.csv"], "path_id", tables["map_paths.csv"], "path_id", report);
            ValidateMapIds(tables["map_paths.csv"], maps, report);

            TableValidator.ValidatePathContinuity(tables["map_paths.csv"], report);
            var battleMaps = ValidateMapPaths(files, tables["map_paths.csv"], maps, report);
            ValidateWaves(tables["waves.csv"], tables[EncounterTable.FileName], tables["enemies.csv"], battleMaps, report);
            ValidateEncounters(tables[EncounterTable.FileName], tables["waves.csv"], routeTable, report);
            ValidateSlotPatterns(tables[SlotPatterns.FileName], report);
            ValidateSlotReels(tables[SlotReels.FileName], report);
            TableValidator.ValidateReference(tables[SkillTable.FileName], "unit_id", tables["units.csv"], "id", report);
            ValidateSkills(tables[SkillTable.FileName], report);
            TableValidator.ValidateReference(tables[ShopTable.FileName], "unit_id", tables["units.csv"], "id", report);
            ValidateShopGauges(tables[ShopTable.FileName], tables[SkillTable.FileName], report);
            ValidateUiTexts(tables[UiTexts.FileName], report);
            return report;
        }

        /// <summary>
        /// route_rules.csv のキー（plan 16.7）。重みのキーはマスの種類の登録表から作るので、
        /// 登録にある種類の重みがない・登録にない種類の重みがある、のどちらもエラーになる（D-69）。
        /// </summary>
        private static List<ColumnRule> RouteRuleKeys()
        {
            var keys = new List<ColumnRule>
            {
                ColumnRule.Int("floors", RouteRules.MinFloors, 30),
                ColumnRule.Int("columns", 1, 9),
                ColumnRule.Int("paths", 1, 10),
                ColumnRule.Int("elite_min_floor", 1, 99),
                ColumnRule.Int("rest_heal", 0, 1000),
                ColumnRule.Int("shop_slots", 0, 10),
                ColumnRule.Int("exchange_money_per_coin", 1, 10000),
            };
            foreach (var kind in RouteNodeKinds.All)
            {
                if (kind.IsWeighted)
                {
                    keys.Add(ColumnRule.Int(kind.WeightKey, 0, 10000));
                }
            }

            return keys;
        }

        /// <summary>文字あり版の文言が空でないことを確かめる（plan 16.9）。</summary>
        private static void ValidateUiTexts(CsvTable table, ValidationReport report)
        {
            try
            {
                UiTexts.Load(table);
            }
            catch (CsvFormatException ex)
            {
                report.Add(ex.Error);
            }
        }

        /// <summary>ゲージの品の職にスキル（skills.csv の unit_id）があることを確かめる（plan 16.2）。</summary>
        private static void ValidateShopGauges(CsvTable shop, CsvTable skills, ValidationReport report)
        {
            var skilled = new HashSet<string>();
            foreach (var row in skills.Rows)
            {
                skilled.Add(row.Get("unit_id"));
            }

            var gauge = ShopTable.KindIds[(int)ShopItemKind.Gauge];
            foreach (var row in shop.Rows)
            {
                if (row.Get("kind") == gauge && !skilled.Contains(row.Get("unit_id")))
                {
                    report.Add(row.Error("unit_id", $"ゲージの品の職 '{row.Get("unit_id")}' にスキルがない").Error);
                }
            }
        }

        /// <summary>key,value 表を検証し、エラーがなければ表を返す（なければ null）。</summary>
        private static CsvTable ValidateKeyValueFile(IReadOnlyDictionary<string, string> files, string fileName,
            IReadOnlyList<ColumnRule> keys, ValidationReport report)
        {
            var table = Parse(files, fileName, report);
            if (table == null)
            {
                return null;
            }

            var before = report.Errors.Count;
            TableValidator.ValidateKeyValues(table, keys, report);
            return report.Errors.Count == before ? table : null;
        }

        /// <summary>色7の4色がそろっていることを <see cref="SkillTable"/> で確かめる（plan 16.4）。</summary>
        private static void ValidateSkills(CsvTable table, ValidationReport report)
        {
            try
            {
                SkillTable.Load(table);
            }
            catch (CsvFormatException ex)
            {
                report.Add(ex.Error);
            }
        }

        /// <summary>設定1〜6がそろい、各設定の合計が20であることを <see cref="SlotPatterns"/> で確かめる（D-60）。</summary>
        private static void ValidateSlotPatterns(CsvTable table, ValidationReport report)
        {
            try
            {
                SlotPatterns.Load(table);
            }
            catch (CsvFormatException ex)
            {
                report.Add(ex.Error);
            }
        }

        /// <summary>3本×6マスがそろい、並びの決まり（<see cref="SlotReels.Problems"/>）を満たすことを確かめる（D-62）。</summary>
        private static void ValidateSlotReels(CsvTable table, ValidationReport report)
        {
            SlotReels reels;
            try
            {
                reels = SlotReels.Load(table);
            }
            catch (CsvFormatException ex)
            {
                report.Add(ex.Error);
                return;
            }

            foreach (var problem in reels.Problems())
            {
                report.Add(new CsvError(table.FileName, 0, 0, problem));
            }
        }

        private static CsvTable Parse(IReadOnlyDictionary<string, string> files, string fileName, ValidationReport report)
        {
            if (!files.TryGetValue(fileName, out var text))
            {
                report.Add(new CsvError(fileName, 0, 0, "ファイルがない"));
                return null;
            }

            try
            {
                return CsvParser.Parse(fileName, text);
            }
            catch (CsvFormatException ex)
            {
                report.Add(ex.Error);
                return null;
            }
        }

        /// <summary>マップは見出しなしのグリッド。長方形で、既知のマス記号だけを使う。</summary>
        private static bool ValidateMap(IReadOnlyDictionary<string, string> files, string fileName, ValidationReport report)
        {
            if (!files.TryGetValue(fileName, out var text))
            {
                report.Add(new CsvError(fileName, 0, 0, "ファイルがない"));
                return false;
            }

            List<CsvRecord> records;
            try
            {
                records = CsvParser.ParseRecords(fileName, text);
            }
            catch (CsvFormatException ex)
            {
                report.Add(ex.Error);
                return false;
            }

            if (records.Count == 0)
            {
                report.Add(new CsvError(fileName, 0, 0, "マスがない"));
                return false;
            }

            var width = records[0].Cells.Count;
            foreach (var record in records)
            {
                if (record.Cells.Count != width)
                {
                    report.Add(new CsvError(fileName, record.Line, 0, $"列数 {record.Cells.Count} が 1 行目の {width} と違う"));
                }

                for (var column = 0; column < record.Cells.Count; column++)
                {
                    if (!MapTiles.Contains(record.Cells[column]))
                    {
                        report.Add(new CsvError(fileName, record.Line, column + 1, $"未知のマス '{record.Cells[column]}'"));
                    }
                }
            }

            if (width < MinMapWidth || width > MaxMapWidth || records.Count < MinMapHeight || records.Count > MaxMapHeight)
            {
                report.Add(new CsvError(fileName, 0, 0,
                    $"大きさ {width}×{records.Count} が 横{MinMapWidth}〜{MaxMapWidth}×縦{MinMapHeight}〜{MaxMapHeight} の外"));
            }

            return true;
        }

        /// <summary>
        /// 出現表の組み合わせ（plan 16.6）: 強敵・ボスのマップは専用、ボス（is_boss）は強敵・ボスマスの最後のウェーブだけ、
        /// ルートの各階に置かれうる戦闘マスの種類に合う行がある。
        /// </summary>
        private static void ValidateEncounters(CsvTable encounterTable, CsvTable waves, CsvTable routeTable,
            ValidationReport report)
        {
            EncounterTable encounters;
            try
            {
                encounters = EncounterTable.Load(encounterTable);
            }
            catch (CsvFormatException)
            {
                return; // ValidateWaves で報告済み。
            }

            ValidateDedicatedMaps(encounterTable, encounters, report);
            ValidateBosses(waves, encounters, report);
            if (routeTable == null)
            {
                return;
            }

            RouteRules rules;
            try
            {
                rules = RouteRules.Load(routeTable);
            }
            catch (CsvFormatException ex)
            {
                report.Add(ex.Error);
                return;
            }

            ValidateFloorCoverage(encounters, rules, report);
        }

        private static bool IsBossKind(EncounterKind kind) => kind == EncounterKind.Elite || kind == EncounterKind.Boss;

        /// <summary>強敵・ボスのマップを最初の戦闘・通常の戦闘で使っていればエラー（FR-018）。</summary>
        private static void ValidateDedicatedMaps(CsvTable table, EncounterTable encounters, ValidationReport report)
        {
            var dedicated = new HashSet<string>();
            foreach (var encounter in encounters.All)
            {
                if (IsBossKind(encounter.Kind))
                {
                    dedicated.Add(encounter.MapId);
                }
            }

            foreach (var row in table.Rows)
            {
                var encounter = encounters.Get(row.Get("id"));
                if (!IsBossKind(encounter.Kind) && dedicated.Contains(encounter.MapId))
                {
                    report.Add(row.Error("map_id", $"マップ '{encounter.MapId}' は強敵・ボスの専用").Error);
                }
            }
        }

        /// <summary>ボス（is_boss）は強敵・ボスマスの最後のウェーブにだけ出て、強敵・ボスマスには必ず出る（FR-010）。</summary>
        private static void ValidateBosses(CsvTable waves, EncounterTable encounters, ValidationReport report)
        {
            var withBoss = new HashSet<string>();
            foreach (var row in waves.Rows)
            {
                var id = row.Get("encounter_id");
                if (!encounters.Contains(id))
                {
                    continue; // 参照の検証で報告済み。
                }

                bool isBoss;
                try
                {
                    isBoss = row.GetBool("is_boss");
                }
                catch (CsvFormatException)
                {
                    continue; // 列の検証で報告済み。
                }

                if (!isBoss)
                {
                    continue;
                }

                if (IsBossKind(encounters.Get(id).Kind))
                {
                    withBoss.Add(id);
                }
                else
                {
                    report.Add(row.Error("is_boss", $"ボスが出られるのは強敵・ボスマスだけ（'{id}'）").Error);
                }
            }

            foreach (var encounter in encounters.All)
            {
                if (IsBossKind(encounter.Kind) && !withBoss.Contains(encounter.Id))
                {
                    report.Add(new CsvError(waves.FileName, 0, 0,
                        $"戦闘マス '{encounter.Id}' にボス（is_boss）がいない"));
                }
            }
        }

        /// <summary>
        /// ルートの生成（plan 16.7）が置きうる戦闘マスの種類と階のすべてに、出現表の行があることを確かめる。
        /// 1階は first、最上階は boss、間の階は通常（重みが全部0のときの代わりでもある）と、重みが正の戦闘マスの種類
        /// （強敵は elite_min_floor 以上）。
        /// </summary>
        private static void ValidateFloorCoverage(EncounterTable encounters, RouteRules rules, ValidationReport report)
        {
            RequireEncounter(encounters, EncounterKind.First, 1, report);
            RequireEncounter(encounters, EncounterKind.Boss, rules.Floors, report);
            var restFloor = rules.Floors - 1;
            for (var floor = 2; floor < restFloor; floor++)
            {
                foreach (var kind in RouteNodeKinds.All)
                {
                    if (!kind.IsWeighted || !kind.Encounter.HasValue)
                    {
                        continue;
                    }

                    var placed = kind.Id == RouteNodeKinds.Battle || rules.WeightOf(kind.Id) > 0;
                    if (placed && (kind.Id != RouteNodeKinds.Elite || floor >= rules.EliteMinFloor))
                    {
                        RequireEncounter(encounters, kind.Encounter.Value, floor, report);
                    }
                }
            }
        }

        private static void RequireEncounter(EncounterTable encounters, EncounterKind kind, int floor,
            ValidationReport report)
        {
            foreach (var encounter in encounters.All)
            {
                if (encounter.Kind == kind && encounter.IsOnFloor(floor))
                {
                    return;
                }
            }

            report.Add(new CsvError(EncounterTable.FileName, 0, 0,
                $"{EncounterTable.KindIds[(int)kind]} の {floor} 階に合う戦闘マスがない（{RouteRules.FileName} のルートで置かれうる）"));
        }

        private static void ValidateMapIds(CsvTable paths, List<string> maps, ValidationReport report)
        {
            foreach (var row in paths.Rows)
            {
                if (!maps.Contains(row.Get("map_id")))
                {
                    report.Add(row.Error("map_id", $"マップ 'maps/{row.Get("map_id")}.csv' がない").Error);
                }
            }
        }

        /// <summary>
        /// 各マップの経路の有無・範囲・通行可否・出現口からゴールまでを <see cref="BattleMap"/> で確かめる。
        /// 全ウェーブがマップを使うので、経路のないマップもエラーにする。
        /// </summary>
        private static Dictionary<string, BattleMap> ValidateMapPaths(IReadOnlyDictionary<string, string> files,
            CsvTable paths, List<string> maps, ValidationReport report)
        {
            var loaded = new Dictionary<string, BattleMap>();
            foreach (var map in maps)
            {
                try
                {
                    loaded.Add(map, BattleMap.Load(map, files[BattleMap.MapFileName(map)], paths));
                }
                catch (CsvFormatException ex)
                {
                    report.Add(ex.Error);
                }
            }

            return loaded;
        }

        /// <summary>
        /// 各戦闘マス（encounters.csv）の行が、そのマップの経路・出現口・空中フラグと合うかを <see cref="WaveSchedule"/> で確かめる。
        /// 敵のいない戦闘マスもエラーにする。
        /// </summary>
        private static void ValidateWaves(CsvTable waves, CsvTable encounterTable, CsvTable enemyTable,
            Dictionary<string, BattleMap> maps, ValidationReport report)
        {
            EnemyTable enemies;
            EncounterTable encounters;
            try
            {
                enemies = EnemyTable.Load(enemyTable);
                encounters = EncounterTable.Load(encounterTable);
            }
            catch (CsvFormatException ex)
            {
                report.Add(ex.Error);
                return;
            }

            foreach (var encounter in encounters.All)
            {
                if (!maps.TryGetValue(encounter.MapId, out var map))
                {
                    continue;
                }

                try
                {
                    if (WaveSchedule.Load(waves, encounter.Id, map, enemies).Spawns.Count == 0)
                    {
                        report.Add(new CsvError(waves.FileName, 0, 0, $"戦闘マス '{encounter.Id}' の敵がない"));
                    }
                }
                catch (CsvFormatException ex)
                {
                    report.Add(ex.Error);
                }

                if (encounter.Kind == EncounterKind.First)
                {
                    ValidateCenterLane(waves, encounter.Id, map, report);
                }
            }
        }

        /// <summary>最初の戦闘の敵は中央のレーン（出現口がマップの中央の行にある道）からだけ出る（FR-034, D-80）。</summary>
        private static void ValidateCenterLane(CsvTable waves, string encounterId, BattleMap map, ValidationReport report)
        {
            foreach (var row in waves.Rows)
            {
                if (row.Get("encounter_id") != encounterId)
                {
                    continue;
                }

                var path = map.FindPath(row.Get("path_id"));
                if (path != null && path.Cells[0].Y * 2 != map.Height - 1)
                {
                    report.Add(row.Error("path_id",
                        $"最初の戦闘の敵は中央のレーンから出る: 経路 '{path.Id}' の出現口は中央の行（y={(map.Height - 1) / 2.0}）にない").Error);
                }
            }
        }
    }
}
