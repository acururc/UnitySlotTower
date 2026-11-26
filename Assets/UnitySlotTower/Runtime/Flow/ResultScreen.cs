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
    /// 結果のアイコン、到達した階、所持金、コインを出す。ボタンは左右に2つで、右は ◀（タイトルへ戻る）。左はゲームオーバーなら
    /// ▶＋旗（負けたステージの始めからやり直す: spec 010）、クリアなら ▶（Game を読み込み直してステージ1から: AC-004）。
    /// マウスはクリック、キーボードは左右で選んで Enter。
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        private const string ActionMap = "SlotTower";

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
        [Tooltip("クリアで出す、ステージ1から遊び直すボタン。")]
        [SerializeField] private Button retryButton;
        [SerializeField] private Button titleButton;
        [Tooltip("キーボードで選んでいるボタンの枠（retry, title の順）。")]
        [SerializeField] private GameObject[] selectionFrames = new GameObject[2];

        private InputAction _cursor;
        private InputAction _confirm;
        private Vector2 _lastCursorValue;
        private int _selected;
        private int _openedFrame = -1;
        private bool _showsStageRetry;

        /// <summary>開いているか。</summary>
        public bool IsOpen => panel.activeSelf;

        /// <summary>開いた結果がクリアか（開いていないときは false）。</summary>
        public bool IsClear { get; private set; }

        public Button StageRetryButton => stageRetryButton;
        public Button RetryButton => retryButton;
        public Button TitleButton => titleButton;

        /// <summary>
        /// キーボードで選んでいるボタン。0: 左（ゲームオーバーはやり直し、クリアはステージ1から）、1: タイトル。
        /// </summary>
        public int Selected => _selected;

        private void Awake()
        {
            var map = inputActions.FindActionMap(ActionMap, true);
            _cursor = map.FindAction("Cursor", true);
            _confirm = map.FindAction("Confirm", true);
            if (stageRetryButton != null) stageRetryButton.onClick.AddListener(RetryStage);
            retryButton.onClick.AddListener(Retry);
            titleButton.onClick.AddListener(BackToTitle);
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

        // 左のボタン: ゲームオーバーはやり直し、クリアはステージ1から（spec 010 FR-202・FR-204）。2つは同じ位置にある。
        private void LayOutButtons(bool showStageRetry)
        {
            _showsStageRetry = showStageRetry;
            if (stageRetryButton != null) stageRetryButton.gameObject.SetActive(showStageRetry);
            retryButton.gameObject.SetActive(!showStageRetry);
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
            if (stageRetryFrame != null) stageRetryFrame.SetActive(index == 0);
            for (var i = 0; i < selectionFrames.Length; i++)
            {
                selectionFrames[i].SetActive(i == index);
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
                Select(Mathf.Clamp(_selected + dx, 0, 1));
            }
        }

        // 開いたフレームの Enter（戦闘側の操作の続き）では決めない。
        private void OnConfirmAction(InputAction.CallbackContext context)
        {
            if (!IsOpen || Time.frameCount == _openedFrame)
            {
                return;
            }

            if (_selected == 1)
            {
                BackToTitle();
            }
            else if (_showsStageRetry)
            {
                RetryStage();
            }
            else
            {
                Retry();
            }
        }
    }
}
