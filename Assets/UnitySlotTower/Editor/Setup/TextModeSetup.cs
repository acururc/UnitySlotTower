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
        private const string TextModeButtonName = "Text Mode Button";

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
            var titlePanel = GameObject.Find("Title Panel").GetComponent<RectTransform>();
            var logo = (RectTransform)titlePanel.Find(TextAudit.TitleLogoName);
            var start = (RectTransform)((Button)serialized.FindProperty("startButton").objectReferenceValue).transform;

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
            // T359e: 字は 36 以上。
            Below(start, "title_start", 60f, 40f);
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

            AddResultLabels(root);
            AddHelpLabels(root, units);
            AddGuideCaption(root);
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

        internal static void AddPauseLabels(RectTransform root, HelpScreen help)
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            _table = AssetDatabase.LoadAssetAtPath<TextAsset>(TablePath);
            _texts = UiTexts.Parse(_table.text);
            var menu = help.GetComponent<QuitToTitle>();
            InsideBottom((RectTransform)help.OpenButton.transform, "help_open", 44f, 36f);
            InsideBottom((RectTransform)menu.HomeButton.transform, "pause_home", 44f, 36f);
            var board = Child(root, "Quit Confirmation/Board");
            Label(board, "pause_question", new Vector2(0.04f, 0.65f), new Vector2(0.96f, 0.95f),
                Vector2.zero, Vector2.zero, 56f);
            InsideBottom((RectTransform)menu.ConfirmButton.transform, "pause_yes", 64f, 44f);
            InsideBottom((RectTransform)menu.CancelButton.transform, "pause_no", 64f, 44f);
        }

        internal static void AddHelpLabels(RectTransform root)
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            _table = AssetDatabase.LoadAssetAtPath<TextAsset>(TablePath);
            _texts = UiTexts.Parse(_table.text);
            AddHelpLabels(root, CsvParser.Parse("units.csv", AssetDatabase.LoadAssetAtPath<TextAsset>(UnitsPath).text));
        }

        private static void AddHelpLabels(RectTransform root, CsvTable units)
        {
            var help = root.GetComponentInChildren<HelpScreen>(true);
            if (help.GetComponent<QuitToTitle>() != null)
                AddPauseLabels(root, help);
            else
                Above((RectTransform)help.OpenButton.transform, "help_open", 52f, 36f);
            var panel = PanelOf(help);
            Label(Child(panel, "Mark"), "help_title", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(220f, 0f), 56f)
                .Text.alignment = TextAlignmentOptions.Left;
            RightOf(Child(panel, "Close Button"), "help_close");

            var slotPage = Child(panel, "Slot Page");
            var unitPage = Child(panel, "Unit Page");
            InsideBottom(Child(panel, "Page Tabs/Slot Tab"), "help_tab_slot", 54f, 40f);
            InsideBottom(Child(panel, "Page Tabs/Unit Tab"), "help_tab_units", 54f, 40f);

            // 表の見出し（48）と列の名前（36）。
            HelpLine(slotPage, "help_slot_heading", 0.02f, 0.6f, TextModeLayoutSetup.HelpSlotHeadingY, 48f);
            HelpLine(slotPage, "help_col_symbol", 0.005f, 0.09f, TextModeLayoutSetup.HelpSlotColumnsY, 36f, TextAlignmentOptions.Center);
            HelpLine(slotPage, "help_col_chance", 0.09f, 0.18f, TextModeLayoutSetup.HelpSlotColumnsY, 36f, TextAlignmentOptions.Right);
            HelpLine(slotPage, "help_col_effect", 0.215f, 0.99f, TextModeLayoutSetup.HelpSlotColumnsY, 36f);
            HelpLine(unitPage, "help_units_heading", 0.02f, 0.6f, TextModeLayoutSetup.HelpUnitHeadingY, 48f);
            HelpLine(unitPage, "help_col_job", 0.005f, TextModeLayoutSetup.HelpRangeLeft - 0.01f, TextModeLayoutSetup.HelpUnitColumnsY, 36f,
                TextAlignmentOptions.Center);
            HelpLine(unitPage, "help_col_range", TextModeLayoutSetup.HelpRangeLeft, TextModeLayoutSetup.HelpRangeRight,
                TextModeLayoutSetup.HelpUnitColumnsY, 36f, TextAlignmentOptions.Center);
            HelpLine(unitPage, "help_col_stats", TextModeLayoutSetup.HelpStatColumns[0], 0.99f, TextModeLayoutSetup.HelpUnitColumnsY, 36f);

            string[] stats = { "hp", "atk", "def", "interval", "block", "cost", "cooldown" };
            for (var i = 0; i < 4 && i < units.Rows.Count; i++)
            {
                var row = Child(unitPage, $"Unit {i + 1}");
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
            }

            // 図柄の行の説明は効果の右（1行）。
            var slotRows = Child(slotPage, HelpScreenSetup.SlotRowsName);
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

            var background = TextModeRestyleSetup.IsInsidePanel(rect)
                ? null : TextModeRestyleSetup.AddBackground(rect.gameObject);

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
