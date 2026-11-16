using System.Collections.Generic;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;

namespace UnitySlotTower.Tests.EditMode.Data
{
    /// <summary>
    /// D-86、plan 17.1: 1つの戦闘で手に入るコインの期待値（敵のドロップ＋戦闘の報酬）から、回せる回数の目安を確かめる。
    /// 目安の回数はテストの引数（Core の定数にしない）。スロットの出目で増える分は数えない（下限の見積もり）。
    /// </summary>
    public sealed class CoinIncomeTests
    {
        private static CsvTable Parse(IReadOnlyDictionary<string, string> files, string file) =>
            CsvParser.Parse(file, files[file]);

        /// <summary>1体を倒したときのコインの期待値（1回の抽選でコイン率なら min〜max の一様、Battlefield と同じ）。</summary>
        private static double ExpectedCoins(EnemyDefinition enemy) =>
            enemy.CoinRate * (enemy.CoinMin + enemy.CoinMax) / 2.0;

        /// <summary>その戦闘の全ウェーブの敵をすべて倒し、報酬まで受け取ったときのコインの期待値。</summary>
        private static double ExpectedCoinsPerEncounter(IReadOnlyDictionary<string, string> files, Encounter encounter)
        {
            var enemies = EnemyTable.Load(Parse(files, EnemyTable.FileName));
            var map = BattleMap.Load(encounter.MapId, files[BattleMap.MapFileName(encounter.MapId)],
                Parse(files, BattleMap.PathsFileName));
            var waves = Parse(files, WaveSchedule.FileName);

            double total = encounter.RewardCoins;
            for (var wave = 1; wave <= WaveSchedule.WavesPerEncounter; wave++)
            {
                foreach (var spawn in WaveSchedule.Load(waves, encounter.Id, wave, map, enemies).Spawns)
                {
                    total += ExpectedCoins(spawn.Enemy);
                }
            }

            return total;
        }

        [TestCase(12)]
        [Description("D-86、plan 17.1: コインを使う戦闘（回数固定でない）はどれも、1戦闘のコインの期待値 ÷ 掛け金が目安の回数以上")]
        public void Every_Coin_Battle_Yields_Enough_Spins(int minimumSpins)
        {
            var files = TablePaths.ReadAll();
            var bet = GameConfig.Load(Parse(files, GameConfig.FileName)).Bet;
            var encounters = EncounterTable.Load(Parse(files, EncounterTable.FileName));

            var checkedCount = 0;
            foreach (var encounter in encounters.All)
            {
                if (encounter.FixedSpins > 0)
                {
                    continue;
                }

                var spins = ExpectedCoinsPerEncounter(files, encounter) / bet;
                TestContext.WriteLine($"{encounter.Id}: {spins:0.0} 回");
                Assert.That(spins, Is.GreaterThanOrEqualTo(minimumSpins), encounter.Id);
                checkedCount++;
            }

            Assert.That(checkedCount, Is.GreaterThan(0), "コインを使う戦闘がない");
        }

        [Test]
        [Description("AC-019、plan 17.1: 2枚賭け、両替は 10 円 = 1 コイン、一番安いキャラは 100 円くらい")]
        public void Project_Money_Is_Yen_Scale_With_Bet_Two_And_Ten_Yen_Per_Coin()
        {
            var files = TablePaths.ReadAll();
            var config = GameConfig.Load(Parse(files, GameConfig.FileName));
            var rules = RouteRules.Load(Parse(files, RouteRules.FileName));
            var units = UnitTable.Load(Parse(files, UnitTable.FileName));

            var cheapest = int.MaxValue;
            foreach (var unit in units.All)
            {
                cheapest = System.Math.Min(cheapest, unit.Cost);
            }

            Assert.That((config.Bet, rules.ExchangeMoneyPerCoin), Is.EqualTo((2, 10)));
            Assert.That(cheapest, Is.InRange(80, 150));
        }
    }
}
