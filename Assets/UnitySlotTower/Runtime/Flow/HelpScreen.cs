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

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// 一時停止中の説明（T404・T351、FR-012、plan 16.10）。一時停止中だけ「?」ボタンを出し、押すと早見表を開く。
    /// 上半分はスロット: 現在の設定の20パターンの内訳（ハズレと図柄ごとの %＝個数×5）と効果（色7 → その職とゲージ+1、
    /// バー → 攻撃力 +10%〔上限〕、コイン → 所持金の倍率〔上限〕。値は slot_rules.csv）。
    /// 下半分は味方: 本体の色とスキルの色、右向きの範囲図、現在の強化段階での HP・攻撃（バッファーは倍率）・防御・攻撃間隔・
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
            [Tooltip("スキルの印（ゲージの点）。スキルのない職では隠す。")]
            public GameObject skillMark;
            [Tooltip("スキルの印の点。スキルの色に塗る。")]
            public Image[] skillDots = Array.Empty<Image>();
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
        [SerializeField] private TMP_Text missText;
        [Tooltip("図柄ごとの20パターン中の割合（SlotSymbol の順）。")]
        [SerializeField] private TMP_Text[] chanceTexts = new TMP_Text[SlotSymbols.Count];
        [Tooltip("図柄ごとの効果の数値（SlotSymbol の順）。")]
        [SerializeField] private TMP_Text[] effectTexts = new TMP_Text[SlotSymbols.Count];
        [Tooltip("効果の上限（SlotSymbol の順。上限のない図柄は null）。")]
        [FormerlySerializedAs("secondsTexts")]
        [SerializeField] private TMP_Text[] capTexts = new TMP_Text[SlotSymbols.Count];
        [Tooltip("色7の行の、その色のスキルを持つ職の札（SlotSymbol の順。色7以外は null）。職の本体の色に塗る。")]
        [SerializeField] private Image[] jobCards = new Image[SlotSymbols.Count];
        [Tooltip("色7（赤・青・緑・黄）の色。スキルの印に使う。")]
        [SerializeField] private Color[] sevenColors = new Color[4];
        [Tooltip("units.csv の並び。")]
        [SerializeField] private UnitRow[] unitRows = Array.Empty<UnitRow>();
        [SerializeField] private Color rangeCellColor = new Color(1f, 0.6f, 0.15f);
        [SerializeField] private Color emptyCellColor = new Color(0.22f, 0.22f, 0.26f);

        private InputAction _help;
        private bool _gridsBuilt;

        /// <summary>早見表を開いているか。</summary>
        public bool IsOpen => panel.activeSelf;

        public Button OpenButton => openButton;
        public Button CloseButton => closeButton;

        private void Awake()
        {
            _help = inputActions.FindActionMap(ActionMap, true).FindAction("Help", true);
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            panel.SetActive(false);
            openButton.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _help.performed += OnHelpAction;
            _help.actionMap.Enable();
        }

        private void OnDisable()
        {
            _help.performed -= OnHelpAction;
        }

        private void Update()
        {
            // 再開したら閉じる（Esc・P・一時停止ボタン）。
            if (IsOpen && !battle.IsPaused)
            {
                panel.SetActive(false);
            }

            var showButton = battle.IsPaused && !IsOpen;
            if (openButton.gameObject.activeSelf != showButton)
            {
                openButton.gameObject.SetActive(showButton);
            }
        }

        /// <summary>一時停止中なら早見表を開く。一時停止していなければ何もしない。</summary>
        public void Open()
        {
            if (!battle.IsPaused || battle.Run == null)
            {
                return;
            }

            Deselect();
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

                var skillColor = default(SlotSymbol);
                var hasSkill = battle.Skills != null && battle.Skills.TryGetColor(unit.Id, out skillColor);
                if (row.skillMark != null)
                {
                    row.skillMark.SetActive(hasSkill);
                    if (hasSkill)
                    {
                        for (var d = 0; d < row.skillDots.Length; d++)
                        {
                            row.skillDots[d].color = SevenColor(skillColor);
                        }
                    }
                }

                row.defText.SetText("{0}", level.DefOf(unit));
                row.intervalText.SetText("{0:1}", (float)unit.AttackInterval);
                row.blockText.SetText("{0}", unit.Block);
                row.costText.SetText(NumericText.YenFormat, unit.Cost);
                row.cooldownText.SetText("{0}", (float)Math.Ceiling(unit.Cooldown));
            }

            BuildGridsOnce();
        }

        private static int Percent(int count, int total) =>
            total > 0 ? (int)Math.Round(count * 100.0 / total, MidpointRounding.AwayFromZero) : 0;

        private Color SevenColor(SlotSymbol color) =>
            (int)color < sevenColors.Length ? sevenColors[(int)color] : Color.white;

        // 効果の欄（plan 16.4）: 色7 → その職の札とゲージ+1、バー → 攻撃力の上がり幅と上限、コイン → 所持金の倍率と上限。
        private void RefreshEffects()
        {
            var rules = slotCabinet.Rules;
            var units = battle.Units.All;
            for (var s = 0; s < SlotSymbols.Count; s++)
            {
                var symbol = (SlotSymbol)s;
                var effect = effectTexts[s];
                var cap = s < capTexts.Length ? capTexts[s] : null;
                if (effect == null)
                {
                    continue;
                }

                switch (symbol)
                {
                    case SlotSymbol.Bar:
                        effect.SetText("+{0}%", (int)Math.Round(rules.BarStep * 100, MidpointRounding.AwayFromZero));
                        if (cap != null)
                        {
                            cap.SetText("+{0}%", (int)Math.Round(rules.BarCap * 100, MidpointRounding.AwayFromZero));
                        }

                        break;
                    case SlotSymbol.Coin:
                        effect.SetText("×{0:1}", (float)(1 + rules.CoinStep));
                        if (cap != null)
                        {
                            cap.SetText("×{0:1}", (float)rules.CoinCap);
                        }

                        break;
                    default:
                        effect.SetText("+1");
                        var card = s < jobCards.Length ? jobCards[s] : null;
                        if (card != null && battle.Skills != null)
                        {
                            var unitId = battle.Skills.For(symbol).UnitId;
                            for (var i = 0; i < units.Count && i < unitRows.Length; i++)
                            {
                                if (units[i].Id == unitId)
                                {
                                    card.color = unitRows[i].bodyColor;
                                }
                            }
                        }

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
