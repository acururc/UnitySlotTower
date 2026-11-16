using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Ui;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// Game シーンのリザルト（T402、FR-005、plan 4章）。最終ウェーブを終える（クリア）かライフが0になる（ゲームオーバー）と開き、
    /// 結果のアイコン、到達した階、所持金、コインを出す。▶ でリトライ（同じ設定で Game を読み込み直し、ルートも作り直す: AC-004）、
    /// ◀ でタイトルへ戻る。マウスはクリック、キーボードは左右で選んで Enter。
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        private const string ActionMap = "SlotTower";

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private BattleController battle;
        [Tooltip("開いているあいだだけ表示する画面全体（背景で下の操作を遮る）。")]
        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject clearIcon;
        [SerializeField] private GameObject gameOverIcon;
        [Tooltip("到達した階（旗の横）。")]
        [FormerlySerializedAs("waveText")]
        [SerializeField] private TMP_Text floorText;
        [Tooltip("所持金（T402 の版では所持コインの欄だった。中身は当時から所持金の財布）。")]
        [FormerlySerializedAs("coinsText")]
        [SerializeField] private TMP_Text moneyText;
        [Tooltip("筐体のコイン。")]
        [SerializeField] private TMP_Text slotCoinsText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button titleButton;
        [Tooltip("キーボードで選んでいるボタンの枠（retry, title の順）。")]
        [SerializeField] private GameObject[] selectionFrames = new GameObject[2];

        private InputAction _cursor;
        private InputAction _confirm;
        private Vector2 _lastCursorValue;
        private int _selected;
        private int _openedFrame = -1;

        /// <summary>開いているか。</summary>
        public bool IsOpen => panel.activeSelf;

        /// <summary>開いた結果がクリアか（開いていないときは false）。</summary>
        public bool IsClear { get; private set; }

        public Button RetryButton => retryButton;
        public Button TitleButton => titleButton;

        /// <summary>キーボードで選んでいるボタン（0: リトライ、1: タイトル）。</summary>
        public int Selected => _selected;

        private void Awake()
        {
            var map = inputActions.FindActionMap(ActionMap, true);
            _cursor = map.FindAction("Cursor", true);
            _confirm = map.FindAction("Confirm", true);
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
            var run = battle.Run;
            if (IsOpen || run == null || !battle.IsFinished)
            {
                return;
            }

            Open(run);
        }

        private void Open(BattleNodeRun run)
        {
            IsClear = run.IsRunCleared;
            clearIcon.SetActive(IsClear);
            gameOverIcon.SetActive(!IsClear);
            // 到達した階・所持金・コイン（T350d、plan 16章）。
            var state = run.State;
            floorText.SetText("{0}", (float)state.Floor);
            moneyText.SetText(NumericText.YenFormat, (float)state.Money.Coins);
            slotCoinsText.SetText("{0}", (float)state.Coins.Coins);
            _openedFrame = Time.frameCount;
            Select(0);
            panel.SetActive(true);
        }

        /// <summary>同じ設定のまま Game シーンを読み込み直す（設定は <see cref="GameSession.SelectedSetting"/> に残っている）。</summary>
        public void Retry() => SceneManager.LoadScene(GameSession.GameScene);

        public void BackToTitle() => SceneManager.LoadScene(GameSession.TitleScene);

        private void Select(int index)
        {
            _selected = index;
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

            if (_selected == 0)
            {
                Retry();
            }
            else
            {
                BackToTitle();
            }
        }
    }
}
