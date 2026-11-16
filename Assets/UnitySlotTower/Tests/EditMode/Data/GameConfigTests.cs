using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Tests.EditMode.Data
{
    public sealed class GameConfigTests
    {
        private const string ValidConfig =
            "key,value\ninitial_coins,15\ninitial_money,30\nbet,3\nlife,10\ndeploy_limit,8\nretreat_refund_rate,0.5\n";

        [Test]
        [Description("plan 16.2: コインと所持金の初期値を別々に読む（ライフボーナスのキーはない）")]
        public void Values_Are_Read_By_Key()
        {
            var config = GameConfig.Load(CsvParser.Parse("game_config.csv", ValidConfig));

            Assert.That((config.InitialCoins, config.InitialMoney, config.Bet, config.Life, config.DeployLimit,
                    config.RetreatRefundRate),
                Is.EqualTo((15, 30, 3, 10, 8, 0.5)));
        }

        [Test]
        public void Missing_Key_Is_Reported_With_Its_Name()
        {
            var text = ValidConfig.Replace("bet,3\n", string.Empty);

            var ex = Assert.Throws<CsvFormatException>(() => GameConfig.Load(CsvParser.Parse("game_config.csv", text)));
            Assert.That(ex.Error.Message, Does.Contain("bet"));
        }

        [Test]
        public void Invalid_Value_Reports_Its_Line()
        {
            var text = ValidConfig.Replace("life,10", "life,ten");

            var ex = Assert.Throws<CsvFormatException>(() => GameConfig.Load(CsvParser.Parse("game_config.csv", text)));
            Assert.That(ex.Error.Line, Is.EqualTo(5));
        }

        [Test]
        [Description("plan 16.2・17.1: コイン15・所持金250円・2枚賭け・ライフ10")]
        public void Project_Table_Holds_Planned_Initial_Values()
        {
            var text = File.ReadAllText(Path.Combine(TablePaths.Root, "game_config.csv"));

            var config = GameConfig.Load(CsvParser.Parse("game_config.csv", text));

            Assert.That((config.InitialCoins, config.InitialMoney, config.Bet, config.Life, config.DeployLimit,
                    config.RetreatRefundRate),
                Is.EqualTo((15, 250, 2, 10, 8, 0.5)));
        }

        [Test]
        [Description("plan 16.2: ライフボーナスは廃止。プロジェクトの表にキーが残っていない")]
        public void Project_Table_Has_No_Life_Bonus_Key()
        {
            var text = File.ReadAllText(Path.Combine(TablePaths.Root, "game_config.csv"));

            Assert.That(text, Does.Not.Contain("life_bonus_per_life"));
        }
    }
}
