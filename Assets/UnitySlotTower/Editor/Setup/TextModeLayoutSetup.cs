using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySlotTower.Core.Layout;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;
using UnitySlotTower.Ui;
using Object = UnityEngine.Object;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// 文字あり版の作り直し（T359、FR-006、D-76、plan 16.13）。既存の部品に <see cref="TextModeLayout"/> を付け、文字あり版の配置と見た目を置く。
    /// シーンの値（文字なし版）は書き換えない。再実行しても同じ結果になる（文字なし版の値は、部品が既にあればその値、なければシーンの値）。
    /// 画面ごとの中身は T359b〜T359f で足す。
    /// </summary>
    internal static class TextModeLayoutSetup
    {
        /// <summary>文字あり版の文字の最小の大きさ（基準解像度 1440×1920、plan 16.13）。</summary>
        public const float MinFontSize = 36f;

        [MenuItem("Tools/UnitySlotTower/Setup/Build Text Mode Layout (T359)")]
        private static void Build()
        {
            var title = Object.FindAnyObjectByType<TitleScreen>(FindObjectsInactive.Include);
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            if (title != null)
            {
                BuildTitle(title);
                Save(title.gameObject);
            }
            else if (cabinet != null)
            {
                BuildGame(cabinet);
                Save(cabinet.gameObject);
            }
            else
            {
                Debug.LogError("[SlotTower Setup] Title か Game シーンを開いてから実行する。");
            }
        }

        // 文字あり版の難易度選択の札の下端（Title の割合。上の段・下の段）。札の下に名前（TextModeSetup）が入るように段の間を広げる。
        private static readonly float[] PlateRowBottoms = { 0.5f, 0.2f };

        /// <summary>難易度選択: 札を下げて段の間を広げ、札の下に設定の名前、上に大きな見出しを入れる（plan 16.13）。タイトルは今のまま。</summary>
        private static void BuildTitle(TitleScreen title)
        {
            var settings = new SerializedObject(title).FindProperty("settingButtons");
            for (var i = 0; i < settings.arraySize; i++)
            {
                var plate = ((Component)settings.GetArrayElementAtIndex(i).objectReferenceValue).gameObject;
                var bottom = PlateRowBottoms[i / 3];
                Set(plate, TextModeLayout.Parts.Rect, s =>
                {
                    var height = s.anchorMax.y - s.anchorMin.y;
                    s.anchorMin.y = bottom;
                    s.anchorMax.y = bottom + height;
                    return s;
                });
            }
        }

        private static void BuildGame(SlotCabinet cabinet)
        {
            var root = (RectTransform)cabinet.transform.parent;
            BuildBattleHud(root);
            BuildCabinetLabels((RectTransform)cabinet.transform);
            BuildRoute(root);
            BuildShop(root);
            BuildRest(root);
            BuildResult(root);
            BuildHelp(root);
        }

        // --- T359f 早見表 ---
        // 画面いっぱい（1440×1920 px）。上から 閉じる・題（1795〜1891）、スロットの見出し・列の名前・7行（70 px）、区切り、
        // 味方の見出し・列の名前・4行（255 px）。y は px（下端, 上端）。ラベルは TextModeSetup がこの値で作る。

        internal static readonly Vector2 HelpSlotHeadingY = new Vector2(1716f, 1786f);
        internal static readonly Vector2 HelpSlotColumnsY = new Vector2(1666f, 1716f);
        private const float HelpSlotRowHeight = 70f;
        private static readonly Vector2 HelpDividerY = new Vector2(1160f, 1166f);
        internal static readonly Vector2 HelpUnitHeadingY = new Vector2(1080f, 1150f);
        internal static readonly Vector2 HelpUnitColumnsY = new Vector2(1030f, 1080f);
        private const float HelpUnitRowHeight = 255f;

        // 図柄の行（行の割合）: 図柄・確率・▶・効果（〜0.48）・説明（0.49〜）。
        internal const float HelpDescriptionLeft = 0.49f;

        // 味方の行（行の割合）: 左に本体の札と職名とスキルの印、範囲図、数値2列（各4段 50 px）、下の段にスキルの説明。
        internal const float HelpRangeLeft = 0.165f;
        internal const float HelpRangeRight = 0.335f;
        internal const float HelpSkillLineTop = 52f / HelpUnitRowHeight;
        // 数値の列の左端（アイコン）と数字の左端。名前はその間。
        internal static readonly float[] HelpStatColumns = { 0.35f, 0.66f };
        private static readonly float[] HelpStatValueLefts = { 0.505f, 0.86f };
        private const float HelpStatIcon = 36f;

        /// <summary>数値の名前の幅（px）。アイコンの右から数字の左まで。</summary>
        internal static float HelpStatNameWidth(int column) =>
            (HelpStatValueLefts[column] - HelpStatColumns[column]) * 1440f - HelpStatIcon - 12f;

        /// <summary>
        /// 早見表: 表に見出しと列の名前を入れ、図柄の行は1行にして説明を右に、味方の行は数値に名前を付けて下にスキルの説明を入れる（plan 16.13）。
        /// </summary>
        private static void BuildHelp(RectTransform root)
        {
            var panel = PanelOf(root.GetComponentInChildren<HelpScreen>(true));
            SetRect(Child(panel, "Divider"), new Vector2(0.04f, HelpDividerY.x / 1920f), new Vector2(0.96f, HelpDividerY.y / 1920f));

            var slotRows = Child(panel, HelpScreenSetup.SlotRowsName);
            for (var i = 0; i < slotRows.childCount; i++)
            {
                var top = HelpSlotColumnsY.x - i * HelpSlotRowHeight;
                var row = (RectTransform)slotRows.GetChild(i);
                SetRect(row, new Vector2(0f, (top - HelpSlotRowHeight) / 1920f), new Vector2(1f, top / 1920f));
                HelpSlotRow(row);
            }

            for (var i = 0; i < 4; i++)
            {
                var top = HelpUnitColumnsY.x - i * HelpUnitRowHeight;
                var row = Child(panel, $"Unit {i + 1}");
                SetRect(row, new Vector2(0f, (top - HelpUnitRowHeight) / 1920f), new Vector2(1f, top / 1920f));
                HelpUnitRow(row);
            }
        }

        // 図柄の行（70 px）の部品。名前は HelpScreenSetup.BuildSlot のもの。ない部品（ハズレの行など）は飛ばす。
        private static void HelpSlotRow(RectTransform row)
        {
            const float px = 1f / 1440f;
            var icon = row.Find("Icon") as RectTransform;
            if (icon != null)
            {
                // 札は行の高さの正方形（ハズレは少し小さい）。縦の割合はシーンの値のまま。
                var size = (icon.anchorMax.y - icon.anchorMin.y) * HelpSlotRowHeight * px;
                Set(icon.gameObject, TextModeLayout.Parts.Rect, s =>
                {
                    s.anchorMin.x = 0.047f - size / 2f;
                    s.anchorMax.x = 0.047f + size / 2f;
                    return s;
                });
            }

            SetXIfAny(row, "Chance", 0.08f, 0.175f);
            SetXIfAny(row, "Arrow", 0.185f, 0.2f);
            var effectIcon = 50f * px;
            foreach (var name in new[] { "Job Card", "Attack Icon", "Coin Icon" })
            {
                SetXIfAny(row, name, 0.215f, 0.215f + effectIcon);
            }

            SetXIfAny(row, "Gauge", 0.255f, 0.365f);
            var isSeven = row.Find("Gauge") != null;
            SetXIfAny(row, "Effect", isSeven ? 0.37f : 0.255f, isSeven ? 0.45f : 0.36f);
            SetXIfAny(row, "Cap Icon", 0.365f, 0.365f + effectIcon);
            SetXIfAny(row, "Cap", 0.405f, HelpDescriptionLeft - 0.005f);
        }

        // 味方の行（255 px）の部品。名前は HelpScreenSetup.BuildUnits のもの。
        private static void HelpUnitRow(RectTransform row)
        {
            const float h = HelpUnitRowHeight;
            // 本体の札 130 px の正方形（上）、その下に職名（TextModeSetup）、いちばん下にスキルの印。
            SetRect(Child(row, "Body"), new Vector2(0.035f, 120f / h), new Vector2(0.035f + 130f / 1440f, 250f / h));
            SetRect(Child(row, "Skill Mark"), new Vector2(0.02f, 8f / h), new Vector2(0.14f, 48f / h));
            SetRect(Child(row, "Range"), new Vector2(HelpRangeLeft, 58f / h), new Vector2(HelpRangeRight, 250f / h));

            // 左の列は上から HP・攻撃・防御・攻撃間隔、右の列は ブロック・コスト・待ち時間（シーンと同じ並び）。段は下の 55 px より上に 50 px ずつ。
            string[][] columns = { new[] { "hp", "atk", "def", "interval" }, new[] { "block", "cost", "cooldown" } };
            for (var c = 0; c < columns.Length; c++)
            {
                for (var line = 0; line < columns[c].Length; line++)
                {
                    var stat = columns[c][line];
                    var bottom = 55f + (3 - line) * 50f;
                    var middle = bottom + 25f;
                    SetRect(Child(row, stat + " Icon"), new Vector2(HelpStatColumns[c], (middle - HelpStatIcon / 2f) / h),
                        new Vector2(HelpStatColumns[c] + HelpStatIcon / 1440f, (middle + HelpStatIcon / 2f) / h));
                    SetRect(Child(row, stat), new Vector2(HelpStatValueLefts[c], bottom / h),
                        new Vector2(HelpStatValueLefts[c] + 0.135f, (bottom + 50f) / h));
                }
            }
        }

        private static void SetXIfAny(RectTransform row, string name, float left, float right)
        {
            if (row.Find(name) is RectTransform target)
            {
                SetX(target, left, right);
            }
        }

        // --- T359e ショップ・回復・リザルト ---
        // 3つとも Portrait Root いっぱい（1440×1920 px）。値はその割合。ラベルは TextModeSetup が文字あり版の位置で作る。

        /// <summary>
        /// ショップ: 上の帯を「名前＋絵と数字」の2段に（ルートと同じ）、見出しはアイコンの右に 56、品の札を広げて上に3行の名前と説明
        /// （札の中の絵は ShopScreen が下げる）、両替のボタンは上に名前・真ん中に絵と数字・下に文（plan 16.13）。
        /// </summary>
        private static void BuildShop(RectTransform root)
        {
            var panel = PanelOf(root.GetComponentInChildren<ShopScreen>(true));
            var status = Child(panel, "Status");
            SetRect(status, new Vector2(0f, 0.9f), Vector2.one);
            var stripHeight = 0.1f * 1920f;
            RouteItem(Child(status, "Money Icon"), Child(status, "Money"), "money", 0.01f, 0.2f, stripHeight);
            RouteItem(Child(status, "Coin Icon"), Child(status, "Coins"), "coins", 0.21f, 0.4f, stripHeight);

            SetRect(Child(panel, "Header"), new Vector2(0.3f, 0.835f), new Vector2(0.4f, 0.895f));
            SetRect(Child(panel, "Offers"), new Vector2(0.04f, 0.45f), new Vector2(0.96f, 0.82f));
            var exchange = Child(panel, "Exchange");
            SetRect(exchange, new Vector2(0.03f, 0.15f), new Vector2(0.97f, 0.43f));
            TradeButton(Child(exchange, "Buy Coins"), "Money Icon", "Coin Icon");
            TradeButton(Child(exchange, "Sell Coins"), "Coin Icon", "Money Icon");
        }

        // 両替のボタンの真ん中の段（上の名前 0.8〜、下の文 〜0.32 の間）: 払う側の絵と数字 → 受け取る側の絵と数字。横の位置は変えない。
        private static void TradeButton(RectTransform button, string fromIcon, string toIcon)
        {
            SetRect(Child(button, fromIcon), new Vector2(0.04f, 0.42f), new Vector2(0.18f, 0.7f));
            SetRect(Child(button, "From"), new Vector2(0.19f, 0.36f), new Vector2(0.4f, 0.78f));
            SetRect(Child(button, "Arrow"), new Vector2(0.43f, 0.47f), new Vector2(0.55f, 0.67f));
            SetRect(Child(button, toIcon), new Vector2(0.58f, 0.42f), new Vector2(0.72f, 0.7f));
            SetRect(Child(button, "To"), new Vector2(0.73f, 0.36f), new Vector2(0.96f, 0.78f));
        }

        /// <summary>回復: アイコンの下に回復量の文（TextModeSetup）、その下に「+2」、さらに下に名前付きのライフ（名前は絵の左）。</summary>
        private static void BuildRest(RectTransform root)
        {
            var panel = PanelOf(root.GetComponentInChildren<RestScreen>(true));
            SetRect(Child(panel, "Healed"), new Vector2(0.3f, 0.36f), new Vector2(0.7f, 0.44f));
            SetRect(Child(panel, "Life Icon"), new Vector2(0.44f, 0.26f), new Vector2(0.52f, 0.33f));
            SetRect(Child(panel, "Life"), new Vector2(0.54f, 0.25f), new Vector2(0.74f, 0.34f));
        }

        /// <summary>リザルト: 数字の行を右に寄せて左に名前（48）を入れる。ボタンの三角は上に寄せて下に名前（40）。</summary>
        private static void BuildResult(RectTransform root)
        {
            var panel = PanelOf(root.GetComponentInChildren<ResultScreen>(true));
            foreach (var (icon, value) in new[] { ("Floor Icon", "Floor"), ("Money Icon", "Money"), ("Coin Icon", "Coins") })
            {
                SetX(Child(panel, icon), 0.42f, 0.52f);
                SetX(Child(panel, value), 0.55f, 0.88f);
            }

            SetRect(Child(Child(panel, "Retry Button"), "Arrow"), new Vector2(0.4f, 0.36f), new Vector2(0.6f, 0.92f));
            SetRect(Child(Child(panel, "Title Button"), "Arrow"), new Vector2(0.4f, 0.36f), new Vector2(0.6f, 0.92f));
        }

        // 横の位置だけ変える（縦はシーンの値のまま）。
        private static void SetX(RectTransform target, float left, float right) =>
            Set(target.gameObject, TextModeLayout.Parts.Rect, s =>
            {
                s.anchorMin.x = left;
                s.anchorMax.x = right;
                return s;
            });

        private static RectTransform PanelOf(Component screen) =>
            ((GameObject)new SerializedObject(screen).FindProperty("panel").objectReferenceValue).GetComponent<RectTransform>();

        // --- T359d ルート ---

        // Route Screen は Portrait Root の y SlotAreaTop〜1（1440×1200 px）。値はその割合。
        private const float RouteHeight = (1f - PortraitLayout.SlotAreaTop) * PortraitLayout.ReferenceHeight;
        private const float RouteStripBottom = 0.88f;
        private const float RouteMapLeft = 0.02f;
        private const float RouteMapRight = 0.66f;
        // 札の高さ 約 288 px（T368 でルート画面が 1306→1200 px になったので 0.22 から上げた）。
        private const float RouteCardTop = 0.24f;
        private const float RouteCardHalfWidth = 0.093f;
        private const float RouteFont = 40f;
        private const float RouteTitleFont = 56f;

        /// <summary>
        /// ルート画面: 上の帯を「名前＋数字」の2段に、マップを左に寄せて右に凡例（TextModeSetup が作る）、
        /// 行き先は各リールの真上の札（上から 矢印とアイコン・方向・マスの名前・短い説明）にする（plan 16.13）。
        /// </summary>
        private static void BuildRoute(RectTransform root)
        {
            var screen = root.GetComponentInChildren<RouteScreen>(true);
            var panel = ((GameObject)new SerializedObject(screen).FindProperty("panel").objectReferenceValue).GetComponent<RectTransform>();

            var status = Child(panel, "Status");
            SetRect(status, new Vector2(0f, RouteStripBottom), Vector2.one);
            var stripHeight = (1f - RouteStripBottom) * RouteHeight;
            RouteItem(Child(status, "Life Icon"), Child(status, "Life"), "life", 0.01f, 0.2f, stripHeight);
            RouteItem(Child(status, "Money Icon"), Child(status, "Money"), "money", 0.21f, 0.4f, stripHeight);
            RouteItem(Child(status, "Coin Icon"), Child(status, "Coins"), "coins", 0.41f, 0.6f, stripHeight);
            for (var c = 0; c < 4; c++)
            {
                var left = 0.64f + c * 0.087f;
                SetRect(Child(status, $"Gauge {c + 1}"), new Vector2(left, 0.10f), new Vector2(left + 0.08f, 0.44f));
            }

            // 見出しはマップの上、凡例の見出しと同じ段。
            SetLabel(Child(panel, "Text Label route_title"), new Vector2(RouteMapLeft, 0.815f), new Vector2(RouteMapRight, 0.875f),
                Vector2.zero, Vector2.zero, RouteTitleFont);
            SetRect(Child(panel, "Map"), new Vector2(RouteMapLeft, RouteCardTop + 0.015f), new Vector2(RouteMapRight, 0.81f));
            Set(Child(panel, TextModeSetup.RouteLegendName).gameObject, TextModeLayout.Parts.Visibility, s =>
            {
                s.hidden = false;
                return s;
            });

            for (var i = 0; i < 3; i++)
            {
                var card = Child(panel, $"Direction {i + 1}");
                // 札の中心は文字なし版と同じ（リールの真上）。幅はリールの幅（280 px）に収める。
                Set(card.gameObject, TextModeLayout.Parts.Rect, s =>
                {
                    var center = (s.anchorMin.x + s.anchorMax.x) / 2f;
                    s.anchorMin = new Vector2(center - RouteCardHalfWidth, 0f);
                    s.anchorMax = new Vector2(center + RouteCardHalfWidth, RouteCardTop);
                    s.offsetMin = new Vector2(0f, 8f);
                    s.offsetMax = Vector2.zero;
                    return s;
                });
                // 札（約 268×279 px）: 上から 矢印とアイコン・方向（TextModeSetup）・マスの名前・短い説明（TextModeSetup）。
                SetRect(Child(card, "Arrow"), new Vector2(0.06f, 0.69f), new Vector2(0.46f, 0.96f));
                SetRect(Child(card, "Target"), new Vector2(0.52f, 0.67f), new Vector2(0.92f, 0.98f));
                SetLabel(Child(card, "Text Label direction"), new Vector2(0f, 0.25f), new Vector2(1f, 0.47f), Vector2.zero, Vector2.zero,
                    RouteFont);
            }
        }

        // ルートの帯の1項目: 下の段に絵と数字、上の段に名前（名前は絵の子なので絵の左上を基準に置く）。
        private static void RouteItem(RectTransform icon, RectTransform value, string id, float left, float right, float stripHeight)
        {
            const float iconWidth = 0.042f;
            const float iconBottom = 0.06f;
            const float iconTop = 0.48f;
            SetRect(icon, new Vector2(left, iconBottom), new Vector2(left + iconWidth, iconTop));
            SetRect(value, new Vector2(left + iconWidth + 0.004f, 0.02f), new Vector2(right, 0.52f));
            SetLabel(Child(icon, "Text Label " + id), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, (0.55f - iconTop) * stripHeight), new Vector2((right - left) * 1440f, (0.97f - iconTop) * stripHeight),
                RouteFont);
        }

        // --- T359c 筐体の外の名前 ---

        private const float CabinetFont = 40f;

        /// <summary>
        /// レバー・コイン・回数固定の名前を、リール窓の下の段に大きく出す。筐体の部品の配置と見た目は変えない（ラベルだけ動かす）。
        /// 筐体の中は 1440×720 px、リール窓は x 212〜1228・y 205〜705、窓の下の段にレバーとコイン（GameSceneSetup、T368）。
        /// </summary>
        private static void BuildCabinetLabels(RectTransform cabinet)
        {
            // レバー（x 40〜300・y 22〜188）: レバーの右に2行（x 310〜640）。
            var lever = Child(cabinet, "Lever Button");
            SetLabel(Child(lever, "Text Label cabinet_lever"), new Vector2(1f, 0f), Vector2.one,
                new Vector2(10f, 0f), new Vector2(340f, 0f), CabinetFont);

            // コイン（数字 x 1150〜1420・y 62〜168、絵 x 1036〜1140）: 絵の左に2行（x 650〜1026）。
            var credit = Child(cabinet, "Credit");
            SetLabel(Child(credit, "Text Label cabinet_credit"), Vector2.zero, new Vector2(0f, 1f),
                new Vector2(-500f, 0f), new Vector2(-124f, 0f), CabinetFont);

            // 回数固定（絵 x 1036〜1140・y 70〜160）: 絵の下（x 990〜1190・y 8〜62）。
            var fixedSpins = Child(cabinet, "Fixed Spins Icon");
            SetLabel(Child(fixedSpins, "Text Label cabinet_fixed"), Vector2.zero, new Vector2(1f, 0f),
                new Vector2(-46f, -62f), new Vector2(50f, -8f), CabinetFont);
        }

        // --- T359b 戦闘の HUD ---

        // 操作バーの位置（Portrait Root の割合）。バーの子の割合はこれで換算する。
        private const float BarBottom = PortraitLayout.SlotAreaTop;
        private const float BarTop = 1f - (float)PortraitLayout.BattleHeightRatio;

        // 文字あり版の状態の帯（Portrait Root の割合）。戦闘フィールドの上の空き（Battle Camera の上端）に重ねる。
        private const float BandBottom = 1752f / 1920f;
        private const float BandTop = 1912f / 1920f;

        // 帯の中の段（帯の割合）。上が名前、下が数字・ボタンの絵。
        private const float NameBottom = 0.56f;
        private const float NameTop = 0.98f;
        private const float ValueBottom = 0.04f;
        private const float ValueTop = 0.54f;

        private const float BandFont = 40f;
        private const float ButtonFont = 36f;
        private const float CardFont = 40f;

        /// <summary>
        /// 操作バーを味方4種の札にし、ライフ・所持金・ウェーブ・倍率・▶・撤退・一時停止を戦闘フィールドの上端の帯へ移す。
        /// 説明の文を大きくする（plan 16.13）。
        /// </summary>
        private static void BuildBattleHud(RectTransform root)
        {
            var bar = Child(root, "Operation Bar");
            var barHeight = bar.rect.height;
            for (var i = 0; i < 4; i++)
            {
                BuildUnitCard(bar, i, barHeight);
            }

            BuildStatusBand(bar);
            BuildRates(bar);
            BuildGuideCaption(root);
        }

        // 札: 横 345px（基準解像度）。上の段の左に味方アイコン（コスト・ストックの数字は中のまま）、右に強化、下の段に職名、右端にスキルゲージ。
        // 職名を下にするのは、説明の文がアイコンのすぐ上に出るため（GuideOverlay.PlaceCaption）。
        private static void BuildUnitCard(RectTransform bar, int slot, float barHeight)
        {
            const float width = 1440f;
            var left = 8f + slot * 358f;
            const float nameBottom = 0.03f;
            const float nameTop = 0.35f;
            const float iconBottom = 0.38f;
            const float iconTop = 0.97f;

            var icon = Child(bar, $"Unit Icon {slot + 1}");
            SetRect(icon, new Vector2(left / width, iconBottom), new Vector2((left + 110f) / width, iconTop));
            // 職名はアイコンの子。アイコンの下の段に札の幅（ゲージの手前まで）で出す。
            var name = FindLabel(icon, "unit_");
            SetLabel(name, Vector2.zero, Vector2.zero,
                new Vector2(0f, (nameBottom - iconBottom) * barHeight), new Vector2(292f, (nameTop - iconBottom) * barHeight), CardFont);

            var upgrade = Child(bar, $"Upgrade Icon {slot + 1}");
            SetRect(upgrade, new Vector2((left + 116f) / width, iconBottom), new Vector2((left + 296f) / width, iconTop));
            SetLabel(Child(upgrade, "Text Label bar_upgrade"), Vector2.zero, new Vector2(0.46f, 1f), Vector2.zero, Vector2.zero,
                ButtonFont);
            SetRect(Child(upgrade, "Arrow"), new Vector2(0.48f, 0.3f), new Vector2(0.62f, 0.7f));
            SetRect(Child(upgrade, "Cost"), new Vector2(0.64f, 0.4f), new Vector2(0.98f, 0.98f));
            for (var level = 0; level < 5; level++)
            {
                var x = 0.645f + level * 0.068f;
                SetRect(Child(upgrade, $"Level {level + 1}"), new Vector2(x, 0.1f), new Vector2(x + 0.054f, 0.3f));
            }

            SetRect(Child(bar, $"Skill Gauge {slot + 1}"), new Vector2((left + 302f) / width, nameBottom),
                new Vector2((left + 340f) / width, iconTop));
        }

        // 帯: ライフ・所持金（左）、倍率（中、BuildRates）、撤退・一時停止（右）。▶ とウェーブは T361 で廃止。
        private static void BuildStatusBand(RectTransform bar)
        {
            var status = Child(bar, "Status");
            SetRect(status, new Vector2(0f, BarY(BandBottom)), new Vector2(1f, BarY(BandTop)));
            var bandHeight = (BandTop - BandBottom) * 1920f;

            NamedIcon(Child(status, "Life Icon"), Child(status, "Life"), "life", 0.012f, 0.13f, bandHeight);
            NamedIcon(Child(status, "Coin Icon"), Child(status, "Coins"), "money", 0.142f, 0.27f, bandHeight);

            var retreat = Child(status, "Retreat Button");
            BandButton(retreat, "bar_retreat", 0.75f, 0.86f);
            SetRect(Child(retreat, "Stroke 1"), new Vector2(0.45f, 0.44f), new Vector2(0.55f, 0.94f));
            SetRect(Child(retreat, "Stroke 2"), new Vector2(0.45f, 0.44f), new Vector2(0.55f, 0.94f));
            var pause = Child(status, "Pause Button");
            BandButton(pause, "bar_pause", 0.87f, 0.99f);
            SetRect(Child(pause, "Bar Left"), new Vector2(0.38f, 0.46f), new Vector2(0.46f, 0.92f));
            SetRect(Child(pause, "Bar Right"), new Vector2(0.54f, 0.46f), new Vector2(0.62f, 0.92f));
        }

        // 倍率は帯の中央に横並び（左が攻撃力、右が獲得金）。名前は数字の上。
        private static void BuildRates(RectTransform bar)
        {
            var rates = Child(bar, "Rates");
            SetRect(rates, new Vector2(0.41f, BarY(BandBottom)), new Vector2(0.62f, BarY(BandTop)));
            var bandHeight = (BandTop - BandBottom) * 1920f;
            var attack = Child(rates, "Attack Rate");
            SetRect(attack, new Vector2(0f, ValueBottom), new Vector2(0.49f, ValueTop));
            SetLabel(Child(attack, "Text Label bar_attack_rate"), new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, (NameBottom - ValueTop) * bandHeight), new Vector2(0f, (NameTop - ValueTop) * bandHeight), BandFont);
            var money = Child(rates, "Money Rate");
            SetRect(money, new Vector2(0.51f, ValueBottom), new Vector2(1f, ValueTop));
            SetLabel(Child(money, "Text Label bar_money_rate"), new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, (NameBottom - ValueTop) * bandHeight), new Vector2(0f, (NameTop - ValueTop) * bandHeight), BandFont);
        }

        // 説明の文（GuideOverlay が毎フレーム位置を決め、高さは sizeDelta.y を使う）。高さと字の大きさだけを変える。
        private static void BuildGuideCaption(RectTransform root)
        {
            var guide = root.GetComponentInChildren<GuideOverlay>(true);
            var caption = (TextModeLabel)new SerializedObject(guide).FindProperty("caption").objectReferenceValue;
            var rect = (RectTransform)caption.transform;
            Set(rect.gameObject, TextModeLayout.Parts.Rect, s =>
            {
                s.offsetMax = s.offsetMin + new Vector2(1200f, 124f);
                return s;
            });
            Set(caption.Text.gameObject, TextModeLayout.Parts.Font, s =>
            {
                s.autoSize = true;
                s.fontSizeMin = MinFontSize;
                s.fontSizeMax = 48f;
                s.fontSize = 48f;
                return s;
            });
        }

        // 帯の1項目: 左に絵、右に数字、上の段に名前（名前は絵の子なので絵の左上を基準に置く）。
        private static void NamedIcon(RectTransform icon, RectTransform value, string id, float left, float right, float bandHeight)
        {
            const float iconWidth = 0.034f;
            const float iconBottom = 0.10f;
            const float iconTop = 0.48f;
            SetRect(icon, new Vector2(left, iconBottom), new Vector2(left + iconWidth, iconTop));
            SetRect(value, new Vector2(left + iconWidth + 0.002f, ValueBottom), new Vector2(right, ValueTop));
            SetLabel(Child(icon, "Text Label " + id), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, (NameBottom - iconTop) * bandHeight), new Vector2((right - left) * 1440f, (NameTop - iconTop) * bandHeight),
                BandFont);
        }

        // 帯のボタン: 帯の高さいっぱい、下に名前、絵は上に寄せる（絵は呼び出し側）。
        private static void BandButton(RectTransform button, string id, float left, float right)
        {
            SetRect(button, new Vector2(left, ValueBottom), new Vector2(right, NameTop));
            SetLabel(Child(button, "Text Label " + id), Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 4f), new Vector2(0f, 60f),
                ButtonFont);
        }

        // 操作バーの子に、Portrait Root の高さの割合を換算する。
        private static float BarY(float rootY) => (rootY - BarBottom) / (BarTop - BarBottom);

        private static void SetRect(RectTransform target, Vector2 anchorMin, Vector2 anchorMax) =>
            SetRect(target, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        private static void SetRect(RectTransform target, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) =>
            Set(target.gameObject, TextModeLayout.Parts.Rect, s =>
            {
                s.anchorMin = anchorMin;
                s.anchorMax = anchorMax;
                s.offsetMin = offsetMin;
                s.offsetMax = offsetMax;
                return s;
            });

        /// <summary>ラベル（TextModeSetup が作る。下地と子の Text）の位置と字の大きさ。</summary>
        private static void SetLabel(RectTransform label, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            float fontSize)
        {
            SetRect(label, anchorMin, anchorMax, offsetMin, offsetMax);
            Set(label.GetComponent<TextModeLabel>().Text.gameObject, TextModeLayout.Parts.Font, s =>
            {
                s.autoSize = false;
                s.fontSize = fontSize;
                return s;
            });
        }

        private static RectTransform FindLabel(RectTransform parent, string idPrefix)
        {
            foreach (Transform child in parent)
            {
                var label = child.GetComponent<TextModeLabel>();
                if (label != null && label.Id.StartsWith(idPrefix, StringComparison.Ordinal))
                {
                    return (RectTransform)child;
                }
            }

            throw new InvalidOperationException($"{parent.name} に {idPrefix} のラベルがない（先に Add Text Mode Labels (T352)）");
        }

        private static RectTransform Child(RectTransform parent, string name)
        {
            var child = parent.Find(name) as RectTransform;
            if (child == null)
            {
                throw new InvalidOperationException($"{parent.name} に {name} がない");
            }

            return child;
        }

        /// <summary>
        /// 部品に文字あり版の値を置く。<paramref name="edit"/> は文字なし版の値の写しを受け取り、文字あり版に変える部分だけを書き換える。
        /// </summary>
        internal static TextModeLayout Set(GameObject target, TextModeLayout.Parts parts, Func<TextModeLayout.State, TextModeLayout.State> edit)
        {
            var layout = target.GetComponent<TextModeLayout>();
            var noText = layout != null ? layout.NoText : TextModeLayout.Read(target);
            if (layout == null)
            {
                layout = Undo.AddComponent<TextModeLayout>(target);
            }

            if ((parts & TextModeLayout.Parts.Visibility) != 0 && target.GetComponent<CanvasGroup>() == null)
            {
                Undo.AddComponent<CanvasGroup>(target);
            }

            Undo.RecordObject(layout, "Text Mode Layout");
            layout.Configure(parts, noText, edit(noText));
            EditorUtility.SetDirty(layout);
            return layout;
        }

        private static void Save(GameObject inScene)
        {
            var scene = inScene.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] {scene.name} に文字あり版の配置を置いた。");
        }
    }
}
