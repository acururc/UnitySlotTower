using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Ui;
using UnitySlotTower.Slot;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// 一時停止中の早見表（T404・T351、spec 016）。一時停止中だけ「?」ボタンを出し、押すと開く。
    /// ページは 遊び方・スロットの回し方・図柄の効果・味方・敵 の5つ（spec 016 FR-001）。タブと ◀ ▶（端で反対の端へ回る）で切り替える。
    /// 数値は FR-004 の項目だけを開くたびに CSV の値で書く: 青7・黄7・バーの続く秒数、味方のブロック数・コスト・再配置の待ち秒数。
    /// 範囲図は右向きで、本人のマスを本体の色に ▶ で示す。各行の動く図は <see cref="HelpDemo"/>。
    /// キーボードは ?（/）キーで開閉（一時停止していなければ一時停止して開く）。◀ で閉じる。再開すると閉じる。
    /// </summary>
    public sealed class HelpScreen : MonoBehaviour
    {
        private const string ActionMap = "SlotTower";
        private const float CellGap = 0.08f;

        [Serializable]
        public sealed class UnitRow
        {
            [Tooltip("範囲図を描く領域。マスは開いたときに作る。")]
            public RectTransform rangeArea;
            [Tooltip("範囲図で味方のいるマスの色（操作バーのアイコンと同じ色）。")]
            public Color bodyColor = Color.white;
            public TMP_Text blockText;
            public TMP_Text costText;
            public TMP_Text cooldownText;
        }

        public enum Page { Guide, Bet, Symbols, Units, Enemies }

        public const int PageCount = 5;

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private BattleController battle;
        [SerializeField] private SlotCabinet slotCabinet;
        [Tooltip("一時停止中だけ出す「?」ボタン。")]
        [SerializeField] private Button openButton;
        [Tooltip("開いているあいだだけ表示する画面全体（背景で下の操作を遮る）。")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button closeButton;
        [Tooltip("Page の順。")]
        [SerializeField] private GameObject[] pages = new GameObject[PageCount];
        [Tooltip("Page の順。")]
        [SerializeField] private Button[] tabs = new Button[PageCount];
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [Tooltip("図柄ごとの続く秒数（SlotSymbol の順。秒数のない図柄は null）。")]
        [SerializeField] private TMP_Text[] secondsTexts = new TMP_Text[SlotSymbols.Count];
        [Tooltip("units.csv の並び。")]
        [SerializeField] private UnitRow[] unitRows = Array.Empty<UnitRow>();
        [SerializeField] private Color rangeCellColor = new Color(1f, 0.6f, 0.15f);
        [SerializeField] private Color emptyCellColor = new Color(0.22f, 0.22f, 0.26f);

        private QuitToTitle _quit;
        private bool ConfirmOpen => _quit != null && _quit.IsConfirmOpen;
        private InputAction _help;
        private bool _gridsBuilt;
        private UnityAction[] _tabActions;

        /// <summary>早見表を開いているか。</summary>
        public bool IsOpen => panel.activeSelf;

        public Button OpenButton => openButton;
        public Button CloseButton => closeButton;

        public Page CurrentPage { get; private set; } = Page.Guide;
        public Button PreviousButton => previousButton;
        public Button NextButton => nextButton;

        /// <summary>ページのタブ。</summary>
        public Button Tab(Page page) => tabs[(int)page];

        /// <summary>ページの中身。</summary>
        public GameObject PageRoot(Page page) => pages[(int)page];

        public void ShowPage(Page page)
        {
            CurrentPage = page;
            for (var i = 0; i < pages.Length; i++)
            {
                var shown = i == (int)page;
                if (pages[i] != null && pages[i].activeSelf != shown)
                {
                    pages[i].SetActive(shown);
                }

                if (i < tabs.Length && tabs[i] != null)
                {
                    tabs[i].image.color = shown ? UiStyle.Ally : UiStyle.ButtonFill;
                }
            }

            Deselect();
        }

        public void NextPage() => ShowPage((Page)(((int)CurrentPage + 1) % PageCount));
        public void PreviousPage() => ShowPage((Page)(((int)CurrentPage + PageCount - 1) % PageCount));

        private void Awake()
        {
            _quit = GetComponent<QuitToTitle>();
            _help = inputActions.FindActionMap(ActionMap, true).FindAction("Help", true);
            _tabActions = new UnityAction[PageCount];
            for (var i = 0; i < PageCount; i++)
            {
                var page = (Page)i;
                _tabActions[i] = () => ShowPage(page);
            }

            panel.SetActive(false);
            openButton.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            for (var i = 0; i < PageCount && i < tabs.Length; i++)
            {
                if (tabs[i] != null) tabs[i].onClick.AddListener(_tabActions[i]);
            }

            previousButton.onClick.AddListener(PreviousPage);
            nextButton.onClick.AddListener(NextPage);
            _help.performed += OnHelpAction;
            _help.actionMap.Enable();
        }

        private void OnDisable()
        {
            openButton.onClick.RemoveListener(Open);
            closeButton.onClick.RemoveListener(Close);
            for (var i = 0; i < PageCount && i < tabs.Length; i++)
            {
                if (tabs[i] != null) tabs[i].onClick.RemoveListener(_tabActions[i]);
            }

            previousButton.onClick.RemoveListener(PreviousPage);
            nextButton.onClick.RemoveListener(NextPage);
            _help.performed -= OnHelpAction;
        }

        private void Update()
        {
            // 再開したら閉じる（Esc・P・一時停止ボタン）。
            if (IsOpen && !battle.IsPaused)
            {
                panel.SetActive(false);
            }

            var showButton = battle.IsPaused && !IsOpen && !ConfirmOpen;
            if (openButton.gameObject.activeSelf != showButton)
            {
                openButton.gameObject.SetActive(showButton);
            }
        }

        /// <summary>一時停止中なら早見表を開く。一時停止していなければ何もしない。</summary>
        public void Open()
        {
            if (ConfirmOpen || !battle.IsPaused || battle.Run == null)
            {
                return;
            }

            ShowPage(CurrentPage);
            Refresh();
            panel.SetActive(true);
            openButton.gameObject.SetActive(false);
        }

        /// <summary>早見表を閉じる。一時停止は続く。</summary>
        public void Close()
        {
            Deselect();
            panel.SetActive(false);
        }

        private void OnHelpAction(InputAction.CallbackContext context)
        {
            if (ConfirmOpen) return;
            if (IsOpen)
            {
                Close();
                return;
            }

            // カスタム中・ゲーム終了後は一時停止できないので開かない。
            if (!battle.IsPaused)
            {
                battle.SetPaused(true);
            }

            Open();
        }

        // 開くたびに今の値で書き直す（FR-004 の項目だけ）。
        private void Refresh()
        {
            var rules = slotCabinet.Rules;
            SetSeconds(SlotSymbol.Blue7, rules.BlueStopSeconds);
            SetSeconds(SlotSymbol.Yellow7, rules.YellowPoisonSeconds);
            SetSeconds(SlotSymbol.Bar, rules.BarSeconds);

            var units = battle.Units.All;
            for (var i = 0; i < unitRows.Length && i < units.Count; i++)
            {
                var row = unitRows[i];
                var unit = units[i];
                BlockText.Show(row.blockText, unit.Block);
                row.costText.SetText(NumericText.YenFormat, unit.Cost);
                row.cooldownText.SetText("{0}", (float)Math.Ceiling(unit.Cooldown));
            }

            BuildGridsOnce();
        }

        private void SetSeconds(SlotSymbol symbol, double seconds)
        {
            var index = (int)symbol;
            if (index < secondsTexts.Length && secondsTexts[index] != null)
            {
                secondsTexts[index].SetText("{0}", (int)Math.Round(seconds, MidpointRounding.AwayFromZero));
            }
        }

        // 範囲図: 全員の範囲（右向き）と本人のマスが収まる同じ大きさの格子に、範囲のマスを塗る。本人のマスは本体の色に ▶ で向きを示す。
        private void BuildGridsOnce()
        {
            if (_gridsBuilt)
            {
                return;
            }

            _gridsBuilt = true;
            var units = battle.Units.All;
            int minX = 0, maxX = 0, minY = 0, maxY = 0;
            for (var i = 0; i < unitRows.Length && i < units.Count; i++)
            {
                foreach (var cell in battle.Ranges.CellsFrom(units[i].Id, default, Facing.Right))
                {
                    minX = Mathf.Min(minX, cell.X);
                    maxX = Mathf.Max(maxX, cell.X);
                    minY = Mathf.Min(minY, cell.Y);
                    maxY = Mathf.Max(maxY, cell.Y);
                }
            }

            var columns = maxX - minX + 1;
            var rows = maxY - minY + 1;
            for (var i = 0; i < unitRows.Length && i < units.Count; i++)
            {
                var row = unitRows[i];
                var grid = CreateRect("Grid", row.rangeArea, Vector2.zero, Vector2.one);
                var fitter = grid.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = (float)columns / rows;
                var range = battle.Ranges.CellsFrom(units[i].Id, default, Facing.Right);
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var min = new Vector2((x - minX + CellGap) / columns, 1f - (y - minY + 1 - CellGap) / rows);
                        var max = new Vector2((x - minX + 1 - CellGap) / columns, 1f - (y - minY + CellGap) / rows);
                        var isOrigin = x == 0 && y == 0;
                        var color = isOrigin ? row.bodyColor : Contains(range, x, y) ? rangeCellColor : emptyCellColor;
                        var cell = CreateRect($"Cell {x},{y}", grid, min, max);
                        var image = cell.gameObject.AddComponent<Image>();
                        image.color = color;
                        image.raycastTarget = false;
                        if (isOrigin)
                        {
                            var arrow = CreateRect("Facing", cell, new Vector2(0.3f, 0.25f), new Vector2(0.75f, 0.75f));
                            var triangle = arrow.gameObject.AddComponent<TriangleGraphic>();
                            triangle.color = Color.white;
                            triangle.raycastTarget = false;
                        }
                    }
                }
            }
        }

        private static bool Contains(System.Collections.Generic.IReadOnlyList<GridPoint> cells, int x, int y)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                if (cells[i].X == x && cells[i].Y == y)
                {
                    return true;
                }
            }

            return false;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
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
