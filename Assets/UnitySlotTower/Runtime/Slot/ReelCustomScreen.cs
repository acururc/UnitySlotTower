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
    /// リールのカスタム画面（T316・T322、FR-046〜048、D-46・D-50、plan 3・5章）。ゲーム開始時だけ Game の上に重ねて開く。
    /// ロックのマス（鍵の印）は変えられない。マウスは下の4図柄から空きマス（または入れたマス）へドラッグ＆ドロップで入れ、
    /// 入れたマスから画面の外側（マス以外）へドラッグすると外す。キーボードは上下で図柄・左右でマス・Space で入れる・
    /// Backspace/Delete で外す・Enter で確定。空きが残っているあいだは ✔ が押せず、空きマスの枠を点滅させる。
    /// マスは <see cref="cellTemplate"/> を複製してロック配置のマス数だけ作り、<see cref="cellsPerRow"/> 個ずつの段に並べる。
    /// </summary>
    public sealed class ReelCustomScreen : MonoBehaviour
    {
        private const string ActionMap = "SlotTower";
        private const string SymbolsName = "Symbols";
        private const string FrameName = "Frame";
        private const string LockName = "Lock";
        private const string EmptyName = "Empty";
        private const float BlinkPerSecond = 3f;

        [SerializeField] private InputActionAsset inputActions;
        [Tooltip("開いているあいだだけ表示する画面全体（背景で下の操作を遮る）。")]
        [SerializeField] private GameObject panel;
        [Tooltip("マスを並べる領域。")]
        [SerializeField] private RectTransform cellArea;
        [Tooltip("マス1つ。子に図柄6つ（SlotSymbol の順）の「Symbols」、選択中の「Frame」、ロックの「Lock」、空きの「Empty」を置く。")]
        [SerializeField] private RectTransform cellTemplate;
        [SerializeField, Min(1)] private int cellsPerRow = 9;
        [Tooltip("入れられる図柄（ReelLocks.Fillable の順: 攻撃力・攻撃速度・回復・コイン）。子に選択中の「Frame」を置く。")]
        [SerializeField] private RectTransform[] paletteItems = new RectTransform[4];
        [Tooltip("ドラッグ中にポインターに付いて動く図柄（子に図柄6つ）。")]
        [SerializeField] private RectTransform dragGhost;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text missText;
        [SerializeField] private TMP_Text hitText;
        [Tooltip("当たり時の図柄ごとの%（SlotSymbol の順）。")]
        [SerializeField] private TMP_Text[] symbolPercentTexts = new TMP_Text[SlotSymbols.Count];
        [SerializeField] private Color lockedCellColor = new Color(0.55f, 0.55f, 0.6f);
        [SerializeField] private Color openCellColor = Color.white;
        [SerializeField] private Color emptyColor = new Color(1f, 0.85f, 0.2f);

        private readonly List<RectTransform> _cells = new List<RectTransform>();
        private readonly List<Transform> _cellSymbols = new List<Transform>();
        private readonly List<GameObject> _cellFrames = new List<GameObject>();
        private readonly List<Graphic> _cellEmpties = new List<Graphic>();
        private readonly int[] _shownTenths = new int[SlotSymbols.Count];

        private InputAction _cursor;
        private InputAction _confirm;
        private InputAction _place;
        private InputAction _clear;
        private Vector2 _lastCursorValue;
        private ReelCustomizer _customizer;
        private Action<ReelLayout> _confirmed;
        private int _shownSelected = -1;
        private int _shownBrush = -1;
        private bool _shownBlink;

        // ドラッグ中の図柄と、入れたマスから持ち出したときの元のマス（パレットからなら -1）。
        private SlotSymbol? _dragSymbol;
        private int _dragFrom = -1;

        /// <summary>開いているか。</summary>
        public bool IsOpen => panel.activeSelf;

        /// <summary>開いているあいだの編集中の配列（テスト・表示用）。閉じているときは null。</summary>
        public ReelCustomizer Customizer => IsOpen ? _customizer : null;

        /// <summary>表示中のマス（リールの並び順）。</summary>
        public IReadOnlyList<RectTransform> Cells => _cells;

        /// <summary>入れられる図柄の札（<see cref="ReelLocks.Fillable"/> の順）。</summary>
        public IReadOnlyList<RectTransform> PaletteItems => paletteItems;

        public Button ConfirmButton => confirmButton;

        /// <summary>最初の空きマス。空きがなければ -1（説明の手の行き先）。</summary>
        public int FirstEmptyCell
        {
            get
            {
                if (_customizer == null)
                {
                    return -1;
                }

                for (var i = 0; i < _customizer.Length; i++)
                {
                    if (_customizer.At(i) == null)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        private void Awake()
        {
            var map = inputActions.FindActionMap(ActionMap, true);
            _cursor = map.FindAction("Cursor", true);
            _confirm = map.FindAction("Confirm", true);
            _place = map.FindAction("Lever", true);
            _clear = map.FindAction("ClearCell", true);
            confirmButton.onClick.AddListener(() =>
            {
                Deselect();
                Confirm();
            });
            for (var i = 0; i < paletteItems.Length; i++)
            {
                AddHandle(paletteItems[i].gameObject, ReelCustomHandle.PaletteKind, i);
            }

            cellTemplate.gameObject.SetActive(false);
            dragGhost.gameObject.SetActive(false);
            panel.SetActive(false);
        }

        private void OnEnable()
        {
            _cursor.performed += OnCursorAction;
            _cursor.canceled += OnCursorAction;
            _confirm.performed += OnConfirmAction;
            _place.performed += OnPlaceAction;
            _clear.performed += OnClearAction;
            _cursor.actionMap.Enable();
        }

        private void OnDisable()
        {
            _cursor.performed -= OnCursorAction;
            _cursor.canceled -= OnCursorAction;
            _confirm.performed -= OnConfirmAction;
            _place.performed -= OnPlaceAction;
            _clear.performed -= OnClearAction;
        }

        /// <summary>
        /// 画面を開く。ロック以外を空きにして編集を始め、確定すると <paramref name="confirmed"/> に新しい配列を渡して閉じる。
        /// </summary>
        public void Open(ReelLocks locks, double missRate, Action<ReelLayout> confirmed)
        {
            if (_customizer != null)
            {
                _customizer.Changed -= RefreshLayout;
            }

            _customizer = new ReelCustomizer(locks, missRate);
            _customizer.Changed += RefreshLayout;
            _confirmed = confirmed;
            EnsureCells(_customizer.Length);
            panel.SetActive(true);
            _lastCursorValue = _cursor.ReadValue<Vector2>();
            EndDrag();

            missText.SetText("{0:1}%", _customizer.MissTenths / 10f);
            hitText.SetText("{0:1}%", _customizer.HitTenths / 10f);
            for (var s = 0; s < _shownTenths.Length; s++)
            {
                _shownTenths[s] = -1;
            }

            _shownSelected = -1;
            _shownBrush = -1;
            _shownBlink = false;
            RefreshLayout();
            RefreshSelection();
        }

        /// <summary>空きがなければ確定して閉じ、true を返す。空きがあれば何もしない（✔・Enter）。</summary>
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

            EndDrag();
            panel.SetActive(false);
            var confirmed = _confirmed;
            _confirmed = null;
            confirmed?.Invoke(layout);
            return true;
        }

        // ---- マウス（ReelCustomHandle から呼ぶ） ----

        internal void OnHandleClick(int kind, int index)
        {
            if (!IsOpen)
            {
                return;
            }

            // クリックはキーボードの選択を合わせるだけ（入れるのはドラッグ＆ドロップ）。
            if (kind == ReelCustomHandle.PaletteKind)
            {
                _customizer.CycleBrush(index - Array.IndexOf(ReelLocks.Fillable, _customizer.Brush));
            }
            else
            {
                _customizer.Select(index);
            }

            RefreshSelection();
        }

        internal void OnHandleBeginDrag(int kind, int index, PointerEventData eventData)
        {
            if (!IsOpen)
            {
                return;
            }

            if (kind == ReelCustomHandle.PaletteKind)
            {
                _dragSymbol = ReelLocks.Fillable[index];
                _dragFrom = -1;
            }
            else if (!_customizer.IsLocked(index) && _customizer.At(index) != null)
            {
                _dragSymbol = _customizer.At(index);
                _dragFrom = index;
            }
            else
            {
                return;
            }

            ShowSymbol(dragGhost, _dragSymbol.Value);
            dragGhost.gameObject.SetActive(true);
            MoveGhost(eventData);
        }

        internal void OnHandleDrag(PointerEventData eventData)
        {
            if (_dragSymbol != null)
            {
                MoveGhost(eventData);
            }
        }

        internal void OnHandleEndDrag(PointerEventData eventData)
        {
            if (_dragSymbol == null || !IsOpen)
            {
                EndDrag();
                return;
            }

            var symbol = _dragSymbol.Value;
            var from = _dragFrom;
            var target = CellUnder(eventData);
            EndDrag();
            if (target >= 0)
            {
                // 入れたマスから別のマスへ動かしたら、元のマスは空きに戻す。
                if (_customizer.Place(target, symbol) && from >= 0 && from != target)
                {
                    _customizer.Clear(from);
                }

                _customizer.Select(target);
                RefreshSelection();
            }
            else if (from >= 0)
            {
                _customizer.Clear(from);
            }
        }

        private int CellUnder(PointerEventData eventData)
        {
            for (var i = 0; i < _cells.Count; i++)
            {
                if (_cells[i].gameObject.activeSelf &&
                    RectTransformUtility.RectangleContainsScreenPoint(_cells[i], eventData.position, eventData.pressEventCamera))
                {
                    return i;
                }
            }

            return -1;
        }

        private void MoveGhost(PointerEventData eventData)
        {
            var parent = (RectTransform)dragGhost.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera,
                    out var local))
            {
                dragGhost.anchoredPosition = local;
            }
        }

        private void EndDrag()
        {
            _dragSymbol = null;
            _dragFrom = -1;
            dragGhost.gameObject.SetActive(false);
        }

        // ---- キーボード ----

        private void OnCursorAction(InputAction.CallbackContext context)
        {
            // 押し続けで繰り返さず、新しく押された方向だけ1回動かす。左右でマス、下で次・上で前の図柄（D-50）。
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
            }
            else if (dy != 0)
            {
                _customizer.CycleBrush(-dy);
            }

            RefreshSelection();
        }

        private void OnConfirmAction(InputAction.CallbackContext context) => Confirm();

        private void OnPlaceAction(InputAction.CallbackContext context)
        {
            if (IsOpen)
            {
                _customizer.PlaceSelected();
            }
        }

        private void OnClearAction(InputAction.CallbackContext context)
        {
            if (IsOpen)
            {
                _customizer.ClearSelected();
            }
        }

        // ---- 表示 ----

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            // 空きマスの枠を点滅させる。一時停止の timeScale に左右されないよう実時間で数える。
            var blink = (int)(Time.unscaledTime * BlinkPerSecond) % 2 == 0;
            if (blink != _shownBlink)
            {
                _shownBlink = blink;
                RefreshEmpties();
            }
        }

        private void RefreshLayout()
        {
            for (var i = 0; i < _customizer.Length; i++)
            {
                var symbol = _customizer.At(i);
                var symbols = _cellSymbols[i];
                for (var s = 0; s < symbols.childCount; s++)
                {
                    symbols.GetChild(s).gameObject.SetActive(symbol.HasValue && s == (int)symbol.Value);
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
            RefreshEmpties();
        }

        private void RefreshEmpties()
        {
            for (var i = 0; i < _customizer.Length; i++)
            {
                var empty = _customizer.At(i) == null;
                var graphic = _cellEmpties[i];
                graphic.gameObject.SetActive(empty);
                if (empty)
                {
                    var color = emptyColor;
                    color.a = _shownBlink ? 1f : 0.35f;
                    graphic.color = color;
                }
            }
        }

        private void RefreshSelection()
        {
            var selected = _customizer.Selected;
            if (selected != _shownSelected)
            {
                _shownSelected = selected;
                for (var i = 0; i < _cellFrames.Count; i++)
                {
                    _cellFrames[i].SetActive(i == selected);
                }
            }

            var brush = Array.IndexOf(ReelLocks.Fillable, _customizer.Brush);
            if (brush != _shownBrush)
            {
                _shownBrush = brush;
                for (var i = 0; i < paletteItems.Length; i++)
                {
                    paletteItems[i].Find(FrameName).gameObject.SetActive(i == brush);
                }
            }
        }

        private static void ShowSymbol(Transform root, SlotSymbol symbol)
        {
            var symbols = root.Find(SymbolsName);
            for (var s = 0; s < symbols.childCount; s++)
            {
                symbols.GetChild(s).gameObject.SetActive(s == (int)symbol);
            }
        }

        // マスはロック配置の長さに合わせて複製し、cellsPerRow 個ずつ上の段から並べる（reel_length はデータで変えられる）。
        private void EnsureCells(int count)
        {
            while (_cells.Count < count)
            {
                var index = _cells.Count;
                var cell = Instantiate(cellTemplate, cellArea);
                cell.name = $"Cell {index + 1}";
                AddHandle(cell.gameObject, ReelCustomHandle.CellKind, index);
                _cells.Add(cell);
                _cellSymbols.Add(cell.Find(SymbolsName));
                _cellFrames.Add(cell.Find(FrameName).gameObject);
                _cellEmpties.Add(cell.Find(EmptyName).GetComponent<Graphic>());
            }

            var rows = Mathf.Max(1, Mathf.CeilToInt((float)count / cellsPerRow));
            const float gap = 0.12f;
            for (var i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                cell.gameObject.SetActive(i < count);
                if (i >= count)
                {
                    continue;
                }

                var column = i % cellsPerRow;
                var row = i / cellsPerRow;
                cell.anchorMin = new Vector2((column + gap / 2f) / cellsPerRow, 1f - (row + 1 - gap / 2f) / rows);
                cell.anchorMax = new Vector2((column + 1 - gap / 2f) / cellsPerRow, 1f - (row + gap / 2f) / rows);
                cell.offsetMin = cell.offsetMax = Vector2.zero;
                var locked = _customizer.IsLocked(i);
                cell.GetComponent<Image>().color = locked ? lockedCellColor : openCellColor;
                cell.Find(LockName).gameObject.SetActive(locked);
            }
        }

        private void AddHandle(GameObject target, int kind, int index)
        {
            // Unity の「偽の null」は ?? で扱えないので比較で確かめる。
            var handle = target.GetComponent<ReelCustomHandle>();
            if (handle == null)
            {
                handle = target.AddComponent<ReelCustomHandle>();
            }

            handle.Bind(this, kind, index);
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
