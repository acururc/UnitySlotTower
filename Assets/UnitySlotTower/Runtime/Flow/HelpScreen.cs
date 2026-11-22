using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
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
    /// 一時停止中の説明（T404・T351、FR-012、plan 16.10）。一時停止中だけ「?」ボタンを出し、押すと早見表を開く。
    /// 1ページ目はスロット: 全ステージ共通の20パターンの内訳（ハズレと図柄ごとの %＝個数×5）と効果（T615、spec 002 FR-112:
    /// 赤7 → 敵全員にダメージ、青7 → 敵の動きが ×0〔秒数〕、緑7 → 味方の HP を回復〔%〕、黄7 → 毒のダメージ〔秒数〕、
    /// バー → 攻撃力の倍率〔秒数〕、コイン → 所持金の倍率〔上限〕。値は slot_rules.csv）。
    /// 2ページ目は味方: 本体の色、右向きの範囲図、現在の強化段階での HP・攻撃（バッファーは倍率）・防御・攻撃間隔・
    /// ブロック数・コスト・待ち時間。
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
            [Tooltip("攻撃の味方で出すアイコン。")]
            public GameObject attackIcon;
            [Tooltip("バッファーで攻撃アイコンの代わりに出すアイコン（数字は倍率）。")]
            [FormerlySerializedAs("healIcon")]
            public GameObject buffIcon;
            public TMP_Text hpText;
            public TMP_Text atkText;
            public TMP_Text defText;
            public TMP_Text intervalText;
            public TMP_Text blockText;
            public TMP_Text costText;
            public TMP_Text cooldownText;
        }

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private BattleController battle;
        [SerializeField] private SlotCabinet slotCabinet;
        [Tooltip("一時停止中だけ出す「?」ボタン。")]
        [SerializeField] private Button openButton;
        [Tooltip("開いているあいだだけ表示する画面全体（背景で下の操作を遮る）。")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button closeButton;
        public enum Page { Slot, Units }

        [SerializeField] private GameObject slotPage;
        [SerializeField] private GameObject unitPage;
        [SerializeField] private Button slotTab;
        [SerializeField] private Button unitTab;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text missText;
        [Tooltip("図柄ごとの20パターン中の割合（SlotSymbol の順）。")]
        [SerializeField] private TMP_Text[] chanceTexts = new TMP_Text[SlotSymbols.Count];
        [Tooltip("図柄ごとの効果の数値（SlotSymbol の順）。")]
        [SerializeField] private TMP_Text[] effectTexts = new TMP_Text[SlotSymbols.Count];
        [Tooltip("効果の2つ目の数値（SlotSymbol の順。続く秒数か上限。ない図柄は null）。")]
        [FormerlySerializedAs("secondsTexts")]
        [SerializeField] private TMP_Text[] capTexts = new TMP_Text[SlotSymbols.Count];
        [Tooltip("units.csv の並び。")]
        [SerializeField] private UnitRow[] unitRows = Array.Empty<UnitRow>();
        [SerializeField] private Color rangeCellColor = new Color(1f, 0.6f, 0.15f);
        [SerializeField] private Color emptyCellColor = new Color(0.22f, 0.22f, 0.26f);

        private QuitToTitle _quit;
        private bool ConfirmOpen => _quit != null && _quit.IsConfirmOpen;
        private InputAction _help;
        private bool _gridsBuilt;

        /// <summary>早見表を開いているか。</summary>
        public bool IsOpen => panel.activeSelf;

        public Button OpenButton => openButton;
        public Button CloseButton => closeButton;

        public Page CurrentPage { get; private set; } = Page.Slot;
        public Button SlotTab => slotTab;
        public Button UnitTab => unitTab;
        public Button PreviousButton => previousButton;
        public Button NextButton => nextButton;

        public void ShowPage(Page page)
        {
            CurrentPage = page;
            var slot = page == Page.Slot;
            slotPage.SetActive(slot);
            unitPage.SetActive(!slot);
            slotTab.image.color = slot ? UiStyle.Ally : UiStyle.ButtonFill;
            unitTab.image.color = slot ? UiStyle.ButtonFill : UiStyle.Ally;
            Deselect();
        }

        private void ShowSlot() => ShowPage(Page.Slot);
        private void ShowUnits() => ShowPage(Page.Units);
        public void NextPage() => ShowPage(CurrentPage == Page.Slot ? Page.Units : Page.Slot);
        public void PreviousPage() => NextPage();

        private void Awake()
        {
            _quit = GetComponent<QuitToTitle>();
            _help = inputActions.FindActionMap(ActionMap, true).FindAction("Help", true);
            panel.SetActive(false);
            openButton.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            slotTab.onClick.AddListener(ShowSlot);
            unitTab.onClick.AddListener(ShowUnits);
            previousButton.onClick.AddListener(PreviousPage);
            nextButton.onClick.AddListener(NextPage);
            _help.performed += OnHelpAction;
            _help.actionMap.Enable();
        }

        private void OnDisable()
        {
            openButton.onClick.RemoveListener(Open);
            closeButton.onClick.RemoveListener(Close);
            slotTab.onClick.RemoveListener(ShowSlot);
            unitTab.onClick.RemoveListener(ShowUnits);
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

        // 開くたびに今の値で書き直す（設定・強化段階は一時停止の前に変わっている）。
        private void Refresh()
        {
            // 20パターンの内訳（plan 16.10）。% は個数×(100/合計)。
            var patterns = slotCabinet.Patterns;
            var counts = new int[SlotSymbols.Count + 1];
            foreach (var outcome in patterns)
            {
                counts[outcome.HasValue ? (int)outcome.Value : SlotSymbols.Count]++;
            }

            missText.SetText("{0}%", Percent(counts[SlotSymbols.Count], patterns.Count));
            for (var s = 0; s < SlotSymbols.Count; s++)
            {
                chanceTexts[s].SetText("{0}%", Percent(counts[s], patterns.Count));
            }

            RefreshEffects();

            var units = battle.Units.All;
            for (var i = 0; i < unitRows.Length && i < units.Count; i++)
            {
                var row = unitRows[i];
                var unit = units[i];
                var level = battle.Upgrades.Current(unit.Id);
                // バッファーは攻撃しないので、攻撃の代わりに倍率を出す（倍率は強化で変わらない）。
                var buffs = unit.Role == UnitRole.Buff;
                row.attackIcon.SetActive(!buffs);
                row.buffIcon.SetActive(buffs);
                row.hpText.SetText("{0}", level.MaxHpOf(unit));
                if (buffs)
                {
                    row.atkText.SetText("×{0:1}", (float)unit.BuffRate);
                }
                else
                {
                    row.atkText.SetText("{0}", level.AtkOf(unit));
                }

                row.defText.SetText("{0}", level.DefOf(unit));
                row.intervalText.SetText("{0:1}", (float)unit.AttackInterval);
                BlockText.Show(row.blockText, unit.Block);
                row.costText.SetText(NumericText.YenFormat, unit.Cost);
                row.cooldownText.SetText("{0}", (float)Math.Ceiling(unit.Cooldown));
            }

            BuildGridsOnce();
        }

        private static int Percent(int count, int total) =>
            total > 0 ? (int)Math.Round(count * 100.0 / total, MidpointRounding.AwayFromZero) : 0;

        private static void SetCap(TMP_Text cap, int seconds)
        {
            if (cap != null)
            {
                cap.SetText("{0}", seconds);
            }
        }

        private static int Seconds(double seconds) => (int)Math.Round(seconds, MidpointRounding.AwayFromZero);

        // 効果の欄（T615）: 1つ目の数値は効果の強さ、2つ目は続く秒数（青7・黄7・バー）か上限（コイン）。
        private void RefreshEffects()
        {
            var rules = slotCabinet.Rules;
            for (var s = 0; s < SlotSymbols.Count; s++)
            {
                var effect = effectTexts[s];
                var cap = s < capTexts.Length ? capTexts[s] : null;
                if (effect == null)
                {
                    continue;
                }

                switch ((SlotSymbol)s)
                {
                    case SlotSymbol.Red7:
                        effect.SetText("-{0}", rules.RedDamage);
                        break;
                    case SlotSymbol.Blue7:
                        effect.SetText("×0");
                        SetCap(cap, Seconds(rules.BlueStopSeconds));
                        break;
                    case SlotSymbol.Green7:
                        effect.SetText("+{0}%", (int)Math.Round(rules.GreenHealRate * 100, MidpointRounding.AwayFromZero));
                        break;
                    case SlotSymbol.Yellow7:
                        effect.SetText("-{0}", rules.YellowPoisonDamage);
                        SetCap(cap, Seconds(rules.YellowPoisonSeconds));
                        break;
                    case SlotSymbol.Bar:
                        effect.SetText("×{0:1}", (float)rules.BarAttackRate);
                        SetCap(cap, Seconds(rules.BarSeconds));
                        break;
                    case SlotSymbol.Coin:
                        effect.SetText("×{0:1}", (float)(1 + rules.CoinStep));
                        if (cap != null) cap.SetText("×{0:1}", (float)rules.CoinCap);
                        break;
                }
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
