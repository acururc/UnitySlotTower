using System;
using System.Collections.Generic;
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
    /// 下部のスロット筐体（plan 16.3・16.8）。レバー（クリックか Space）で回し、3本は左から自動で止まる。止まり終えたら
    /// <see cref="SlotMachine.Settle"/> で揃った図柄を通知する。コインの数、回数固定の戦闘ではその代わりに残り回数を出し、
    /// 回せないときはそのアイコンを点滅させる。ルート画面ではルート選びのモード（<see cref="BeginRouteChoice"/>、D-75）で使う。
    /// </summary>
    public sealed class SlotCabinet : MonoBehaviour
    {
        private const string ActionMap = "SlotTower";

        [Header("Tables")]
        [SerializeField] private TextAsset gameConfigTable;
        [FormerlySerializedAs("settingsTable")]
        [SerializeField] private TextAsset patternsTable;
        [SerializeField] private TextAsset rulesTable;
        [FormerlySerializedAs("lockedReelTable")]
        [FormerlySerializedAs("defaultReelTable")]
        [SerializeField] private TextAsset reelsTable;

        [Tooltip("難易度（設定1〜6）。Game シーンを直接開いたときだけ使う。難易度選択を通ったときはその設定（T401）。")]
        [SerializeField, Range(1, 6)] private int setting = 1;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Button leverButton;

        [Header("View")]
        [SerializeField] private SlotReelView[] reels = new SlotReelView[SlotMachine.ReelCount];
        [SerializeField] private RectTransform reelWindow;
        [SerializeField] private Graphic coinIcon;
        [Tooltip("回数固定の戦闘でコインのアイコンの代わりに出す印（plan 16.8）。")]
        [SerializeField] private GameObject fixedSpinsIcon;
        [Tooltip("コインの数。回数固定の戦闘では残り回数を出す。")]
        [SerializeField] private TMP_Text creditText;

        [Header("Route choice (D-75)")]
        [Tooltip("ルート選びのモードで各リールに重ねる透明のボタン（左・中・右）。クリックでそのリールを止める。")]
        [SerializeField] private Button[] routeReelButtons = new Button[SlotMachine.ReelCount];
        [Tooltip("行き先のないリールを暗くする覆い（左・中・右）。")]
        [SerializeField] private GameObject[] routeReelDims = new GameObject[SlotMachine.ReelCount];

        private const double CoinBlinkSeconds = 1.0;

        private SlotMachine _machine;
        private CoinWallet _wallet;
        private SlotReels _reels;
        private int _bet;
        private InputAction _lever;
        private double _coinBlinkUntil;
        private int _shownCredit = -1;

        // 回転中の各リールを止める時刻。止める指示を出したら null。
        private readonly double?[] _stopAt = new double?[SlotMachine.ReelCount];

        // ルート選びのモード（D-75）。null ならモード外。
        private bool[] _routeDirections;
        private bool _routeSpinning;
        private int _routePick = -1;

        /// <summary>
        /// テスト用: null でなければ、この後に読み込まれる筐体の抽選にこの乱数を使う（既定は時刻から作る）。
        /// </summary>
        public static IRandom RandomOverride { get; set; }

        /// <summary>所持コイン。Awake で作り、<see cref="UseWallet"/> でゲーム1回分の状態と共有する。</summary>
        public CoinWallet Wallet => _wallet;

        /// <summary>true のあいだはレバーを受け付けない（一時停止中: FR-012）。</summary>
        public bool InputLocked { get; set; }

        /// <summary>このプレイの難易度（設定1〜6、FR-045）。</summary>
        public int Setting => setting;

        /// <summary>slot_rules.csv の値。Awake で読む。</summary>
        public SlotRules Rules { get; private set; }

        /// <summary>このプレイの難易度の20パターン（ハズレは null）。早見表に使う。Awake で読む。</summary>
        public IReadOnlyList<SlotSymbol?> Patterns { get; private set; }

        /// <summary>3本が止まり終えて図柄が揃ったときに、その図柄を通知する（FR-042）。</summary>
        public event Action<SlotSymbol> Aligned;

        /// <summary>レバーから3本が見た目の上で止まり終えるまで true。</summary>
        public bool IsSpinning => _machine != null && (_machine.IsSpinning || AnyReelSpinning());

        /// <summary>このプレイでリールを回した回数。</summary>
        public int SpinCount { get; private set; }

        /// <summary>いまレバーを引けば回るか（回数固定なら残り回数、そうでなければコイン）。</summary>
        public bool CanLever => _machine != null &&
            (_machine.FixedSpinsLeft.HasValue ? _machine.FixedSpinsLeft.Value > 0 : _wallet.Coins >= _bet);

        /// <summary>回数固定の戦闘の残り回数。コインで回しているときは null（D-68）。</summary>
        public int? FixedSpinsLeft => _machine?.FixedSpinsLeft;

        /// <summary>1回に賭けるコインの枚数（game_config の bet）。</summary>
        public int Bet => _bet;

        /// <summary>直近のレバーで決まった出目。揃う図柄、ハズレなら null。</summary>
        public SlotSymbol? Outcome => _machine?.Outcome;

        /// <summary>ルート選びのモード中か（<see cref="BeginRouteChoice"/> から行き先が決まるまで）。</summary>
        public bool IsChoosingRoute => _routeDirections != null;

        /// <summary>ルート選びのモードでレバーを引いてリールが回っている（止めるリールを選べる、または止まる途中）。</summary>
        public bool IsRouteSpinning => _routeSpinning;

        /// <summary>ルート選びのモードで止めたリールの番号。止めるまで null。</summary>
        public int? RoutePick => _routePick >= 0 ? _routePick : (int?)null;

        /// <summary>ルート選びのモードでそのリールに行き先があるか。モード外は false。</summary>
        public bool HasRouteDirection(int reel) =>
            _routeDirections != null && reel >= 0 && reel < _routeDirections.Length && _routeDirections[reel];

        /// <summary>
        /// ルート選びのモードで、止めたリールが見た目の上で止まり終えたときにその番号（0=左・1=中・2=右）を通知する。
        /// 通知の時点でモードは終わっている。
        /// </summary>
        public event Action<int> RouteReelChosen;

        public RectTransform LeverRect => (RectTransform)leverButton.transform;
        public RectTransform ReelWindowRect => reelWindow != null ? reelWindow : (RectTransform)reels[0].transform.parent;

        /// <summary>
        /// コインの財布を差し替えて抽選器を作り直す（ゲーム1回分の <c>RunState.Coins</c> と共有するため。回す前に呼ぶ）。
        /// </summary>
        public void UseWallet(CoinWallet wallet)
        {
            _wallet = wallet;
            _machine = new SlotMachine(_reels, Patterns, _wallet, _bet,
                RandomOverride ?? new SeededRandom((ulong)DateTime.UtcNow.Ticks));
            _machine.Aligned += symbol => Aligned?.Invoke(symbol);
        }

        /// <summary>
        /// 戦闘マスの回数固定を設定する（<c>Encounter.FixedSpins</c>）。1以上ならその回数だけコインを使わずに回せ、
        /// 0 ならコインで回す状態に戻す。
        /// </summary>
        public void SetFixedSpins(int count)
        {
            if (count > 0)
            {
                _machine.StartFixedSpins(count);
            }
            else
            {
                _machine.EndFixedSpins();
            }
        }

        /// <summary>
        /// ルート選びのモードに入る（D-75、plan 16.7）。レバーでコインを使わずに回し（回数固定も減らない）、リールは自動で止まらない。
        /// 行き先のあるリール（<paramref name="directions"/> が true）をクリックするとそのリールが止まり、残りもその場で止まって
        /// <see cref="RouteReelChosen"/> を通知する。図柄の効果はない（Core の <see cref="SlotMachine"/> を通さない）。
        /// 通常の回転が止まり終わる前に呼ばれたら、止まり終えてからレバーを受け付ける。
        /// </summary>
        public void BeginRouteChoice(IReadOnlyList<bool> directions)
        {
            if (directions == null || directions.Count != SlotMachine.ReelCount)
            {
                throw new ArgumentException($"行き先は {SlotMachine.ReelCount} 本分", nameof(directions));
            }

            var any = false;
            var copy = new bool[directions.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = directions[i];
                any |= copy[i];
            }

            if (!any)
            {
                throw new ArgumentException("行き先のあるリールがない", nameof(directions));
            }

            _routeDirections = copy;
            _routeSpinning = false;
            _routePick = -1;
            ShowRoutePicks();
        }

        /// <summary>
        /// ルート選びのモードで、そのリールを止める（リールのクリック・ルート画面の矢印）。レバーで回した後の行き先のあるリールだけ。
        /// 止めたら true。
        /// </summary>
        public bool PickRouteReel(int reel)
        {
            if (InputLocked || !_routeSpinning || _routePick >= 0 || !HasRouteDirection(reel))
            {
                return false;
            }

            _routePick = reel;
            for (var i = 0; i < reels.Length; i++)
            {
                // 選んだリールは見た目だけの乱数の位置まで滑らせ、残りはその場（次のコマ）で止める。
                reels[i].StopAt(i == reel ? UnityEngine.Random.Range(0, _reels[i].Length) : reels[i].PressPosition);
            }

            return true;
        }

        private void EndRouteChoice()
        {
            _routeDirections = null;
            _routeSpinning = false;
            _routePick = -1;
            ShowRoutePicks();
        }

        private void ShowRoutePicks()
        {
            for (var i = 0; i < routeReelButtons.Length; i++)
            {
                if (routeReelButtons[i] != null)
                {
                    routeReelButtons[i].gameObject.SetActive(IsChoosingRoute);
                }

                if (i < routeReelDims.Length && routeReelDims[i] != null)
                {
                    routeReelDims[i].SetActive(IsChoosingRoute && !_routeDirections[i]);
                }
            }
        }

        private void Awake()
        {
            setting = GameSession.SelectedSetting ?? setting;
            try
            {
                var config = GameConfig.Load(Parse(GameConfig.FileName, gameConfigTable));
                Rules = SlotRules.Load(Parse(SlotRules.FileName, rulesTable));
                Patterns = SlotPatterns.Load(Parse(SlotPatterns.FileName, patternsTable)).For(setting);
                _reels = SlotReels.Load(Parse(SlotReels.FileName, reelsTable));
                var problems = _reels.Problems();
                if (problems.Count > 0)
                {
                    throw new InvalidOperationException($"{SlotReels.FileName}: {string.Join(" / ", problems)}");
                }

                _bet = config.Bet;
                UseWallet(new CoinWallet(config.InitialCoins));
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                enabled = false;
                return;
            }

            // 最初の表示は当たりに見えないよう、どの段も揃わない位置から始める。
            var start = InitialStops(_reels);
            for (var i = 0; i < reels.Length; i++)
            {
                reels[i].Initialize(_reels[i], start[i]);
            }

            var map = inputActions.FindActionMap(ActionMap, true);
            _lever = map.FindAction("Lever", true);

            // クリックしたボタンが選択されたままだと Space（Submit）で押されてしまうので、押したら選択を外す。
            leverButton.onClick.AddListener(() =>
            {
                Deselect();
                OnLever();
            });

            for (var i = 0; i < routeReelButtons.Length; i++)
            {
                if (routeReelButtons[i] == null)
                {
                    continue;
                }

                var reel = i;
                routeReelButtons[i].onClick.AddListener(() =>
                {
                    Deselect();
                    PickRouteReel(reel);
                });
            }

            ShowRoutePicks();
        }

        private void OnEnable()
        {
            if (_lever == null)
            {
                return;
            }

            _lever.performed += OnLeverAction;
            _lever.actionMap.Enable();
        }

        private void OnDisable()
        {
            if (_lever != null)
            {
                _lever.performed -= OnLeverAction;
            }
        }

        private void OnLeverAction(InputAction.CallbackContext context) => OnLever();

        private static void Deselect()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        // 回転中のレバーは無視する（plan 16.3）。
        private void OnLever()
        {
            if (InputLocked || IsSpinning)
            {
                return;
            }

            // ルート選び: コインも回数固定も使わず、自動では止めない（D-75）。
            if (IsChoosingRoute)
            {
                _routeSpinning = true;
                foreach (var reel in reels)
                {
                    reel.StartSpin();
                }

                return;
            }

            var now = Time.timeAsDouble;
            switch (_machine.TryLever())
            {
                case LeverResult.Started:
                    SpinCount++;
                    for (var i = 0; i < reels.Length; i++)
                    {
                        reels[i].StartSpin();
                        _stopAt[i] = now + Rules.FirstStopSeconds + i * Rules.StopIntervalSeconds;
                    }

                    break;
                case LeverResult.NotEnoughCoins:
                case LeverResult.NoSpinsLeft:
                    _coinBlinkUntil = now + CoinBlinkSeconds;
                    break;
            }
        }

        private void Update()
        {
            var now = Time.timeAsDouble;
            if (_machine.IsSpinning)
            {
                var allIssued = true;
                for (var i = 0; i < reels.Length; i++)
                {
                    if (_stopAt[i].HasValue && now >= _stopAt[i].Value)
                    {
                        reels[i].StopAt(_machine.StopPosition(i));
                        _stopAt[i] = null;
                    }

                    allIssued &= !_stopAt[i].HasValue;
                }

                if (allIssued && !AnyReelSpinning())
                {
                    _machine.Settle();
                }
            }
            else if (_routePick >= 0 && !AnyReelSpinning())
            {
                var pick = _routePick;
                EndRouteChoice();
                RouteReelChosen?.Invoke(pick);
            }

            // 回数固定ならコインの代わりに残り回数を出す。回せなかったときは出ている方のアイコンを点滅させる。
            var fixedSpins = _machine.FixedSpinsLeft;
            var blink = now < _coinBlinkUntil && (int)(now * 10) % 2 == 0;
            coinIcon.enabled = !fixedSpins.HasValue && !blink;
            if (fixedSpinsIcon != null)
            {
                var show = fixedSpins.HasValue && !blink;
                if (fixedSpinsIcon.activeSelf != show)
                {
                    fixedSpinsIcon.SetActive(show);
                }
            }

            var credit = fixedSpins ?? _wallet.Coins;
            if (credit != _shownCredit)
            {
                _shownCredit = credit;
                creditText.SetText("{0}", _shownCredit);
            }
        }

        // 3本の停止位置のうち、上段・中段・下段のどれも揃わない最初の組み合わせ（Awake で1回だけ）。
        private static int[] InitialStops(SlotReels slotReels)
        {
            var count = slotReels[0].Length;
            for (var a = 0; a < count; a++)
            {
                for (var b = 0; b < count; b++)
                {
                    for (var c = 0; c < count; c++)
                    {
                        var aligned = false;
                        for (var row = -1; row <= 1 && !aligned; row++)
                        {
                            var s = slotReels[0].At(a + row);
                            aligned = s == slotReels[1].At(b + row) && s == slotReels[2].At(c + row);
                        }

                        if (!aligned)
                        {
                            return new[] { a, b, c };
                        }
                    }
                }
            }

            return new int[SlotMachine.ReelCount];
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
