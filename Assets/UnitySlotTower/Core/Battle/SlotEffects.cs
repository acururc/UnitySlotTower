using System;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// 1つの戦闘の間だけ効く出目の効果（FR-112、plan 2.3）。停止（青7）・毒（黄7）・攻撃力（バー）は残りステップ数で持ち、
    /// <see cref="Battlefield.Advance"/> の中でだけ減る（倍速・スロー・一時停止に従う、E-26）。コインは所持金のドロップの倍率を上げる。
    /// 全体ダメージ（赤7）と回復（緑7）はその場の効果なので <see cref="Battlefield.ApplySlot"/> が直接出す。
    /// 戦闘ごとに新しく作る（持ち越さない、E-16）。値は slot_rules.csv。
    /// </summary>
    public sealed class SlotEffects
    {
        private readonly SlotRules _rules;
        private readonly int _poisonTickSteps;
        private int _poisonTickCount;

        // 上限との比較で浮動小数の誤差を吸収する幅。
        private const double CapEpsilon = 1e-9;

        public SlotEffects(SlotRules rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            StopSteps = StepsOf(rules.BlueStopSeconds);
            PoisonSteps = StepsOf(rules.YellowPoisonSeconds);
            AttackSteps = StepsOf(rules.BarSeconds);
            RedDelaySteps = Math.Max(0, StepsOf(rules.RedDelaySeconds));
            BlueDelaySteps = Math.Max(0, StepsOf(rules.BlueDelaySeconds));
            GreenDelaySteps = Math.Max(0, StepsOf(rules.GreenDelaySeconds));
            YellowDelaySteps = Math.Max(0, StepsOf(rules.YellowDelaySeconds));
            BarDelaySteps = Math.Max(0, StepsOf(rules.BarDelaySeconds));
            _poisonTickSteps = Math.Max(1, StepsOf(rules.YellowPoisonTickSeconds));
        }

        /// <summary>停止の長さ（ステップ）。表示の円ゲージの分母。</summary>
        public int StopSteps { get; }

        /// <summary>毒の長さ（ステップ）。</summary>
        public int PoisonSteps { get; }

        /// <summary>攻撃力アップの長さ（ステップ）。</summary>
        public int AttackSteps { get; }

        /// <summary>赤7が揃ってからダメージが入るまでのステップ（spec 003 FR-005）。0 ならすぐ。</summary>
        public int RedDelaySteps { get; }

        /// <summary>青7が揃ってから停止が始まる（盤面が凍りつく）までのステップ（spec 004 FR-003）。0 ならすぐ。</summary>
        public int BlueDelaySteps { get; }

        /// <summary>緑7が揃ってから回復する（光の柱が立つ）までのステップ（spec 005 FR-002）。0 ならすぐ。</summary>
        public int GreenDelaySteps { get; }

        /// <summary>黄7が揃ってから毒が始まる（毒の沼が弾ける）までのステップ（spec 006 FR-003）。0 ならすぐ。</summary>
        public int YellowDelaySteps { get; }

        /// <summary>沼が弾けるまでの残りステップ。0 なら待っていない。</summary>
        public int PoisonDelayStepsLeft { get; private set; }

        /// <summary>黄7が揃い、毒の沼が弾けるのを待っている（spec 006 FR-003）。</summary>
        public bool IsPoisonPending => PoisonDelayStepsLeft > 0;

        /// <summary>バーが揃ってから攻撃力アップが始まる（闘気が爆発する）までのステップ（spec 007 FR-003）。0 ならすぐ。</summary>
        public int BarDelaySteps { get; }

        /// <summary>闘気が爆発するまでの残りステップ。0 なら待っていない。</summary>
        public int AttackDelayStepsLeft { get; private set; }

        /// <summary>バーが揃い、闘気が爆発するのを待っている（spec 007 FR-003）。</summary>
        public bool IsAttackPending => AttackDelayStepsLeft > 0;

        /// <summary>凍りつくまでの残りステップ。0 なら待っていない。</summary>
        public int StopDelayStepsLeft { get; private set; }

        /// <summary>青7が揃い、凍りつくのを待っている（spec 004 FR-003）。</summary>
        public bool IsStopPending => StopDelayStepsLeft > 0;

        public int StopStepsLeft { get; private set; }
        public int PoisonStepsLeft { get; private set; }
        public int AttackStepsLeft { get; private set; }

        /// <summary>全ての敵が止まっている（移動も攻撃もしない、E-27）。</summary>
        public bool IsStopped => StopStepsLeft > 0;

        public bool IsPoisoned => PoisonStepsLeft > 0;
        public bool IsAttackUp => AttackStepsLeft > 0;

        /// <summary>コインが揃った回数。</summary>
        public int Coins { get; private set; }

        /// <summary>攻撃力の倍率: バーの効果中は bar_attack_rate、それ以外は1。</summary>
        public double AttackRate => IsAttackUp ? _rules.BarAttackRate : 1;

        /// <summary>赤7の全体ダメージ（防御を無視した固定値）。</summary>
        public int RedDamage => _rules.RedDamage;

        /// <summary>緑7の回復の率（最大 HP に掛ける）。</summary>
        public double GreenHealRate => _rules.GreenHealRate;

        /// <summary>毒の1回のダメージ（防御を無視した固定値）。</summary>
        public int PoisonDamage => _rules.YellowPoisonDamage;

        /// <summary>所持金のドロップの倍率: min(1 + coin_step × 回数, coin_cap)。</summary>
        public double MoneyRate => Tidy(Math.Min(1 + _rules.CoinStep * Coins, _rules.CoinCap));

        public void AddCoin() => Coins++;

        /// <summary>停止を始める。効果中なら長さを最初に戻す（ふつうは抽選で外れるので起きない）。</summary>
        public void StartStop() => StopStepsLeft = StopSteps;

        /// <summary>
        /// 青7が揃った: 待ちが 0 ならその場で停止を始めて true、そうでなければ凍りつくまでの待ちを入れて false（spec 004 FR-003）。
        /// 待ち中にもう一度来たら待ちを最初に戻す（ふつうは抽選で外れるので起きない）。
        /// </summary>
        public bool QueueStop()
        {
            if (BlueDelaySteps <= 0)
            {
                StartStop();
                return true;
            }

            StopDelayStepsLeft = BlueDelaySteps;
            return false;
        }

        /// <summary>凍りつくまでの待ちを1ステップ減らし、0 になったら停止を始めて true。<see cref="Step"/> の後に呼ぶ。</summary>
        internal bool StepStopDelay()
        {
            if (StopDelayStepsLeft <= 0 || --StopDelayStepsLeft > 0)
            {
                return false;
            }

            StartStop();
            return true;
        }

        /// <summary>毒を始める。最初のダメージは1間隔の後。</summary>
        public void StartPoison()
        {
            PoisonStepsLeft = PoisonSteps;
            _poisonTickCount = 0;
        }

        /// <summary>
        /// 黄7が揃った: 待ちが 0 ならその場で毒を始めて true、そうでなければ沼が弾けるまでの待ちを入れて false（spec 006 FR-003）。
        /// 待ち中にもう一度来たら待ちを最初に戻す（ふつうは抽選で外れるので起きない）。
        /// </summary>
        public bool QueuePoison()
        {
            if (YellowDelaySteps <= 0)
            {
                StartPoison();
                return true;
            }

            PoisonDelayStepsLeft = YellowDelaySteps;
            return false;
        }

        /// <summary>沼が弾けるまでの待ちを1ステップ減らし、0 になったら毒を始めて true。<see cref="Step"/> の後に呼ぶ。</summary>
        internal bool StepPoisonDelay()
        {
            if (PoisonDelayStepsLeft <= 0 || --PoisonDelayStepsLeft > 0)
            {
                return false;
            }

            StartPoison();
            return true;
        }

        public void StartAttack() => AttackStepsLeft = AttackSteps;

        /// <summary>
        /// バーが揃った: 待ちが 0 ならその場で攻撃力アップを始めて true、そうでなければ闘気が爆発するまでの待ちを入れて false（spec 007 FR-003）。
        /// 待ち中にもう一度来たら待ちを最初に戻す（ふつうは抽選で外れるので起きない）。
        /// </summary>
        public bool QueueAttack()
        {
            if (BarDelaySteps <= 0)
            {
                StartAttack();
                return true;
            }

            AttackDelayStepsLeft = BarDelaySteps;
            return false;
        }

        /// <summary>闘気が爆発するまでの待ちを1ステップ減らし、0 になったら攻撃力アップを始めて true。<see cref="Step"/> の後に呼ぶ。</summary>
        internal bool StepAttackDelay()
        {
            if (AttackDelayStepsLeft <= 0 || --AttackDelayStepsLeft > 0)
            {
                return false;
            }

            StartAttack();
            return true;
        }

        /// <summary>
        /// 発動中の出目（E-11）。青7・黄7・バーは残り時間がある間（青7は凍りつくのを待つ間も、spec 004 FR-005。黄7は沼が弾けるのを待つ間も、spec 006 FR-005。バーは闘気が爆発するのを待つ間も、spec 007 FR-005）、
        /// コインは倍率が上限に達したら true。赤7・緑7は常に false。
        /// </summary>
        public bool IsActive(SlotSymbol symbol)
        {
            switch (symbol)
            {
                case SlotSymbol.Blue7:
                    return IsStopped || IsStopPending;
                case SlotSymbol.Yellow7:
                    return IsPoisoned || IsPoisonPending;
                case SlotSymbol.Bar:
                    return IsAttackUp || IsAttackPending;
                case SlotSymbol.Coin:
                    return MoneyRate >= _rules.CoinCap - CapEpsilon;
                default:
                    return false;
            }
        }

        /// <summary>レバーの抽選に出してよい出目（<see cref="IsActive"/> でない）。</summary>
        public bool CanRoll(SlotSymbol symbol) => !IsActive(symbol);

        /// <summary>
        /// 戦闘の1ステップ分、残り時間を減らす。このステップで毒のダメージが入るなら true（毒の最後のステップも含む）。
        /// </summary>
        internal bool Step()
        {
            var poisonTick = false;
            if (PoisonStepsLeft > 0)
            {
                _poisonTickCount++;
                if (_poisonTickCount >= _poisonTickSteps)
                {
                    _poisonTickCount = 0;
                    poisonTick = true;
                }

                PoisonStepsLeft--;
            }

            if (StopStepsLeft > 0)
            {
                StopStepsLeft--;
            }

            if (AttackStepsLeft > 0)
            {
                AttackStepsLeft--;
            }

            return poisonTick;
        }

        /// <summary>所持金のドロップ量に倍率を掛けて四捨五入する（plan 16.2）。</summary>
        public int ApplyMoneyRate(int amount) =>
            (int)Math.Round(amount * MoneyRate, MidpointRounding.AwayFromZero);

        private static int StepsOf(double seconds) => (int)Math.Round(seconds * SimulationClock.StepsPerSecond);

        // 0.1 刻みの和の誤差（1.1000000000000001 など）で四捨五入がずれないよう、倍率を小数6桁にそろえる。
        private static double Tidy(double rate) => Math.Round(rate, 6);
    }
}
