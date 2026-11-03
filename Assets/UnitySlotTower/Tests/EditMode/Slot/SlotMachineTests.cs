using System;
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
        private const string DefaultReel = "seven atk coin heal speed coin bar atk coin heal speed coin";

        private static ReelLayout Layout(string ids) =>
            new ReelLayout(ids.Split(' ').Select(id => (SlotSymbol)Array.IndexOf(SlotSymbols.Ids, id)).ToArray());

        private static SlotMachine Machine(CoinWallet wallet, double missRate = 0.85) =>
            new SlotMachine(Layout(DefaultReel), missRate, wallet, Bet, new SeededRandom(1));

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
        private static SlotMachine Fixed(SlotSymbol? result, CoinWallet wallet)
        {
            var layout = Layout(DefaultReel);
            var cell = result.HasValue ? Enumerable.Range(0, layout.Length).First(i => layout.At(i) == result.Value) : 0;
            return new SlotMachine(layout, result.HasValue ? 0.0 : 1.0, wallet, Bet, new FixedRandom(cell));
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
        [Description("AC-041, FR-041: 全押し順×全押し位置で、中段の出目が決まった結果（揃う図柄／ハズレ）と一致する")]
        public void Every_Press_Order_And_Position_Shows_The_Decided_Result(SlotSymbol? result)
        {
            const int length = 12;
            var machine = Fixed(result, new CoinWallet(int.MaxValue));
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

                    var middle = Enumerable.Range(0, SlotMachine.ReelCount)
                        .Select(r => machine.Layout.At(machine.StopPosition(r))).ToArray();
                    var aligned = middle.All(s => s == middle[0]);
                    if (result.HasValue ? !(aligned && middle[0] == result.Value) : aligned)
                    {
                        mismatches++;
                    }
                }
            }

            Assert.That(mismatches, Is.Zero);
        }

        [Test]
        [Description("plan 5章: 当たりは押した位置から下へ進めて、結果の図柄が来る最初の位置で止める（既定配列の BAR はコマ6、最大11コマ）")]
        public void Hit_Stops_At_The_First_Matching_Cell_From_The_Press()
        {
            var machine = Fixed(SlotSymbol.Bar, new CoinWallet(10));
            machine.TryLever();

            Assert.That((machine.Stop(0, 9), machine.Stop(1, 5), machine.Stop(2, 6)), Is.EqualTo((6, 6, 6)));
        }

        [Test]
        [Description("plan 5章: ハズレは押した位置で止め、最後のリールで揃うときだけ揃わない最初の位置まで進める")]
        public void Miss_Stops_At_The_Press_Unless_The_Last_Reel_Would_Align()
        {
            var machine = Fixed(null, new CoinWallet(10));
            machine.TryLever();

            // コマ2・5・8・11 はコイン。左と右をコマ2で止め、中をコマ5で押すと揃うので、下のコマ4（攻撃速度）で止まる。
            Assert.That((machine.Stop(0, 2), machine.Stop(2, 2), machine.Stop(1, 5)), Is.EqualTo((2, 2, 4)));
        }

        [Test]
        [Description("AC-041, D-44: 押す位置を変えても、同じシードなら結果の列は変わらない")]
        public void Press_Positions_Do_Not_Change_The_Results()
        {
            SlotSymbol?[] Results(Func<int, int, int> press)
            {
                var machine = new SlotMachine(Layout(DefaultReel), 0.7, new CoinWallet(int.MaxValue), Bet, new SeededRandom(5));
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
    }
}
