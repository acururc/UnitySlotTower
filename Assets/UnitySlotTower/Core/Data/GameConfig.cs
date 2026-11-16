namespace UnitySlotTower.Core.Data
{
    /// <summary>game_config.csv の値（plan 16.2。コインはスロット、所持金は召喚・強化に使う）。</summary>
    public sealed class GameConfig
    {
        public const string FileName = "game_config.csv";

        public int InitialCoins { get; private set; }
        public int InitialMoney { get; private set; }
        public int Bet { get; private set; }
        public int Life { get; private set; }
        public int DeployLimit { get; private set; }
        public double RetreatRefundRate { get; private set; }

        /// <summary>戦闘マスの開始の演出の秒数（FR-019、plan 17.2）。この間は戦闘の時間が進まず操作もできない。</summary>
        public double IntroSeconds { get; private set; }

        public static GameConfig Load(CsvTable table)
        {
            var values = new KeyValueTable(table);
            return new GameConfig
            {
                InitialCoins = values.GetInt("initial_coins"),
                InitialMoney = values.GetInt("initial_money"),
                Bet = values.GetInt("bet"),
                Life = values.GetInt("life"),
                DeployLimit = values.GetInt("deploy_limit"),
                RetreatRefundRate = values.GetDouble("retreat_refund_rate"),
                IntroSeconds = values.GetDouble("intro_seconds"),
            };
        }
    }
}
