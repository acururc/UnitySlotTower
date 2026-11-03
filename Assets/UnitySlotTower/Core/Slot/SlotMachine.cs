using System;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Slot
{
    /// <summary>
    /// 1台のスロットのゲーム進行（FR-040・FR-041、plan 5章 スロット）。レバーで掛け金を払ってハズレ抽選をし、
    /// 当たりなら配列の図柄比率で揃う図柄を決める。ストップは押した位置から結果に合う最初の位置で止める。
    /// </summary>
    public sealed class SlotMachine
    {
        public const int ReelCount = 3;

        private readonly double _missRate;
        private readonly CoinWallet _wallet;
        private readonly int _bet;
        private readonly IRandom _random;
        private readonly int[] _stops = new int[ReelCount];
        private readonly bool[] _stopped = new bool[ReelCount];

        public SlotMachine(ReelLayout layout, double missRate, CoinWallet wallet, int bet, IRandom random)
        {
            Layout = layout;
            _missRate = missRate;
            _wallet = wallet;
            _bet = bet;
            _random = random;
        }

        public ReelLayout Layout { get; }

        /// <summary>レバーオン。回転中か、所持コインが掛け金未満なら何もせず false（FR-040）。</summary>
        public bool TryLever()
        {
            if (IsSpinning || !_wallet.TrySpend(_bet))
            {
                return false;
            }

            // まずハズレを抽選し、当たりならコマを1つ等確率で選んでその図柄を揃える（マス数 ÷ 総マス数、D-43）。
            Result = _random.NextDouble() < _missRate ? (SlotSymbol?)null : Layout.At(_random.NextInt(0, Layout.Length));
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
            return stop;
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
