using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Flow;

namespace UnitySlotTower.Slot
{
    /// <summary>
    /// 下部のスロット筐体の仮表示（T110・T315、plan 2・3・5章）。ベット＋レバー（Space）で回し、J/K/L かボタンで止める。
    /// 有効ラインは中段の1本。数字: クレジット・直前の払い出し・ボーナスの残りゲーム数。ボーナス中はランプが BIG/REG の色で点く。
    /// </summary>
    public sealed class SlotCabinet : MonoBehaviour
    {
        private const string ActionMap = "SlotTower";
        private static readonly string[] StopActions = { "StopLeft", "StopCenter", "StopRight" };

        [Header("Tables")]
        [SerializeField] private TextAsset gameConfigTable;
        [SerializeField] private TextAsset settingsTable;
        [SerializeField] private TextAsset rulesTable;
        [FormerlySerializedAs("defaultReelTable")]
        [SerializeField] private TextAsset lockedReelTable;

        [Tooltip("難易度（設定1〜6）。Game シーンを直接開いたときだけ使う。難易度選択を通ったときはその設定（T401）。")]
        [SerializeField, Range(1, 6)] private int setting = 1;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Button leverButton;
        [SerializeField] private Button[] stopButtons = new Button[SlotMachine.ReelCount];

        [Header("View")]
        [SerializeField] private SlotReelView[] reels = new SlotReelView[SlotMachine.ReelCount];
        [SerializeField] private Graphic bonusLamp;
        [SerializeField] private Graphic coinIcon;
        [SerializeField] private TMP_Text creditText;
        [SerializeField] private TMP_Text payoutText;
        [FormerlySerializedAs("counterText")]
        [SerializeField] private TMP_Text bonusGamesText;
        [SerializeField] private Color lampOffColor = new Color(0.18f, 0.18f, 0.2f);
        [SerializeField] private Color bigColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private Color regColor = new Color(0.3f, 0.55f, 1f);

        private const double CoinBlinkSeconds = 1.0;

        private SlotMachine _machine;
        private CoinWallet _wallet;
        private ReelLayout _initialLayout;
        private SlotWait _wait;
        private InputAction _lever;
        private readonly InputAction[] _stops = new InputAction[SlotMachine.ReelCount];
        private double _coinBlinkUntil;
        private int _shownCredit = -1;
        private int _shownPayout = -1;
        private int _shownBonusGames = -1;
        private SlotBonus? _shownBonus;

        /// <summary>
        /// テスト用: null でなければ、この後に読み込まれる筐体の抽選にこの乱数を使う（既定は時刻から作る）。
        /// </summary>
        public static IRandom RandomOverride { get; set; }

        /// <summary>所持コイン。戦闘の召喚・撃破ドロップと共有する（FR-040、T301）。Awake で作る。</summary>
        public CoinWallet Wallet => _wallet;

        /// <summary>true のあいだはレバーとストップを受け付けない（一時停止中: FR-012）。</summary>
        public bool InputLocked { get; set; }

        /// <summary>このプレイの難易度（設定1〜6、FR-045）。</summary>
        public int Setting => setting;

        /// <summary>slot_rules.csv の値（効果の倍率・秒数・回復量を戦闘へ渡すときに使う）。Awake で読む。</summary>
        public SlotRules Rules { get; private set; }

        /// <summary>最後のリールが止まって図柄が揃ったとき（払い出しの後）に、その図柄を通知する（FR-042、T314）。</summary>
        public event Action<SlotSymbol> Aligned;

        /// <summary>リールが回っているか（3本目を押したあと見た目の上で止まるまでを含む）。</summary>
        public bool IsSpinning => _machine != null && (_machine.IsSpinning || AnyReelSpinning());

        /// <summary>このプレイでリールを回した回数。</summary>
        public int SpinCount { get; private set; }

        /// <summary>ボーナス中か（表示と同じく、リールが見た目の上で止まってから切り替わる）。</summary>
        public SlotBonus Bonus => _shownBonus ?? SlotBonus.None;

