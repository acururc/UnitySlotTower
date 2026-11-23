using System;
using UnityEngine;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 戦闘マスの進行（<see cref="BattleNodeRun"/>）を組み立てて実時間で進める。スロット操作中も止まらない（FR-011）。
    /// 戦闘マスに入ると開始の演出（FR-019）の後すぐ戦闘が始まる。演出中は操作とスロットを受け付けない。
    /// ゲーム1回分の状態は RunController が持ち、ステージを始めるたびに <see cref="BeginStage"/> を呼ぶ（spec 002 FR-101）。
    /// ステージが始まるとフィールドの表示をそのマップに差し替える。
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class BattleController : MonoBehaviour
    {
        [Header("Tables")]
        [SerializeField] private TextAsset gameConfigTable;
        [SerializeField] private TextAsset unitsTable;
        [SerializeField] private TextAsset unitRangesTable;
        [SerializeField] private TextAsset unitUpgradesTable;
        [SerializeField] private TextAsset enemiesTable;
        [SerializeField] private TextAsset wavesTable;
        [SerializeField] private TextAsset stagesTable;
        [SerializeField] private TextAsset stageWavesTable;

        [SerializeField] private BattleFieldView fieldView;
        [Tooltip("揃った図柄を受け取るスロット筐体。")]
        [SerializeField] private SlotCabinet slotCabinet;
        [Tooltip("赤7の息吹が爆発したときに戦闘だけを止める実時間の秒数（ヒットストップ、spec 003 D-05）。")]
        [SerializeField, Min(0f)] private float hitStopSeconds = 0.1f;
        [Tooltip("青7で盤面が凍りついたときに戦闘だけを止める実時間の秒数（ヒットストップ、spec 004 D-06）。")]
        [SerializeField, Min(0f)] private float blueHitStopSeconds = 0.1f;

        private SimulationClock _clock;
        private float _hitStopLeft;
        private EnemyTable _enemies;
        private CsvTable _waves;
        private CsvTable _stageWaves;
        private StageTable _stages;
        private IRandom _random;
        private bool _completionReported;
        private bool _slotLocked;
        private float _appliedTimeScale = 1f;
        private bool _destroying;
        private float _victoryLeft;

        public bool IsShowingVictory => _victoryLeft > 0f;

        /// <summary>ヒットストップ中（戦闘の時間が進まない、spec 003 D-05）。</summary>
        public bool IsHitStopping => _hitStopLeft > 0f;

        /// <summary>赤7のダメージが入った（息吹の爆発、spec 003 FR-001）。戦闘のステップの中から呼ばれる。</summary>
        public event Action RedStruck;

        /// <summary>青7で盤面が凍りついた（停止の始まり、spec 004 FR-003）。戦闘のステップかレバーの結果の中から呼ばれる。</summary>
        public event Action BlueFroze;
        public string NodeKind { get; private set; }
        public float BaseFlashUntil { get; private set; }

        /// <summary>本拠地に入られたときの画面の縁の赤い光（FR-078）。Initialize で作る。</summary>
        public ScreenEdgeFlash EdgeFlash { get; private set; }

        /// <summary><see cref="Initialize"/> で読んだゲーム設定（RunState を作るのに使う）。</summary>
        public GameConfig Config { get; private set; }

        /// <summary><see cref="Initialize"/> で読んだ強化表（RunState を作るのに使う）。</summary>
        public UnitUpgradeTable UpgradeTable { get; private set; }

        public UnitTable Units { get; private set; }

        /// <summary>味方の攻撃範囲（一時停止中の説明の範囲図にも使う: T404）。</summary>
        public UnitRanges Ranges { get; private set; }

        /// <summary>いまの戦闘マス。戦闘マスが変わると別のものになる。最初の戦闘マスに入るまで null。</summary>
        public BattleNodeRun Run { get; private set; }

        /// <summary>いまのステージの番号（1 から）。</summary>
        public int StageNumber { get; private set; }

        /// <summary>ステージの数（stages.csv の行数）。<see cref="Initialize"/> で読む。</summary>
        public int StageCount => _stages?.Count ?? 0;

        /// <summary>このシーン（1回のプレイ）で入った戦闘マスの数。いまの戦闘マスが何番目か（説明の撤退の題目に使う）。</summary>
        public int NodeCount { get; private set; }

        /// <summary>新しい戦闘マスに入った（開始の演出が始まる）。</summary>
        public event Action NodeBegan;

        /// <summary>いまのステージが最後以外で終わり、勝利の帯が消えた（RunController が次のステージへ進める）。1ステージにつき1回。</summary>
        public event Action NodeCompleted;

        public CoinWallet Wallet => Run?.Wallet;
        public UnitSummoner Summoner => Run?.Summoner;
        public UnitUpgrades Upgrades => Run?.Upgrades;
        public Battlefield Field => Run?.Field;
        public UnitDeployment Deployment => Run?.Deployment;

        /// <summary>表を読んでいるか。</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 戦闘の表を読む（RunController の Start から呼ぶ。2回目以降は何もしない）。マップは BattleFieldView の Awake、
        /// スロットの規則は SlotCabinet の Awake で作られている。読めなければ例外。
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            if (fieldView == null || fieldView.Map == null)
            {
                throw new InvalidOperationException("戦闘フィールドのマップがない");
            }

            if (slotCabinet == null)
            {
                throw new InvalidOperationException("スロット筐体がない");
            }

            Config = GameConfig.Load(Parse(GameConfig.FileName, gameConfigTable));
            Units = UnitTable.Load(Parse(UnitTable.FileName, unitsTable));
            Ranges = UnitRanges.Load(Parse(UnitRanges.FileName, unitRangesTable), Units);
            UpgradeTable = UnitUpgradeTable.Load(Parse(UnitUpgradeTable.FileName, unitUpgradesTable), Units);
            _enemies = EnemyTable.Load(Parse(EnemyTable.FileName, enemiesTable));
            _waves = Parse(WaveSchedule.FileName, wavesTable);
            _stages = StageTable.Load(Parse(StageTable.FileName, stagesTable));
            _stageWaves = Parse(WaveSchedule.StageWavesFileName, stageWavesTable);
            _random = new SeededRandom((ulong)DateTime.UtcNow.Ticks);
            _clock = new SimulationClock { IsPaused = IsPaused };
            slotCabinet.Aligned += OnSlotAligned;
            slotCabinet.OutcomeFilter = CanRoll;
            EdgeFlash = ScreenEdgeFlash.Create();
            IsInitialized = true;
        }

        /// <summary>一時停止中か（FR-012）。時間が止まり、配置・撤退・強化・スロットの操作を受け付けない。</summary>
        public bool IsPaused { get; private set; }


        /// <summary>クリア（ボスマスを終えた）かゲームオーバーで、ゲームが終わったか（リザルトを出す: T402）。</summary>
        public bool IsFinished => Run != null && !IsShowingVictory &&
            (Run.Phase == WavePhase.GameOver || (Run.IsRunCleared && _completionReported));

        /// <summary>
        /// 戦闘側の操作（配置・撤退・強化）を受け付けないか。一時停止中・ゲーム終了後・開始の演出中（AC-016）・
        /// ステージの終了後（勝利の帯から次のステージまで）。
        /// </summary>
        public bool IsInputBlocked => IsPaused || IsShowingVictory || IsFinished || IsInIntro || (Run != null && Run.Phase == WavePhase.Completed);

        /// <summary>開始の演出中（FR-019）。時間は進まず、操作とスロットを受け付けない。</summary>
        public bool IsInIntro => Run != null && Run.Phase == WavePhase.Intro;

        /// <summary>一時停止を切り替える。スロットのリールやウェイトも止めるため Time.timeScale を 0 にする。ゲーム終了後は何もしない。</summary>
        public void SetPaused(bool paused)
        {
            if (IsFinished || IsShowingVictory)
            {
                return;
            }

            IsPaused = paused;
            if (_clock != null)
            {
                _clock.IsPaused = paused;
            }

            ApplyTimeScale();
            UpdateSlotLock();
        }

        public void TogglePause() => SetPaused(!IsPaused);

        /// <summary>倍速の最大（D-84: 1・2・3倍）。</summary>
        public const int MaxBattleSpeed = 3;

        /// <summary>
        /// 戦闘の速さ（FR-011、D-84）。1 から <see cref="MaxBattleSpeed"/>。戦闘マスに入るたびに 1 に戻る（D-96）。
        /// </summary>
        public int BattleSpeed { get; private set; } = 1;

        /// <summary>倍速のボタン: 1→2→3→1倍と変える。一時停止中・ゲーム終了後は受け付けない。</summary>
        public void CycleBattleSpeed()
        {
            if (IsPaused || IsFinished || IsShowingVictory)
            {
                return;
            }

            BattleSpeed = BattleSpeed % MaxBattleSpeed + 1;
            ApplyTimeScale();
        }

        /// <summary>
        /// 戦闘中に札を選んでから配置を終えるかやめるまで（FR-074、D-99）。このあいだ時間は倍速に関係なく
        /// <see cref="GameConfig.PlacementTimeScale"/> で進む。
        /// </summary>
        public bool IsPlacing => Run != null && Run.Phase == WavePhase.Fighting && Run.Deployment.Phase != DeployPhase.Idle;

        // Time.timeScale を書くのはここだけ（plan 17.5・18.1・18.7）。一時停止中・勝利の帯は 0、配置中は placement_time_scale、戦闘中は倍率、
        // それ以外（開始の演出・ルートなど）は 1。
        // 決まる値が変わったときだけ書く（テストが早送りに使う timeScale を毎フレーム上書きしない）。
        private void ApplyTimeScale()
        {
            var scale = _destroying ? 1f : IsPaused || IsShowingVictory ? 0f
                : IsPlacing ? (float)Config.PlacementTimeScale
                : Run != null && Run.Phase == WavePhase.Fighting ? BattleSpeed
                : 1f;
            if (scale != _appliedTimeScale)
            {
                _appliedTimeScale = scale;
                Time.timeScale = scale;
            }
        }

        private void UpdateSlotLock()
        {
            // Completed のルート選びではリールの停止入力が必要。
            _slotLocked = IsSlotInputBlocked;
            if (slotCabinet != null)
            {
                slotCabinet.InputLocked = _slotLocked;
            }
        }

        private bool IsSlotInputBlocked => IsPaused || IsFinished || IsInIntro || IsShowingVictory;

        private void OnDestroy()
        {
            // シーンを離れても時間が止まったまま・速いままにならないようにする。
            _destroying = true;
            ApplyTimeScale();
            if (Field != null)
            {
                Field.BaseReached -= OnBaseReached;
                Field.RedStruck -= OnRedStruck;
                Field.BlueFroze -= OnBlueFroze;
            }

            if (EdgeFlash != null) Destroy(EdgeFlash.gameObject);

            if (IsInitialized && slotCabinet != null)
            {
                slotCabinet.Aligned -= OnSlotAligned;
                slotCabinet.OutcomeFilter = null;
            }
        }

        private bool CanRoll(SlotSymbol symbol) => Run == null || Run.Field.Effects == null || Run.Field.Effects.CanRoll(symbol);

        /// <summary>
        /// <see cref="RunState.StageIndex"/> のステージを始める（spec 002 FR-101）。開始の演出に入り、フィールドの表示をそのマップに差し替える。
        /// </summary>
        public void BeginStage(RunState state)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("表を読む前にステージを始めた");
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var stage = _stages.Get(state.StageIndex);
            var isFinal = stage.Number == _stages.Count;
            var map = fieldView.LoadMap(stage.MapId);
            var schedule = WaveSchedule.Load(_waves, stage.Number, map, _enemies, _stageWaves);
            if (Field != null)
            {
                Field.BaseReached -= OnBaseReached;
                Field.RedStruck -= OnRedStruck;
                Field.BlueFroze -= OnBlueFroze;
            }

            _hitStopLeft = 0f;
            NodeKind = IntroKind(stage, isFinal);
            StageNumber = stage.Number;
            Run = new BattleNodeRun(Config, Units, Ranges, state, stage, isFinal, map,
                new WaveProgress(schedule, stage.Mode, Config.WaveGapSeconds), _random);
            NodeCount++;
            Field.BaseReached += OnBaseReached;
            Field.RedStruck += OnRedStruck;
            Field.BlueFroze += OnBlueFroze;
            BaseFlashUntil = 0f;
            EdgeFlash.Hide();
            _victoryLeft = 0f;
            BattleSpeed = 1;
            _completionReported = false;
            UpdateSlotLock();
            fieldView.Show(map);
            fieldView.Observe(Field);
            NodeBegan?.Invoke();
        }

        // 開始の演出の印（旧ルートのマスの種類の絵を流用: 最初・通常・中ボス・ボス）。
        private static string IntroKind(StageDefinition stage, bool isFinal)
        {
            if (stage.Number == 1) return RouteNodeKinds.First;
            if (stage.BossId == null) return RouteNodeKinds.Battle;
            return isFinal ? RouteNodeKinds.Boss : RouteNodeKinds.Elite;
        }

        // 揃った図柄の効果を戦闘へ渡す（FR-042）。
        private void OnSlotAligned(SlotSymbol symbol) => Run?.ApplySlot(symbol);

        private void Update()
        {
            if (Run == null)
            {
                return;
            }

            if (IsShowingVictory)
            {
                _victoryLeft = Mathf.Max(0f, _victoryLeft - Time.unscaledDeltaTime);
                if (!IsShowingVictory)
                {
                    _completionReported = true;
                    ApplyTimeScale();
                    if (!Run.IsRunCleared) NodeCompleted?.Invoke();
                }
            }
            else if (IsHitStopping)
            {
                // ヒットストップ（spec 003 D-05）: 戦闘だけを止める。Time.timeScale は触らないのでスロットと操作は動く。
                // 実時間で減らすが、一時停止中（timeScale 0）は減らさない。
                if (Time.timeScale > 0f) _hitStopLeft -= Time.unscaledDeltaTime;
            }
            else
            {
                Run.Advance(_clock.Advance(Time.deltaTime));
                if (Run.Phase == WavePhase.Completed && !_completionReported)
                    _victoryLeft = BattlePresentationTiming.VictoryBand;
            }

            // 勝利の帯・リザルトの間は時間が止まるので、縁の光を残さない。
            if (EdgeFlash.IsFlashing && (IsShowingVictory || IsFinished)) EdgeFlash.Hide();

            if (_slotLocked != IsSlotInputBlocked)
            {
                UpdateSlotLock();
            }

            ApplyTimeScale();
        }

        // 赤7の息吹が爆発した（spec 003 FR-002）: 戦闘を一瞬止め、表示へ知らせる。
        private void OnRedStruck()
        {
            _hitStopLeft = Mathf.Max(_hitStopLeft, hitStopSeconds);
            RedStruck?.Invoke();
        }

        // 青7で盤面が凍りついた（spec 004 FR-002）: 戦闘を一瞬止め、表示へ知らせる。赤7と重なったら長いほうを残す。
        private void OnBlueFroze()
        {
            _hitStopLeft = Mathf.Max(_hitStopLeft, blueHitStopSeconds);
            BlueFroze?.Invoke();
        }

        private void OnBaseReached(GridPoint cell)
        {
            BaseFlashUntil = Time.unscaledTime + BattlePresentationTiming.BaseFlash;
            EdgeFlash.Play();
        }

        private static CsvTable Parse(string fileName, TextAsset asset)
        {
            if (asset == null)
            {
                throw new InvalidOperationException($"{fileName} が設定されていない");
            }

            return CsvParser.Parse(fileName, asset.text);
        }
    }
}
