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
    /// ゲーム1回分の状態とルートは RunController（T350a）が持ち、戦闘マスに入るたびに <see cref="BeginNode"/> を呼ぶ。
    /// 戦闘マスが始まるとフィールドの表示をそのマップに差し替える。
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
        [SerializeField] private TextAsset encountersTable;
        [SerializeField] private TextAsset skillsTable;

        [SerializeField] private BattleFieldView fieldView;
        [Tooltip("揃った図柄を受け取るスロット筐体。")]
        [SerializeField] private SlotCabinet slotCabinet;

        private SimulationClock _clock;
        private EncounterTable _encounters;
        private EnemyTable _enemies;
        private CsvTable _waves;
        private IRandom _random;
        private bool _completionReported;
        private bool _slotLocked;
        private float _appliedTimeScale = 1f;

        /// <summary><see cref="Initialize"/> で読んだゲーム設定（RunState を作るのに使う）。</summary>
        public GameConfig Config { get; private set; }

        /// <summary><see cref="Initialize"/> で読んだ強化表（RunState を作るのに使う）。</summary>
        public UnitUpgradeTable UpgradeTable { get; private set; }

        public UnitTable Units { get; private set; }

        /// <summary>味方の攻撃範囲（一時停止中の説明の範囲図にも使う: T404）。</summary>
        public UnitRanges Ranges { get; private set; }

        /// <summary>色7とスキルの対応（skills.csv: T349）。操作バーのゲージが職から色を引く。</summary>
        public SkillTable Skills { get; private set; }

        /// <summary>いまの戦闘マス。戦闘マスが変わると別のものになる。最初の戦闘マスに入るまで null。</summary>
        public BattleNodeRun Run { get; private set; }

        /// <summary>いまの戦闘マスの階（1 から）。</summary>
        public int Floor { get; private set; }

        /// <summary>このシーン（1回のプレイ）で入った戦闘マスの数。いまの戦闘マスが何番目か（説明の撤退の題目に使う）。</summary>
        public int NodeCount { get; private set; }

        /// <summary>新しい戦闘マスに入った（開始の演出が始まる）。</summary>
        public event Action NodeBegan;

        /// <summary>いまの戦闘マスがボス以外で終わった（RunController が次のマスへ進める）。1マスにつき1回。</summary>
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
            _encounters = EncounterTable.Load(Parse(EncounterTable.FileName, encountersTable));
            Skills = SkillTable.Load(Parse(SkillTable.FileName, skillsTable));
            _random = new SeededRandom((ulong)DateTime.UtcNow.Ticks);
            _clock = new SimulationClock { IsPaused = IsPaused };
            slotCabinet.Aligned += OnSlotAligned;
            IsInitialized = true;
        }

        /// <summary>一時停止中か（FR-012）。時間が止まり、配置・撤退・強化・スロットの操作を受け付けない。</summary>
        public bool IsPaused { get; private set; }


        /// <summary>クリア（ボスマスを終えた）かゲームオーバーで、ゲームが終わったか（リザルトを出す: T402）。</summary>
        public bool IsFinished => Run != null && (Run.IsRunCleared || Run.Phase == WavePhase.GameOver);

        /// <summary>
        /// 戦闘側の操作（配置・撤退・強化・スキル）を受け付けないか。一時停止中・ゲーム終了後・開始の演出中（AC-016）・
        /// 戦闘マスの終了後（ルート画面: T350b）。
        /// </summary>
        public bool IsInputBlocked => IsPaused || IsFinished || IsInIntro || (Run != null && Run.Phase == WavePhase.Completed);

        /// <summary>開始の演出中（FR-019）。時間は進まず、操作とスロットを受け付けない。</summary>
        public bool IsInIntro => Run != null && Run.Phase == WavePhase.Intro;

        /// <summary>一時停止を切り替える。スロットのリールやウェイトも止めるため Time.timeScale を 0 にする。ゲーム終了後は何もしない。</summary>
        public void SetPaused(bool paused)
        {
            if (IsFinished)
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
        /// 戦闘の速さ（FR-011、D-84）。1 から <see cref="MaxBattleSpeed"/>。Game シーン（ゲーム1回分）の間は戦闘マスをまたいで引き継ぎ、
        /// 新しいゲーム（シーンの読み込み）で 1 に戻る。
        /// </summary>
        public int BattleSpeed { get; private set; } = 1;

        /// <summary>倍速のボタン: 1→2→3→1倍と変える。一時停止中・ゲーム終了後は受け付けない。</summary>
        public void CycleBattleSpeed()
        {
            if (IsPaused || IsFinished)
            {
                return;
            }

            BattleSpeed = BattleSpeed % MaxBattleSpeed + 1;
            ApplyTimeScale();
        }

        // Time.timeScale を書くのはここだけ（plan 17.5）。一時停止中は 0、戦闘中は倍率、それ以外（開始の演出・ルートなど）は 1。
        // 決まる値が変わったときだけ書く（テストが早送りに使う timeScale を毎フレーム上書きしない）。
        private void ApplyTimeScale()
        {
            var scale = IsPaused ? 0f : Run != null && Run.Phase == WavePhase.Fighting ? BattleSpeed : 1f;
            if (scale != _appliedTimeScale)
            {
                _appliedTimeScale = scale;
                Time.timeScale = scale;
            }
        }

        /// <summary>
        /// 文字なし説明（T403）を見せているあいだ true。戦闘の時間だけを止め、操作とスロット（Time.timeScale）は止めない。
        /// </summary>
        public bool IsHeldByGuide { get; set; }

        /// <summary>説明の撤退の題目を表示中は true（D-56）。このあいだの撤退は召喚コストの全額を返す。</summary>
        public bool FullRefundRetreat { get; set; }

        /// <summary>このシーン（1回のプレイ）でスキルを発動した回数（説明の進行に使う）。</summary>
        public int SkillActivations { get; private set; }

        /// <summary>
        /// その色のスキルを発動する（操作バーのゲージのクリック、FR-029）。一時停止中・ゲーム終了後は受け付けない（AC-011）。
        /// ほかの条件（満タン・配置1体以上・戦闘中）は <see cref="BattleNodeRun.TryActivateSkill"/>。発動したら true。
        /// </summary>
        public bool TryActivateSkill(SlotSymbol color)
        {
            if (IsInputBlocked || Run == null || Skills == null)
            {
                return false;
            }

            if (Run.TryActivateSkill(Skills.For(color)) != SkillResult.Activated)
            {
                return false;
            }

            SkillActivations++;
            return true;
        }

        private void UpdateSlotLock()
        {
            _slotLocked = IsPaused || IsFinished || IsInIntro;
            if (slotCabinet != null)
            {
                slotCabinet.InputLocked = _slotLocked;
            }
        }

        private void OnDestroy()
        {
            // シーンを離れても時間が止まったまま・速いままにならないようにする。
            if (_appliedTimeScale != 1f)
            {
                Time.timeScale = 1f;
            }

            if (IsInitialized && slotCabinet != null)
            {
                slotCabinet.Aligned -= OnSlotAligned;
            }
        }

        /// <summary>
        /// 戦闘マス（<paramref name="node"/>）に入る。種類と階が合う出現を選んで開始の演出に入り、フィールドの表示をそのマップに差し替える。
        /// 回数固定（<see cref="Encounter.FixedSpins"/>）を筐体へ渡す（0 ならコインで回す）。
        /// </summary>
        public void BeginNode(RunState state, RouteNode node)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("表を読む前に戦闘マスに入った");
            }

            var kind = RouteNodeKinds.Get(node.Kind).Encounter
                ?? throw new ArgumentException($"戦闘マスではない: {node.Kind}", nameof(node));
            var encounter = state.PickEncounter(_encounters, kind, node.Floor, _random);
            slotCabinet.SetFixedSpins(encounter.FixedSpins);
            var map = fieldView.LoadMap(encounter.MapId);
            Floor = node.Floor;
            Run = new BattleNodeRun(Config, Units, Ranges, state, encounter, map,
                WaveSchedule.Load(_waves, encounter.Id, map, _enemies), _random);
            NodeCount++;
            _completionReported = false;
            UpdateSlotLock();
            fieldView.Show(map);
            NodeBegan?.Invoke();
        }

        // 揃った図柄の効果を戦闘へ渡す（FR-042）。
        private void OnSlotAligned(SlotSymbol symbol) => Run?.ApplySlot(symbol);

        private void Update()
        {
            if (Run == null)
            {
                return;
            }

            Run.Advance(_clock.Advance(IsHeldByGuide ? 0d : Time.deltaTime));
            if (Run.Phase == WavePhase.Completed && !Run.IsRunCleared && !_completionReported)
            {
                _completionReported = true;
                NodeCompleted?.Invoke();
            }

            if (_slotLocked != (IsPaused || IsFinished || IsInIntro))
            {
                UpdateSlotLock();
            }

            ApplyTimeScale();
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
