using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Tests.EditMode.Slot
{
    public sealed class SlotMachineTests
    {
        private const int Bet = 3;
        // plan 5章のロック配置（空きは 6〜8・15〜17）を混ぜて埋めた配列。
        private const string DefaultReel = "atk speed coin bar coin seven coin heal coin bar speed heal atk seven heal coin atk coin";
        private const int Length = 18;

        /// <summary>有効ライン5本（上・中・下と斜め2本、D-52・D-58）の、リールごとの段のずれ。</summary>
        private static readonly int[][] Lines =
        {
            new[] { -1, -1, -1 }, new[] { 0, 0, 0 }, new[] { 1, 1, 1 }, new[] { -1, 0, 1 }, new[] { 1, 0, -1 },
        };

        /// <summary>5本の有効ラインのうち3つ揃ったラインの図柄（D-52・D-58）。</summary>
        private static List<SlotSymbol> AlignedRows(SlotMachine machine)
        {
            var rows = new List<SlotSymbol>();
            foreach (var line in Lines)
            {
                var cells = Enumerable.Range(0, SlotMachine.ReelCount).Select(r => machine.Layout.At(machine.StopPosition(r) + line[r])).ToArray();
                if (cells.All(s => s == cells[0]))
                {
                    rows.Add(cells[0]);
                }
            }

            return rows;
        }

        /// <summary>当たりは結果の図柄がちょうど1本だけ、ハズレはどのラインも揃わない（D-53・D-58）。</summary>
        private static bool ShowsResult(SlotMachine machine, SlotSymbol? result)
        {
            var rows = AlignedRows(machine);
            return result.HasValue ? rows.Count == 1 && rows[0] == result.Value : rows.Count == 0;
        }

        private static ReelLayout Layout(string ids) =>
            new ReelLayout(ids.Split(' ').Select(id => (SlotSymbol)Array.IndexOf(SlotSymbols.Ids, id)).ToArray());

        private static SlotMachine Machine(CoinWallet wallet, double missRate = 0.85) =>
            new SlotMachine(Layout(DefaultReel), missRate, wallet, Bet, SlotTestRules.NoBonus(), new SeededRandom(1));

        [Test]
        [Description("FR-040: レバーオンで掛け金を払ってリールが回る")]
        public void Lever_Spends_The_Bet_And_Spins()
        {
            var wallet = new CoinWallet(10);
            var machine = Machine(wallet);

            var started = machine.TryLever();

            Assert.That((started, machine.IsSpinning, wallet.Coins), Is.EqualTo((true, true, 7)));
        }

        [Test]
        [Description("FR-040: 所持コインが掛け金未満なら回せない")]
        public void Lever_Is_Refused_Without_Enough_Coins()
        {
            var wallet = new CoinWallet(Bet - 1);
            var machine = Machine(wallet);

            var started = machine.TryLever();

            Assert.That((started, machine.IsSpinning, wallet.Coins), Is.EqualTo((false, false, Bet - 1)));
        }

        [Test]
        [Description("plan 5章: 回転中のレバーは無視して、掛け金も取らない")]
        public void Lever_While_Spinning_Is_Ignored()
        {
            var wallet = new CoinWallet(10);
            var machine = Machine(wallet);
            machine.TryLever();

            var started = machine.TryLever();

            Assert.That((started, wallet.Coins), Is.EqualTo((false, 7)));
        }

        [Test]
        [Description("AC-044: 確定した配列に替えると、以後の抽選と停止はその配列で行う（空きをすべて攻撃力アップにした配列）")]
        public void Changed_Layout_Is_Used_For_Next_Games()
        {
            var machine = Machine(new CoinWallet(100), 0.0);
            var custom = Layout("atk speed coin bar coin seven atk atk atk bar speed heal atk seven heal atk atk atk");

            machine.ChangeLayout(custom);
            var hits = new HashSet<SlotSymbol>();
            for (var game = 0; game < 50; game++)
            {
                machine.TryLever();
                for (var reel = 0; reel < SlotMachine.ReelCount; reel++)
                {
                    machine.Stop(reel, game);
                }

                Assert.That(ShowsResult(machine, machine.Result), Is.True, $"ゲーム {game}");
                hits.Add(machine.Result.Value);
            }

            Assert.That((machine.Layout, hits.Contains(SlotSymbol.Atk)), Is.EqualTo((custom, true)));
        }

        [Test]
        [Description("回転中は配列を替えられない（出目が配列とずれるため）")]
        public void Layout_Cannot_Change_While_Spinning()
        {
            var machine = Machine(new CoinWallet(10));
            machine.TryLever();

            Assert.Throws<InvalidOperationException>(() => machine.ChangeLayout(Layout(DefaultReel)));
        }

        [Test]
        [Description("D-43: ハズレ率 0 なら毎回当たり、1 なら毎回ハズレ")]
        public void Miss_Rate_Zero_Always_Hits_And_One_Always_Misses()
        {
            var always = Machine(new CoinWallet(10), 0.0);
            var never = Machine(new CoinWallet(10), 1.0);

            always.TryLever();
            never.TryLever();

            Assert.That((always.Result.HasValue, never.Result.HasValue), Is.EqualTo((true, false)));
        }

        /// <summary>結果を固定する乱数: 当たりのときに選ぶコマを <paramref name="cell"/> にする。</summary>
        private sealed class FixedRandom : IRandom
        {
            private readonly int _cell;

            public FixedRandom(int cell)
            {
                _cell = cell;
            }

            public int NextInt(int minInclusive, int maxExclusive) => _cell;

            public double NextDouble() => 0.5;
        }

        /// <summary>結果が <paramref name="result"/>（null ならハズレ）に決まる既定配列の台。</summary>
        private static SlotMachine Fixed(SlotSymbol? result, CoinWallet wallet, SlotRules rules = null)
        {
            var layout = Layout(DefaultReel);
            var cell = result.HasValue ? Enumerable.Range(0, layout.Length).First(i => layout.At(i) == result.Value) : 0;
            return new SlotMachine(layout, result.HasValue ? 0.0 : 1.0, wallet, Bet, rules ?? SlotTestRules.NoBonus(),
                new FixedRandom(cell));
        }

        private static readonly int[][] Orders =
        {
            new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 },
        };

        [TestCase(SlotSymbol.Seven)]
        [TestCase(SlotSymbol.Bar)]
        [TestCase(SlotSymbol.Atk)]
        [TestCase(SlotSymbol.Speed)]
        [TestCase(SlotSymbol.Heal)]
        [TestCase(SlotSymbol.Coin)]
        [TestCase(null)]
        [Description("AC-041, FR-041, D-53: 全押し順×全押し位置で、当たりは結果の図柄がちょうど1段に揃い、ハズレはどの段も揃わない")]
        public void Every_Press_Order_And_Position_Shows_The_Decided_Result(SlotSymbol? result)
        {
            const int length = Length;
            var machine = Fixed(result, new CoinWallet(1_000_000_000));
            var mismatches = 0;
            foreach (var order in Orders)
            {
                for (var presses = 0; presses < length * length * length; presses++)
                {
                    machine.TryLever();
                    var press = new[] { presses % length, presses / length % length, presses / (length * length) };
                    foreach (var reel in order)
                    {
                        machine.Stop(reel, press[reel]);
                    }

                    if (!ShowsResult(machine, result))
                    {
                        mismatches++;
                    }
                }
            }

            Assert.That(mismatches, Is.Zero);
        }

        [Test]
        [Description("plan 5章: 当たりの1本目は、押した位置で結果の図柄がどこかの段に見えていればそこで止まる（BAR はコマ3と9）")]
        public void First_Hit_Reel_Stops_At_The_Press_When_The_Symbol_Is_Visible()
        {
            var machine = Fixed(SlotSymbol.Bar, new CoinWallet(10));
            machine.TryLever();

            Assert.That(machine.Stop(0, 10), Is.EqualTo(10), "上段にコマ9の BAR");
        }

        [Test]
        [Description("plan 5章: ハズレは押した位置で止め、最後のリールで段が揃うときだけ揃わない位置まで進める")]
        public void Miss_Stops_At_The_Press_Unless_The_Last_Reel_Would_Align()
        {
            var machine = Fixed(null, new CoinWallet(10));
            machine.TryLever();

            // 3本とも同じ位置だと3段とも揃うので、最後のリールだけ下へ進む。
            Assert.That((machine.Stop(0, 4), machine.Stop(2, 4), machine.Stop(1, 4) != 4, ShowsResult(machine, null)),
                Is.EqualTo((4, 4, true, true)));
        }

        [Test]
        [Description("AC-041, D-44: 押す位置を変えても、同じシードなら結果の列は変わらない")]
        public void Press_Positions_Do_Not_Change_The_Results()
        {
            SlotSymbol?[] Results(Func<int, int, int> press)
            {
                var machine = new SlotMachine(Layout(DefaultReel), 0.7, new CoinWallet(1_000_000_000), Bet, SlotTestRules.NoBonus(),
                    new SeededRandom(5));
                var results = new SlotSymbol?[1000];
                for (var game = 0; game < results.Length; game++)
                {
                    machine.TryLever();
                    results[game] = machine.Result;
                    for (var reel = 0; reel < SlotMachine.ReelCount; reel++)
                    {
                        machine.Stop(reel, press(game, reel));
                    }
                }

                return results;
            }

            Assert.That(Results((game, reel) => 0), Is.EqualTo(Results((game, reel) => game * 7 + reel * 5)));
        }

        /// <summary>レバーを引いて左から順に3本止める。レバーが引けなければ false。</summary>
        private static bool Play(SlotMachine machine)
        {
            if (!machine.TryLever())
            {
                return false;
            }

            for (var reel = 0; reel < SlotMachine.ReelCount; reel++)
            {
                machine.Stop(reel, 0);
            }

            return true;
        }

        [Test]
        [Description("AC-042, FR-042: コイン揃いでコイン払い出し（15枚）を払う")]
        public void Coin_Alignment_Pays_The_Coin_Payout()
        {
            var wallet = new CoinWallet(10);
            var machine = Fixed(SlotSymbol.Coin, wallet, SlotTestRules.Create(coinPayout: 15));

            Play(machine);

            Assert.That((wallet.Coins, machine.LastPayout), Is.EqualTo((10 - Bet + 15, 15)));
        }

        [Test]
        [Description("plan 5章: 払い出しは最後のリールが止まったときに払う")]
        public void Payout_Waits_For_The_Last_Reel()
        {
            var wallet = new CoinWallet(10);
            var machine = Fixed(SlotSymbol.Coin, wallet, SlotTestRules.Create(coinPayout: 15));
            machine.TryLever();

            machine.Stop(0, 0);
            machine.Stop(1, 0);

            Assert.That(wallet.Coins, Is.EqualTo(10 - Bet));
        }

        [TestCase(SlotSymbol.Atk)]
        [TestCase(SlotSymbol.Speed)]
        [TestCase(SlotSymbol.Heal)]
        [TestCase(null)]
        [Description("plan 5章: 効果の図柄とハズレは払い出しなし")]
        public void Effect_Symbols_And_Misses_Pay_Nothing(SlotSymbol? result)
        {
            var wallet = new CoinWallet(10);
            var machine = Fixed(result, wallet, SlotTestRules.Create());

            Play(machine);

            Assert.That((wallet.Coins, machine.LastPayout), Is.EqualTo((10 - Bet, 0)));
        }

        [TestCase(SlotSymbol.Atk)]
        [TestCase(SlotSymbol.Speed)]
        [TestCase(SlotSymbol.Heal)]
        [TestCase(SlotSymbol.Coin)]
        [TestCase(SlotSymbol.Seven)]
        [TestCase(SlotSymbol.Bar)]
        [Description("FR-042: 揃った図柄を最後のリールが止まったときに通知する（戦闘へ効果を渡すため）")]
        public void Alignment_Is_Notified_With_The_Symbol(SlotSymbol result)
        {
            var machine = Fixed(result, new CoinWallet(10), SlotTestRules.Create());
            var aligned = new List<SlotSymbol>();
            machine.Aligned += aligned.Add;

            Play(machine);

            Assert.That(aligned, Is.EqualTo(new[] { result }));
        }

        [Test]
        public void Miss_Is_Not_Notified()
        {
            var machine = Fixed(null, new CoinWallet(10), SlotTestRules.Create());
            var aligned = new List<SlotSymbol>();
            machine.Aligned += aligned.Add;

            Play(machine);

            Assert.That(aligned, Is.Empty);
        }

        [TestCase(SlotSymbol.Seven, SlotBonus.Big, 12)]
        [TestCase(SlotSymbol.Bar, SlotBonus.Reg, 6)]
        [Description("AC-043, FR-043: 7揃いで BIG、BAR揃いで REG になり、残りゲーム数は定義どおり")]
        public void Seven_Starts_Big_And_Bar_Starts_Reg(SlotSymbol result, SlotBonus bonus, int games)
        {
            var machine = Fixed(result, new CoinWallet(10), SlotTestRules.Create(bigGames: 12, regGames: 6));

            Play(machine);

            Assert.That((machine.Bonus, machine.BonusGamesLeft), Is.EqualTo((bonus, games)));
        }

        [Test]
        [Description("AC-043, plan 5章: ボーナス中は掛け金なしでレバーを引け、ハズレ抽選なしでコインが揃う")]
        public void Bonus_Game_Needs_No_Bet_And_Aligns_Coins()
        {
            var wallet = new CoinWallet(Bet);
            var machine = Fixed(SlotSymbol.Seven, wallet, SlotTestRules.Create());
            Play(machine);

            var started = machine.TryLever();

            Assert.That((started, wallet.Coins, machine.Result), Is.EqualTo((true, 0, (SlotSymbol?)SlotSymbol.Coin)));
        }

        [TestCase(SlotSymbol.Seven, 12, 10)]
        [TestCase(SlotSymbol.Bar, 6, 8)]
        [Description("AC-043: ボーナスはゲームごとに定義どおり払い出し、定義どおりのゲーム数で通常に戻る（BIG 12G×10枚、REG 6G×8枚）")]
        public void Bonus_Pays_Each_Game_And_Ends_After_Its_Games(SlotSymbol result, int games, int payout)
        {
            var wallet = new CoinWallet(Bet);
            var machine = Fixed(result, wallet, SlotTestRules.Create(coinPayout: 15, bigGames: 12, bigPayout: 10,
                regGames: 6, regPayout: 8));
            Play(machine);

            var played = 0;
            while (machine.Bonus != SlotBonus.None && played < 100)
            {
                Play(machine);
                played++;
            }

            Assert.That((played, wallet.Coins), Is.EqualTo((games, games * payout)));
        }

        [Test]
        [Description("plan 5章: ボーナスを消化し終えたら、次のゲームは掛け金を払って通常どおり抽選する")]
        public void Game_After_The_Bonus_Needs_The_Bet_Again()
        {
            var wallet = new CoinWallet(Bet);
            var machine = Fixed(SlotSymbol.Bar, wallet, SlotTestRules.Create(regGames: 1, regPayout: 2));
            Play(machine);
            Play(machine);

            var started = machine.TryLever();

            Assert.That((started, wallet.Coins), Is.EqualTo((false, 2)));
        }

        [Test]
        [Description("AC-041, D-58: 斜め（左上→中央→右下）に揃った止め方はハズレの最終形ではなく、その図柄の当たりの最終形になる")]
        public void Diagonal_Line_Counts_As_Aligned()
        {
            var layout = Layout(DefaultReel);

            // 停止位置 1・12・15: 1本目の上段コマ0、2本目の中段コマ12、3本目の下段コマ16 がどれも攻撃力アップ。横の3段は揃わない。
            Assert.That((SlotStops.IsFinal(layout, 1, 12, 15, null), SlotStops.IsFinal(layout, 1, 12, 15, SlotSymbol.Atk)),
                Is.EqualTo((false, true)));
        }

        /// <summary>NextDouble は常に同じ値、NextInt は常に同じコマを返す乱数。</summary>
        private sealed class ConstantRandom : IRandom
        {
            private readonly double _value;
            private readonly int _cell;

            public ConstantRandom(double value, int cell)
            {
                _value = value;
                _cell = cell;
            }

            public int NextInt(int minInclusive, int maxExclusive) => _cell;
            public double NextDouble() => _value;
        }

        private static SlotSymbol? PlayResult(SlotMachine machine)
        {
            machine.TryLever();
            var result = machine.Result;
            for (var reel = 0; reel < SlotMachine.ReelCount; reel++)
            {
                machine.Stop(reel, 0);
            }

            return result;
        }

        [Test]
        [Description("AC-040, D-57: 最初の early_games 通常ゲームはハズレ率を early_miss_reduction だけ下げ（85%→65%）、その後は設定の値に戻る")]
        public void Early_Games_Use_The_Reduced_Miss_Rate()
        {
            // 乱数 0.7: 65% では当たり、85% ではハズレ。コマ0は攻撃力アップ。
            var machine = new SlotMachine(Layout(DefaultReel), 0.85, new CoinWallet(100), Bet,
                SlotTestRules.Create(bigGames: 0, regGames: 0, earlyGames: 2, earlyMissReduction: 0.2), new ConstantRandom(0.7, 0));

            var results = Enumerable.Range(0, 3).Select(_ => PlayResult(machine)).ToArray();

            Assert.That(results, Is.EqualTo(new SlotSymbol?[] { SlotSymbol.Atk, SlotSymbol.Atk, null }));
        }

        [Test]
        [Description("D-57: ボーナス中のゲームは序盤のゲーム数に数えない")]
        public void Bonus_Games_Do_Not_Count_As_Early_Games()
        {
            // コマ5は 7。通常1回目で BIG（1ゲーム）、ボーナス、通常2回目も序盤で当たり（BIG）、ボーナス、通常3回目は通常のハズレ率。
            var machine = new SlotMachine(Layout(DefaultReel), 0.85, new CoinWallet(100), Bet,
                SlotTestRules.Create(bigGames: 1, earlyGames: 2, earlyMissReduction: 0.2), new ConstantRandom(0.7, 5));

            var results = Enumerable.Range(0, 5).Select(_ => PlayResult(machine)).ToArray();

            Assert.That(results,
                Is.EqualTo(new SlotSymbol?[] { SlotSymbol.Seven, SlotSymbol.Coin, SlotSymbol.Seven, SlotSymbol.Coin, null }));
        }
    }
}
