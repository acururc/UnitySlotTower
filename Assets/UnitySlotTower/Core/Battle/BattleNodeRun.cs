using System;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Core.Battle
{
    public enum WavePhase
    {
        /// <summary>開始の演出（FR-019、plan 17.2）。戦闘の時間は進まない（操作の拒否は Runtime）。</summary>
        Intro,
        Fighting,
        /// <summary>このステージを終えた。</summary>
        Completed,
        GameOver,
    }

    /// <summary>
    /// 1つのステージの戦闘の進行（FR-010, FR-013〜016, FR-019、plan 17.2、spec 002 FR-101）。開始の演出 → 戦闘（1ウェーブ）→ 終了。
    /// ライフ・お金・強化は <see cref="RunState"/> を直接書き換え、戦闘（バー・コインの倍率を含む）・召喚・配置操作は
    /// このステージだけのもの（spec 002 FR-105・FR-106: 配置と効果は持ち越さない）。ステージ1では最初の1体が無料（FR-108）。
    /// 報酬はなく、最後のステージの終了でクリア。
    /// </summary>
    public sealed class BattleNodeRun
    {
        private int _collectedCoins;
        private int _collectedMoney;
        private int _fieldLife;
        private readonly int _introSteps;
        private int _introStepsLeft;
        private readonly bool _isFinal;

        /// <summary>
        /// ステージの戦闘（spec 002 FR-101、T607）。報酬はなく、<paramref name="isFinal"/>（最後のステージ）を終えたときだけクリア。
        /// </summary>
        /// <param name="map">このステージのマップ（<see cref="StageDefinition.MapId"/>）。</param>
        /// <param name="waves">このステージのウェーブの進行（FR-102）。</param>
        public BattleNodeRun(GameConfig config, UnitTable units, UnitRanges ranges, RunState state, StageDefinition stage,
            bool isFinal, BattleMap map, WaveProgress waves, IRandom random)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Stage = stage ?? throw new ArgumentNullException(nameof(stage));
            _isFinal = isFinal;
            Map = map ?? throw new ArgumentNullException(nameof(map));
            _introSteps = Math.Max(0, (int)Math.Round(config.IntroSeconds * SimulationClock.StepsPerSecond));
            _introStepsLeft = _introSteps;
            Phase = _introSteps > 0 ? WavePhase.Intro : WavePhase.Fighting;
            _fieldLife = state.Life;
            Field = new Battlefield(waves, ranges, _fieldLife, random, new SlotEffects(state.SlotRules))
            {
                EnemyHpRate = stage.EnemyHpRate,
            };
            Summoner = new UnitSummoner(units, Wallet, config.DeployLimit, config.RetreatRefundRate,
                state.FreeSummons);
            Deployment = new UnitDeployment(Map, units, ranges, Summoner, Field, Upgrades);
        }

        /// <summary>このステージ。</summary>
        public StageDefinition Stage { get; }

        public WavePhase Phase { get; private set; }

        /// <summary>開始の演出の進み具合（0 から 1、表示用）。演出がなければ 1。</summary>
        public double IntroProgress => _introSteps == 0 ? 1d : 1d - (double)_introStepsLeft / _introSteps;

        /// <summary>最後のステージを終えた（spec 002 FR-101）。</summary>
        public bool IsRunCleared => Phase == WavePhase.Completed && _isFinal;

        /// <summary>ゲーム1回分の状態（plan 16.2）。</summary>
        public RunState State { get; }

        /// <summary>召喚・強化・撤退の返還に使う所持金（<see cref="RunState.Money"/>）。</summary>
        public CoinWallet Wallet => State.Money;

        public UnitUpgrades Upgrades => State.Upgrades;

        /// <summary>
        /// 残りの敵の数（FR-009、AC-017）: まだ出ていない敵と、フィールドにいる（倒していない・ゴールしていない）敵の合計。
        /// </summary>
        public int RemainingEnemies => Field.RemainingEnemies;

        public BattleMap Map { get; }
        public Battlefield Field { get; }
        public UnitSummoner Summoner { get; }
        public UnitDeployment Deployment { get; }

        /// <summary>この戦闘マスを終えた（<see cref="WavePhase.Completed"/> か <see cref="WavePhase.GameOver"/>）。</summary>
        public event Action Ended;

        /// <summary>
        /// 揃った図柄の効果をこの戦闘にすぐ出す（FR-112、<see cref="Battlefield.ApplySlot"/>）。
        /// </summary>
        public void ApplySlot(SlotSymbol symbol)
        {
            Field.ApplySlot(symbol);
        }

        /// <summary>
        /// 時間を進める。開始の演出中は演出の残りだけを減らし、終わったら残りのステップで戦闘を進める（FR-019）。
        /// 撃破ドロップは落ちた時点でコイン／所持金に、ゴールに着いた敵の分はライフから引く（FR-013, FR-033）。
        /// 戦闘マスが終わったら残りのステップは捨てる。
        /// </summary>
        public void Advance(int steps)
        {
            var i = 0;
            for (; i < steps && Phase == WavePhase.Intro; i++)
            {
                if (--_introStepsLeft <= 0)
                {
                    Phase = WavePhase.Fighting;
                }
            }

            for (; i < steps && Phase == WavePhase.Fighting; i++)
            {
                Field.Advance(1);
                Summoner.Advance(1);
                CollectDrops();
                CollectLifeLoss();

                if (State.IsGameOver)
                {
                    Phase = WavePhase.GameOver;
                    Ended?.Invoke();
                }
                else if (Field.IsCleared)
                {
                    End();
                }
            }
        }

        private void CollectDrops()
        {
            var coins = Field.DroppedCoins;
            if (coins > _collectedCoins)
            {
                State.Coins.Add(coins - _collectedCoins);
                _collectedCoins = coins;
            }

            var money = Field.DroppedMoney;
            if (money > _collectedMoney)
            {
                State.Money.Add(money - _collectedMoney);
                _collectedMoney = money;
            }
        }

        private void CollectLifeLoss()
        {
            if (Field.Life < _fieldLife)
            {
                State.LoseLife(_fieldLife - Field.Life);
                _fieldLife = Field.Life;
            }
        }

        /// <summary>ステージを終える。配置中の味方はストックにならない（spec 002 FR-106）。</summary>
        private void End()
        {
            Phase = WavePhase.Completed;
            Ended?.Invoke();
        }
    }
}
