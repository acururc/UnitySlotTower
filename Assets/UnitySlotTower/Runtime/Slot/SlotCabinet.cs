using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Economy;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Flow;

namespace UnitySlotTower.Slot
{
    /// <summary>
    /// 下部のスロット筐体の仮表示（T110、plan 2・3・5章）。ベット＋レバー（Space）で回し、J/K/L かボタンで止める。
    /// 数字: クレジット・払い出し。ランプとボーナスの数字は T314・T315 で作り直すまで消灯（先バレ・確変・リプレイは廃止）。
    /// </summary>
    public sealed class SlotCabinet : MonoBehaviour
    {
        private const string ActionMap = "SlotTower";
        private static readonly string[] StopActions = { "StopLeft", "StopCenter", "StopRight" };

        [Header("Tables")]
        [SerializeField] private TextAsset gameConfigTable;
        [SerializeField] private TextAsset settingsTable;
        [SerializeField] private TextAsset rulesTable;
        [SerializeField] private TextAsset defaultReelTable;

        [Tooltip("難易度（設定1〜6）。Game シーンを直接開いたときだけ使う。難易度選択を通ったときはその設定（T401）。")]
        [SerializeField, Range(1, 6)] private int setting = 1;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Button leverButton;
        [SerializeField] private Button[] stopButtons = new Button[SlotMachine.ReelCount];

        [Header("View")]
        [SerializeField] private SlotReelView[] reels = new SlotReelView[SlotMachine.ReelCount];
        [SerializeField] private Graphic premonitionLamp;
        [SerializeField] private Graphic bonusLamp;
        [SerializeField] private Graphic kakuhenLamp;
        [SerializeField] private Graphic replayLamp;
        [SerializeField] private Graphic coinIcon;
        [SerializeField] private TMP_Text creditText;
        [SerializeField] private TMP_Text payoutText;
        [SerializeField] private TMP_Text counterText;
        [SerializeField] private Color lampOffColor = new Color(0.18f, 0.18f, 0.2f);

        [Header("Sound")]
        [SerializeField] private AudioSource audioSource;

        private const double CoinBlinkSeconds = 1.0;

        private SlotMachine _machine;
        private CoinWallet _wallet;
        private SlotWait _wait;
        private InputAction _lever;
        private readonly InputAction[] _stops = new InputAction[SlotMachine.ReelCount];
        private double _coinBlinkUntil;
        private int _coinsBeforeLastStop;
        private int _lastPayout;
        private int _shownCredit = -1;
        private int _shownPayout = -1;

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
                layout = ReelLayout.Load(Parse(ReelLayout.FileName, defaultReelTable));
                if (!layout.IsValid(rules.ReelLength))
                {
                    throw new InvalidOperationException($"{ReelLayout.FileName} が配列の条件（D-46）を満たさない");
                }

                _wallet = new CoinWallet(config.InitialCoins);
                _wait = new SlotWait(rules.WaitSeconds);
                _machine = new SlotMachine(layout, settings.MissRate(setting), _wallet, config.Bet, rules,
                    new SeededRandom((ulong)DateTime.UtcNow.Ticks));
                _machine.Aligned += symbol => Aligned?.Invoke(symbol);
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

            foreach (var lamp in new[] { premonitionLamp, bonusLamp, kakuhenLamp, replayLamp })
            {
                lamp.color = lampOffColor;
            }

            counterText.enabled = false;

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

            _coinsBeforeLastStop = _wallet.Coins;
            var stop = _machine.Stop(reel, reels[reel].PressPosition);
            reels[reel].StopAt(stop);
            if (!_machine.IsSpinning)
            {
                _lastPayout = _wallet.Coins - _coinsBeforeLastStop;
            }
        }

        private void Update()
        {
            var now = Time.timeAsDouble;
            if (_wait.Consume(now))
            {
                if (_machine.TryLever())
                {
                    _wait.Started(now);
                    _lastPayout = 0;
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

            // 3本目を押したあとの払い出しは、リールが見た目の上でも止まってから数字に出す。
            if (_machine.IsSpinning || !AnyReelSpinning())
            {
                UpdateNumbers();
            }
        }

        private void UpdateNumbers()
        {
            SetNumber(creditText, _wallet.Coins, ref _shownCredit);
            SetNumber(payoutText, _lastPayout, ref _shownPayout);
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