        /// <summary>いまレバーを引けば回るだけのコインがあるか（ボーナス中は掛け金なし）。</summary>
        public bool CanLever => _machine != null && (_machine.Bonus != SlotBonus.None || _wallet.Coins >= _bet);

        public RectTransform LeverRect => (RectTransform)leverButton.transform;
        public RectTransform BonusLampRect => bonusLamp.rectTransform;
        public RectTransform StopRect(int reel) => (RectTransform)stopButtons[reel].transform;

        private int _bet;

        /// <summary>このプレイの難易度のハズレ率（0〜1、FR-045）。カスタム画面の表示に使う。</summary>
        public double MissRate { get; private set; }

        /// <summary>リールのロック配置（slot_locked_reel.csv、D-46）。カスタム画面に渡す。Awake で読む。</summary>
        public ReelLocks Locks { get; private set; }

        /// <summary>いま3本のリールに使っている配列。カスタムで確定するまでは、ロック配置の空きをコインで埋めた仮の配列。</summary>
        public ReelLayout Layout => _pendingLayout ?? _machine?.Layout;

        /// <summary>
        /// カスタムで確定した配列を3本すべてに使う（FR-046）。リールが回っていればすべて止まってから替える
        /// （回転中のゲームは前の配列のまま終える）。
        /// </summary>
        public void ApplyLayout(ReelLayout layout)
        {
            if (!Locks.Accepts(layout))
            {
                throw new ArgumentException("ロック配置と合わない配列", nameof(layout));
            }

            _pendingLayout = layout;
            ApplyPendingLayout();
        }

        private ReelLayout _pendingLayout;

        private void ApplyPendingLayout()
        {
            if (_pendingLayout == null || _machine == null || _machine.IsSpinning || AnyReelSpinning())
            {
                return;
            }

            _machine.ChangeLayout(_pendingLayout);
            for (var i = 0; i < reels.Length; i++)
            {
                reels[i].Initialize(_pendingLayout, 0);
            }

            _pendingLayout = null;
        }

        /// <summary>
        /// コインの財布を差し替えて抽選器を作り直す（T340: ゲーム1回分の <c>RunState.Coins</c> と共有するため。回す前に呼ぶ）。
        /// 筐体の作り直し（T348）までのつなぎ。
        /// </summary>
        public void UseWallet(CoinWallet wallet)
        {
            _wallet = wallet;
            _machine = new SlotMachine(_initialLayout, MissRate, _wallet, _bet, Rules,
                RandomOverride ?? new SeededRandom((ulong)DateTime.UtcNow.Ticks));
            _machine.Aligned += symbol => Aligned?.Invoke(symbol);
        }

        private void Awake()
        {
            setting = GameSession.SelectedSetting ?? setting;
            ReelLayout layout;
            try
            {
                var config = GameConfig.Load(Parse(GameConfig.FileName, gameConfigTable));
                var settings = SlotSettings.Load(Parse(SlotSettings.FileName, settingsTable));
                var rules = SlotRules.Load(Parse(SlotRules.FileName, rulesTable));
                Rules = rules;
                Locks = ReelLocks.Load(Parse(ReelLocks.FileName, lockedReelTable));
                var problems = Locks.Problems(rules.ReelLength);
                if (problems.Count > 0)
                {
                    throw new InvalidOperationException($"{ReelLocks.FileName}: {string.Join(" / ", problems)}");
                }

                layout = Locks.Fill(SlotSymbol.Coin);

                MissRate = settings.MissRate(setting);
                _bet = config.Bet;
                _wait = new SlotWait(rules.WaitSeconds);
                _initialLayout = layout;
                UseWallet(new CoinWallet(config.InitialCoins));
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                enabled = false;
                return;
            }

            for (var i = 0; i < reels.Length; i++)
            {
                reels[i].Initialize(layout, 0);
            }

            var map = inputActions.FindActionMap(ActionMap, true);
            _lever = map.FindAction("Lever", true);
            for (var i = 0; i < _stops.Length; i++)
            {
                _stops[i] = map.FindAction(StopActions[i], true);
            }

            // クリックしたボタンが選択されたままだと Space（Submit）で押されてしまうので、押したら選択を外す。
            leverButton.onClick.AddListener(() =>
            {
                Deselect();
                OnLever();
            });
            for (var i = 0; i < stopButtons.Length; i++)
            {
                var reel = i;
                stopButtons[i].onClick.AddListener(() =>
                {
                    Deselect();
                    OnStop(reel);
                });
            }
        }

