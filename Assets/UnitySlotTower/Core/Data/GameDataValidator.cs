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
            ColumnRule.Float("placement_time_scale", 0.01, 1),
            ColumnRule.Float("wave_gap_seconds", 0, 10),
            ColumnRule.Int("first_stage_free_summons", 0, 10),
        };

        private static readonly ColumnRule[] SlotRuleKeys =
        {
            ColumnRule.Float("first_stop_seconds", 0, 60),
            ColumnRule.Float("stop_interval_seconds", 0, 60),
            ColumnRule.Float("red_hp_rate", 0, 10),
            ColumnRule.Float("red_delay_seconds", 0, 10),
            ColumnRule.Float("blue_stop_seconds", 0, 600),
            ColumnRule.Float("blue_delay_seconds", 0, 10),
            ColumnRule.Float("green_heal_rate", 0, 10),
            ColumnRule.Float("green_delay_seconds", 0, 10),
            ColumnRule.Float("yellow_delay_seconds", 0, 10),
            ColumnRule.Float("bar_delay_seconds", 0, 10),
            ColumnRule.Float("coin_delay_seconds", 0, 10),
            ColumnRule.Float("yellow_hp_rate", 0, 10),
            ColumnRule.Float("yellow_poison_tick_seconds", 0.01, 60),
            ColumnRule.Float("yellow_poison_seconds", 0, 600),
            ColumnRule.Float("bar_attack_step", 0, 10),
            ColumnRule.Float("bar_seconds", 0, 600),
            ColumnRule.Float("coin_step", 0, 10),
            ColumnRule.Float("coin_cap", 1, 10),
            ColumnRule.Float("boss_damage_rate", 0, 10),
            ColumnRule.Float("line_hit_rate", 0, 1),
            ColumnRule.Float("line_gap_seconds", 0, 10),
            ColumnRule.Int("showcase_coins", 1, 1000),
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
            new TableSchema(StageTable.FileName, new[] { "stage" },
                ColumnRule.Int("stage", 1, 99),
                ColumnRule.Id("map_id"),
                ColumnRule.Enum("wave_mode", StageTable.ModeIds),
                ColumnRule.Id("boss"),
                ColumnRule.Float(StageTable.EnemyHpRateColumn, 0.1, 100)),
            new TableSchema(WaveSchedule.StageWavesFileName, new[] { "stage", WaveSchedule.WaveColumn },
                ColumnRule.Int("stage", 1, 99),
                ColumnRule.Int(WaveSchedule.WaveColumn, 1, 99),
                ColumnRule.Float("start_after", 0, 3600),
                ColumnRule.Bool(WaveSchedule.SlotShowcaseColumn)),
            new TableSchema("waves.csv", null,
                ColumnRule.Int("stage", 1, 99),
                ColumnRule.Int(WaveSchedule.WaveColumn, 1, 99),
                ColumnRule.Float("time", 0, 3600),
                ColumnRule.Id("enemy_id"),
                ColumnRule.Id("spawn"),
                ColumnRule.Id("path_id"),
                ColumnRule.Bool("is_boss"),
                ColumnRule.Int(WaveSchedule.CountColumn, 1, 50),
                ColumnRule.Float(WaveSchedule.IntervalColumn, 0, 60)),
            new TableSchema(SlotPatterns.FileName, new[] { "outcome" },
                ColumnRule.Enum("outcome", OutcomeIds),
                ColumnRule.Int("count", 0, SlotPatterns.PatternCount)),
            new TableSchema(SlotReels.FileName, new[] { "reel", "index" },
                ColumnRule.Int("reel", 0, SlotReels.ReelCount - 1),
                ColumnRule.Int("index", 0, SlotReels.ReelLength - 1),
                ColumnRule.Enum("symbol", SlotSymbols.Ids)),
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

            foreach (var schema in Schemas)
            {
                var table = Parse(files, schema.FileName, report);
                if (table != null)
                {
                    tables.Add(schema.FileName, table);
                    TableValidator.ValidateColumns(table, schema, report);
                }
            }

            // 検証するマップはステージ（stages.csv）が使うもの（plan 16.6、002 plan 2.1）。
            var maps = new List<string>();
            if (tables.TryGetValue(StageTable.FileName, out var mapRows))
            {
                foreach (var row in mapRows.Rows)
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
            TableValidator.ValidateReference(tables["waves.csv"], "stage", tables[StageTable.FileName], "stage", report);
            TableValidator.ValidateReference(tables[WaveSchedule.StageWavesFileName], "stage", tables[StageTable.FileName],
                "stage", report);
            TableValidator.ValidateReference(tables["waves.csv"], "enemy_id", tables["enemies.csv"], "id", report);
            TableValidator.ValidateReference(tables["waves.csv"], "path_id", tables["map_paths.csv"], "path_id", report);
            ValidateMapIds(tables["map_paths.csv"], maps, report);

            TableValidator.ValidatePathContinuity(tables["map_paths.csv"], report);
            var battleMaps = ValidateMapPaths(files, tables["map_paths.csv"], maps, report);
            ValidateStages(tables[StageTable.FileName], tables["waves.csv"], tables[WaveSchedule.StageWavesFileName],
                tables["enemies.csv"], battleMaps, report);
            ValidateSlotPatterns(tables[SlotPatterns.FileName], report);
            ValidateSlotReels(tables[SlotReels.FileName], report);
            ValidateUiTexts(tables[UiTexts.FileName], report);
            return report;
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

        /// <summary>出目の個数の合計が20であることを <see cref="SlotPatterns"/> で確かめる（D-60）。</summary>
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
        /// ステージとウェーブ（FR-102〜FR-104、002 plan 2.1）: ステージ番号は1から連番、各ステージにマップとウェーブがあり、
        /// ウェーブは1から連番で敵がいる、ウェーブ1の start_after は0、timed では次のウェーブが前のウェーブを出し終わってから来る、
        /// ボスは boss 列のステージの最後のウェーブにだけ出る。各行がマップの経路・出現口・空中フラグと合うかは <see cref="WaveSchedule"/> で確かめる。
        /// </summary>
        private static void ValidateStages(CsvTable stageTable, CsvTable waves, CsvTable stageWaves, CsvTable enemyTable,
            Dictionary<string, BattleMap> maps, ValidationReport report)
        {
            EnemyTable enemies;
            StageTable stages;
            try
            {
                enemies = EnemyTable.Load(enemyTable);
                stages = StageTable.Load(stageTable);
            }
            catch (CsvFormatException ex)
            {
                report.Add(ex.Error);
                return;
            }

            ValidateStageNumbers(stageTable, stages, report);
            for (var i = 0; i < stages.Count; i++)
            {
                var stage = stages.Stages[i];
                var stageRow = stageTable.Rows[i];
                if (stage.BossId != null && !enemies.Contains(stage.BossId))
                {
                    report.Add(stageRow.Error("boss", $"敵 '{stage.BossId}' がない").Error);
                }

                if (!maps.TryGetValue(stage.MapId, out var map))
                {
                    continue; // マップの検証で報告済み。
                }

                WaveSchedule schedule;
                try
                {
                    schedule = WaveSchedule.Load(waves, stage.Number, map, enemies, stageWaves);
                }
                catch (CsvFormatException ex)
                {
                    report.Add(ex.Error);
                    continue;
                }

                if (schedule.Waves.Count == 0)
                {
                    report.Add(stageRow.Error("stage", $"ステージ {stage.Number} の敵が {waves.FileName} にない").Error);
                    continue;
                }

                try
                {
                    ValidateWaveTiming(stage, schedule, stageWaves, report);
                    ValidateEmptyWaves(stage, schedule, stageWaves, report);
                    ValidateStageBoss(stage, schedule, waves, report);
                    if (stage.Number == 1)
                    {
                        ValidateCenterLane(waves, stage.Number, map, schedule, report);
                        foreach (var row in waves.Rows)
                        {
                            if (row.GetInt("stage") == stage.Number && row.Get("enemy_id") == "heavy")
                            {
                                report.Add(row.Error("enemy_id", "ステージ1には重装を出せない（FR-075）").Error);
                            }
                        }
                    }
                }
                catch (CsvFormatException ex)
                {
                    report.Add(ex.Error); // 型の誤りは列の検証でも報告される。ここでは残りのステージの検証を続ける。
                }
            }
        }

        /// <summary>ステージ番号は1から連番（全ステージを順に進む、FR-101）。</summary>
        private static void ValidateStageNumbers(CsvTable stageTable, StageTable stages, ValidationReport report)
        {
            for (var i = 0; i < stages.Count; i++)
            {
                var number = stages.Stages[i].Number;
                if (number > stages.Count)
                {
                    report.Add(stageTable.Rows[i].Error("stage",
                        $"ステージは1から連番: {number} は {stages.Count} 行の表の番号を超える").Error);
                }
            }
        }

        /// <summary>ウェーブは1から連番、ウェーブ1の start_after は0、timed では start_after が前のウェーブの最後の出現より後。</summary>
        private static void ValidateWaveTiming(StageDefinition stage, WaveSchedule schedule, CsvTable stageWaves,
            ValidationReport report)
        {
            for (var i = 0; i < schedule.Waves.Count; i++)
            {
                var wave = schedule.Waves[i];
                var row = FindStageWaveRow(stageWaves, stage.Number, wave.Number);
                if (wave.Number != i + 1)
                {
                    report.Add(row.Error(WaveSchedule.WaveColumn,
                        $"ステージ {stage.Number} のウェーブは1から連番: {i + 1} がない").Error);
                    return;
                }

                if (i == 0)
                {
                    if (wave.StartAfter != 0)
                    {
                        report.Add(row.Error("start_after", "ウェーブ1の start_after は0").Error);
                    }

                    continue;
                }

                var previous = schedule.Waves[i - 1].Spawns;
                var lastSpawn = previous[previous.Count - 1].Time;
                if (stage.Mode == WaveMode.Timed && wave.StartAfter <= lastSpawn)
                {
                    report.Add(row.Error("start_after",
                        $"ステージ {stage.Number} のウェーブ {wave.Number} は前のウェーブの最後の出現（{lastSpawn} 秒）より後に始める").Error);
                }
            }
        }

        /// <summary>stage_waves.csv のウェーブには waves.csv の出現がある。</summary>
        private static void ValidateEmptyWaves(StageDefinition stage, WaveSchedule schedule, CsvTable stageWaves,
            ValidationReport report)
        {
            foreach (var row in stageWaves.Rows)
            {
                if (row.GetInt("stage") != stage.Number)
                {
                    continue;
                }

                var number = row.GetInt(WaveSchedule.WaveColumn);
                var found = false;
                foreach (var wave in schedule.Waves)
                {
                    found |= wave.Number == number;
                }

                if (!found)
                {
                    report.Add(row.Error(WaveSchedule.WaveColumn,
                        $"ステージ {stage.Number} のウェーブ {number} の出現が waves.csv にない").Error);
                }
            }
        }

        private static CsvRow FindStageWaveRow(CsvTable stageWaves, int stage, int wave)
        {
            foreach (var row in stageWaves.Rows)
            {
                if (row.GetInt("stage") == stage && row.GetInt(WaveSchedule.WaveColumn) == wave)
                {
                    return row;
                }
            }

            throw new KeyNotFoundException($"{WaveSchedule.StageWavesFileName} にステージ {stage} のウェーブ {wave} がない");
        }

        /// <summary>ボス（is_boss）は boss 列の敵が、そのステージの最後のウェーブにだけ出る。boss 列のステージには必ず出る（FR-104）。</summary>
        private static void ValidateStageBoss(StageDefinition stage, WaveSchedule schedule, CsvTable waves,
            ValidationReport report)
        {
            var lastWave = schedule.Waves[schedule.Waves.Count - 1].Number;
            var found = false;
            foreach (var row in waves.Rows)
            {
                if (row.GetInt("stage") != stage.Number || !row.GetBool("is_boss"))
                {
                    continue;
                }

                if (stage.BossId == null)
                {
                    report.Add(row.Error("is_boss", $"ステージ {stage.Number} にはボスが出ない（{StageTable.FileName} の boss）").Error);
                }
                else if (row.GetInt(WaveSchedule.WaveColumn) != lastWave)
                {
                    report.Add(row.Error(WaveSchedule.WaveColumn, $"ボスはステージ {stage.Number} の最後のウェーブ {lastWave} だけ").Error);
                }
                else if (row.Get("enemy_id") != stage.BossId)
                {
                    report.Add(row.Error("enemy_id", $"ステージ {stage.Number} のボスは '{stage.BossId}'").Error);
                }
                else
                {
                    found = true;
                }
            }

            if (stage.BossId != null && !found)
            {
                report.Add(new CsvError(waves.FileName, 0, 0,
                    $"ステージ {stage.Number} の最後のウェーブにボス '{stage.BossId}'（is_boss）がいない"));
            }
        }

        /// <summary>
        /// ステージ1の敵は中央のレーン（出現口がマップの中央の行にある道）からだけ出る（FR-034, D-80）。
        /// 見せ場のウェーブ（stage_waves.csv の slot_showcase）は3レーンすべてから来てよい（spec 015 FR-001）。
        /// </summary>
        private static void ValidateCenterLane(CsvTable waves, int stage, BattleMap map, WaveSchedule schedule, ValidationReport report)
        {
            foreach (var row in waves.Rows)
            {
                if (row.GetInt("stage") != stage)
                {
                    continue;
                }

                var wave = waves.HasColumn(WaveSchedule.WaveColumn) ? schedule.FindWave(row.GetInt(WaveSchedule.WaveColumn)) : null;
                if (wave != null && wave.SlotShowcase)
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
