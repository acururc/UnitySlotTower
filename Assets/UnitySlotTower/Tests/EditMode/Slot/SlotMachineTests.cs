using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class SlotMachineTests
    {
        private const int Bet = 3;
        private const string ProbabilityHeader = "role,state,setting1,setting2,setting3,setting4,setting5,setting6\n";

        private static CsvTable ProjectTable(string fileName) =>
            CsvParser.Parse(fileName, File.ReadAllText(Path.Combine(TablePaths.Root, fileName)));

        /// <summary>プロジェクトのリール・払い出し表と、<paramref name="role"/> が確率1で成立する抽選表の台（null ならハズレのみ）。</summary>
        private static SlotMachine Machine(CoinWallet wallet, string role)
        {
            var reels = SlotReels.Load(ProjectTable(SlotReels.FileName));
            var payouts = SlotPayouts.Load(ProjectTable(SlotPayouts.FileName));
            var csv = ProbabilityHeader + (role == null ? "" : $"{role},normal,1,1,1,1,1,1\n");
            var probabilities = SlotProbabilities.Load(CsvParser.Parse(SlotProbabilities.FileName, csv), payouts);
            var control = new SlotReelControl(reels, payouts, SlotRules.Load(ProjectTable(SlotRules.FileName)).MaxSlide);
            return new SlotMachine(reels, payouts, probabilities, control, wallet, Bet, 1, new SeededRandom(1));
        }

        [Test]
        [Description("FR-040")]
        public void Lever_Takes_The_Bet_From_The_Wallet()
        {
            var wallet = new CoinWallet(30);

            Machine(wallet, null).TryLever();

            Assert.That(wallet.Coins, Is.EqualTo(27));
        }

        [Test]
        [Description("FR-040")]
        public void Lever_Is_Refused_When_Coins_Are_Below_The_Bet()
        {
            var wallet = new CoinWallet(2);

            var started = Machine(wallet, null).TryLever();

            Assert.That((started, wallet.Coins), Is.EqualTo((false, 2)));
        }

        private static void PlayOneGame(SlotMachine machine)
        {
            machine.TryLever();
            for (var reel = 0; reel < SlotReels.ReelCount; reel++)
            {
                machine.Stop(reel, 0);
            }
        }

        [Test]
        [Description("FR-040, FR-041")]
        public void Aligned_Bell_Pays_Its_Payout_Once()
        {
            var wallet = new CoinWallet(30);

            PlayOneGame(Machine(wallet, "bell"));

            // 30 - 掛け金3 + ベル8（slot_payouts.csv）。
            Assert.That(wallet.Coins, Is.EqualTo(35));
        }

        [Test]
        [Description("FR-041")]
        public void Game_After_Aligned_Replay_Takes_No_Bet()
        {
            var wallet = new CoinWallet(30);
            var machine = Machine(wallet, "replay");
            PlayOneGame(machine);

            machine.TryLever();

            // 1ゲーム目の掛け金3だけ減り、リプレイの払い出しは0（slot_payouts.csv）。
            Assert.That(wallet.Coins, Is.EqualTo(27));
        }

        [Test]
        [Description("FR-040")]
        public void Hazure_Pays_Nothing()
        {
            var wallet = new CoinWallet(30);

            PlayOneGame(Machine(wallet, null));

            Assert.That(wallet.Coins, Is.EqualTo(27));
        }

        [Test]
        [Description("FR-040")]
        public void Lever_Is_Refused_While_The_Reels_Spin()
        {
            var wallet = new CoinWallet(30);
            var machine = Machine(wallet, null);
            machine.TryLever();

            var started = machine.TryLever();

            Assert.That((started, wallet.Coins), Is.EqualTo((false, 27)));
        }
    }
}
