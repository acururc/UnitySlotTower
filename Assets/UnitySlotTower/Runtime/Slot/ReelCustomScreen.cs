using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Slot
{
    /// <summary>
    /// リールのカスタム画面（T316、FR-046〜048、plan 3・4・5章）。ゲーム開始前と各ステージの間に Game の上に重ねて開く。
    /// マウス: マスをクリックで選び、選んだマスをもう一度クリックするたびに次の図柄。✔ で確定。
    /// キーボード: 左右でマス、上下か Space で図柄、Enter で確定。条件（D-46）を満たさないあいだは確定できず、
    /// 足りない・多すぎる図柄の印を赤く点滅させる。ハズレ・当たり・当たり時の各図柄の%は配列を変えるたびに更新する。
    /// マスの数は配列（slot_rules.csv の reel_length）に合わせ、<see cref="cellTemplate"/> を複製して作る。
    /// </summary>
    public sealed class ReelCustomScreen : MonoBehaviour
    {
        private const string ActionMap = "SlotTower";
        private const string SymbolsName = "Symbols";
        private const string FrameName = "Frame";
        private const float BlinkPerSecond = 4f;

        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("開いているあいだだけ表示する画面全体（背景で下の操作を遮る）。")]
        [SerializeField] private GameObject panel;
        [Tooltip("マス1つ。子に図柄6つ（SlotSymbol の順）を持つ「Symbols」と、選択中に点く「Frame」を置く。")]
        [SerializeField] private Button cellTemplate;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text missText;
        [SerializeField] private TMP_Text hitText;
        [Tooltip("図柄ごとの印の背景（SlotSymbol の順）。条件を満たさない図柄は赤く点滅する。")]
        [SerializeField] private Graphic[] symbolMarks = new Graphic[SlotSymbols.Count];
        [Tooltip("当たり時の図柄ごとの%（SlotSymbol の順）。")]
        [SerializeField] private TMP_Text[] symbolPercentTexts = new TMP_Text[SlotSymbols.Count];
        [SerializeField] private Color markColor = new Color(0.25f, 0.25f, 0.3f);
        [SerializeField] private Color markErrorColor = new Color(0.95f, 0.15f, 0.15f);

        private readonly List<Button> _cells = new List<Button>();
        private readonly List<Transform> _cellSymbols = new List<Transform>();
        private readonly List<GameObject> _cellFrames = new List<GameObject>();
        private readonly int[] _shownTenths = new int[SlotSymbols.Count];

        private InputAction _cursor;
        private InputAction _confirm;
        private InputAction _cycle;
        private Vector2 _lastCursorValue;
        private ReelCustomizer _customizer;
        private Action<ReelLayout> _confirmed;
        private int _shownSelected = -1;
        private bool _shownBlink;

        /// <summary>開いているか。</summary>
        public bool IsOpen => panel.activeSelf;

        /// <summary>開いているあいだの編集中の配列（テスト・表示用）。閉じているときは null。</summary>
        public ReelCustomizer Customizer => IsOpen ? _customizer : null;

        /// <summary>表示中のマスのボタン（左から）。</summary>
        public IReadOnlyList<Button> Cells => _cells;

        public Button ConfirmButton => confirmButton;

        private void Awake()
        {
            var map = inputActions.FindActionMap(ActionMap, true);
            _cursor = map.FindAction("Cursor", true);
            _confirm = map.FindAction("Confirm", true);
            _cycle = map.FindAction("Lever", true);
            confirmButton.onClick.AddListener(() =>
            {
                Deselect();
                Confirm();
            });
            cellTemplate.gameObject.SetActive(false);
            panel.SetActive(false);
        }

        private void OnEnable()
        {
            _cursor.performed += OnCursorAction;
            _cursor.canceled += OnCursorAction;
            _confirm.performed += OnConfirmAction;
            _cycle.performed += OnCycleAction;
            _cursor.actionMap.Enable();
        }

        private void OnDisable()
        {
            _cursor.performed -= OnCursorAction;
            _cursor.canceled -= OnCursorAction;
            _confirm.performed -= OnConfirmAction;
            _cycle.performed -= OnCycleAction;
        }

        /// <summary>
        /// 画面を開く。<paramref name="start"/> から編集を始め、確定すると <paramref name="confirmed"/> に新しい配列を渡して閉じる。
        /// </summary>
        public void Open(ReelLayout start, int reelLength, double missRate, Action<ReelLayout> confirmed)
        {
            if (_customizer != null)
            {
                _customizer.Changed -= RefreshLayout;
            }

            _customizer = new ReelCustomizer(start, reelLength, missRate);
            _customizer.Changed += RefreshLayout;
            _confirmed = confirmed;
            EnsureCells(_customizer.Length);
            panel.SetActive(true);
            _lastCursorValue = _cursor.ReadValue<Vector2>();

            missText.SetText("{0:1}%", _customizer.MissTenths / 10f);
            hitText.SetText("{0:1}%", _customizer.HitTenths / 10f);
            for (var s = 0; s < _shownTenths.Length; s++)
            {
                _shownTenths[s] = -1;
            }

            _shownSelected = -1;
            _shownBlink = false;
            RefreshLayout();
            RefreshSelection();
        }

        /// <summary>条件を満たしていれば確定して閉じ、true を返す。満たさなければ何もしない（✔・Enter）。</summary>
        public bool Confirm()
        {
            if (!IsOpen)
            {
                return false;
            }

            var layout = _customizer.TryConfirm();
            if (layout == null)
            {
                return false;
            }

            panel.SetActive(false);
            var confirmed = _confirmed;
            _confirmed = null;
            confirmed?.Invoke(layout);
            return true;
        }

        /// <summary>マスをクリックしたとき: 選んでいないマスなら選び、選んでいるマスなら次の図柄にする（plan 3章）。</summary>
        public void ClickCell(int index)
        {
            if (!IsOpen)
            {
                return;
            }

            if (_customizer.Selected == index)
            {
                _customizer.CycleSelected(1);
            }
            else
            {
                _customizer.Select(index);
                RefreshSelection();
            }
        }

        private void OnCursorAction(InputAction.CallbackContext context)
        {
            // 押し続けで繰り返さず、新しく押された方向だけ1回動かす。左右でマス、上で次・下で前の図柄（plan 3章）。
            var value = context.ReadValue<Vector2>();
            var dx = value.x != 0f && _lastCursorValue.x == 0f ? (int)Mathf.Sign(value.x) : 0;
            var dy = dx == 0 && value.y != 0f && _lastCursorValue.y == 0f ? (int)Mathf.Sign(value.y) : 0;
            _lastCursorValue = value;
            if (!IsOpen)
            {
                return;
            }

            if (dx != 0)
            {
                _customizer.MoveSelection(dx);
                RefreshSelection();
            }
            else if (dy != 0)
            {
                _customizer.CycleSelected(dy);
            }
        }

        private void OnConfirmAction(InputAction.CallbackContext context) => Confirm();

        private void OnCycleAction(InputAction.CallbackContext context)
        {
            if (IsOpen)
            {
                _customizer.CycleSelected(1);
            }
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            // 条件を満たさない図柄の印を点滅させる。一時停止の timeScale に左右されないよう実時間で数える。
            var blink = (int)(Time.unscaledTime * BlinkPerSecond) % 2 == 0;
            if (blink != _shownBlink)
            {
                _shownBlink = blink;
                RefreshMarks();
            }
        }

        private void RefreshLayout()
        {
            for (var i = 0; i < _customizer.Length; i++)
            {
                var symbols = _cellSymbols[i];
                var symbol = (int)_customizer.At(i);
                for (var s = 0; s < symbols.childCount; s++)
                {
                    symbols.GetChild(s).gameObject.SetActive(s == symbol);
                }
            }

            for (var s = 0; s < SlotSymbols.Count; s++)
            {
                var tenths = _customizer.SymbolTenths((SlotSymbol)s);
                if (tenths != _shownTenths[s])
                {
                    _shownTenths[s] = tenths;
                    symbolPercentTexts[s].SetText("{0:1}%", tenths / 10f);
                }
            }

            confirmButton.interactable = _customizer.IsValid;
            RefreshMarks();
        }

        private void RefreshMarks()
        {
            for (var s = 0; s < SlotSymbols.Count; s++)
            {
                var error = !_customizer.IsCountAllowed((SlotSymbol)s);
                symbolMarks[s].color = error && _shownBlink ? markErrorColor : markColor;
            }
        }

        private void RefreshSelection()
        {
            var selected = _customizer.Selected;
            if (selected == _shownSelected)
            {
                return;
            }

            _shownSelected = selected;
            for (var i = 0; i < _cellFrames.Count; i++)
            {
                _cellFrames[i].SetActive(i == selected);
            }
        }

        // マスは配列の長さに合わせて複製する（reel_length はデータで変えられる: FR-046）。余りは隠す。
        private void EnsureCells(int count)
        {
            while (_cells.Count < count)
            {
                var index = _cells.Count;
                var cell = Instantiate(cellTemplate, cellTemplate.transform.parent);
                cell.name = $"Cell {index + 1}";
                cell.onClick.AddListener(() =>
                {
                    Deselect();
                    ClickCell(index);
                });
                _cells.Add(cell);
                _cellSymbols.Add(cell.transform.Find(SymbolsName));
                _cellFrames.Add(cell.transform.Find(FrameName).gameObject);
            }

            for (var i = 0; i < _cells.Count; i++)
            {
                _cells[i].gameObject.SetActive(i < count);
            }
        }

        // クリックしたボタンが選択されたままだと Space・Enter（Submit）で押されてしまうので、押したら選択を外す。
        private static void Deselect()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}