        private void OnEnable()
        {
            if (_lever == null)
            {
                return;
            }

            _lever.performed += OnLeverAction;
            _stops[0].performed += OnStopLeftAction;
            _stops[1].performed += OnStopCenterAction;
            _stops[2].performed += OnStopRightAction;
            _lever.actionMap.Enable();
        }

        private void OnDisable()
        {
            if (_lever == null)
            {
                return;
            }

            _lever.performed -= OnLeverAction;
            _stops[0].performed -= OnStopLeftAction;
            _stops[1].performed -= OnStopCenterAction;
            _stops[2].performed -= OnStopRightAction;
        }

        private void OnLeverAction(InputAction.CallbackContext context) => OnLever();
        private void OnStopLeftAction(InputAction.CallbackContext context) => OnStop(0);
        private void OnStopCenterAction(InputAction.CallbackContext context) => OnStop(1);
        private void OnStopRightAction(InputAction.CallbackContext context) => OnStop(2);

        private static void Deselect()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        // 回転中のレバーは無視する。ウェイト中は受け付けておき、時間が来たら回す（plan 5章）。
        private void OnLever()
        {
            if (!InputLocked && !_machine.IsSpinning)
            {
                _wait.Request();
            }
        }

        private void OnStop(int reel)
        {
            if (InputLocked || !_machine.IsSpinning || _machine.IsStopped(reel))
            {
                return;
            }

            var stop = _machine.Stop(reel, reels[reel].PressPosition);
            reels[reel].StopAt(stop);
        }

        private void Update()
        {
            ApplyPendingLayout();
            var now = Time.timeAsDouble;
            // 操作を止めている間（一時停止・カスタム中）は、待っていたレバーも回さない。
            if (!InputLocked && _wait.Consume(now))
            {
                if (_machine.TryLever())
                {
                    _wait.Started(now);
                    SpinCount++;
                    foreach (var reel in reels)
                    {
                        reel.StartSpin();
                    }
                }
                else
                {
                    // 所持コインが足りない（FR-040、plan 3章）。
                    _coinBlinkUntil = now + CoinBlinkSeconds;
                }
            }

            var blink = now < _coinBlinkUntil && (int)(now * 10) % 2 == 0;
            coinIcon.enabled = !blink;

            // 3本目を押したあとの払い出しとボーナスは、リールが見た目の上でも止まってから表示に出す。
            if (_machine.IsSpinning || !AnyReelSpinning())
            {
                UpdateNumbers();
            }
        }

        private void UpdateNumbers()
        {
            SetNumber(creditText, _wallet.Coins, ref _shownCredit);
            SetNumber(payoutText, _machine.LastPayout, ref _shownPayout);

            var bonus = _machine.Bonus;
            if (bonus != _shownBonus)
            {
                _shownBonus = bonus;
                var color = bonus == SlotBonus.Big ? bigColor : bonus == SlotBonus.Reg ? regColor : lampOffColor;
                bonusLamp.color = color;
                bonusGamesText.color = color;
                bonusGamesText.enabled = bonus != SlotBonus.None;
            }

            SetNumber(bonusGamesText, _machine.BonusGamesLeft, ref _shownBonusGames);
        }

        private static void SetNumber(TMP_Text text, int value, ref int shown)
        {
            if (value != shown)
            {
                shown = value;
                text.SetText("{0}", value);
            }
        }

        private bool AnyReelSpinning()
        {
            foreach (var reel in reels)
            {
                if (reel.IsSpinning)
                {
                    return true;
                }
            }

            return false;
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
