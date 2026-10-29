using System.IO;
using System.Linq;
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
        private static SlotMachine Machine(CoinWallet wallet, string role) =>
            Machine(wallet, role == null ? "" : $"{role},normal,1,1,1,1,1,1\n", ProjectTable(SlotRules.FileName));

        /// <summary>プロジェクトのリール・払い出し表と、行 <paramref name="probabilityRows"/> だけの抽選表、<paramref name="rules"/> の台。</summary>
        private static SlotMachine Machine(CoinWallet wallet, string probabilityRows, CsvTable rules)
        {
            var reels = SlotReels.Load(ProjectTable(SlotReels.FileName));
            var payouts = SlotPayouts.Load(ProjectTable(SlotPayouts.FileName));
            var probabilities = SlotProbabilities.Load(CsvParser.Parse(SlotProbabilities.FileName, ProbabilityHeader + probabilityRows), payouts);
            var slotRules = SlotRules.Load(rules);
            var control = new SlotReelControl(reels, payouts, slotRules.MaxSlide);
            return new SlotMachine(reels, payouts, probabilities, control, slotRules, wallet, Bet, 1, new SeededRandom(1));
        }

        /// <summary>通常時は BIG、ボーナス中はベルが確率1の台。</summary>
        private static SlotMachine BigThenBellMachine(CoinWallet wallet, CsvTable rules) =>
            Machine(wallet, "big,normal,1,1,1,1,1,1\nbell,bonus,1,1,1,1,1,1\n", rules);

        [Test]
        [Description("FR-042 / plan 5章: ボーナス中はボーナス用の抽選表で抽選する")]
        public void Bonus_Game_Draws_From_The_Bonus_Table()
        {
            var machine = BigThenBellMachine(new CoinWallet(30), ProjectTable(SlotRules.FileName));
            PlayOneGame(machine, BonusPresses("big", null, "big", true));

            machine.TryLever();

            Assert.That(machine.Flag.Id, Is.EqualTo("bell"));
        }

        /// <summary>BIG を揃えてから、ボーナス中のゲームを <paramref name="games"/> 回（ベルを押し位置0で揃える）消化する。</summary>
        private static void PlayBigBonusGames(SlotMachine machine, int games)
        {
            PlayOneGame(machine, BonusPresses("big", null, "big", true));
            for (var i = 0; i < games; i++)
            {
                PlayOneGame(machine);
            }
        }

        [Test]
        [Description("FR-042, AC-040: BIG の払い出し累計が上限 240枚（slot_rules.csv）に達したら通常に戻る。ベル8枚×30ゲーム")]
        public void Big_Bonus_Ends_When_Its_Payout_Reaches_The_Limit()
        {
            var machine = BigThenBellMachine(new CoinWallet(1000), ProjectTable(SlotRules.FileName));

            PlayBigBonusGames(machine, 30);

            Assert.That((machine.State, machine.ActiveBonus), Is.EqualTo((SlotState.Normal, (string)null)));
        }

        [Test]
        [Description("FR-042: 上限 240枚の手前（ベル8枚×29ゲーム = 232枚）ではボーナスが続く")]
        public void Big_Bonus_Continues_Below_The_Limit()
        {
            var machine = BigThenBellMachine(new CoinWallet(1000), ProjectTable(SlotRules.FileName));

            PlayBigBonusGames(machine, 29);

            Assert.That((machine.State, machine.BonusPayout), Is.EqualTo((SlotState.Bonus, 232)));
        }

        [Test]
        [Description("FR-042, AC-040: REG は上限 96枚（slot_rules.csv）。ベル8枚×12ゲームで終わる")]
        public void Reg_Bonus_Ends_At_Its_Own_Limit()
        {
            var machine = Machine(new CoinWallet(1000), "reg,normal,1,1,1,1,1,1\nbell,bonus,1,1,1,1,1,1\n", ProjectTable(SlotRules.FileName));
            PlayOneGame(machine, BonusPresses("reg", null, "reg", true));
            for (var i = 0; i < 11; i++)
            {
                PlayOneGame(machine);
            }

            var stateAfter11 = machine.State;
            PlayOneGame(machine);

            Assert.That((stateAfter11, machine.State), Is.EqualTo((SlotState.Bonus, SlotState.Normal)));
        }

        [Test]
        [Description("plan 5章: 上限をまたいだゲームの払い出しは削らずに全額払い、そのゲームで終える（上限20枚・ベル8枚なら3ゲーム24枚）")]
        public void Last_Bonus_Game_Pays_In_Full_Past_The_Limit()
        {
            var rules = CsvParser.Parse(SlotRules.FileName,
                "key,value\nbig_payout_limit,20\nreg_payout_limit,20\nkakuhen_games,100\nkakuhen_entry_rate,1\nwait_seconds,2\nmax_slide,4\n");
            var wallet = new CoinWallet(1000);
            var machine = BigThenBellMachine(wallet, rules);

            PlayBigBonusGames(machine, 3);

            // 1000 - 掛け金3×4ゲーム + ベル8×3。
            Assert.That((machine.State, wallet.Coins), Is.EqualTo((SlotState.Normal, 1012)));
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

        /// <summary>
        /// 左・中・右の順に止めたとき、<paramref name="bonus"/> が揃う（<paramref name="aligned"/> が false なら揃わない）押し位置。
        /// 制御は成立フラグと持ち越しだけで決まるので、同じ表の別の制御で探せる。
        /// </summary>
        private static int[] BonusPresses(string flagId, string carried, string bonus, bool aligned)
        {
            var payouts = SlotPayouts.Load(ProjectTable(SlotPayouts.FileName));
            var reels = SlotReels.Load(ProjectTable(SlotReels.FileName));
            var flag = SlotProbabilities.Load(CsvParser.Parse(SlotProbabilities.FileName, ProbabilityHeader + "big,normal,0,0,0,0,0,0\nreg,normal,0,0,0,0,0,0\n"), payouts)
                .Flags.Single(candidate => candidate.Id == flagId);
            var control = new SlotReelControl(reels, payouts, SlotRules.Load(ProjectTable(SlotRules.FileName)).MaxSlide);
            for (var press = 0; press < SlotReels.StopCount * SlotReels.StopCount * SlotReels.StopCount; press++)
            {
                var presses = new[] { press % SlotReels.StopCount, press / SlotReels.StopCount % SlotReels.StopCount, press / (SlotReels.StopCount * SlotReels.StopCount) };
                var spin = control.Start(flag, carried);
                for (var reel = 0; reel < SlotReels.ReelCount; reel++)
                {
                    spin.Stop(reel, presses[reel]);
                }

                if (SlotLines.Aligned(reels, payouts, spin.Stops).Contains(bonus) == aligned)
                {
                    return presses;
                }
            }

            throw new AssertionException($"{flagId}+{carried ?? "-"} で {bonus} が aligned={aligned} になる押し位置がない");
        }

        private static void PlayOneGame(SlotMachine machine, int[] presses)
        {
            machine.TryLever();
            for (var reel = 0; reel < SlotReels.ReelCount; reel++)
            {
                machine.Stop(reel, presses[reel]);
            }
        }

        [Test]
        [Description("FR-041 / plan 5章: 成立したが揃えなかった BIG は次ゲームに持ち越す")]
        public void Unaligned_Bonus_Is_Carried_To_The_Next_Game()
        {
            var machine = Machine(new CoinWallet(30), "big");

            PlayOneGame(machine, BonusPresses("big", null, "big", false));

            Assert.That(machine.CarriedBonus, Is.EqualTo("big"));
        }

        [Test]
        [Description("plan 5章: 持ち越し中に引いたボーナス役は成立させない（ボーナス確率1の表でもハズレになる）")]
        public void Bonus_Drawn_While_Carrying_Is_Not_Flagged()
        {
            var machine = Machine(new CoinWallet(30), "big");
            PlayOneGame(machine, BonusPresses("big", null, "big", false));

            machine.TryLever();

            Assert.That(machine.Flag.Id, Is.EqualTo(SlotFlag.HazureId));
        }

        [Test]
        [Description("FR-042: 成立したゲームで BIG を揃えるとボーナスが始まる")]
        public void Aligned_Bonus_Starts_The_Bonus()
        {
            var machine = Machine(new CoinWallet(30), "big");

            PlayOneGame(machine, BonusPresses("big", null, "big", true));

            Assert.That((machine.State, machine.ActiveBonus), Is.EqualTo((SlotState.Bonus, "big")));
        }

        [Test]
        [Description("FR-042 / plan 5章: 持ち越した BIG を後のゲームで揃えるとボーナスが始まり、持ち越しは消える")]
        public void Carried_Bonus_Aligned_Later_Starts_The_Bonus()
        {
            var machine = Machine(new CoinWallet(30), "big");
            PlayOneGame(machine, BonusPresses("big", null, "big", false));

            PlayOneGame(machine, BonusPresses(SlotFlag.HazureId, "big", "big", true));

            Assert.That((machine.State, machine.ActiveBonus, machine.CarriedBonus), Is.EqualTo((SlotState.Bonus, "big", (string)null)));
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
