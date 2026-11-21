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
        /// <summary>開始の演出（FR-019、plan 17.2）。戦闘の時間は進まず、スキルは拒否する（ほかの操作の拒否は Runtime）。</summary>
        Intro,
        Fighting,
        /// <summary>この戦闘マスを終えた（ストック化と報酬は済んでいる）。</summary>
        Completed,
        GameOver,
    }

    /// <summary><see cref="BattleNodeRun.TryActivateSkill"/> の結果（plan 16.4、AC-023）。</summary>
    public enum SkillResult
    {
        Activated,
        /// <summary>その色のゲージが満タンでない。</summary>
        NotFull,
        /// <summary>その職が1体も配置されていない。</summary>
        NoUnits,
        /// <summary>戦闘中でない（開始の演出中・終了後・ゲームオーバー）。</summary>
        NotInBattle,
    }

    /// <summary>
    /// 1つの戦闘マスの進行（FR-010, FR-013〜016, FR-019、plan 17.2）。開始の演出 → 戦闘（1ウェーブ）→ 終了。
    /// ライフ・お金・強化・ストック・スキルゲージは <see cref="RunState"/> を直接書き換え、戦闘（バー・コインの倍率を含む）・召喚・配置操作は
    /// このマスだけのもの。終了時に配置中の味方を種類ごとにストックへ足し、報酬を加える。ボスマスの終了でクリア。
    /// </summary>
    public sealed class BattleNodeRun
    {
        private int _collectedCoins;
        private int _collectedMoney;
        private int _fieldLife;
        private readonly int _introSteps;
        private int _introStepsLeft;

        /// <param name="map">この戦闘マスのマップ（<see cref="Encounter.MapId"/>）。</param>
        /// <param name="wave">この戦闘マスの出現内容。</param>
        public BattleNodeRun(GameConfig config, UnitTable units, UnitRanges ranges, RunState state, Encounter encounter,
            BattleMap map, WaveSchedule wave, IRandom random)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            Map = map ?? throw new ArgumentNullException(nameof(map));
            _introSteps = Math.Max(0, (int)Math.Round(config.IntroSeconds * SimulationClock.StepsPerSecond));
            _introStepsLeft = _introSteps;
            Phase = _introSteps > 0 ? WavePhase.Intro : WavePhase.Fighting;
            _fieldLife = state.Life;
            Field = new Battlefield(wave, ranges, _fieldLife, random, new SlotEffects(state.SlotRules));
            Summoner = new UnitSummoner(units, Wallet, config.DeployLimit, config.RetreatRefundRate, Stock);
            Deployment = new UnitDeployment(Map, units, ranges, Summoner, Field, Upgrades);
        }

        public Encounter Encounter { get; }

        public WavePhase Phase { get; private set; }

        /// <summary>開始の演出の進み具合（0 から 1、表示用）。演出がなければ 1。</summary>
        public double IntroProgress => _introSteps == 0 ? 1d : 1d - (double)_introStepsLeft / _introSteps;

        /// <summary>ボスマスを終えた（最終ボス撃破、FR-010）。</summary>
        public bool IsRunCleared => Phase == WavePhase.Completed && Encounter.Kind == EncounterKind.Boss;

        /// <summary>ゲーム1回分の状態（plan 16.2）。</summary>
        public RunState State { get; }

        /// <summary>召喚・強化・撤退の返還に使う所持金（<see cref="RunState.Money"/>）。</summary>
        public CoinWallet Wallet => State.Money;

        public UnitUpgrades Upgrades => State.Upgrades;
        public UnitStock Stock => State.Stock;

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
        /// 揃った図柄の効果をこの戦闘にすぐ出す（FR-112、<see cref="Battlefield.ApplySlot"/>）。スキルゲージはもうたまらない（E-14）。
        /// </summary>
        public void ApplySlot(SlotSymbol symbol)
        {
            Field.ApplySlot(symbol);
        }

        /// <summary>
        /// スキルを発動する（FR-029、AC-023）。戦闘中で、その色のゲージが満タンで、その職が1体以上配置されているときだけ、
        /// 配置中の全員に効果を出してゲージを0に戻す。拒否なら何も変えない。一時停止中の拒否は Runtime（T349）。
        /// </summary>
        public SkillResult TryActivateSkill(SkillDefinition skill)
        {
            if (Phase != WavePhase.Fighting)
            {
                return SkillResult.NotInBattle;
            }

            if (!State.Gauges.IsFull(skill.Color))
            {
                return SkillResult.NotFull;
            }

            if (!Field.ActivateSkill(skill))
            {
                return SkillResult.NoUnits;
            }

            State.Gauges.Clear(skill.Color);
            return SkillResult.Activated;
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

        /// <summary>配置中の味方を種類ごとに無料ストックへ足し（FR-015）、報酬を加える（plan 16.2）。</summary>
        private void End()
        {
            foreach (var unit in Field.Units)
            {
                Stock.Add(unit.Definition.Id, 1);
            }

            State.Money.Add(Encounter.RewardMoney);
            State.Coins.Add(Encounter.RewardCoins);
            Phase = WavePhase.Completed;
            Ended?.Invoke();
        }
    }
}
