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
        /// <summary>敵が出る前の配置時間（plan C-2）。<see cref="WaveRun.Start"/> まで続く。</summary>
        Preparing,
        Fighting,
        /// <summary>最後のステージを終えた。</summary>
        Completed,
        GameOver,
    }

    /// <summary><see cref="WaveRun.TryActivateSkill"/> の結果（plan 16.4、AC-023）。</summary>
    public enum SkillResult
    {
        Activated,
        /// <summary>その色のゲージが満タンでない。</summary>
        NotFull,
        /// <summary>その職が1体も配置されていない。</summary>
        NoUnits,
        /// <summary>準備中・戦闘中でない（クリア後・ゲームオーバー）。</summary>
        NotInBattle,
    }

    /// <summary>
    /// ステージとウェーブの進行（FR-010, FR-013〜016、plan 4章）。ステージごとに 準備 → 戦闘 を各ウェーブで繰り返し、
    /// 最後のウェーブを終えるとステージ終了（ライフボーナス・ストック化）→ 次のステージの準備 と進める。
    /// ステージ内のウェーブ切り替えでは、配置・HP・ライフ・召喚の状態をそのまま残す。
    /// <see cref="RunState"/>（コイン・所持金・強化・ストック・スキルゲージ）はステージをまたいで同じものを使い、戦闘（バー・コインの倍率を含む）・召喚・配置操作はステージごとに作り直す（ライフも満タンに戻る）。
    /// </summary>
    public sealed class WaveRun
    {
        private readonly GameConfig _config;
        private readonly UnitTable _units;
        private readonly UnitRanges _ranges;
        private readonly int _stageCount;
        private readonly int _wavesPerStage;
        private readonly Func<int, BattleMap> _loadMap;
        private readonly Func<int, int, BattleMap, WaveSchedule> _loadWave;
        private readonly IRandom _random;

        private int _collectedCoins;
        private int _collectedMoney;

        /// <param name="stageCount">最後のステージの番号（1から）。</param>
        /// <param name="wavesPerStage">1ステージのウェーブ数。</param>
        /// <param name="loadMap">ステージ番号からそのステージのマップを返す。</param>
        /// <param name="loadWave">ステージ番号・ウェーブ番号・マップから、そのウェーブの出現内容を返す。</param>
        public WaveRun(GameConfig config, UnitTable units, UnitRanges ranges, RunState state,
            int stageCount, int wavesPerStage, Func<int, BattleMap> loadMap,
            Func<int, int, BattleMap, WaveSchedule> loadWave, IRandom random)
        {
            _config = config;
            _units = units;
            _ranges = ranges;
            State = state;
            Upgrades = state.Upgrades;
            Wallet = state.Money;
            _stageCount = stageCount;
            _wavesPerStage = wavesPerStage;
            _loadMap = loadMap;
            _loadWave = loadWave;
            _random = random;
            BeginStage(1);
        }

        public int Stage { get; private set; }

        /// <summary>ステージ内のウェーブ番号（1 から）。</summary>
        public int Wave { get; private set; }

        public WavePhase Phase { get; private set; }
        /// <summary>ゲーム1回分の状態（plan 16.2）。コイン・所持金・強化・ストックはここにある。</summary>
        public RunState State { get; }

        /// <summary>召喚・強化・撤退の返還に使う所持金（<see cref="RunState.Money"/>）。</summary>
        public CoinWallet Wallet { get; }

        public UnitUpgrades Upgrades { get; }
        public UnitStock Stock => State.Stock;

        /// <summary>
        /// 揃った図柄の効果を渡す（FR-042、plan 16.4）。色7はその色のスキルゲージ（<see cref="RunState.Gauges"/>）、
        /// バー・コインはいまの戦闘の倍率（<see cref="Battlefield.Boosts"/>）に足す。
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

        /// <summary>いまのステージのマップ。</summary>
        public BattleMap Map { get; private set; }

        public Battlefield Field { get; private set; }
        public UnitSummoner Summoner { get; private set; }
        public UnitDeployment Deployment { get; private set; }

        /// <summary>新しいステージの準備に入った（マップが変わり、戦闘・召喚・配置操作が作り直された）。続けて <see cref="WaveBegan"/> も起きる。</summary>
        public event Action StageBegan;

        /// <summary>新しいウェーブの準備に入った（ステージの最初のウェーブを含む）。</summary>
        public event Action WaveBegan;

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
        /// 戦闘中だけ時間を進める。撃破ドロップは落ちた時点でコイン／所持金に入れる（FR-033、plan 16.2）。ウェーブが終わったら残りのステップは捨てる
        /// （次のウェーブも準備から）。
        /// </summary>
        public void Advance(int steps)
        {
            for (var i = 0; i < steps && Phase == WavePhase.Fighting; i++)
            {
                Field.Advance(1);
                Summoner.Advance(1);
                CollectDrops();

                if (Field.IsGameOver)
                {
                    Phase = WavePhase.GameOver;
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

        private void EndWave()
        {
            if (Wave < _wavesPerStage)
            {
                BeginWave(Wave + 1);
                return;
            }

            EndStage();
        }

        /// <summary>
        /// 配置中の味方を種類ごとに無料ストックへ足す（FR-015。ライフボーナスは 2026-11-14 に廃止）。
        /// フィールドは次のステージで作り直すので、味方はそこで全員外れ（返還なし）、ライフは満タンに戻る（FR-013）。
        /// </summary>
        private void EndStage()
        {
            foreach (var unit in Field.Units)
            {
                Stock.Add(unit.Definition.Id, 1);
            }

            if (Stage >= _stageCount)
            {
                Phase = WavePhase.Completed;
                return;
            }

            BeginStage(Stage + 1);
        }

        private void BeginStage(int stage)
        {
            var map = _loadMap(stage);
            var schedule = _loadWave(stage, 1, map);
            Stage = stage;
            Wave = 1;
            Phase = WavePhase.Preparing;
            Map = map;
            Field = new Battlefield(schedule, _ranges, _config.Life, _random, new BattleBoosts(State.SlotRules));
            _collectedCoins = 0;
            _collectedMoney = 0;
            Summoner = new UnitSummoner(_units, Wallet, _config.DeployLimit, _config.RetreatRefundRate, Stock)
            {
                IgnoresCooldown = true,
            };
            Deployment = new UnitDeployment(Map, _units, _ranges, Summoner, Field, Upgrades);
            StageBegan?.Invoke();
            WaveBegan?.Invoke();
        }

        /// <summary>同じステージの次のウェーブの準備に入る。配置中の味方はそのまま残り、召喚の待ち時間は 0 に戻る（plan 4章）。</summary>
        private void BeginWave(int wave)
        {
            Field.BeginWave(_loadWave(Stage, wave, Map));
            Wave = wave;
            Phase = WavePhase.Preparing;
            Summoner.IgnoresCooldown = true;
            Summoner.ResetCooldowns();
            WaveBegan?.Invoke();
        }
    }
}
