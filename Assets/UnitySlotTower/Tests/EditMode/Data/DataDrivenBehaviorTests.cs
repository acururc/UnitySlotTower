using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Data
{
    /// <summary>
    /// プロジェクトの CSV 正本の1セルだけを書き換え、コードを変えずに挙動が変わることを確かめる。
    /// 書き換えた後も <see cref="GameDataValidator"/> を通る（ゲームが読み込める）ことも見る。
    /// </summary>
    [Description("AC-050 / FR-050, T503: データテーブルの値を変えるだけで挙動が変わる")]
    public sealed class DataDrivenBehaviorTests
    {
        /// <summary>
        /// <paramref name="file"/> のうち <paramref name="match"/>（列名=値）にすべて合う行の <paramref name="column"/> を
        /// <paramref name="value"/> に置き換える。合う行がちょうど1行でなければ失敗する。
        /// 引用符を含む行は扱わない（数値テーブル用）。
        /// </summary>
        private static void Set(Dictionary<string, string> files, string file, string column, string value,
            params (string column, string value)[] match)
        {
            var lines = files[file].TrimStart('﻿').Replace("\r\n", "\n").Split('\n');
            var header = lines[0].Split(',');
            var target = Array.IndexOf(header, column);
            Assert.That(target, Is.GreaterThanOrEqualTo(0), $"{file} に列 {column} がない");
            var keys = match.Select(m => (index: Array.IndexOf(header, m.column), m.value)).ToArray();
            Assert.That(keys.All(k => k.index >= 0), Is.True, $"{file} に照合の列がない");

            var hits = 0;
            for (var i = 1; i < lines.Length; i++)
            {
                var cells = lines[i].Split(',');
                if (lines[i].Contains('"') || cells.Length != header.Length ||
                    !keys.All(k => cells[k.index] == k.value))
                {
                    continue;
                }

                cells[target] = value;
                lines[i] = string.Join(",", cells);
                hits++;
            }

            Assert.That(hits, Is.EqualTo(1), $"{file} で照合に合う行の数");
            files[file] = string.Join("\n", lines);
        }

        /// <summary>1セルだけ書き換えた全テーブル（書き換え後も検証を通ることを確かめる）。</summary>
        private static Dictionary<string, string> TablesWith(string file, string column, string value,
            params (string column, string value)[] match)
        {
            var files = TablePaths.ReadAll();
            Set(files, file, column, value, match);
            AssertValid(files);
            return files;
        }

        private static void AssertValid(IReadOnlyDictionary<string, string> files)
        {
            var report = GameDataValidator.ValidateAll(files);
            Assert.That(report.IsValid, Is.True, report.ToString());
        }

        private static CsvTable Parse(IReadOnlyDictionary<string, string> files, string file) =>
            CsvParser.Parse(file, files[file]);

        private static int SpentOnSummon(IReadOnlyDictionary<string, string> files, string unitId)
        {
            var wallet = new CoinWallet(1000);
            var summoner = new UnitSummoner(UnitTable.Load(Parse(files, UnitTable.FileName)), wallet, 8, 0.5);
            Assert.That(summoner.TrySummon(unitId), Is.EqualTo(SummonResult.Summoned));
            return 1000 - wallet.Coins;
        }

        private static int Red7Hits(IReadOnlyDictionary<string, string> files, int spins)
        {
            var patterns = SlotPatterns.Load(Parse(files, SlotPatterns.FileName));
            var reels = SlotReels.Load(Parse(files, SlotReels.FileName));
            var machine = new SlotMachine(reels, patterns.Patterns, new CoinWallet(spins), new SeededRandom(7), 0.6);
            var hits = 0;
            for (var i = 0; i < spins; i++)
            {
                Assert.That(machine.TryLever(), Is.EqualTo(LeverResult.Started));
                if (machine.Hits.Any(h => h.Symbol == SlotSymbol.Red7))
                {
                    hits++;
                }

                machine.Settle();
            }

            return hits;
        }

        private static double AttackRateAfterOneBar(IReadOnlyDictionary<string, string> files)
        {
            var boosts = new SlotEffects(SlotRules.Load(Parse(files, SlotRules.FileName)));
            boosts.StartAttack();
            return boosts.AttackRate;
        }

        private static string FirstSpawnEnemy(IReadOnlyDictionary<string, string> files)
        {
            var map = BattleMap.Load("first", files[BattleMap.MapFileName("first")], Parse(files, "map_paths.csv"));
            var enemies = EnemyTable.Load(Parse(files, EnemyTable.FileName));
            var schedule = WaveSchedule.Load(Parse(files, WaveSchedule.FileName), 1, map, enemies);
            return schedule.Spawns.Single(s => s.Wave == 1 && s.Time == 0 && s.Path.Id == "first_g1").Enemy.Id;
        }

        [Test]
        public void Unchanged_Project_Tables_Are_Valid() => AssertValid(TablePaths.ReadAll());

        [Test]
        [Description("召喚費用: units.csv の sword の cost 100 → 160")]
        public void Summon_Cost_Follows_Units_Csv()
        {
            var changed = TablesWith(UnitTable.FileName, "cost", "160", ("id", "sword"));

            Assert.That((SpentOnSummon(TablePaths.ReadAll(), "sword"), SpentOnSummon(changed, "sword")),
                Is.EqualTo((100, 160)));
        }

        [Test]
        [Description("抽選表: slot_patterns.csv  で red7 3→6・bar 4→1（合計20のまま）")]
        public void Slot_Odds_Follow_Slot_Patterns_Csv()
        {
            var changed = TablePaths.ReadAll();
            Set(changed, SlotPatterns.FileName, "count", "6", ("outcome", "red7"));
            Set(changed, SlotPatterns.FileName, "count", "1", ("outcome", "bar"));
            AssertValid(changed);

            const int spins = 2000;
            var before = Red7Hits(TablePaths.ReadAll(), spins);
            var after = Red7Hits(changed, spins);

            // 1ライン 60% × 重み: 期待値は 9%（180回）→ 18%（360回）。シード固定なので結果は毎回同じ。
            Assert.That(before, Is.InRange(120, 240), "変更前の red7 の回数");
            Assert.That(after, Is.InRange(280, 440), "変更後の red7 の回数");
        }

        [Test]
        [Description("図柄効果: slot_rules.csv の bar_attack_step（倍率 1+幅×1枚）")]
        public void Bar_Effect_Follows_Slot_Rules_Csv()
        {
            var changed = TablesWith(SlotRules.FileName, "value", "2", ("key", "bar_attack_step"));

            Assert.That((AttackRateAfterOneBar(TablePaths.ReadAll()), AttackRateAfterOneBar(changed)),
                Is.EqualTo((2.0, 3.0)));
        }

        [Test]
        [Description("敵の出現: waves.csv のステージ1・ウェーブ1・0秒の敵 melee → ranged")]
        public void Enemy_Spawn_Follows_Waves_Csv()
        {
            var changed = TablesWith(WaveSchedule.FileName, "enemy_id", "ranged",
                ("stage", "1"), ("wave", "1"), ("time", "0"), ("path_id", "first_g1"));

            Assert.That((FirstSpawnEnemy(TablePaths.ReadAll()), FirstSpawnEnemy(changed)),
                Is.EqualTo(("melee", "ranged")));
        }
    }
}
