using System.Collections.Generic;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;

namespace UnitySlotTower.Tests.EditMode.Data
{
    /// <summary>
    /// AC-019、plan 17.1: お金の桁（掛け金・両替・召喚費用）。001 D-86 の「1戦闘で回せる回数」は 002 E-08（回数固定の戦闘を廃止）でなくした。
    /// </summary>
    public sealed class CoinIncomeTests
    {
        private static CsvTable Parse(IReadOnlyDictionary<string, string> files, string file) =>
            CsvParser.Parse(file, files[file]);

        [Test]
        [Description("AC-019、plan 17.1: 1枚賭け、両替は 10 円 = 1 コイン、一番安いキャラは 100 円くらい")]
        public void Project_Money_Is_Yen_Scale_With_Bet_One_And_Ten_Yen_Per_Coin()
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

            Assert.That((config.Bet, rules.ExchangeMoneyPerCoin), Is.EqualTo((1, 10)));
            Assert.That(cheapest, Is.InRange(80, 150));
        }
    }
}
