using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Tests.EditMode.Data
{
    public sealed class GameConfigTests
    {
        private const string ValidConfig =
            "key,value\ninitial_coins,15\ninitial_money,30\nbet,3\nlife,10\ndeploy_limit,8\nretreat_refund_rate,0.5\nintro_seconds,2\nplacement_time_scale,0.1\nwave_gap_seconds,1\n";

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
        [Description("FR-102, plan 2.1: 全滅から次のウェーブまでの間隔を読む")]
        public void Wave_Gap_Is_Read()
        {
            var config = GameConfig.Load(CsvParser.Parse("game_config.csv",
                ValidConfig.Replace("wave_gap_seconds,1", "wave_gap_seconds,2.5")));

            Assert.That(config.WaveGapSeconds, Is.EqualTo(2.5));
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
        [Description("plan 16.2・17.1: コイン3・所持金250円・1枚賭け・ライフ10")]
        public void Project_Table_Holds_Planned_Initial_Values()
        {
            var text = File.ReadAllText(Path.Combine(TablePaths.Root, "game_config.csv"));

            var config = GameConfig.Load(CsvParser.Parse("game_config.csv", text));

            Assert.That((config.InitialCoins, config.InitialMoney, config.Bet, config.Life, config.DeployLimit,
                    config.RetreatRefundRate),
                Is.EqualTo((3, 250, 1, 10, 8, 0.5)));
        }

        [Test]
        [Description("FR-019, plan 17.2: 開始の演出の秒数を読む（プロジェクトの表は仮 2 秒）")]
        public void Intro_Seconds_Are_Read()
        {
            var text = File.ReadAllText(Path.Combine(TablePaths.Root, "game_config.csv"));

            Assert.That((GameConfig.Load(CsvParser.Parse("game_config.csv", ValidConfig.Replace("intro_seconds,2", "intro_seconds,1.5"))).IntroSeconds,
                    GameConfig.Load(CsvParser.Parse("game_config.csv", text)).IntroSeconds),
                Is.EqualTo((1.5, 2.0)));
        }

        [Test]
        [Description("AC-066: 配置中の時間の倍率を読む（プロジェクトの表は 1/5）")]
        public void Placement_Time_Scale_Is_Read()
        {
            var text = File.ReadAllText(Path.Combine(TablePaths.Root, "game_config.csv"));

            Assert.That((GameConfig.Load(CsvParser.Parse("game_config.csv",
                        ValidConfig.Replace("placement_time_scale,0.1", "placement_time_scale,0.25"))).PlacementTimeScale,
                    GameConfig.Load(CsvParser.Parse("game_config.csv", text)).PlacementTimeScale),
                Is.EqualTo((0.25, 0.2)));
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
