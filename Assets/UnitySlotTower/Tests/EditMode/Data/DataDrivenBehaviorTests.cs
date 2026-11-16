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
using UnitySlotTower.Tests.EditMode.Run;

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
            var machine = new SlotMachine(reels, patterns.For(1), new CoinWallet(spins * 3), 3, new SeededRandom(7));
            var hits = 0;
            for (var i = 0; i < spins; i++)
            {
                Assert.That(machine.TryLever(), Is.EqualTo(LeverResult.Started));
                if (machine.Outcome == SlotSymbol.Red7)
                {
                    hits++;
                }

                machine.Settle();
            }

            return hits;
        }

        private static double AttackRateAfterOneBar(IReadOnlyDictionary<string, string> files)
        {
            var boosts = new BattleBoosts(SlotRules.Load(Parse(files, SlotRules.FileName)));
            boosts.AddBar();
            return boosts.AttackRate;
        }

        private static int MoneyPaidForCoins(IReadOnlyDictionary<string, string> files)
        {
            var rules = RouteRules.Load(Parse(files, RouteRules.FileName));
            var run = RunFixtures.CreateRun(coins: 15, money: 300);
            Assert.That(new Exchange(run, 2, rules.ExchangeMoneyPerCoin).TryBuyCoins(), Is.True);
            return 300 - run.Money.Coins;
        }

        private static RouteMap Route(IReadOnlyDictionary<string, string> files, ulong seed) =>
            RouteGenerator.Generate(RouteRules.Load(Parse(files, RouteRules.FileName)), new SeededRandom(seed));

        private static string FirstSpawnEnemy(IReadOnlyDictionary<string, string> files)
        {
            var map = BattleMap.Load("first", files[BattleMap.MapFileName("first")], Parse(files, "map_paths.csv"));
            var enemies = EnemyTable.Load(Parse(files, EnemyTable.FileName));
            var schedule = WaveSchedule.Load(Parse(files, WaveSchedule.FileName), "first", map, enemies);
            return schedule.Spawns.Single(s => s.Time == 0).Enemy.Id;
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
        [Description("抽選表: slot_patterns.csv の設定1で red7 3→6・miss 4→1（合計20のまま）")]
        public void Slot_Odds_Follow_Slot_Patterns_Csv()
        {
            var changed = TablePaths.ReadAll();
            Set(changed, SlotPatterns.FileName, "count", "6", ("setting", "1"), ("outcome", "red7"));
            Set(changed, SlotPatterns.FileName, "count", "1", ("setting", "1"), ("outcome", "miss"));
            AssertValid(changed);

            const int spins = 2000;
            var before = Red7Hits(TablePaths.ReadAll(), spins);
            var after = Red7Hits(changed, spins);

            // 期待値は 15%（300回）→ 30%（600回）。シード固定なので結果は毎回同じ。
            Assert.That(before, Is.InRange(200, 400), "変更前の red7 の回数");
            Assert.That(after, Is.InRange(500, 700), "変更後の red7 の回数");
        }

        [Test]
        [Description("図柄効果: slot_rules.csv の bar_step 0.1 → 0.2")]
        public void Bar_Effect_Follows_Slot_Rules_Csv()
        {
            var changed = TablesWith(SlotRules.FileName, "value", "0.2", ("key", "bar_step"));

            Assert.That((AttackRateAfterOneBar(TablePaths.ReadAll()), AttackRateAfterOneBar(changed)),
                Is.EqualTo((1.1, 1.2)));
        }

        [Test]
        [Description("両替レート: route_rules.csv の exchange_money_per_coin 10 → 15（コイン2枚の値段 20 → 30）")]
        public void Exchange_Rate_Follows_Route_Rules_Csv()
        {
            var changed = TablesWith(RouteRules.FileName, "value", "15", ("key", "exchange_money_per_coin"));

            Assert.That((MoneyPaidForCoins(TablePaths.ReadAll()), MoneyPaidForCoins(changed)), Is.EqualTo((20, 30)));
        }

        [Test]
        [Description("ルート生成: route_rules.csv の columns 7 → 3")]
        public void Route_Width_Follows_Route_Rules_Csv()
        {
            var changed = TablesWith(RouteRules.FileName, "value", "3", ("key", "columns"));

            for (ulong seed = 1; seed <= 20; seed++)
            {
                Assert.That((Route(TablePaths.ReadAll(), seed).Columns, Route(changed, seed).Columns),
                    Is.EqualTo((7, 3)));
                Assert.That(Route(changed, seed).All.All(node => node.Column < 3), Is.True, $"seed {seed}");
            }
        }

        [Test]
        [Description("敵の出現: waves.csv の最初の戦闘・0秒の敵 melee → ranged")]
        public void Enemy_Spawn_Follows_Waves_Csv()
        {
            var changed = TablesWith(WaveSchedule.FileName, "enemy_id", "ranged",
                ("encounter_id", "first"), ("time", "0"));

            Assert.That((FirstSpawnEnemy(TablePaths.ReadAll()), FirstSpawnEnemy(changed)),
                Is.EqualTo(("melee", "ranged")));
        }
    }
}
