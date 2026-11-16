using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Ui;
using UnitySlotTower.Editor.Ui;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// 文字あり版（T352、FR-006、plan 16.9）の日本語フォントとラベル。どちらも再実行すると作り直す（同じ結果）。
    /// フォントは ui_texts.csv に出てくる字だけを持つ静的アセット（Play 中にアセットが書き換わらない）。文言を足したら作り直す。
    /// ラベルは既存の部品の子に足すだけで、部品の配置は変えない。
    /// </summary>
    internal static class TextModeSetup
    {
        public const string FontAssetPath = "Assets/UnitySlotTower/Art/Fonts/TextMode SDF.asset";
        public const string SourceFontPath = "Assets/UnitySlotTower/Art/Fonts/NotoSansJP-Regular.otf";
        public const string TablePath = "Assets/UnitySlotTower/Data/Tables/" + UiTexts.FileName;
        private const string UnitsPath = "Assets/UnitySlotTower/Data/Tables/units.csv";
        private const string LabelPrefix = "Text Label ";
        private const string CaptionName = "Text Label Caption";
        private const string OfferTemplateName = "Text Label Offer Template";
        private const string TextModeButtonName = "Text Mode Button";
        public const string RouteLegendName = "Text Mode Legend";

        private static readonly Color BackgroundColor = new Color(0.05f, 0.06f, 0.1f, 0.72f);

        private static TMP_FontAsset _font;
        private static TextAsset _table;
        private static UiTexts _texts;

        [MenuItem("Tools/UnitySlotTower/Setup/Create Text Mode Font (T352)")]
        private static void CreateFont()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            var table = AssetDatabase.LoadAssetAtPath<TextAsset>(TablePath);
            if (source == null || table == null)
            {
                Debug.LogError($"[SlotTower Setup] {SourceFontPath} か {TablePath} がない。");
                return;
            }

            var characters = UiTexts.Parse(table.text).Characters() + "0123456789";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var created = font == null;
            if (created)
            {
                // 全字が1枚に入る大きさにする（2枚目以降のアトラスは保存されない、T308）。
                font = TMP_FontAsset.CreateFontAsset(source, 44, 6, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, false);
                font.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            }
            else
            {
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                font.ClearFontAssetData(true);
            }

            var added = font.TryAddCharacters(characters, out var missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            if (created)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FontAssetPath));
                AssetDatabase.CreateAsset(font, FontAssetPath);
                font.atlasTexture.name = font.name + " Atlas";
                font.material.name = font.name + " Material";
                AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
                AssetDatabase.AddObjectToAsset(font.material, font);
            }

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            if (!added)
            {
                Debug.LogError($"[SlotTower Setup] 字形を足せなかった文字: {missing}");
                return;
            }

            Debug.Log($"[SlotTower Setup] {FontAssetPath} に {characters.Length} 字を入れた。");
        }

        /// <summary>開いている Title か Game シーンにラベルを足して保存する。</summary>
        [MenuItem("Tools/UnitySlotTower/Setup/Add Text Mode Labels (T352)")]
        private static void AddLabels()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            _table = AssetDatabase.LoadAssetAtPath<TextAsset>(TablePath);
            if (_font == null || _table == null)
            {
                Debug.LogError($"[SlotTower Setup] {FontAssetPath} か {TablePath} がない。先に Create Text Mode Font を実行する。");
                return;
            }

            _texts = UiTexts.Parse(_table.text);
            var title = Object.FindAnyObjectByType<TitleScreen>(FindObjectsInactive.Include);
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            if (title != null)
            {
                AddTitleLabels(title);
                Save(title.gameObject);
            }
            else if (cabinet != null)
            {
                AddGameLabels(cabinet);
                Save(cabinet.gameObject);
            }
            else
            {
                Debug.LogError("[SlotTower Setup] Title か Game シーンを開いてから実行する。");
            }
        }

        private static void Save(GameObject inScene)
        {
            var scene = inScene.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] {scene.name} に文字あり版のラベルを足した。");
        }

        private static void AddTitleLabels(TitleScreen title)
        {
            var serialized = new SerializedObject(title);
            var titlePanel = ((GameObject)serialized.FindProperty("titlePanel").objectReferenceValue).GetComponent<RectTransform>();
            var difficulty = ((GameObject)serialized.FindProperty("difficultyPanel").objectReferenceValue).GetComponent<RectTransform>();
            var logo = (RectTransform)titlePanel.Find(TextAudit.TitleLogoName);
            var start = (RectTransform)((Button)serialized.FindProperty("startButton").objectReferenceValue).transform;
            var back = (RectTransform)((Button)serialized.FindProperty("backButton").objectReferenceValue).transform;

            // ロゴ右下の透明なボタン（キーボードの選択の対象にしない）と、その横の「あ」の印。
            DestroyChild(logo, TextModeButtonName);
            var buttonRect = Rect(TextModeButtonName, logo, new Vector2(0.86f, 0f), new Vector2(1f, 0.28f));
            var image = buttonRect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            serialized.FindProperty("textModeButton").objectReferenceValue = button;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Label(logo, "title_text_mode", new Vector2(1f, 0f), new Vector2(1f, 0.28f), new Vector2(4f, 0f), new Vector2(84f, 0f), 56f);
            // T359e: 字は 36 以上。難易度選択の見出しは札の上（札は TextModeLayoutSetup.BuildTitle が下げる）、もどるは ◀ の右。
            Below(start, "title_start", 60f, 40f);
            Label(difficulty, "title_difficulty", new Vector2(0.1f, 0.76f), new Vector2(0.9f, 0.82f), Vector2.zero, Vector2.zero, 56f);
            RightOf(back, "title_back");
            var settings = serialized.FindProperty("settingButtons");
            for (var i = 0; i < settings.arraySize; i++)
            {
                // 札の下に設定の名前（「やさしい」など）。
                var plate = (RectTransform)((Button)settings.GetArrayElementAtIndex(i).objectReferenceValue).transform;
                Below(plate, $"setting_{i + 1}", 66f, 40f);
            }
        }

        private static void AddGameLabels(SlotCabinet cabinet)
        {
            var root = (RectTransform)cabinet.transform.parent;
            var units = CsvParser.Parse("units.csv", AssetDatabase.LoadAssetAtPath<TextAsset>(UnitsPath).text);

            // 操作バー
            var bar = (RectTransform)root.Find("Operation Bar");
            for (var i = 0; i < 4 && i < units.Rows.Count; i++)
            {
                var unitId = units.Rows[i].Get("id");
                Label(Child(bar, $"Unit Icon {i + 1}"), "unit_" + unitId, new Vector2(0f, 1f), Vector2.one,
                    new Vector2(0f, -28f), Vector2.zero, 20f);
                Label(Child(bar, $"Upgrade Icon {i + 1}"), "bar_upgrade", Vector2.zero, new Vector2(0.3f, 1f), Vector2.zero,
                    Vector2.zero, 16f);
            }

            var status = Child(bar, "Status");
            IconTag(Child(status, "Life Icon"), "life", 16f);
            // 操作バーの黄色い丸の数字は所持金（OperationBarStatus.moneyText）。2026-11-15 T359b まで「コイン」と書いていた。
            DestroyChild(Child(status, "Coin Icon"), LabelPrefix + "coins");
            IconTag(Child(status, "Coin Icon"), "money", 16f);
            InsideBottom(Child(status, "Retreat Button"), "bar_retreat", 22f, 16f);
            InsideBottom(Child(status, "Pause Button"), "bar_pause", 22f, 16f);
            // T365 の残りの敵の数と倍速（BattleSpeedSetup が作る。作り直したらこのメニューも実行し直す）。T370。
            IconTag(Child(status, "Enemy Icon"), "bar_remaining", 16f);
            InsideBottom(Child(status, "Speed Button"), "bar_speed", 22f, 16f);
            var rates = Child(bar, "Rates");
            Label(Child(rates, "Attack Rate"), "bar_attack_rate", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -20f), Vector2.zero, 15f);
            Label(Child(rates, "Money Rate"), "bar_money_rate", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -20f), Vector2.zero, 15f);

            // 筐体
            var slot = (RectTransform)cabinet.transform;
            Above(Child(slot, "Lever Button"), "cabinet_lever", 36f, 24f);
            Label(Child(slot, "Credit"), "cabinet_credit", new Vector2(-0.4f, 1f), Vector2.one, new Vector2(0f, 2f),
                new Vector2(0f, 34f), 20f);
            Below(Child(slot, "Fixed Spins Icon"), "cabinet_fixed", 26f, 18f);

            // 開始の演出の ▶ の下（BattleIntroSetup が作る Play の子。▶ と一緒に出て消える）。T370。
            var intro = Object.FindAnyObjectByType<BattleIntroView>(FindObjectsInactive.Include);
            Below(Child((RectTransform)intro.transform, "Play"), "battle_start", 84f, 64f);

            AddRouteLabels(root);
            AddNodeLabels(root);
            AddResultLabels(root);
            AddHelpLabels(root, units);
            AddGuideCaption(root);
        }

        private static void AddRouteLabels(RectTransform root)
        {
            var screen = root.GetComponentInChildren<RouteScreen>(true);
            var serialized = new SerializedObject(screen);
            var panel = ((GameObject)serialized.FindProperty("panel").objectReferenceValue).GetComponent<RectTransform>();
            var status = Child(panel, "Status");
            IconTag(Child(status, "Life Icon"), "life", 18f);
            IconTag(Child(status, "Money Icon"), "money", 18f);
            IconTag(Child(status, "Coin Icon"), "coins", 18f);
            Label(panel, "route_title", new Vector2(0.3f, 0.87f), new Vector2(0.7f, 0.915f), Vector2.zero, Vector2.zero, 32f);

            // T359d: スキルゲージの名前（文字あり版の帯の上の段、点の上）。帯の大きさは TextModeLayoutSetup が変える。
            Label(status, "route_gauge", new Vector2(0.64f, 0.55f), new Vector2(0.99f, 0.97f), Vector2.zero, Vector2.zero, 40f);

            var labels = serialized.FindProperty("directionLabels");
            var descriptions = serialized.FindProperty("directionDescriptions");
            labels.arraySize = 3;
            descriptions.arraySize = 3;
            for (var i = 0; i < 3; i++)
            {
                var button = Child(panel, $"Direction {i + 1}");
                // 文言は行き先に合わせて RouteScreen が入れる。位置は文字あり版の札の中（TextModeLayoutSetup）。
                var label = Above(button, string.Empty, 36f, 24f, "direction");
                labels.GetArrayElementAtIndex(i).objectReferenceValue = label;

                // T359d: 札の方向と短い説明（札の割合。札の大きさは TextModeLayoutSetup が変える）。
                Label(button, DirectionIds[i], new Vector2(0f, 0.47f), new Vector2(1f, 0.65f), Vector2.zero, Vector2.zero, 36f);
                var description = Label(button, string.Empty, new Vector2(0f, 0.03f), new Vector2(1f, 0.25f), Vector2.zero, Vector2.zero,
                    36f, LabelPrefix + "description");
                descriptions.GetArrayElementAtIndex(i).objectReferenceValue = description;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AddRouteLegend(panel, (Sprite)serialized.FindProperty("knobSprite").objectReferenceValue,
                (Sprite)serialized.FindProperty("boxSprite").objectReferenceValue);
        }

        private static readonly string[] DirectionIds = { "route_dir_left", "route_dir_center", "route_dir_right" };

        private static readonly string[] LegendKinds =
            { RouteNodeKinds.Battle, RouteNodeKinds.Elite, RouteNodeKinds.Shop, RouteNodeKinds.Rest, RouteNodeKinds.Boss };

        /// <summary>
        /// T359d: マスの種類の凡例（マップの右、アイコン・名前・短い説明）。アイコンは文字なし版でも描かれる絵なので、
        /// 全体を CanvasGroup の濃さ 0 で隠して保存し、TextModeLayoutSetup が文字あり版だけ出す（Visibility）。
        /// 位置は文字あり版の位置（Route Screen の割合と px）。
        /// </summary>
        private static void AddRouteLegend(RectTransform panel, Sprite knob, Sprite box)
        {
            DestroyChild(panel, RouteLegendName);
            var legend = Rect(RouteLegendName, panel, new Vector2(0.68f, 0.265f), new Vector2(0.99f, 0.875f));
            var group = legend.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            var background = legend.gameObject.AddComponent<Image>();
            background.color = BackgroundColor;
            background.raycastTarget = false;

            const float headerHeight = 70f;
            // 説明は1行（7字以内、札の幅にも1行で入る）。Noto Sans JP は行が高い（36 で約 52 px）。
            const float entryHeight = 134f;
            Label(legend, "route_legend", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -headerHeight), Vector2.zero, 48f);
            for (var k = 0; k < LegendKinds.Length; k++)
            {
                var kind = LegendKinds[k];
                var top = -headerHeight - k * entryHeight;
                var entry = Rect($"Entry {kind}", legend, new Vector2(0f, 1f), Vector2.one);
                entry.offsetMin = new Vector2(0f, top - entryHeight);
                entry.offsetMax = new Vector2(0f, top);

                var frame = Rect("Icon", entry, new Vector2(0f, 1f), new Vector2(0f, 1f));
                frame.offsetMin = new Vector2(8f, -96f);
                frame.offsetMax = new Vector2(96f, -8f);
                RouteNodeIcons.Create(frame, kind, knob, box).raycastTarget = false;

                var name = Label(entry, "node_" + kind, new Vector2(0f, 1f), Vector2.one, new Vector2(104f, -60f), new Vector2(-8f, -4f), 40f);
                name.Text.alignment = TextAlignmentOptions.Left;
                var description = Label(entry, "node_" + kind + "_desc", new Vector2(0f, 1f), Vector2.one, new Vector2(104f, -120f),
                    new Vector2(-8f, -62f), 36f);
                description.Text.alignment = TextAlignmentOptions.Left;
            }
        }

        private static void AddNodeLabels(RectTransform root)
        {
            var rest = root.GetComponentInChildren<RestScreen>(true);
            // T359e: 位置は文字あり版のもの（部品の文字あり版の位置は TextModeLayoutSetup.BuildNodeScreens）。字は 36 以上。
            var restPanel = PanelOf(rest);
            Label(restPanel, "rest_title", new Vector2(0.25f, 0.82f), new Vector2(0.75f, 0.88f), Vector2.zero, Vector2.zero, 56f);
            DestroyChild(restPanel, LabelPrefix + "rest_healed");
            // 回復量の文は RestScreen が開くたびに入れる（「ライフが 2 回復した」）。
            var healed = Label(restPanel, string.Empty, new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.51f), Vector2.zero, Vector2.zero,
                48f, LabelPrefix + "healed");
            LeftOf(Child(restPanel, "Life Icon"), "life");
            RightOf(Child(restPanel, "Back"), "back");
            var restSerialized = new SerializedObject(rest);
            restSerialized.FindProperty("healedLabel").objectReferenceValue = healed;
            restSerialized.ApplyModifiedPropertiesWithoutUndo();

            var shop = root.GetComponentInChildren<ShopScreen>(true);
            var shopPanel = PanelOf(shop);
            var status = Child(shopPanel, "Status");
            // 名前の位置と大きさは TextModeLayoutSetup（ルートの帯と同じ置き方）。
            IconTag(Child(status, "Money Icon"), "money", 18f);
            IconTag(Child(status, "Coin Icon"), "coins", 18f);
            Label(shopPanel, "shop_title", new Vector2(0.4f, 0.835f), new Vector2(0.7f, 0.895f), Vector2.zero, Vector2.zero, 56f);
            var exchange = Child(shopPanel, "Exchange");
            var serialized = new SerializedObject(shop);
            TradeLabels(Child(exchange, "Buy Coins"), "shop_buy_coins", serialized.FindProperty("buyCoinsRateLabel"));
            TradeLabels(Child(exchange, "Sell Coins"), "shop_sell_coins", serialized.FindProperty("sellCoinsRateLabel"));
            RightOf(Child(shopPanel, "Back"), "back");

            // 品の札は開くたびに作られるので、ひな形を非表示で置き、ShopScreen が札ごとに複製する（札の上に 職名・品の名前・短い説明 の3行）。
            DestroyChild(shopPanel, OfferTemplateName);
            var template = Label(shopPanel, string.Empty, new Vector2(0f, 0.725f), new Vector2(1f, 0.995f), Vector2.zero, Vector2.zero,
                40f, OfferTemplateName);
            template.gameObject.SetActive(false);
            serialized.FindProperty("offerLabelTemplate").objectReferenceValue = template;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // 両替のボタン: 上に名前、下に数字の入った文（ShopScreen が入れる）。真ん中の絵と数字は TextModeLayoutSetup が寄せる。
        private static void TradeLabels(RectTransform button, string id, SerializedProperty rateLabel)
        {
            Label(button, id, new Vector2(0f, 0.8f), new Vector2(1f, 0.98f), Vector2.zero, Vector2.zero, 40f);
            rateLabel.objectReferenceValue = Label(button, string.Empty, new Vector2(0f, 0.05f), new Vector2(1f, 0.32f), Vector2.zero,
                Vector2.zero, 40f, LabelPrefix + "rate");
        }

        private static void AddResultLabels(RectTransform root)
        {
            var result = root.GetComponentInChildren<ResultScreen>(true);
            var panel = PanelOf(result);
            // T359e: 見出し 56、行の名前は絵の左に 48、ボタンの名前は中の下に 40（絵と三角の文字あり版の位置は TextModeLayoutSetup）。
            Label(Child(panel, "Clear Icon"), "result_clear", Vector2.zero, new Vector2(1f, 0.26f), Vector2.zero, Vector2.zero, 56f);
            Label(Child(panel, "Game Over Icon"), "result_game_over", Vector2.zero, new Vector2(1f, 0.2f), new Vector2(-60f, -40f),
                new Vector2(60f, 0f), 56f);
            LeftOf(Child(panel, "Floor Icon"), "result_floor", 48f);
            LeftOf(Child(panel, "Money Icon"), "money", 48f);
            LeftOf(Child(panel, "Coin Icon"), "coins", 48f);
            InsideBottom(Child(panel, "Retry Button"), "result_retry", 64f, 40f);
            InsideBottom(Child(panel, "Title Button"), "result_title", 64f, 40f);
        }

        private static void AddHelpLabels(RectTransform root, CsvTable units)
        {
            var help = root.GetComponentInChildren<HelpScreen>(true);
            var helpRect = (RectTransform)help.transform;
            // T359f: 位置は文字あり版のもの（部品の文字あり版の位置は TextModeLayoutSetup.BuildHelp）。字は 36 以上。
            // 「?」の名前はボタンの上（下だと操作バーの札4の職名に掛かる）。
            Above(Child(helpRect, "Open Button"), "help_open", 52f, 36f);
            var panel = PanelOf(help);
            Label(Child(panel, "Mark"), "help_title", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(220f, 0f), 56f)
                .Text.alignment = TextAlignmentOptions.Left;
            RightOf(Child(panel, "Close Button"), "help_close");

            // 表の見出し（48）と列の名前（36）。
            HelpLine(panel, "help_slot_heading", 0.02f, 0.6f, TextModeLayoutSetup.HelpSlotHeadingY, 48f);
            HelpLine(panel, "help_col_symbol", 0.005f, 0.09f, TextModeLayoutSetup.HelpSlotColumnsY, 36f, TextAlignmentOptions.Center);
            HelpLine(panel, "help_col_chance", 0.09f, 0.18f, TextModeLayoutSetup.HelpSlotColumnsY, 36f, TextAlignmentOptions.Right);
            HelpLine(panel, "help_col_effect", 0.215f, 0.99f, TextModeLayoutSetup.HelpSlotColumnsY, 36f);
            HelpLine(panel, "help_units_heading", 0.02f, 0.6f, TextModeLayoutSetup.HelpUnitHeadingY, 48f);
            HelpLine(panel, "help_col_job", 0.005f, TextModeLayoutSetup.HelpRangeLeft - 0.01f, TextModeLayoutSetup.HelpUnitColumnsY, 36f,
                TextAlignmentOptions.Center);
            HelpLine(panel, "help_col_range", TextModeLayoutSetup.HelpRangeLeft, TextModeLayoutSetup.HelpRangeRight,
                TextModeLayoutSetup.HelpUnitColumnsY, 36f, TextAlignmentOptions.Center);
            HelpLine(panel, "help_col_stats", TextModeLayoutSetup.HelpStatColumns[0], 0.99f, TextModeLayoutSetup.HelpUnitColumnsY, 36f);

            string[] stats = { "hp", "atk", "def", "interval", "block", "cost", "cooldown" };
            for (var i = 0; i < 4 && i < units.Rows.Count; i++)
            {
                var row = Child(panel, $"Unit {i + 1}");
                var unitId = units.Rows[i].Get("id");
                // 職名は本体の札の下（札の左右に 43 px はみ出す。札の大きさは TextModeLayoutSetup）。
                Label(Child(row, "Body"), "unit_" + unitId, Vector2.zero, new Vector2(1f, 0f), new Vector2(-43f, -56f), new Vector2(43f, -4f), 40f);
                for (var s = 0; s < stats.Length; s++)
                {
                    // 数値の名前は数字の左（数字の子なので左端を基準に置く。間の幅は列ごと）。
                    var nameWidth = TextModeLayoutSetup.HelpStatNameWidth(s < 4 ? 0 : 1);
                    Label(Child(row, stats[s]), "stat_" + stats[s], Vector2.zero, new Vector2(0f, 1f), new Vector2(-nameWidth - 6f, 0f),
                        new Vector2(-6f, 0f), 36f).Text.alignment = TextAlignmentOptions.Left;
                }

                // スキルの説明は行の下の段（範囲図の左端から右端まで1行）。
                Label(row, "skill_" + unitId, new Vector2(TextModeLayoutSetup.HelpRangeLeft, 0f),
                    new Vector2(0.99f, TextModeLayoutSetup.HelpSkillLineTop), Vector2.zero, Vector2.zero, 36f).Text.alignment = TextAlignmentOptions.Left;
            }

            // 図柄の行の説明は効果の右（1行）。
            var slotRows = Child(panel, HelpScreenSetup.SlotRowsName);
            for (var i = 0; i < slotRows.childCount; i++)
            {
                var row = (RectTransform)slotRows.GetChild(i);
                var id = "slot_" + row.name.ToLowerInvariant();
                if (_texts.TryGet(id, out _))
                {
                    Label(row, id, new Vector2(TextModeLayoutSetup.HelpDescriptionLeft, 0f), new Vector2(0.99f, 1f), Vector2.zero, Vector2.zero, 36f)
                        .Text.alignment = TextAlignmentOptions.Left;
                }
            }
        }

        // 早見表の1行のラベル（panel の割合。y は (下端, 上端) の px）。
        private static void HelpLine(RectTransform panel, string id, float left, float right, Vector2 y, float fontSize,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left) =>
            Label(panel, id, new Vector2(left, y.x / 1920f), new Vector2(right, y.y / 1920f), Vector2.zero, Vector2.zero, fontSize)
                .Text.alignment = alignment;

        private static void AddGuideCaption(RectTransform root)
        {
            var guide = root.GetComponentInChildren<GuideOverlay>(true);
            var serialized = new SerializedObject(guide);
            var overlay = (RectTransform)serialized.FindProperty("root").objectReferenceValue;
            DestroyChild(overlay, CaptionName);
            // GuideOverlay が毎フレーム位置と幅を決める（中心が原点・左下が基準、Place と同じ）。
            var caption = Label(overlay, string.Empty, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                34f, CaptionName);
            var rect = (RectTransform)caption.transform;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(1200f, 96f);
            rect.SetAsLastSibling();
            serialized.FindProperty("caption").objectReferenceValue = caption;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- 置き方 ---

        // アイコンの下端にまたがる小さな札。
        private static TextModeLabel IconTag(RectTransform icon, string id, float fontSize) =>
            Label(icon, id, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-fontSize * 3f, -fontSize * 0.75f),
                new Vector2(fontSize * 3f, fontSize * 0.75f), fontSize);

        private static TextModeLabel Above(RectTransform target, string id, float height, float fontSize = 0f, string suffix = null) =>
            Label(target, id, new Vector2(0f, 1f), Vector2.one, new Vector2(-20f, 2f), new Vector2(20f, 2f + height),
                fontSize > 0f ? fontSize : height * 0.7f, suffix != null ? LabelPrefix + suffix : null);

        private static TextModeLabel Below(RectTransform target, string id, float height, float fontSize = 0f) =>
            Label(target, id, Vector2.zero, new Vector2(1f, 0f), new Vector2(-20f, -2f - height), new Vector2(20f, -2f),
                fontSize > 0f ? fontSize : height * 0.7f);

        // 部品の右に横並び（◀ の「もどる」など）。
        private static TextModeLabel RightOf(RectTransform target, string id, float fontSize = 40f) =>
            Label(target, id, new Vector2(1f, 0f), Vector2.one, new Vector2(12f, 0f), new Vector2(260f, 0f), fontSize);

        // 部品の左に横並び（数字の行の名前）。
        private static TextModeLabel LeftOf(RectTransform target, string id, float fontSize = 48f) =>
            Label(target, id, Vector2.zero, new Vector2(0f, 1f), new Vector2(-440f, 0f), new Vector2(-16f, 0f), fontSize);

        private static TextModeLabel InsideBottom(RectTransform target, string id, float height, float fontSize) =>
            Label(target, id, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, height), fontSize);

        /// <summary>
        /// parent の子にラベルを作る（同じ名前の既存のラベルは作り直す）。文字と下地は非表示で保存し、TextModeLabel が出し入れする。
        /// </summary>
        private static TextModeLabel Label(RectTransform parent, string id, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin,
            Vector2 offsetMax, float fontSize, string name = null)
        {
            name ??= LabelPrefix + id;
            DestroyChild(parent, name);
            var rect = Rect(name, parent, anchorMin, anchorMax);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var background = rect.gameObject.AddComponent<Image>();
            background.color = BackgroundColor;
            background.raycastTarget = false;
            background.enabled = false;

            var text = Rect("Text", rect, Vector2.zero, Vector2.one).gameObject.AddComponent<TextMeshProUGUI>();
            text.rectTransform.offsetMin = new Vector2(4f, 0f);
            text.rectTransform.offsetMax = new Vector2(-4f, 0f);
            text.font = _font;
            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = string.IsNullOrEmpty(id) ? string.Empty : _texts.Get(id);
            text.enabled = false;

            var label = rect.gameObject.AddComponent<TextModeLabel>();
            var serialized = new SerializedObject(label);
            serialized.FindProperty("table").objectReferenceValue = _table;
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("text").objectReferenceValue = text;
            serialized.FindProperty("background").objectReferenceValue = background;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return label;
        }

        private static RectTransform Rect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform PanelOf(Component screen) =>
            ((GameObject)new SerializedObject(screen).FindProperty("panel").objectReferenceValue).GetComponent<RectTransform>();

        private static RectTransform Child(RectTransform parent, string name)
        {
            var child = parent.Find(name) as RectTransform;
            if (child == null)
            {
                throw new System.InvalidOperationException($"{parent.name} に {name} がない");
            }

            return child;
        }

        private static void DestroyChild(Transform parent, string name)
        {
            for (var child = parent.Find(name); child != null; child = parent.Find(name))
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
