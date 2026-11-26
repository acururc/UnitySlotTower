using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Ui;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// Game シーンのリザルト（T402、FR-005、plan 4章）。最終ウェーブを終える（クリア）かライフが0になる（ゲームオーバー）と開き、
    /// 結果のアイコン、到達した階、所持金、コインを出す。ゲームオーバーでは ▶＋旗 で負けたステージの始めからやり直し（spec 010）、
    /// ▶＋1 で最初から（Game を読み込み直す: AC-004）、◀ でタイトルへ戻る。クリアでは やり直し を出さない。
    /// マウスはクリック、キーボードは左右で選んで Enter。
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        private const string ActionMap = "SlotTower";

        // ボタンが3つのときの横の範囲（パネル内の比率。左から やり直し・最初から・タイトル）。2つのときはシーンの位置のまま。
        private static readonly Vector2[] ThreeColumns =
        {
            new Vector2(0.05f, 0.31f), new Vector2(0.37f, 0.63f), new Vector2(0.69f, 0.95f),
        };

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private BattleController battle;
        [Tooltip("ステージのやり直しに使う（spec 010）。")]
        [SerializeField] private RunController run;
        [Tooltip("回転中はやり直しを受け付けない（止まった後の効果が新しいステージに入らないように）。")]
        [SerializeField] private SlotCabinet slotCabinet;
        [Tooltip("開いているあいだだけ表示する画面全体（背景で下の操作を遮る）。")]
        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject clearIcon;
        [SerializeField] private GameObject gameOverIcon;
        [SerializeField] private TMP_Text floorText;
        [Tooltip("所持金（T402 の版では所持コインの欄だった。中身は当時から所持金の財布）。")]
        [FormerlySerializedAs("coinsText")]
        [SerializeField] private TMP_Text moneyText;
        [Tooltip("筐体のコイン。")]
        [SerializeField] private TMP_Text slotCoinsText;
        [Tooltip("ゲームオーバーで出す、負けたステージの始めからやり直すボタン（spec 010）。")]
        [SerializeField] private Button stageRetryButton;
        [SerializeField] private GameObject stageRetryFrame;
        [Tooltip("最初から（ステージ1）。")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button titleButton;
        [Tooltip("キーボードで選んでいるボタンの枠（retry, title の順）。")]
        [SerializeField] private GameObject[] selectionFrames = new GameObject[2];

        private InputAction _cursor;
        private InputAction _confirm;
        private Vector2 _lastCursorValue;
        private int _selected;
        private int _openedFrame = -1;
        private Vector2 _retryAnchorX;
        private Vector2 _titleAnchorX;
        private bool _showsStageRetry;

        /// <summary>開いているか。</summary>
        public bool IsOpen => panel.activeSelf;

        /// <summary>開いた結果がクリアか（開いていないときは false）。</summary>
        public bool IsClear { get; private set; }

        public Button StageRetryButton => stageRetryButton;
        public Button RetryButton => retryButton;
        public Button TitleButton => titleButton;

        /// <summary>
        /// キーボードで選んでいるボタンの、見えているボタンの中での位置。ゲームオーバーは 0: やり直し、1: 最初から、2: タイトル、
        /// クリアは 0: 最初から、1: タイトル。
        /// </summary>
        public int Selected => _selected;

        private int ButtonCount => _showsStageRetry ? 3 : 2;

        private void Awake()
        {
            var map = inputActions.FindActionMap(ActionMap, true);
            _cursor = map.FindAction("Cursor", true);
            _confirm = map.FindAction("Confirm", true);
            if (stageRetryButton != null) stageRetryButton.onClick.AddListener(RetryStage);
            retryButton.onClick.AddListener(Retry);
            titleButton.onClick.AddListener(BackToTitle);
            var retryRect = (RectTransform)retryButton.transform;
            var titleRect = (RectTransform)titleButton.transform;
            _retryAnchorX = new Vector2(retryRect.anchorMin.x, retryRect.anchorMax.x);
            _titleAnchorX = new Vector2(titleRect.anchorMin.x, titleRect.anchorMax.x);
            panel.SetActive(false);
        }

        private void OnEnable()
        {
            _cursor.performed += OnCursorAction;
            _cursor.canceled += OnCursorAction;
            _confirm.performed += OnConfirmAction;
            _cursor.actionMap.Enable();
        }

        private void OnDisable()
        {
            _cursor.performed -= OnCursorAction;
            _cursor.canceled -= OnCursorAction;
            _confirm.performed -= OnConfirmAction;
        }

        private void Update()
        {
            var current = battle.Run;
            if (IsOpen || current == null || !battle.IsFinished)
            {
                return;
            }

            Open(current);
        }

        private void Open(BattleNodeRun current)
        {
            IsClear = current.IsRunCleared;
            clearIcon.SetActive(IsClear);
            gameOverIcon.SetActive(!IsClear);
            // 到達したステージ・所持金・コイン（T350d、spec 002 FR-101）。
            var state = current.State;
            floorText.SetText("{0}", (float)state.StageIndex);
            moneyText.SetText(NumericText.YenFormat, (float)state.Money.Coins);
            slotCoinsText.SetText("{0}", (float)state.Coins.Coins);
            LayOutButtons(!IsClear && stageRetryButton != null && run != null);
            _openedFrame = Time.frameCount;
            Select(0);
            panel.SetActive(true);
        }

        // ゲームオーバーは3つ（やり直し・最初から・タイトル）、クリアは2つ（最初から・タイトル）を横に並べる（spec 010 FR-202）。
        private void LayOutButtons(bool showStageRetry)
        {
            _showsStageRetry = showStageRetry;
            if (stageRetryButton != null) stageRetryButton.gameObject.SetActive(showStageRetry);
            SetAnchorX(retryButton, showStageRetry ? ThreeColumns[1] : _retryAnchorX);
            SetAnchorX(titleButton, showStageRetry ? ThreeColumns[2] : _titleAnchorX);
            if (showStageRetry) SetAnchorX(stageRetryButton, ThreeColumns[0]);
        }

        private static void SetAnchorX(Button button, Vector2 x)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(x.x, rect.anchorMin.y);
            rect.anchorMax = new Vector2(x.y, rect.anchorMax.y);
        }

        /// <summary>
        /// 負けたステージの始めからやり直す（spec 010 FR-201）。スロットの回転中は何もしない。
        /// ゲームオーバーのリザルトが開いているときだけ受け付ける。
        /// </summary>
        public void RetryStage()
        {
            if (!IsOpen || !_showsStageRetry || (slotCabinet != null && slotCabinet.IsSpinning))
            {
                return;
            }

            panel.SetActive(false);
            run.RetryStage();
        }

        /// <summary>Game シーンを読み込み直して、ステージ1から始め直す。</summary>
        public void Retry() => SceneManager.LoadScene(GameSession.GameScene);

        public void BackToTitle() => SceneManager.LoadScene(GameSession.TitleScene);

        private void Select(int index)
        {
            _selected = index;
            var offset = _showsStageRetry ? 1 : 0;
            if (stageRetryFrame != null) stageRetryFrame.SetActive(_showsStageRetry && index == 0);
            for (var i = 0; i < selectionFrames.Length; i++)
            {
                selectionFrames[i].SetActive(i + offset == index);
            }
        }

        private void OnCursorAction(InputAction.CallbackContext context)
        {
            // 押し続けで繰り返さず、新しく押された左右だけ1回動かす。
            var value = context.ReadValue<Vector2>();
            var dx = value.x != 0f && _lastCursorValue.x == 0f ? (int)Mathf.Sign(value.x) : 0;
            _lastCursorValue = value;
            if (IsOpen && dx != 0)
            {
                Select(Mathf.Clamp(_selected + dx, 0, ButtonCount - 1));
            }
        }

        // 開いたフレームの Enter（戦闘側の操作の続き）では決めない。
        private void OnConfirmAction(InputAction.CallbackContext context)
        {
            if (!IsOpen || Time.frameCount == _openedFrame)
            {
                return;
            }

            var index = _showsStageRetry ? _selected : _selected + 1;
            switch (index)
            {
                case 0:
                    RetryStage();
                    break;
                case 1:
                    Retry();
                    break;
                default:
                    BackToTitle();
                    break;
            }
        }
    }
}
