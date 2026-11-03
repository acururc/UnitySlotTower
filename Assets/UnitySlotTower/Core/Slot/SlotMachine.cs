using System;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>ボーナスの種類（D-33/D-40）。</summary>
    public enum SlotBonus
    {
        None,

        /// <summary>7・7・7 で入る。</summary>
        Big,

        /// <summary>BAR・BAR・BAR で入る。BIG より払い出しが小さい。</summary>
        Reg,
    }

    /// <summary>
    /// 1台のスロットのゲーム進行（FR-040・FR-041、plan 5章 スロット）。レバーで掛け金を払ってハズレ抽選をし、
    /// 当たりなら配列の図柄比率で揃う図柄を決める。ストップは押した位置から結果に合う最初の位置で止める。
    /// 最後のリールが止まったら払い出しとボーナス（FR-042・FR-043）を精算し、揃った図柄を <see cref="Aligned"/> で通知する。
    /// </summary>
    public sealed class SlotMachine
    {
        public const int ReelCount = 3;

        private readonly double _missRate;
        private readonly CoinWallet _wallet;
        private readonly int _bet;
        private readonly IRandom _random;
        private readonly SlotRules _rules;
        private readonly int[] _stops = new int[ReelCount];
        private readonly bool[] _stopped = new bool[ReelCount];

        public SlotMachine(ReelLayout layout, double missRate, CoinWallet wallet, int bet, SlotRules rules, IRandom random)
        {
            Layout = layout;
            _missRate = missRate;
            _wallet = wallet;
            _bet = bet;
            _random = random;
            _rules = rules;
        }

        public ReelLayout Layout { get; private set; }

        /// <summary>リールのカスタムで確定した配列に替える（FR-046）。3本とも同じ配列。回転中は替えられない。</summary>
        public void ChangeLayout(ReelLayout layout)
        {
            if (IsSpinning)
            {
                throw new InvalidOperationException("回転中は配列を替えられない");
            }

            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        /// <summary>いまのボーナス状態（D-33/D-40）。</summary>
        public SlotBonus Bonus { get; private set; }

        /// <summary>ボーナスの残りゲーム数。通常時は 0。</summary>
        public int BonusGamesLeft { get; private set; }

        /// <summary>直近のゲームの払い出し枚数。レバーオンで 0 に戻し、最後のリールが止まったときに決まる。</summary>
        public int LastPayout { get; private set; }

        /// <summary>最後のリールが止まって図柄が揃ったときに、その図柄を通知する（ハズレでは呼ばない）。払い出しの後に呼ぶ。</summary>
        public event Action<SlotSymbol> Aligned;

        /// <summary>
        /// レバーオン。回転中か、所持コインが掛け金未満なら何もせず false（FR-040）。
        /// ボーナス中は掛け金なしで回り、ハズレ抽選なしでコインが揃う（plan 5章）。
        /// </summary>
        public bool TryLever()
        {
            if (IsSpinning)
            {
                return false;
            }

            if (Bonus != SlotBonus.None)
            {
                Result = SlotSymbol.Coin;
            }
            else if (_wallet.TrySpend(_bet))
            {
                // まずハズレを抽選し、当たりならコマを1つ等確率で選んでその図柄を揃える（マス数 ÷ 総マス数、D-43）。
                Result = _random.NextDouble() < _missRate
                    ? (SlotSymbol?)null
                    : Layout.At(_random.NextInt(0, Layout.Length));
            }
            else
            {
                return false;
            }

            LastPayout = 0;
            for (var i = 0; i < ReelCount; i++)
            {
                _stopped[i] = false;
            }

            IsSpinning = true;
            return true;
        }

        /// <summary>レバーオンからすべてのリールが止まるまで true。</summary>
        public bool IsSpinning { get; private set; }

        /// <summary>直近のレバーオンで決まった結果。揃う図柄、ハズレなら null（D-43）。</summary>
        public SlotSymbol? Result { get; private set; }

        public bool IsStopped(int reel) => _stopped[reel];

        /// <summary>リールの停止位置（中段のコマ）。</summary>
        public int StopPosition(int reel) => _stops[reel];

        /// <summary>
        /// リールを押した位置から下へ（コマ番号が減る向きに）進め、結果に合う最初の位置で止める。停止位置を返す（plan 5章）。
        /// 当たりは中段に結果の図柄を止める。ハズレは押した位置で止め、最後のリールで中段が揃うときだけ揃わない位置まで進める。
        /// </summary>
        public int Stop(int reel, int pressPosition)
        {
            if (!IsSpinning || _stopped[reel])
            {
                throw new InvalidOperationException("回転中のリールではない");
            }

            var press = Layout.Wrap(pressPosition);
            var isLast = Array.IndexOf(_stopped, false) == Array.LastIndexOf(_stopped, false);
            var stop = press;
            for (var slide = 0; slide < Layout.Length; slide++)
            {
                stop = Layout.Wrap(press - slide);
                if (Fits(reel, Layout.At(stop), isLast))
                {
                    break;
                }
            }

            _stops[reel] = stop;
            _stopped[reel] = true;
            IsSpinning = !isLast;
            if (isLast)
            {
                Settle();
            }

            return stop;
        }

        /// <summary>
        /// 最後のリールが止まったときの精算（FR-042・FR-043）。コイン揃いは払い出し、7・BAR揃いはボーナスに入る。
        /// ボーナス中のゲームはボーナスの払い出しを払って残りゲーム数を減らす。効果の図柄は払い出しなし（戦闘への効果は <see cref="Aligned"/> で渡す）。
        /// </summary>
        private void Settle()
        {
            if (!Result.HasValue)
            {
                return;
            }

            var symbol = Result.Value;
            if (Bonus != SlotBonus.None)
            {
                Pay(Bonus == SlotBonus.Big ? _rules.BigPayout : _rules.RegPayout);
                BonusGamesLeft--;
                if (BonusGamesLeft <= 0)
                {
                    Bonus = SlotBonus.None;
                    BonusGamesLeft = 0;
                }
            }
            else if (symbol == SlotSymbol.Coin)
            {
                Pay(_rules.CoinPayout);
            }
            else if (symbol == SlotSymbol.Seven)
            {
                StartBonus(SlotBonus.Big, _rules.BigGames);
            }
            else if (symbol == SlotSymbol.Bar)
            {
                StartBonus(SlotBonus.Reg, _rules.RegGames);
            }

            Aligned?.Invoke(symbol);
        }

        private void Pay(int coins)
        {
            _wallet.Add(coins);
            LastPayout = coins;
        }

        private void StartBonus(SlotBonus bonus, int games)
        {
            if (games > 0)
            {
                Bonus = bonus;
                BonusGamesLeft = games;
            }
        }

        private bool Fits(int reel, SlotSymbol symbol, bool isLast)
        {
            if (Result.HasValue)
            {
                return symbol == Result.Value;
            }

            if (!isLast)
            {
                return true;
            }

            // 最後のリール: ほかの2本の中段が同じ図柄なら、それと違う図柄で止める。
            var first = -1;
            for (var i = 0; i < ReelCount; i++)
            {
                if (i == reel)
                {
                    continue;
                }

                if (first < 0)
                {
                    first = i;
                }
                else if (Layout.At(_stops[first]) == Layout.At(_stops[i]))
                {
                    return symbol != Layout.At(_stops[i]);
                }
            }

            return true;
        }
    }
}
