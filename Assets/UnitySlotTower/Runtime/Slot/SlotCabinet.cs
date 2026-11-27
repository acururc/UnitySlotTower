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
    /// 下部のスロット筐体（plan 16.3・16.8、spec 011）。BET ボタンで賭け枚数（＝有効ライン数）を 1→2→3→1 と切り替え、
    /// レバー（クリックか Space）で回し、3本は左から自動で止まる。止まり終えたら揃ったラインを中段→上段→下段の順に
    /// <c>line_gap_seconds</c> ずつずらして <see cref="Aligned"/> で通知する（通知中は <see cref="LastAlignedRow"/> などで行と賭けが分かる）。
    /// コインの数を出し、回せないときはそのアイコンを点滅させる。
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

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Button leverButton;
        [Tooltip("賭け枚数を 1→2→3→1 と切り替えるボタン（spec 011 FR-001）。")]
        [SerializeField] private Button betButton;

        [Header("View")]
        [SerializeField] private SlotReelView[] reels = new SlotReelView[SlotMachine.ReelCount];
        [SerializeField] private RectTransform reelWindow;
        [SerializeField] private Graphic coinIcon;
        [Tooltip("旧表示の印。常に非表示。")]
        [SerializeField] private GameObject fixedSpinsIcon;
        [Tooltip("コインの数。")]
        [SerializeField] private TMP_Text creditText;
        [Tooltip("BET ボタンの上のコインの絵（1〜3枚目）。次に賭ける枚数だけ表示する。")]
        [SerializeField] private GameObject[] betCoins = new GameObject[SlotMachine.MaxBet];
        [Tooltip("BET ボタンの空きの丸の枠（1〜3枚目）。賭けていない場所だけ表示する。")]
        [SerializeField] private GameObject[] betSlots = new GameObject[SlotMachine.MaxBet];
        [Tooltip("有効ラインの印（上段・中段・下段の順を左右など何組でも）。有効なラインだけ明るくする（spec 011 FR-002）。")]
        [SerializeField] private Graphic[] lineMarks = new Graphic[SlotMachine.RowCount * 2];
        [Tooltip("ステージ1の見せ場でレバーを指す矢印（spec 011 FR-031）。")]
        [SerializeField] private GameObject leverHint;
        [SerializeField] private float leverHintPulse = 0.12f;
        [SerializeField] private float leverHintSpeed = 6f;
        [SerializeField] private Color lineOnColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        [SerializeField] private Color lineOffColor = new Color(1f, 1f, 1f, 0.12f);

        private const double CoinBlinkSeconds = 1.0;

        private SlotMachine _machine;
        private CoinWallet _wallet;
        private SlotReels _reels;
        private int _selectedBet = SlotMachine.MinBet;
        private InputAction _lever;
        private double _coinBlinkUntil;
        private int _shownCredit = -1;
        private int _shownBet = -1;
        private bool _leverHighlighted;
        private Vector3 _leverScale = Vector3.one;

        // 回転中の各リールを止める時刻。止める指示を出したら null。
        private readonly double?[] _stopAt = new double?[SlotMachine.ReelCount];
        private readonly bool[] _stopPending = new bool[SlotMachine.ReelCount];

        // 止まった後、まだ通知していない揃いと次の通知の時刻。
        private readonly Queue<SlotLineHit> _pendingHits = new Queue<SlotLineHit>();
        private double _nextHitAt;
        private int _alignedIndex;

        /// <summary>
        /// テスト用: null でなければ、この後に読み込まれる筐体の抽選にこの乱数を使う（既定は時刻から作る）。
        /// </summary>
        public static IRandom RandomOverride { get; set; }

        /// <summary>
        /// コインの財布を差し替えて抽選器を作り直す（ゲーム1回分の <c>RunState.Coins</c> と共有するため。回す前に呼ぶ）。
        /// 選んだ賭け枚数は引き継ぐ（FR-001）。
        /// </summary>
        public void UseWallet(CoinWallet wallet)
        {
            _wallet = wallet;
            _machine = new SlotMachine(_reels, Patterns, _wallet,
                RandomOverride ?? new SeededRandom((ulong)DateTime.UtcNow.Ticks), Rules.LineHitRate)
            {
                SelectedBet = _selectedBet,
            };
            _machine.LineAligned += hit => _pendingHits.Enqueue(hit);
            _pendingHits.Clear();
        }

        public CoinWallet Wallet => _wallet;

        /// <summary>コインの表示から差し引く枚数（飛んでいる途中のコイン、spec 009）。</summary>
        public int CreditHeldBack { get; set; }

        /// <summary>今表示しているコインの数。</summary>
        public int ShownCredit => _shownCredit;

        public RectTransform CreditRect => creditText.rectTransform;

        /// <summary>true の間はレバーと BET を受け付けない（一時停止・終了後など）。</summary>
        public bool InputLocked { get; set; }

        /// <summary>true の間はレバーと BET を受け付けない（リザルトなどの画面が上にある）。</summary>
        public bool HeldByScreen { get; set; }

        public SlotRules Rules { get; private set; }

        /// <summary>抽選表（図柄の重み）。</summary>
        public IReadOnlyList<SlotSymbol?> Patterns { get; private set; }

        /// <summary>揃ったラインごとに図柄を通知する（中段→上段→下段の順、line_gap_seconds ずつずらす）。</summary>
        public event Action<SlotSymbol> Aligned;

        /// <summary>レバーで回し始めた（コインを払った）ときに通知する。</summary>
        public event Action LeverPulled;

        /// <summary>コインが足りずレバーを引けなかった（spec 014 FR-003 の拒否音）。</summary>
        public event Action LeverRefused;

        /// <summary>BET ボタンで賭け枚数が変わった（spec 014 FR-001）。</summary>
        public event Action BetChanged;

        /// <summary>リールが1本止まった（0 始まりの番号、spec 014 FR-001）。</summary>
        public event Action<int> ReelStopped;

        /// <summary>3本とも止まって出目が決まった。揃ったライン数を渡す（0 ならハズレ、spec 014 FR-001）。揃いの通知より前に呼ぶ。</summary>
        public event Action<int> Settled;

        /// <summary>回転中か、止まった後の揃いをまだ通知し終えていない。</summary>
        public bool IsSpinning => _machine != null && (_machine.IsSpinning || AnyReelSpinning() || _pendingHits.Count > 0);

        /// <summary>レバーで回した回数。</summary>
        public int SpinCount { get; private set; }

        /// <summary>レバーを引ける（コインが1枚以上ある）。</summary>
        public bool CanLever => _wallet != null && (_wallet.Coins >= SlotMachine.MinBet || FreeSpin);

        /// <summary>次のレバーはコインを使わない（ステージ1の見せ場、spec 015 FR-001）。回転が始まると false に戻る。</summary>
        public bool FreeSpin
        {
            get => _machine != null && _machine.NextSpinFree;
            set
            {
                if (_machine != null) _machine.NextSpinFree = value;
            }
        }

        /// <summary>出してよい図柄（発動中の図柄を外す）。null なら全部。</summary>
        public Func<SlotSymbol, bool> OutcomeFilter { get; set; }

        /// <summary>選んだ賭け枚数（1〜3）。</summary>
        public int SelectedBet => _selectedBet;

        /// <summary>次のレバーで実際に賭ける枚数（コインが足りなければ下がる）。</summary>
        public int Bet => _machine?.EffectiveBet ?? _selectedBet;

        /// <summary>直近のレバーで払った枚数。</summary>
        public int SpinBet => _machine?.SpinBet ?? SlotMachine.MinBet;

        /// <summary>直近のレバーで揃うと決まったライン。</summary>
        public IReadOnlyList<SlotLineHit> Hits => _machine?.Hits;

        /// <summary>通知中（直近に通知した）揃いの行（上段 -1・中段 0・下段 1）。</summary>
        public int LastAlignedRow { get; private set; }

        /// <summary>通知中（直近に通知した）揃いの賭け枚数（効果の強さ）。</summary>
        public int LastAlignedPower { get; private set; } = SlotMachine.MinBet;

        /// <summary>通知中（直近に通知した）揃いがこの回の何本目か（1〜3）。二重・三重の演出に使う。</summary>
        public int LastAlignedIndex { get; private set; }

        /// <summary>次のレバーで抽選せずに出す揃い（ステージ1の見せ場、FR-032）。回すと消える。</summary>
        public IReadOnlyList<SlotLineHit> ForcedHits { get; set; }

        /// <summary>レバーを点滅させ、矢印を出す（FR-031）。</summary>
        public bool LeverHighlighted
        {
            get => _leverHighlighted;
            set
            {
                _leverHighlighted = value;
                if (leverHint != null)
                {
                    leverHint.SetActive(value);
                }

                if (!value)
                {
                    leverButton.transform.localScale = _leverScale;
                }
            }
        }

        public RectTransform LeverRect => (RectTransform)leverButton.transform;

        public RectTransform ReelCenterCell(int reel) => reels[reel].CenterCell;

        /// <summary>リール reel の row 段（上段 -1・中段 0・下段 1）のマス。</summary>
        public RectTransform ReelCell(int reel, int row) => reels[reel].Cell(row);

        public RectTransform ReelWindowRect => reelWindow != null ? reelWindow : (RectTransform)reels[0].transform.parent;

        /// <summary>賭け枚数を選ぶ（回転中は変えない）。</summary>
        public void SetBet(int bet)
        {
            if (_machine == null || IsSpinning)
            {
                return;
            }

            _machine.SelectedBet = Mathf.Clamp(bet, SlotMachine.MinBet, SlotMachine.MaxBet);
            _selectedBet = _machine.SelectedBet;
        }

        /// <summary>BET ボタン: 1→2→3→1（FR-001）。入力を受け付けないときと回転中は変えない。</summary>
        public void CycleBet()
        {
            if (InputLocked || HeldByScreen || IsSpinning || _machine == null || ForcedHits != null)
            {
                return;
            }

            _machine.CycleBet();
            _selectedBet = _machine.SelectedBet;
            BetChanged?.Invoke();
        }

        private void Awake()
        {
            if (fixedSpinsIcon != null)
            {
                fixedSpinsIcon.SetActive(false);
            }

            if (leverHint != null)
            {
                leverHint.SetActive(false);
            }

            try
            {
                var config = GameConfig.Load(Parse(GameConfig.FileName, gameConfigTable));
                Rules = SlotRules.Load(Parse(SlotRules.FileName, rulesTable));
                Patterns = SlotPatterns.Load(Parse(SlotPatterns.FileName, patternsTable)).Patterns;
                _reels = SlotReels.Load(Parse(SlotReels.FileName, reelsTable));
                var problems = _reels.Problems();
                if (problems.Count > 0)
                {
                    throw new InvalidOperationException($"{SlotReels.FileName}: {string.Join(" / ", problems)}");
                }

                _selectedBet = Mathf.Clamp(config.Bet, SlotMachine.MinBet, SlotMachine.MaxBet);
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
            _leverScale = leverButton.transform.localScale;

            // クリックしたボタンが選択されたままだと Space（Submit）で押されてしまうので、押したら選択を外す。
            leverButton.onClick.AddListener(() =>
            {
                Deselect();
                OnLever();
            });
            if (betButton != null)
            {
                betButton.onClick.AddListener(() =>
                {
                    Deselect();
                    CycleBet();
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
            if (InputLocked || HeldByScreen || IsSpinning)
            {
                return;
            }

            var now = Time.timeAsDouble;
            switch (_machine.TryLever(OutcomeFilter, ForcedHits))
            {
                case LeverResult.Started:
                    ForcedHits = null;
                    SpinCount++;
                    _alignedIndex = 0;
                    for (var i = 0; i < reels.Length; i++)
                    {
                        reels[i].StartSpin();
                        _stopAt[i] = now + Rules.FirstStopSeconds + i * Rules.StopIntervalSeconds;
                    }

                    LeverPulled?.Invoke();
                    break;
                case LeverResult.NotEnoughCoins:
                    _coinBlinkUntil = now + CoinBlinkSeconds;
                    LeverRefused?.Invoke();
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
                        reels[i].StopAt(_machine.Visible(i, -1), _machine.Visible(i, 0), _machine.Visible(i, 1));
                        _stopAt[i] = null;
                        _stopPending[i] = true;
                    }

                    if (_stopPending[i] && !reels[i].IsSpinning)
                    {
                        _stopPending[i] = false;
                        ReelStopped?.Invoke(i);
                    }

                    allIssued &= !_stopAt[i].HasValue;
                }

                if (allIssued && !AnyReelSpinning())
                {
                    _machine.Settle();
                    _nextHitAt = now;
                    Settled?.Invoke(_pendingHits.Count);
                }
            }

            // 揃いを1本ずつ、間をあけて通知する（FR-012）。
            if (_pendingHits.Count > 0 && now >= _nextHitAt)
            {
                var hit = _pendingHits.Dequeue();
                _alignedIndex++;
                LastAlignedRow = hit.Row;
                LastAlignedPower = _machine.SpinBet;
                LastAlignedIndex = _alignedIndex;
                _nextHitAt = now + Rules.LineGapSeconds;
                Aligned?.Invoke(hit.Symbol);
            }

            var blink = now < _coinBlinkUntil && (int)(now * 10) % 2 == 0;
            coinIcon.enabled = !blink;
            var credit = Mathf.Max(0, _wallet.Coins - CreditHeldBack);
            if (credit != _shownCredit)
            {
                _shownCredit = credit;
                creditText.SetText("{0}", _shownCredit);
            }

            UpdateBetView();
            if (_leverHighlighted)
            {
                var k = 1f + leverHintPulse * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * leverHintSpeed));
                leverButton.transform.localScale = _leverScale * k;
            }
        }

        // BET のコインの絵と有効ラインの印。回転中はその回の枚数、止まっている間は次に賭ける枚数。
        private void UpdateBetView()
        {
            var bet = _machine.IsSpinning || _pendingHits.Count > 0 ? _machine.SpinBet : _machine.EffectiveBet;
            if (bet == _shownBet)
            {
                return;
            }

            _shownBet = bet;
            for (var i = 0; i < betCoins.Length; i++)
            {
                if (betCoins[i] != null)
                {
                    betCoins[i].SetActive(i < bet);
                }

                if (i < betSlots.Length && betSlots[i] != null)
                {
                    betSlots[i].SetActive(i >= bet);
                }
            }

            for (var r = 0; r < lineMarks.Length; r++)
            {
                if (lineMarks[r] != null)
                {
                    lineMarks[r].color = SlotMachine.IsLineActive(r % SlotMachine.RowCount - 1, bet) ? lineOnColor : lineOffColor;
                }
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
