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
        /// <summary>敵が出る前の配置時間（plan C-2）。<see cref="BattleNodeRun.Start"/> まで続く。</summary>
        Preparing,
        Fighting,
        /// <summary>この戦闘マスの最後のウェーブを終えた（ストック化と報酬は済んでいる）。</summary>
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
        /// <summary>準備中・戦闘中でない（終了後・ゲームオーバー）。</summary>
        NotInBattle,
    }

    /// <summary>
    /// 1つの戦闘マスの進行（FR-010, FR-013〜016、plan 16.6）。準備 → W1 → 準備（配置・HP保持）→ W2 → 終了。
    /// ライフ・お金・強化・ストック・スキルゲージは <see cref="RunState"/> を直接書き換え、戦闘（バー・コインの倍率を含む）・召喚・配置操作は
    /// このマスだけのもの。終了時に配置中の味方を種類ごとにストックへ足し、報酬を加える。ボスマスの終了でクリア。
    /// 回数固定（<see cref="Encounter.FixedSpins"/>）は Runtime がスロット（<see cref="SlotMachine.StartFixedSpins"/>）へ渡す。
    /// </summary>
    public sealed class BattleNodeRun
    {
        private readonly Func<int, WaveSchedule> _loadWave;

        private int _collectedCoins;
        private int _collectedMoney;
        private int _fieldLife;

        /// <param name="map">この戦闘マスのマップ（<see cref="Encounter.MapId"/>）。</param>
        /// <param name="loadWave">ウェーブ番号（1から）からそのウェーブの出現内容を返す。</param>
        public BattleNodeRun(GameConfig config, UnitTable units, UnitRanges ranges, RunState state, Encounter encounter,
            BattleMap map, Func<int, WaveSchedule> loadWave, IRandom random)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            Map = map ?? throw new ArgumentNullException(nameof(map));
            _loadWave = loadWave;
            Wave = 1;
            Phase = WavePhase.Preparing;
            _fieldLife = state.Life;
            Field = new Battlefield(loadWave(1), ranges, _fieldLife, random, new BattleBoosts(state.SlotRules));
            Summoner = new UnitSummoner(units, Wallet, config.DeployLimit, config.RetreatRefundRate, Stock)
            {
                IgnoresCooldown = true,
            };
            Deployment = new UnitDeployment(Map, units, ranges, Summoner, Field, Upgrades);
        }

        public Encounter Encounter { get; }

        /// <summary>戦闘マス内のウェーブ番号（1 から <see cref="WaveSchedule.WavesPerEncounter"/>）。</summary>
        public int Wave { get; private set; }

        public WavePhase Phase { get; private set; }

        /// <summary>ボスマスを終えた（最終ボス撃破、FR-010）。</summary>
        public bool IsRunCleared => Phase == WavePhase.Completed && Encounter.Kind == EncounterKind.Boss;

        /// <summary>ゲーム1回分の状態（plan 16.2）。</summary>
        public RunState State { get; }

        /// <summary>召喚・強化・撤退の返還に使う所持金（<see cref="RunState.Money"/>）。</summary>
        public CoinWallet Wallet => State.Money;

        public UnitUpgrades Upgrades => State.Upgrades;
        public UnitStock Stock => State.Stock;

        public BattleMap Map { get; }
        public Battlefield Field { get; }
        public UnitSummoner Summoner { get; }
        public UnitDeployment Deployment { get; }

        /// <summary>W2 の準備に入った（W1 は作った時点で準備中なので起きない）。</summary>
        public event Action WaveBegan;

        /// <summary>この戦闘マスを終えた（<see cref="WavePhase.Completed"/> か <see cref="WavePhase.GameOver"/>）。</summary>
        public event Action Ended;

        /// <summary>
        /// 揃った図柄の効果を渡す（FR-042、plan 16.4）。色7はその色のスキルゲージ（<see cref="RunState.Gauges"/>）、
        /// バー・コインはこの戦闘の倍率（<see cref="Battlefield.Boosts"/>）に足す。
        /// </summary>
        public void ApplySlot(SlotSymbol symbol)
        {
            switch (symbol)
            {
                case SlotSymbol.Bar:
                    Field.Boosts.AddBar();
                    break;
                case SlotSymbol.Coin:
                    Field.Boosts.AddCoin();
                    break;
                default:
                    State.Gauges.Add(symbol);
                    break;
            }
        }

        /// <summary>
        /// スキルを発動する（FR-029、AC-023）。準備中・戦闘中で、その色のゲージが満タンで、その職が1体以上配置されているときだけ、
        /// 配置中の全員に効果を出してゲージを0に戻す。拒否なら何も変えない。一時停止中の拒否は Runtime（T349）。
        /// </summary>
        public SkillResult TryActivateSkill(SkillDefinition skill)
        {
            if (Phase != WavePhase.Preparing && Phase != WavePhase.Fighting)
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

        /// <summary>準備中なら戦闘を始める（▶）。</summary>
        public void Start()
        {
            if (Phase != WavePhase.Preparing)
            {
                return;
            }

            Phase = WavePhase.Fighting;
            Summoner.IgnoresCooldown = false;
        }

        /// <summary>
        /// 戦闘中だけ時間を進める。撃破ドロップは落ちた時点でコイン／所持金に、ゴールに着いた敵の分はライフから引く（FR-013, FR-033）。
        /// ウェーブが終わったら残りのステップは捨てる（次のウェーブも準備から）。
        /// </summary>
        public void Advance(int steps)
        {
            for (var i = 0; i < steps && Phase == WavePhase.Fighting; i++)
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
                    EndWave();
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

        private void EndWave()
        {
            if (Wave < WaveSchedule.WavesPerEncounter)
            {
                BeginWave(Wave + 1);
                return;
            }

            End();
        }

        /// <summary>同じマップの次のウェーブの準備に入る。配置中の味方はそのまま残り、召喚の待ち時間は 0 に戻る（plan 4章）。</summary>
        private void BeginWave(int wave)
        {
            Field.BeginWave(_loadWave(wave));
            Wave = wave;
            Phase = WavePhase.Preparing;
            Summoner.IgnoresCooldown = true;
            Summoner.ResetCooldowns();
            WaveBegan?.Invoke();
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
