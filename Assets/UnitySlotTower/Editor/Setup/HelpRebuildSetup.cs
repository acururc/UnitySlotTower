using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Flow;
using UnitySlotTower.Ui;
using Direction = UnitySlotTower.Battle.TriangleGraphic.Direction;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// spec 016: 早見表の中身（タブ・5ページ・行・動く図・文字ありの文と配置）を作り直す。外枠（Panel・閉じる・見出しの ?）は残す。
    /// 何度実行しても同じ結果。文言を足したら Create Text Mode Font (T352) も実行する。
    /// 行は 左の列（絵と数値）と右の列（動く図、味方は範囲図も）。文字ありでは右の列を行の上へ縮め、下に文を出す（TextModeLayout）。
    /// 動く図（HelpDemo）の位置は親の割合なので、縮めても崩れない。絵は今の盤面の見た目に合わせた仮の図形。
    /// </summary>
    internal static class HelpRebuildSetup
    {
        private const string NumberFontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private const string UnitsPath = "Assets/UnitySlotTower/Data/Tables/units.csv";

        // 基準 1440×1920 の px。
        private const float W = 1440f;
        private const float H = 1920f;
        private const float ColA0 = 80f;
        private const float ColA1 = 320f;
        private const float ColB0 = 350f;
        private const float ColB1 = 1360f;
        private const float ContentTop = 1640f;
        private const float ContentBottom = 40f;
        private const float Pad = 10f;
        /// <summary>文字ありで行の下に取る文の高さ（px）。残りの上を右の列（図）が使う。36 の字の1行は約 66 px。</summary>
        private const float TwoLineSentence = 160f;
        /// <summary>行が多いページ（図柄・敵）の文は1行（24字まで）。</summary>
        private const float OneLineSentence = 96f;
        private const float SentenceFont = 36f;

        private static readonly string[] PageNames = { "Guide", "Bet", "Symbols", "Units", "Enemies" };
        private static readonly string[] TabTextIds =
            { "help2_tab_guide", "help2_tab_bet", "help2_tab_symbols", "help2_tab_units", "help2_tab_enemies" };
        internal static readonly string[] EnemyIds = { "melee", "ranged", "air", "heavy", "mid_boss", "final_boss" };

        private static readonly Color Ground = new Color(0.3f, 0.3f, 0.34f);
        private static readonly Color GroundTile = new Color(0.45f, 0.36f, 0.26f);
        private static readonly Color HighTile = new Color(0.55f, 0.58f, 0.64f);
        private static readonly Color CardColor = new Color(0.42f, 0.44f, 0.5f);
        private static readonly Color Dim = new Color(0.6f, 0.6f, 0.65f);
        private static readonly Color Melee = new Color(0.55f, 0.12f, 0.15f);
        private static readonly Color Ranged = new Color(0.6f, 0.2f, 0.55f);
        private static readonly Color Air = new Color(0.35f, 0.2f, 0.6f);
        private static readonly Color Shield = new Color(0.62f, 0.66f, 0.72f);
        private static readonly Color FinalBoss = new Color(0.35f, 0.05f, 0.1f);
        private static readonly Color Shadow = new Color(0.1f, 0.1f, 0.12f);
        private static readonly Color ArrowColor = new Color(1f, 0.95f, 0.7f);
        private static readonly Color EnemyShot = new Color(1f, 0.45f, 0.85f);
        private static readonly Color Spark = new Color(1f, 0.95f, 0.6f);
        private static readonly Color Blast = new Color(1f, 0.55f, 0.15f);
        private static readonly Color Ice = new Color(0.55f, 0.85f, 1f);
        private static readonly Color Poison = new Color(0.7f, 0.9f, 0.2f);
        private static readonly Color Heal = new Color(0.3f, 0.85f, 0.35f);
        private static readonly Color LifeColor = new Color(0.95f, 0.35f, 0.45f);
        private static readonly Color LineOn = new Color(1f, 0.85f, 0.2f);
        private static readonly Color Window = new Color(0.15f, 0.14f, 0.2f);
        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        private static Sprite _knob;
        private static Sprite _box;
        private static TMP_FontAsset _numbers;
        private static List<UnitLooks.Look> _looks;

        [MenuItem("Tools/UnitySlotTower/Setup/Build Help Pages (spec 016)")]
        private static void BuildMenu()
        {
            var scene = SceneManager.GetActiveScene();
            var portrait = GameObject.Find("Portrait Root");
            if (Application.isPlaying || scene.name != GameSession.GameScene || portrait == null || portrait.scene != scene)
            {
                Debug.LogError("[SlotTower Setup] 文字なしの保存状態で Game シーンを開いてください。");
                return;
            }

            var help = portrait.GetComponentInChildren<HelpScreen>(true);
            var bar = portrait.transform.Find("Operation Bar");
            if (help == null || bar == null)
            {
                Debug.LogError("[SlotTower Setup] 早見表または操作バーがありません。");
                return;
            }

            Build(help, bar);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] 早見表の5ページを作り直した（spec 016）。");
        }

        /// <summary>早見表の中身を作り直す（外枠は HelpScreenSetup が作ったもの）。</summary>
        internal static void Build(HelpScreen help, Transform bar)
        {
            _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            _box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _numbers = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NumberFontPath);
            _looks = UnitLooks.Read();
            TextModeSetup.LoadTexts();

            var fields = new SerializedObject(help);
            var panelObject = (GameObject)fields.FindProperty("panel").objectReferenceValue;
            var panel = (RectTransform)panelObject.transform;
            var wasActive = panelObject.activeSelf;
            panelObject.SetActive(true);
            for (var i = panel.childCount - 1; i >= 0; i--)
            {
                var child = panel.GetChild(i);
                if (child.name != "Close Button" && child.name != "Mark")
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            BuildTabs(panel, fields);
            var pages = fields.FindProperty("pages");
            pages.arraySize = HelpScreen.PageCount;
            var roots = new RectTransform[HelpScreen.PageCount];
            for (var i = 0; i < roots.Length; i++)
            {
                roots[i] = Rect("Page " + PageNames[i], panel, Vector2.zero, Vector2.one);
                pages.GetArrayElementAtIndex(i).objectReferenceValue = roots[i].gameObject;
            }

            BuildGuide(roots[0]);
            BuildBet(roots[1]);
            BuildSymbols(roots[2], fields);
            BuildUnits(roots[3], bar, fields);
            BuildEnemies(roots[4]);
            for (var i = 0; i < roots.Length; i++)
            {
                roots[i].gameObject.SetActive(i == 0);
            }

            fields.ApplyModifiedPropertiesWithoutUndo();
            panelObject.SetActive(wasActive);
        }

        // ---------------- タブ ----------------

        private static void BuildTabs(RectTransform panel, SerializedObject fields)
        {
            var bar = Rect("Page Tabs", panel, new Vector2(0f, 1661f / H), new Vector2(1f, 1776f / H));
            fields.FindProperty("previousButton").objectReferenceValue = Chevron(MakeButton("Previous", bar, ColA0, 190f), true);
            fields.FindProperty("nextButton").objectReferenceValue = Chevron(MakeButton("Next", bar, 1250f, ColB1), false);
            var tabs = fields.FindProperty("tabs");
            tabs.arraySize = HelpScreen.PageCount;
            const float left = 210f;
            const float gap = 16f;
            var width = (1230f - left - gap * (HelpScreen.PageCount - 1)) / HelpScreen.PageCount;
            for (var i = 0; i < HelpScreen.PageCount; i++)
            {
                var x = left + i * (width + gap);
                var button = MakeButton(PageNames[i] + " Tab", bar, x, x + width);
                tabs.GetArrayElementAtIndex(i).objectReferenceValue = button;
                var icon = Rect("Icon", (RectTransform)button.transform, new Vector2(0f, 0.18f), new Vector2(1f, 0.82f));
                TextModeLayoutSetup.Set(icon.gameObject, TextModeLayout.Parts.Rect, s =>
                {
                    s.anchorMin = new Vector2(0f, 0.56f);
                    s.anchorMax = new Vector2(1f, 0.96f);
                    return s;
                });
                var art = Rect("Art", icon, Vector2.zero, Vector2.one);
                var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = 1.6f;
                TabArt(i, art);
                TextModeSetup.HelpInsideBottom((RectTransform)button.transform, TabTextIds[i], 50f, 36f);
            }
        }

        private static void TabArt(int page, RectTransform art)
        {
            switch (page)
            {
                case 0: // 遊び方: 本拠地の家
                    Img("Wall", art, new Vector2(0.3f, 0f), new Vector2(0.7f, 0.55f), Color.white);
                    Tri("Roof", art, new Vector2(0.22f, 0.55f), new Vector2(0.78f, 1f), Color.white, Direction.Up);
                    break;
                case 1: // 回し方: 積んだコイン
                    for (var c = 0; c < 3; c++)
                        Img("Coin " + c, art, new Vector2(0.36f, c * 0.3f), new Vector2(0.64f, c * 0.3f + 0.4f), UiStyle.Coin, _knob);
                    break;
                case 2: // 図柄: 赤7
                    SlotSymbolArt.Create(SlotSymbol.Red7, Rect("Seven", art, new Vector2(0.25f, -0.1f), new Vector2(0.75f, 1.1f)));
                    break;
                case 3: // 味方: 人
                    Img("Head", art, new Vector2(0.40f, 0.62f), new Vector2(0.60f, 0.94f), Color.white, _knob);
                    Img("Body", art, new Vector2(0.30f, 0f), new Vector2(0.70f, 0.56f), Color.white);
                    break;
                default: // 敵: 赤い ◀
                    Tri("Enemy", art, new Vector2(0.28f, 0.05f), new Vector2(0.72f, 0.95f), UiStyle.Danger, Direction.Left);
                    break;
            }
        }

        private static Button MakeButton(string name, RectTransform bar, float x0, float x1)
        {
            var image = Img(name, bar, new Vector2(x0 / W, 0f), new Vector2(x1 / W, 1f), UiStyle.ButtonFill, _box);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static Button Chevron(Button button, bool left)
        {
            var angle = left ? 40f : -40f;
            for (var i = 0; i < 2; i++)
            {
                var y = i == 0 ? 0.59f : 0.41f;
                var stroke = Img("Stroke " + i, (RectTransform)button.transform, new Vector2(0.38f, y - 0.04f), new Vector2(0.62f, y + 0.04f),
                    Color.white);
                stroke.rectTransform.localEulerAngles = new Vector3(0f, 0f, i == 0 ? angle : -angle);
            }

            return button;
        }

        // ---------------- 行の骨組み ----------------

        private sealed class RowParts
        {
            public RectTransform Row;
            public RectTransform Left;
            public RectTransform Show;
            public float Height;
        }

        // 行 index（上から）を、ページの本文の高さを count 等分して作る。右の列（Show）は文字ありで上に縮み、下に文が出る。
        private static RowParts Row(RectTransform page, string name, int index, int count, string textId)
        {
            var height = (ContentTop - ContentBottom) / count;
            var top = ContentTop - index * height;
            var row = Rect(name, page, new Vector2(0f, (top - height) / H), new Vector2(1f, top / H));
            if (index < count - 1)
            {
                Img("Divider", row, new Vector2(ColA0 / W, 0f), new Vector2(ColB1 / W, 2f / height), new Color(1f, 1f, 1f, 0.08f));
            }

            var parts = new RowParts { Row = row, Height = height };
            parts.Left = Rect("Left", row, new Vector2(ColA0 / W, Pad / height), new Vector2(ColA1 / W, 1f - Pad / height));
            parts.Show = Rect("Show", row, new Vector2(ColB0 / W, Pad / height), new Vector2(ColB1 / W, 1f - Pad / height));
            var sentenceTop = (count >= 6 ? OneLineSentence : TwoLineSentence) / height;
            TextModeLayoutSetup.Set(parts.Show.gameObject, TextModeLayout.Parts.Rect, s =>
            {
                s.anchorMin.y = sentenceTop;
                return s;
            });
            if (textId != null)
            {
                TextModeSetup.HelpText(row, textId, new Vector2(ColB0 / W, Pad / height), new Vector2(ColB1 / W, sentenceTop), SentenceFont);
            }

            return parts;
        }

        // 左の列の上の札（正方形 size px、左の列の上端に左寄せ）。
        private static RectTransform Card(RowParts parts, float size, Color color)
        {
            var left = parts.Left;
            var h = parts.Height - 2f * Pad;
            var w = ColA1 - ColA0;
            var card = Img("Card", left, new Vector2(0f, 1f - size / h), new Vector2(size / w, 1f), color, _box);
            card.type = Image.Type.Sliced;
            return card.rectTransform;
        }

        // 左の列の数値の1行（下から line 段目、段は 60 px）。アイコンの枠と、数字（右寄せ）を返す。
        private static (RectTransform icon, TextMeshProUGUI value) Value(RowParts parts, string name, int line, float iconWidth = 56f)
        {
            var left = parts.Left;
            var h = parts.Height - 2f * Pad;
            var w = ColA1 - ColA0;
            const float lineHeight = 60f;
            var y0 = line * lineHeight / h;
            var y1 = (line + 1) * lineHeight / h;
            var icon = Rect(name + " Icon", left, new Vector2(0f, y0 + 4f / h), new Vector2(iconWidth / w, y1 - 4f / h));
            var value = Number(name, left, new Vector2((iconWidth + 8f) / w, y0), new Vector2(170f / w, y1));
            value.alignment = TextAlignmentOptions.Right;
            return (icon, value);
        }

        // 数字の右に「秒」（文字ありだけ）。
        private static void SecondsLabel(RowParts parts, TMP_Text value)
        {
            var w = ColA1 - ColA0;
            var r = value.rectTransform;
            TextModeSetup.HelpText(parts.Left, "help2_seconds", new Vector2(174f / w, r.anchorMin.y), new Vector2(240f / w, r.anchorMax.y),
                SentenceFont);
        }

        // ---------------- 1. 遊び方 ----------------

        private static void BuildGuide(RectTransform page)
        {
            string[] ids = { "help2_guide_base", "help2_guide_place", "help2_guide_drop" };
            for (var i = 0; i < ids.Length; i++)
            {
                var parts = Row(page, "Step " + (i + 1), i, ids.Length, ids[i]);
                var number = Number("Step", parts.Left, new Vector2(0f, 0.6f), new Vector2(0.5f, 1f));
                number.text = (i + 1).ToString();
                number.alignment = TextAlignmentOptions.Center;
                var stage = new Stage(parts.Show, 2.1f);
                switch (i)
                {
                    case 0: GuideBase(stage); break;
                    case 1: GuidePlace(stage); break;
                    default: GuideDrop(stage); break;
                }
            }
        }

        // 敵が道を通って本拠地に着き、ライフが1つ減る。
        private static void GuideBase(Stage s)
        {
            GroundLine(s);
            s.Box("Base", 0.09f, 0.38f, 0.22f, 0.26f, Dim);
            s.Tri("Roof", 0.09f, 0.6f, 0.3f, 0.16f, Dim, Direction.Up);
            var flash = s.Animate(s.Box("Hit", 0.09f, 0.42f, 0.34f, 0.4f, UiStyle.Danger), 0.34f, 0.4f);
            flash.At(0f, 0.09f, 0.42f, 1f, 0f).At(0.56f, 0.09f, 0.42f, 1f, 0f).At(0.6f, 0.09f, 0.42f, 1f, 0.6f).At(0.75f, 0.09f, 0.42f, 1f, 0f);
            for (var i = 0; i < 3; i++)
            {
                var x = 0.05f + i * 0.045f;
                var pip = s.Animate(s.Ball("Life " + i, x, 0.86f, 0.1f, LifeColor), 0.1f, 0.1f);
                if (i == 2)
                    pip.At(0f, x, 0.86f).At(0.58f, x, 0.86f).At(0.64f, x, 0.86f, 1.6f, 0f).At(0.99f, x, 0.86f, 1f, 0f);
                else
                    pip.At(0f, x, 0.86f);
            }

            var enemy = s.Enemy("Enemy", Melee, 0.3f, out _);
            enemy.At(0f, 0.95f, 0.4f).At(0.56f, 0.16f, 0.4f).At(0.6f, 0.14f, 0.4f, 1f, 0f).At(1f, 0.14f, 0.4f, 1f, 0f);
            s.Finish(3f);
        }

        // 札を選び、マスに置いて向きを決める。お金が減る。
        private static void GuidePlace(Stage s)
        {
            float[] tiles = { 0.42f, 0.56f, 0.7f };
            foreach (var x in tiles)
                s.Box("Tile", x, 0.5f, 0.26f, 0.26f, GroundTile);
            s.Box("High Tile", 0.84f, 0.53f, 0.26f, 0.32f, HighTile);
            var cursor = s.Animate(s.Box("Cursor", 0.56f, 0.5f, 0.3f, 0.3f, Heal), 0.3f, 0.3f);
            cursor.At(0f, 0.56f, 0.5f, 1f, 0f).At(0.25f, 0.56f, 0.5f, 1f, 0f).At(0.3f, 0.56f, 0.5f, 1f, 0.55f).At(0.5f, 0.56f, 0.5f, 1f, 0.55f)
                .At(0.55f, 0.56f, 0.5f, 1f, 0f);

            var card = s.Group("Card", 0.12f, 0.2f, 0.26f, 0.32f);
            Img("Back", card, Vector2.zero, Vector2.one, CardColor, _box).type = Image.Type.Sliced;
            UnitShape(Rect("Art", card, new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.95f)), 1);
            var select = s.Animate(s.Box("Selected", 0.12f, 0.2f, 0.32f, 0.38f, UiStyle.Ready), 0.32f, 0.38f);
            select.At(0f, 0.12f, 0.2f, 1f, 0f).At(0.1f, 0.12f, 0.2f, 1f, 0f).At(0.14f, 0.12f, 0.2f, 1f, 0.5f).At(0.45f, 0.12f, 0.2f, 1f, 0.5f)
                .At(0.5f, 0.12f, 0.2f, 1f, 0f);
            select.Target.SetSiblingIndex(card.GetSiblingIndex());

            var ghost = s.Group("Ghost", 0.12f, 0.2f, 0.26f, 0.32f);
            Img("Back", ghost, Vector2.zero, Vector2.one, CardColor, _box).type = Image.Type.Sliced;
            UnitShape(Rect("Art", ghost, new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.95f)), 1);
            s.Animate(ghost, 0.26f, 0.32f).At(0f, 0.12f, 0.2f, 1f, 0f).At(0.3f, 0.12f, 0.2f, 1f, 0.8f).At(0.45f, 0.56f, 0.5f, 0.8f, 0.8f)
                .At(0.48f, 0.56f, 0.5f, 0.8f, 0f);

            var unit = s.Ally("Sword", 1, out _);
            unit.At(0f, 0.56f, 0.55f, 1f, 0f).At(0.45f, 0.56f, 0.55f, 1f, 0f).At(0.48f, 0.56f, 0.55f, 1f, 1f).At(0.95f, 0.56f, 0.55f, 1f, 1f)
                .At(1f, 0.56f, 0.55f, 1f, 0f);
            var facing = s.Animate(s.Tri("Facing", 0.65f, 0.55f, 0.12f, 0.16f, Color.white, Direction.Right), 0.12f, 0.16f);
            facing.At(0f, 0.65f, 0.55f, 1f, 0f).At(0.52f, 0.65f, 0.55f, 1f, 0f).At(0.55f, 0.65f, 0.55f, 1f, 1f).At(0.62f, 0.67f, 0.55f, 1f, 1f)
                .At(0.69f, 0.65f, 0.55f, 1f, 1f).At(0.76f, 0.67f, 0.55f, 1f, 1f).At(0.82f, 0.65f, 0.55f, 1f, 0f);

            var yen = s.Animate(s.Text("Yen", "¥", 0.12f, 0.45f, 0.2f, UiStyle.Yen), 0.2f, 0.2f);
            yen.At(0f, 0.12f, 0.42f, 1f, 0f).At(0.46f, 0.12f, 0.42f, 1f, 0f).At(0.5f, 0.12f, 0.45f, 1.3f, 1f).At(0.75f, 0.12f, 0.75f, 0.8f, 0f);
            s.Finish(3.2f);
        }

        // 敵を倒すと ¥ は札へ、コインはスロットへ。
        private static void GuideDrop(Stage s)
        {
            GroundLine(s);
            s.Ally("Sword", 1, out _).At(0f, 0.3f, 0.43f);
            var enemy = s.Enemy("Enemy", Melee, 0.3f, out var gauge);
            enemy.At(0f, 0.45f, 0.4f).At(0.3f, 0.45f, 0.4f).At(0.35f, 0.45f, 0.4f, 0.6f, 0f).At(1f, 0.45f, 0.4f, 0.6f, 0f);
            gauge.Bar(0f, 1f).Bar(0.1f, 1f).Bar(0.12f, 0.5f).Bar(0.25f, 0.5f).Bar(0.27f, 0f);
            Sparks(s, 0.38f, 0.42f, 0.18f, 0.1f, 0.25f);

            var cardIcon = s.Group("Card", 0.15f, 0.12f, 0.2f, 0.2f);
            Img("Back", cardIcon, Vector2.zero, Vector2.one, CardColor, _box).type = Image.Type.Sliced;
            var slot = s.Group("Slot", 0.85f, 0.12f, 0.3f, 0.2f);
            Img("Back", slot, Vector2.zero, Vector2.one, Window, _box).type = Image.Type.Sliced;
            for (var r = 0; r < 3; r++)
                Img("Reel " + r, slot, new Vector2(0.1f + r * 0.28f, 0.15f), new Vector2(0.34f + r * 0.28f, 0.85f), Color.white);
            var glow = s.Animate(s.Box("Glow", 0.85f, 0.12f, 0.36f, 0.26f, UiStyle.Coin), 0.36f, 0.26f);
            glow.At(0f, 0.85f, 0.12f, 1f, 0f).At(0.68f, 0.85f, 0.12f, 1f, 0f).At(0.72f, 0.85f, 0.12f, 1f, 0.6f).At(0.85f, 0.85f, 0.12f, 1f, 0f);
            glow.Target.SetSiblingIndex(slot.GetSiblingIndex());

            var yen = s.Animate(s.Text("Yen", "¥", 0.45f, 0.45f, 0.2f, UiStyle.Yen), 0.2f, 0.2f);
            yen.At(0f, 0.45f, 0.45f, 1f, 0f).At(0.34f, 0.45f, 0.45f, 1f, 0f).At(0.4f, 0.4f, 0.75f, 1.2f, 1f).At(0.68f, 0.15f, 0.14f, 0.8f, 1f)
                .At(0.72f, 0.15f, 0.14f, 0.8f, 0f);
            var coin = s.Animate(s.Ball("Coin", 0.45f, 0.45f, 0.14f, UiStyle.Coin), 0.14f, 0.14f);
            coin.At(0f, 0.45f, 0.45f, 1f, 0f).At(0.36f, 0.45f, 0.45f, 1f, 0f).At(0.42f, 0.55f, 0.75f, 1f, 1f).At(0.68f, 0.85f, 0.14f, 0.8f, 1f)
                .At(0.72f, 0.85f, 0.14f, 0.8f, 0f);
            s.Finish(3f);
        }

        // ---------------- 2. スロットの回し方 ----------------

        private static void BuildBet(RectTransform page)
        {
            for (var n = 1; n <= 3; n++)
            {
                var parts = Row(page, $"Bet {n}", n - 1, 4, $"help2_bet_{n}");
                var coins = Rect("Coins", parts.Left, new Vector2(0.1f, 0.2f), new Vector2(0.6f, 0.95f));
                var fitter = coins.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = 0.5f;
                for (var c = 0; c < n; c++)
                    Img("Coin " + c, coins, new Vector2(0f, c / 3f), new Vector2(1f, (c + 1) / 3f), UiStyle.Coin, _knob).preserveAspect = true;
                BetLines(new Stage(parts.Show, 2.4f), n);
            }

            var spin = Row(page, "Spin", 3, 4, "help2_bet_spin");
            var lever = Rect("Lever", spin.Left, new Vector2(0.1f, 0.2f), new Vector2(0.6f, 0.95f));
            var leverFit = lever.gameObject.AddComponent<AspectRatioFitter>();
            leverFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            leverFit.aspectRatio = 0.5f;
            Img("Stick", lever, new Vector2(0.42f, 0.1f), new Vector2(0.58f, 0.8f), Dim);
            Img("Knob", lever, new Vector2(0.15f, 0.65f), new Vector2(0.85f, 1f), UiStyle.Danger, _knob).preserveAspect = true;
            Spin(new Stage(spin.Show, 2.4f));
        }

        // 窓と3本の段。賭けたコインが入り、有効な段が光る。
        private static void BetLines(Stage s, int bet)
        {
            var window = s.Box("Window", 0.55f, 0.5f, 0.9f, 0.9f, Window);
            for (var r = 0; r < 3; r++)
                s.Box("Reel " + r, 0.55f + (r - 1) * 0.3f / s.Aspect, 0.5f, 0.26f, 0.84f, new Color(0.25f, 0.24f, 0.3f));
            // 段: 1枚は中段、2枚は上段も、3枚は下段も（spec 011）。
            float[] ys = { 0.5f, 0.78f, 0.22f };
            for (var line = 0; line < 3; line++)
            {
                var y = ys[line];
                var bar = s.Animate(s.Box("Line " + line, 0.55f, y, 1.1f, 0.07f, LineOn), 1.1f, 0.07f);
                if (line < bet)
                    bar.At(0f, 0.55f, y, 1f, 0.12f).At(0.45f, 0.55f, y, 1f, 0.12f).At(0.5f, 0.55f, y, 1f, 1f).At(0.95f, 0.55f, y, 1f, 1f)
                        .At(1f, 0.55f, y, 1f, 0.12f);
                else
                    bar.At(0f, 0.55f, y, 1f, 0.12f);
            }

            for (var c = 0; c < bet; c++)
            {
                var start = 0.05f + c * 0.1f;
                var coin = s.Animate(s.Ball("Coin " + c, 0.1f, 0.5f, 0.16f, UiStyle.Coin), 0.16f, 0.16f);
                coin.At(0f, 0.1f, 0.5f, 1f, 0f).At(start, 0.1f, 0.5f, 1f, 1f).At(start + 0.2f, 0.55f, 0.5f, 0.6f, 1f)
                    .At(start + 0.24f, 0.55f, 0.5f, 0.4f, 0f);
            }

            s.Finish(2.6f);
            window.transform.SetAsFirstSibling();
        }

        // レバーを引くとリールが回って止まり、中段に 7 が3つ揃って光る。
        private static void Spin(Stage s)
        {
            s.Box("Window", 0.45f, 0.5f, 0.9f, 0.9f, Window);
            SlotSymbol[][] strips =
            {
                new[] { SlotSymbol.Coin, SlotSymbol.Bar, SlotSymbol.Blue7, SlotSymbol.Green7, SlotSymbol.Coin, SlotSymbol.Yellow7, SlotSymbol.Bar, SlotSymbol.Red7, SlotSymbol.Coin },
                new[] { SlotSymbol.Bar, SlotSymbol.Green7, SlotSymbol.Coin, SlotSymbol.Yellow7, SlotSymbol.Bar, SlotSymbol.Blue7, SlotSymbol.Coin, SlotSymbol.Red7, SlotSymbol.Bar },
                new[] { SlotSymbol.Yellow7, SlotSymbol.Coin, SlotSymbol.Bar, SlotSymbol.Blue7, SlotSymbol.Coin, SlotSymbol.Green7, SlotSymbol.Blue7, SlotSymbol.Red7, SlotSymbol.Green7 },
            };
            float[] stops = { 0.35f, 0.45f, 0.55f };
            for (var r = 0; r < 3; r++)
            {
                var x = 0.45f + (r - 1) * 0.3f / s.Aspect;
                var reel = s.Box("Reel " + r, x, 0.5f, 0.26f, 0.84f, new Color(0.92f, 0.92f, 0.95f)).rectTransform;
                reel.gameObject.AddComponent<RectMask2D>();
                var strip = Rect("Strip", reel, Vector2.zero, Vector2.one);
                for (var c = 0; c < strips[r].Length; c++)
                {
                    var cell = Rect("Cell " + c, strip, new Vector2(0.08f, 1f - (c + 1) / 9f + 0.01f), new Vector2(0.92f, 1f - c / 9f - 0.01f));
                    SlotSymbolArt.Create(strips[r][c], cell);
                }

                // 帯の中心 y（窓の割合）: -0.5 で上から 0〜2 番、1.5 で 6〜8 番が見える（7番が中段）。
                s.Raw(strip).Span(0f, 0.5f, -0.5f, 1f, 3f).Span(0.1f, 0.5f, -0.5f, 1f, 3f).Span(stops[r], 0.5f, 1.5f, 1f, 3f);
            }

            var line = s.Animate(s.Box("Line", 0.45f, 0.5f, 1.05f, 0.08f, LineOn), 1.05f, 0.08f);
            line.At(0f, 0.45f, 0.5f, 1f, 0f).At(0.58f, 0.45f, 0.5f, 1f, 0f).At(0.62f, 0.45f, 0.5f, 1f, 1f).At(0.7f, 0.45f, 0.5f, 1f, 0.3f)
                .At(0.78f, 0.45f, 0.5f, 1f, 1f).At(0.9f, 0.45f, 0.5f, 1f, 0f);
            var burst = s.Animate(s.Ball("Effect", 0.45f, 0.5f, 0.6f, Blast), 0.6f, 0.6f);
            burst.At(0f, 0.45f, 0.5f, 0.2f, 0f).At(0.65f, 0.45f, 0.5f, 0.2f, 0f).At(0.7f, 0.45f, 0.5f, 0.5f, 0.9f).At(0.88f, 0.45f, 0.5f, 1.6f, 0f);

            s.Box("Lever Base", 0.84f, 0.3f, 0.12f, 0.12f, Dim);
            var stick = s.Animate(s.Box("Lever Stick", 0.84f, 0.5f, 0.05f, 0.4f, Dim), 0.05f, 0.4f);
            stick.At(0f, 0.84f, 0.5f).At(0.05f, 0.84f, 0.5f).At(0.1f, 0.84f, 0.35f, 0.4f).At(0.2f, 0.84f, 0.5f);
            var knob = s.Animate(s.Ball("Lever Knob", 0.84f, 0.72f, 0.16f, UiStyle.Danger), 0.16f, 0.16f);
            knob.At(0f, 0.84f, 0.72f).At(0.05f, 0.84f, 0.72f).At(0.1f, 0.84f, 0.42f).At(0.2f, 0.84f, 0.72f);
            s.Finish(3f);
        }

        // ---------------- 3. 図柄の効果 ----------------

        private static readonly SlotSymbol[] EffectOrder =
            { SlotSymbol.Red7, SlotSymbol.Blue7, SlotSymbol.Green7, SlotSymbol.Yellow7, SlotSymbol.Bar, SlotSymbol.Coin };

        private static void BuildSymbols(RectTransform page, SerializedObject fields)
        {
            var seconds = fields.FindProperty("secondsTexts");
            seconds.arraySize = SlotSymbols.Count;
            for (var i = 0; i < seconds.arraySize; i++)
                seconds.GetArrayElementAtIndex(i).objectReferenceValue = null;

            for (var i = 0; i < EffectOrder.Length; i++)
            {
                var symbol = EffectOrder[i];
                var id = SlotSymbols.Ids[(int)symbol];
                var parts = Row(page, id, i, EffectOrder.Length, "help2_" + id.ToLowerInvariant());
                var card = Card(parts, 140f, new Color(0.9f, 0.9f, 0.93f));
                SlotSymbolArt.Create(symbol, Rect("Symbol", card, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f)));
                if (symbol == SlotSymbol.Blue7 || symbol == SlotSymbol.Yellow7 || symbol == SlotSymbol.Bar)
                {
                    var (icon, value) = Value(parts, "Seconds", 0);
                    Clock(icon);
                    seconds.GetArrayElementAtIndex((int)symbol).objectReferenceValue = value;
                    SecondsLabel(parts, value);
                }

                var s = new Stage(parts.Show, 3.5f);
                switch (symbol)
                {
                    case SlotSymbol.Red7: Red7(s); break;
                    case SlotSymbol.Blue7: Blue7(s); break;
                    case SlotSymbol.Green7: Green7(s); break;
                    case SlotSymbol.Yellow7: Yellow7(s); break;
                    case SlotSymbol.Bar: BarDemo(s); break;
                    default: CoinDemo(s); break;
                }
            }
        }

        private static void Red7(Stage s)
        {
            GroundLine(s);
            for (var i = 0; i < 3; i++)
            {
                var x = 0.55f + i * 0.13f;
                var enemy = s.Enemy("Enemy " + i, Melee, 0.3f, out var gauge);
                enemy.At(0f, x, 0.4f).At(1f, x - 0.05f, 0.4f);
                gauge.Bar(0f, 1f).Bar(0.3f, 1f).Bar(0.36f, 0.4f).Bar(0.95f, 0.4f).Bar(1f, 1f);
                var burst = s.Animate(s.Ball("Burst " + i, x, 0.42f, 0.5f, Blast), 0.5f, 0.5f);
                burst.At(0f, x, 0.42f, 0.3f, 0f).At(0.28f, x - 0.015f, 0.42f, 0.3f, 0f).At(0.32f, x - 0.017f, 0.42f, 1f, 1f)
                    .At(0.5f, x - 0.025f, 0.42f, 1.6f, 0f);
            }

            s.Finish(2.4f);
        }

        private static void Blue7(Stage s)
        {
            GroundLine(s);
            for (var i = 0; i < 3; i++)
            {
                var x = 0.75f + i * 0.1f;
                var enemy = s.Enemy("Enemy " + i, Melee, 0.3f, out _);
                enemy.At(0f, x, 0.4f).At(0.3f, x - 0.15f, 0.4f).At(0.8f, x - 0.15f, 0.4f).At(1f, x - 0.25f, 0.4f);
                var ice = s.Raw(Img("Ice", enemy.Target, Vector2.zero, Vector2.one, Ice));
                ice.Span(0f, 0.5f, 0.5f, 1.3f, 1.3f, 0f).Span(0.3f, 0.5f, 0.5f, 1.3f, 1.3f, 0f).Span(0.33f, 0.5f, 0.5f, 1.3f, 1.3f, 0.75f)
                    .Span(0.8f, 0.5f, 0.5f, 1.3f, 1.3f, 0.75f).Span(0.85f, 0.5f, 0.5f, 1.3f, 1.3f, 0f);
            }

            s.Finish(3f);
        }

        private static void Green7(Stage s)
        {
            GroundLine(s);
            for (var i = 0; i < 3; i++)
            {
                var x = 0.2f + i * 0.17f;
                var ally = s.Ally("Ally " + i, i, out var gauge);
                ally.At(0f, x, 0.43f);
                gauge.Bar(0f, 0.3f).Bar(0.3f, 0.3f).Bar(0.5f, 1f).Bar(0.9f, 1f).Bar(1f, 0.3f);
                var plus = s.Group("Plus " + i, x, 0.6f, 0.16f, 0.16f);
                Img("H", plus, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), Heal);
                Img("V", plus, new Vector2(0.35f, 0f), new Vector2(0.65f, 1f), Heal);
                s.Animate(plus, 0.16f, 0.16f).At(0f, x, 0.6f, 1f, 0f).At(0.28f, x, 0.6f, 1f, 0f).At(0.33f, x, 0.66f, 1f, 1f)
                    .At(0.6f, x, 0.9f, 1f, 0f);
            }

            s.Finish(2.6f);
        }

        private static void Yellow7(Stage s)
        {
            GroundLine(s);
            for (var i = 0; i < 3; i++)
            {
                var x = 0.6f + i * 0.12f;
                var enemy = s.Enemy("Enemy " + i, Melee, 0.3f, out var gauge);
                enemy.At(0f, x, 0.4f).At(1f, x - 0.08f, 0.4f);
                var tint = s.Raw(Img("Poison", enemy.Target, Vector2.zero, Vector2.one, Poison));
                tint.Span(0f, 0.5f, 0.5f, 1f, 1f, 0f).Span(0.15f, 0.5f, 0.5f, 1f, 1f, 0f).Span(0.2f, 0.5f, 0.5f, 1f, 1f, 0.55f)
                    .Span(0.9f, 0.5f, 0.5f, 1f, 1f, 0.55f).Span(0.95f, 0.5f, 0.5f, 1f, 1f, 0f);
                gauge.Bar(0f, 1f).Bar(0.28f, 1f).Bar(0.3f, 0.8f).Bar(0.43f, 0.8f).Bar(0.45f, 0.6f).Bar(0.58f, 0.6f).Bar(0.6f, 0.4f)
                    .Bar(0.73f, 0.4f).Bar(0.75f, 0.2f).Bar(0.97f, 0.2f).Bar(1f, 1f);
                var bubble = s.Raw(Img("Bubble", enemy.Target, Vector2.zero, Vector2.one, Poison, _knob));
                for (var b = 0; b < 4; b++)
                {
                    var t = 0.28f + b * 0.15f;
                    bubble.Span(t, 0.5f, 1.1f, 0.25f, 0.25f, 0f).Span(t + 0.02f, 0.5f, 1.15f, 0.3f, 0.3f, 1f)
                        .Span(t + 0.12f, 0.5f, 1.6f, 0.3f, 0.3f, 0f);
                }
            }

            s.Finish(3f);
        }

        private static void BarDemo(Stage s)
        {
            GroundLine(s);
            s.Ally("Sword", 1, out _).At(0f, 0.25f, 0.43f);
            var enemy = s.Enemy("Enemy", Melee, 0.3f, out var gauge);
            enemy.At(0f, 0.4f, 0.4f);
            gauge.Bar(0f, 1f).Bar(0.15f, 1f).Bar(0.17f, 0.9f).Bar(0.6f, 0.9f).Bar(0.62f, 0.55f).Bar(0.8f, 0.55f).Bar(0.82f, 0.2f)
                .Bar(0.97f, 0.2f).Bar(1f, 1f);
            var up = s.Animate(s.Tri("Up", 0.25f, 0.82f, 0.18f, 0.2f, SlotSymbolArt.AttackColor, Direction.Up), 0.18f, 0.2f);
            up.At(0f, 0.25f, 0.82f, 1f, 0f).At(0.4f, 0.25f, 0.82f, 1f, 0f).At(0.45f, 0.25f, 0.82f, 1.4f, 1f).At(0.5f, 0.25f, 0.82f, 1f, 1f)
                .At(0.95f, 0.25f, 0.82f, 1f, 1f).At(1f, 0.25f, 0.82f, 1f, 0f);
            Sparks(s, 0.33f, 0.42f, 0.12f, 0.15f);
            Sparks(s, 0.33f, 0.42f, 0.32f, 0.6f, 0.8f);
            s.Finish(2.8f);
        }

        private static void CoinDemo(Stage s)
        {
            GroundLine(s);
            s.Ally("Sword", 1, out _).At(0f, 0.3f, 0.43f);
            var enemy = s.Enemy("Enemy", Melee, 0.3f, out _);
            enemy.At(0f, 0.45f, 0.4f).At(0.15f, 0.45f, 0.4f).At(0.2f, 0.45f, 0.4f, 0.6f, 0f).At(0.5f, 0.45f, 0.4f, 1f, 0f)
                .At(0.55f, 0.45f, 0.4f, 1f, 1f).At(0.65f, 0.45f, 0.4f, 1f, 1f).At(0.7f, 0.45f, 0.4f, 0.6f, 0f).At(1f, 0.45f, 0.4f, 0.6f, 0f);
            Sparks(s, 0.38f, 0.42f, 0.16f, 0.12f, 0.62f);

            // 2回目の前にコインの図柄が揃い、落とすお金が増える。
            var mark = s.Group("Coin Symbol", 0.75f, 0.7f, 0.3f, 0.3f);
            SlotSymbolArt.Create(SlotSymbol.Coin, mark);
            s.Animate(mark, 0.3f, 0.3f).At(0f, 0.75f, 0.7f, 1f, 0f).At(0.42f, 0.75f, 0.7f, 1f, 0f).At(0.46f, 0.75f, 0.7f, 1.4f, 1f)
                .At(0.5f, 0.75f, 0.7f, 1f, 1f).At(0.97f, 0.75f, 0.7f, 1f, 1f).At(1f, 0.75f, 0.7f, 1f, 0f);
            Yen(s, "Yen 1", 0.45f, 0.2f);
            Yen(s, "Yen 2a", 0.41f, 0.7f);
            Yen(s, "Yen 2b", 0.49f, 0.72f);
            s.Finish(3.2f);
        }

        private static void Yen(Stage s, string name, float x, float start)
        {
            var yen = s.Animate(s.Text(name, "¥", x, 0.5f, 0.22f, UiStyle.Yen), 0.22f, 0.22f);
            yen.At(0f, x, 0.5f, 1f, 0f).At(start, x, 0.5f, 1f, 0f).At(start + 0.04f, x, 0.6f, 1.2f, 1f).At(start + 0.22f, x, 0.85f, 1f, 0f);
        }

        // ---------------- 4. 味方 ----------------

        private static void BuildUnits(RectTransform page, Transform bar, SerializedObject fields)
        {
            var units = CsvParser.Parse("units.csv", AssetDatabase.LoadAssetAtPath<TextAsset>(UnitsPath).text);
            var rows = fields.FindProperty("unitRows");
            var count = units.Rows.Count;
            rows.arraySize = count;
            for (var i = 0; i < count; i++)
            {
                var id = units.Rows[i].Get("id");
                var parts = Row(page, $"Unit {i + 1}", i, count, "help2_unit_" + id);
                var card = Card(parts, 140f, CardColor);
                UnitShape(Rect("Job Art", card, Vector2.zero, Vector2.one), i);

                // 置く場所（地面・高台）。札の右。
                var w = ColA1 - ColA0;
                var h = parts.Height - 2f * Pad;
                var high = units.Rows[i].Get("tile") == "high";
                Img("Tile", parts.Left, new Vector2(160f / w, 1f - (high ? 110f : 90f) / h), new Vector2(230f / w, 1f - 50f / h),
                    high ? HighTile : GroundTile);

                var element = rows.GetArrayElementAtIndex(i);
                var icon = bar.Find($"Unit Icon {i + 1}");
                var body = icon != null ? icon.GetComponent<Image>() : null;
                element.FindPropertyRelative("bodyColor").colorValue = body != null ? body.color : Color.white;

                var (blockIcon, block) = Value(parts, "Block", 2);
                BlockIcon(blockIcon);
                var (costIcon, cost) = Value(parts, "Cost", 1);
                Object.DestroyImmediate(costIcon.gameObject);
                var (waitIcon, wait) = Value(parts, "Cooldown", 0);
                CooldownIcon(waitIcon);
                SecondsLabel(parts, wait);
                element.FindPropertyRelative("blockText").objectReferenceValue = block;
                element.FindPropertyRelative("costText").objectReferenceValue = cost;
                element.FindPropertyRelative("cooldownText").objectReferenceValue = wait;

                var range = Rect("Range", parts.Show, Vector2.zero, new Vector2(0.27f, 1f));
                element.FindPropertyRelative("rangeArea").objectReferenceValue = range;
                var demo = Rect("Demo", parts.Show, new Vector2(0.29f, 0f), Vector2.one);
                var s = new Stage(demo, 2.0f);
                switch (id)
                {
                    case "tank": TankDemo(s); break;
                    case "sword": SwordDemo(s); break;
                    case "archer": ArcherDemo(s); break;
                    default: BufferDemo(s); break;
                }
            }
        }

        private static void TankDemo(Stage s)
        {
            GroundLine(s);
            s.Ally("Tank", 0, out _).At(0f, 0.25f, 0.43f);
            float[] ys = { 0.42f, 0.56f, 0.3f };
            for (var i = 0; i < 3; i++)
            {
                var start = i * 0.15f;
                var enemy = s.Enemy("Enemy " + i, Melee, 0.26f, out _);
                enemy.At(0f, 0.95f, ys[i], 1f, 0f).At(start, 0.95f, ys[i], 1f, 1f).At(start + 0.3f, 0.42f + i * 0.02f, ys[i])
                    .At(0.9f, 0.42f + i * 0.02f, ys[i]).At(0.96f, 0.42f + i * 0.02f, ys[i], 1f, 0f);
            }

            s.Finish(3f);
        }

        private static void SwordDemo(Stage s)
        {
            GroundLine(s);
            s.Ally("Sword", 1, out _).At(0f, 0.25f, 0.43f);
            var enemy = s.Enemy("Enemy", Melee, 0.3f, out var gauge);
            enemy.At(0f, 0.9f, 0.4f).At(0.3f, 0.42f, 0.4f).At(0.75f, 0.42f, 0.4f).At(0.8f, 0.42f, 0.4f, 0.6f, 0f).At(1f, 0.42f, 0.4f, 0.6f, 0f);
            gauge.Bar(0f, 1f).Bar(0.4f, 1f).Bar(0.42f, 0.66f).Bar(0.55f, 0.66f).Bar(0.57f, 0.33f).Bar(0.7f, 0.33f).Bar(0.72f, 0f);
            Sparks(s, 0.34f, 0.42f, 0.2f, 0.4f, 0.55f, 0.7f);
            s.Finish(2.6f);
        }

        private static void ArcherDemo(Stage s)
        {
            GroundLine(s);
            s.Box("High Tile", 0.15f, 0.32f, 0.3f, 0.16f, HighTile);
            s.Ally("Archer", 2, out _).At(0f, 0.15f, 0.58f);
            var air = s.Enemy("Air", Air, 0.24f, out var airGauge, true);
            air.At(0f, 0.9f, 0.8f).At(0.5f, 0.627f, 0.8f).At(0.55f, 0.6f, 0.8f, 0.6f, 0f).At(1f, 0.6f, 0.8f, 0.6f, 0f);
            airGauge.Bar(0f, 1f).Bar(0.3f, 1f).Bar(0.32f, 0.5f).Bar(0.5f, 0.5f).Bar(0.52f, 0f);
            var ground = s.Enemy("Ground", Melee, 0.26f, out var groundGauge);
            ground.At(0f, 0.95f, 0.38f).At(1f, 0.6f, 0.38f);
            groundGauge.Bar(0f, 1f).Bar(0.75f, 1f).Bar(0.77f, 0.5f);
            Shot(s, "Arrow 1", 0.18f, 0.6f, 0.736f, 0.8f, 0.2f, 0.3f, ArrowColor);
            Shot(s, "Arrow 2", 0.18f, 0.6f, 0.627f, 0.8f, 0.4f, 0.5f, ArrowColor);
            Shot(s, "Arrow 3", 0.18f, 0.6f, 0.69f, 0.4f, 0.65f, 0.75f, ArrowColor);
            s.Finish(3f);
        }

        private static void BufferDemo(Stage s)
        {
            GroundLine(s);
            s.Box("High Tile", 0.15f, 0.32f, 0.3f, 0.16f, HighTile);
            s.Ally("Buffer", 3, out _).At(0f, 0.15f, 0.58f);
            s.Ally("Sword", 1, out _).At(0f, 0.5f, 0.43f);
            var enemy = s.Enemy("Enemy", Melee, 0.3f, out _);
            enemy.At(0f, 0.66f, 0.4f);
            var ring = s.Animate(s.Ball("Ring", 0.15f, 0.58f, 0.5f, Heal), 0.5f, 0.5f);
            ring.At(0f, 0.15f, 0.58f, 0.2f, 0f).At(0.1f, 0.15f, 0.58f, 0.2f, 0.5f).At(0.35f, 0.15f, 0.58f, 2.4f, 0f);
            var up = s.Animate(s.Tri("Up", 0.5f, 0.82f, 0.16f, 0.18f, Heal, Direction.Up), 0.16f, 0.18f);
            up.At(0f, 0.5f, 0.82f, 1f, 0f).At(0.3f, 0.5f, 0.82f, 1f, 0f).At(0.35f, 0.5f, 0.82f, 1.4f, 1f).At(0.4f, 0.5f, 0.82f, 1f, 1f)
                .At(0.95f, 0.5f, 0.82f, 1f, 1f).At(1f, 0.5f, 0.82f, 1f, 0f);
            Sparks(s, 0.58f, 0.42f, 0.12f, 0.2f);
            Sparks(s, 0.58f, 0.42f, 0.3f, 0.55f, 0.8f);
            s.Finish(2.8f);
        }

        // ---------------- 5. 敵 ----------------

        private static void BuildEnemies(RectTransform page)
        {
            for (var i = 0; i < EnemyIds.Length; i++)
            {
                var id = EnemyIds[i];
                var parts = Row(page, id, i, EnemyIds.Length, "help2_enemy_" + id);
                var card = Card(parts, 140f, CardColor);
                EnemyArt(Rect("Art", card, new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f)), id);
                var s = new Stage(parts.Show, 3.5f);
                switch (id)
                {
                    case "melee": MeleeDemo(s); break;
                    case "ranged": RangedDemo(s); break;
                    case "air": AirDemo(s); break;
                    case "heavy": HeavyDemo(s); break;
                    case "mid_boss": MidBossDemo(s); break;
                    default: FinalBossDemo(s); break;
                }
            }
        }

        private static void EnemyArt(RectTransform art, string id)
        {
            switch (id)
            {
                case "ranged":
                    Cylinder(art, Ranged);
                    break;
                case "air":
                    Img("Body", art, new Vector2(0.1f, 0.3f), new Vector2(0.9f, 1f), Air, _knob).preserveAspect = true;
                    Img("Shadow", art, new Vector2(0.15f, 0f), new Vector2(0.85f, 0.12f), Shadow, _knob);
                    break;
                case "heavy":
                    Img("Body", art, new Vector2(0.15f, 0.05f), new Vector2(0.95f, 0.85f), Melee);
                    Img("Shield", art, new Vector2(0f, 0.1f), new Vector2(0.14f, 0.7f), Shield);
                    break;
                case "mid_boss":
                    Img("Body", art, Vector2.zero, Vector2.one, Melee);
                    break;
                case "final_boss":
                    Img("Body", art, Vector2.zero, Vector2.one, FinalBoss);
                    Img("Ring", art, new Vector2(-0.1f, -0.1f), new Vector2(1.1f, 1.1f), new Color(1f, 0.4f, 0.3f, 0.35f), _knob);
                    break;
                default:
                    Img("Body", art, new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.8f), Melee);
                    break;
            }
        }

        private static void MeleeDemo(Stage s)
        {
            GroundLine(s);
            s.Ally("Tank", 0, out _).At(0f, 0.25f, 0.43f);
            var enemy = s.Enemy("Enemy", Melee, 0.3f, out _);
            enemy.At(0f, 0.9f, 0.4f).At(0.45f, 0.4f, 0.4f).At(0.5f, 0.38f, 0.4f).At(0.55f, 0.4f, 0.4f).At(0.65f, 0.38f, 0.4f)
                .At(0.7f, 0.4f, 0.4f).At(0.9f, 0.4f, 0.4f).At(0.98f, 0.4f, 0.4f, 1f, 0f);
            s.Finish(3f);
        }

        private static void RangedDemo(Stage s)
        {
            GroundLine(s);
            s.Box("High Tile", 0.25f, 0.32f, 0.3f, 0.16f, HighTile);
            s.Ally("Archer", 2, out var gauge).At(0f, 0.25f, 0.58f);
            gauge.Bar(0f, 1f).Bar(0.45f, 1f).Bar(0.47f, 0.7f).Bar(0.75f, 0.7f).Bar(0.77f, 0.4f).Bar(0.97f, 0.4f).Bar(1f, 1f);
            var enemy = s.Enemy("Enemy", Ranged, 0.3f, out _, false, true);
            enemy.At(0f, 0.95f, 0.4f).At(0.3f, 0.7f, 0.4f);
            Shot(s, "Shot 1", 0.68f, 0.45f, 0.27f, 0.6f, 0.35f, 0.45f, EnemyShot, true);
            Shot(s, "Shot 2", 0.68f, 0.45f, 0.27f, 0.6f, 0.65f, 0.75f, EnemyShot, true);
            s.Finish(3f);
        }

        private static void AirDemo(Stage s)
        {
            GroundLine(s);
            s.Box("High Tile", 0.15f, 0.32f, 0.3f, 0.16f, HighTile);
            s.Ally("Archer", 2, out _).At(0f, 0.15f, 0.58f);
            s.Ally("Tank", 0, out _).At(0f, 0.45f, 0.43f);
            var shadow = s.Animate(s.Ball("Shadow", 0.95f, 0.23f, 0.24f, Shadow), 0.24f, 0.06f);
            shadow.At(0f, 0.95f, 0.23f).At(1f, 0.05f, 0.23f);
            var air = s.Enemy("Air", Air, 0.24f, out var gauge, true);
            air.At(0f, 0.95f, 0.8f).At(1f, 0.05f, 0.8f);
            gauge.Bar(0f, 1f).Bar(0.45f, 1f).Bar(0.47f, 0.6f).Bar(0.7f, 0.6f).Bar(0.72f, 0.2f);
            Shot(s, "Arrow 1", 0.18f, 0.6f, 0.545f, 0.8f, 0.35f, 0.45f, ArrowColor);
            Shot(s, "Arrow 2", 0.18f, 0.6f, 0.32f, 0.8f, 0.6f, 0.7f, ArrowColor);
            s.Finish(3.4f);
        }

        private static void HeavyDemo(Stage s)
        {
            GroundLine(s);
            s.Box("High Tile", 0.12f, 0.32f, 0.3f, 0.16f, HighTile);
            s.Ally("Archer", 2, out _).At(0f, 0.12f, 0.58f);
            s.Ally("Sword", 1, out _).At(0f, 0.35f, 0.43f);
            var heavy = s.Enemy("Heavy", Melee, 0.32f, out var gauge, false, false, true);
            heavy.At(0f, 0.9f, 0.41f).At(0.35f, 0.5f, 0.41f).At(0.9f, 0.5f, 0.41f).At(0.98f, 0.5f, 0.41f, 1f, 0f);
            gauge.Bar(0f, 1f).Bar(0.2f, 1f).Bar(0.22f, 0.95f).Bar(0.5f, 0.95f).Bar(0.52f, 0.75f).Bar(0.7f, 0.75f).Bar(0.72f, 0.55f);
            Shot(s, "Arrow", 0.15f, 0.6f, 0.67f, 0.45f, 0.1f, 0.2f, ArrowColor);
            Sparks(s, 0.43f, 0.42f, 0.2f, 0.5f, 0.7f);
            s.Finish(3f);
        }

        private static void MidBossDemo(Stage s)
        {
            GroundLine(s);
            s.Ally("Tank", 0, out _).At(0f, 0.25f, 0.43f);
            for (var i = 0; i < 3; i++)
            {
                var x = 0.22f + i * 0.03f;
                var pip = s.Animate(s.Box("Block " + i, x, 0.82f, 0.08f, 0.08f, Dim), 0.08f, 0.08f);
                if (i < 2)
                    pip.At(0f, x, 0.82f).At(0.42f, x, 0.82f).Tint(UiStyle.Danger).At(0.45f, x, 0.82f, 1.4f).At(0.5f, x, 0.82f).At(0.97f, x, 0.82f)
                        .Tint(Dim).At(1f, x, 0.82f);
                else
                    pip.At(0f, x, 0.82f);
            }

            var boss = s.Enemy("Boss", Melee, 0.5f, out _);
            boss.At(0f, 0.95f, 0.5f).At(0.42f, 0.45f, 0.5f).At(0.9f, 0.45f, 0.5f).At(0.98f, 0.45f, 0.5f, 1f, 0f);
            s.Finish(3.2f);
        }

        private static void FinalBossDemo(Stage s)
        {
            GroundLine(s);
            s.Ally("Sword", 1, out var g1).At(0f, 0.32f, 0.43f);
            s.Ally("Tank", 0, out var g2).At(0f, 0.47f, 0.43f);
            g1.Bar(0f, 1f).Bar(0.42f, 1f).Bar(0.45f, 0.6f).Bar(0.82f, 0.6f).Bar(0.85f, 0.2f).Bar(0.97f, 0.2f).Bar(1f, 1f);
            g2.Bar(0f, 1f).Bar(0.42f, 1f).Bar(0.45f, 0.6f).Bar(0.82f, 0.6f).Bar(0.85f, 0.2f).Bar(0.97f, 0.2f).Bar(1f, 1f);
            var ring = s.Animate(s.Ball("Ring", 0.68f, 0.52f, 0.5f, new Color(1f, 0.4f, 0.3f)), 0.5f, 0.5f);
            ring.At(0f, 0.68f, 0.52f, 0.2f, 0f).At(0.3f, 0.68f, 0.52f, 0.2f, 0.6f).At(0.5f, 0.68f, 0.52f, 2.4f, 0f)
                .At(0.7f, 0.68f, 0.52f, 0.2f, 0.6f).At(0.9f, 0.68f, 0.52f, 2.4f, 0f);
            var boss = s.Enemy("Boss", FinalBoss, 0.6f, out _);
            boss.At(0f, 0.75f, 0.55f).At(0.3f, 0.68f, 0.55f);
            s.Finish(3.2f);
        }

        // ---------------- 図の部品 ----------------

        private static void GroundLine(Stage s) => s.Box("Ground", 0.5f, 0.23f, s.Aspect, 0.04f, Ground).transform.SetAsFirstSibling();

        // pos から target へ飛ぶ弾（t0〜t1）。
        private static void Shot(Stage s, string name, float x0, float y0, float x1, float y1, float t0, float t1, Color color, bool ball = false)
        {
            var size = ball ? new Vector2(0.08f, 0.08f) : new Vector2(0.14f, 0.035f);
            var graphic = ball ? (Graphic)s.Ball(name, x0, y0, size.x, color) : s.Box(name, x0, y0, size.x, size.y, color);
            var angle = Mathf.Atan2((y1 - y0), (x1 - x0) * s.Aspect) * Mathf.Rad2Deg;
            graphic.rectTransform.localEulerAngles = new Vector3(0f, 0f, ball ? 0f : angle);
            var shot = s.Animate(graphic, size.x, size.y);
            shot.Angle(ball ? 0f : angle).At(0f, x0, y0, 1f, 0f).At(t0, x0, y0, 1f, 0f).At(t0 + 0.01f, x0, y0, 1f, 1f).At(t1, x1, y1, 1f, 1f)
                .At(t1 + 0.01f, x1, y1, 1f, 0f);
        }

        // (x, y) に当たりの光をいくつかの時刻で出す。
        private static void Sparks(Stage s, float x, float y, float size, params float[] times)
        {
            var spark = s.Animate(s.Ball("Spark", x, y, size, Spark), size, size);
            spark.At(0f, x, y, 0.5f, 0f);
            foreach (var t in times)
                spark.At(t - 0.01f, x, y, 0.5f, 0f).At(t, x, y, 1f, 1f).At(t + 0.08f, x, y, 1.4f, 0f);
        }

        private static void UnitShape(RectTransform art, int index)
        {
            if (index < _looks.Count)
            {
                UnitLooks.BuildShape(art, _looks[index]);
            }
        }

        private static void Cylinder(RectTransform parent, Color color)
        {
            var dark = color;
            Img("Side", parent, new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.6f), dark);
            Img("Bottom", parent, new Vector2(0.1f, 0f), new Vector2(0.9f, 0.3f), dark, _knob);
            Img("Top", parent, new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.75f), Color.Lerp(dark, Color.white, 0.25f), _knob);
        }

        private static void BlockIcon(RectTransform parent)
        {
            Img("Wall", parent, new Vector2(0.05f, 0f), new Vector2(0.35f, 1f), Dim);
            Tri("Enemy", parent, new Vector2(0.42f, 0.2f), new Vector2(1f, 0.8f), UiStyle.Danger, Direction.Left);
        }

        private static void CooldownIcon(RectTransform parent)
        {
            Img("Card", parent, new Vector2(0.15f, 0f), new Vector2(0.85f, 1f), Dim);
            Img("Gauge", parent, new Vector2(0.15f, 0f), new Vector2(0.85f, 0.6f), new Color(0f, 0f, 0f, 0.6f));
        }

        private static void Clock(RectTransform parent)
        {
            var face = Img("Clock", parent, Vector2.zero, Vector2.one, Color.white, _knob);
            face.preserveAspect = true;
            Img("Hand Up", face.rectTransform, new Vector2(0.45f, 0.48f), new Vector2(0.55f, 0.85f), UiStyle.SectionFill);
            Img("Hand Right", face.rectTransform, new Vector2(0.48f, 0.45f), new Vector2(0.78f, 0.55f), UiStyle.SectionFill);
        }

        // ---------------- 低レベルの部品 ----------------

        internal static RectTransform Rect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image Img(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, Sprite sprite = null)
        {
            var image = Rect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TriangleGraphic Tri(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, Direction pointing)
        {
            var triangle = Rect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<TriangleGraphic>();
            triangle.color = color;
            triangle.Pointing = pointing;
            triangle.raycastTarget = false;
            return triangle;
        }

        private static TextMeshProUGUI Number(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var text = Rect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _numbers;
            text.text = "0";
            text.color = Color.white;
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = 52f;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
            return text;
        }

        // ---------------- 動く図の組み立て ----------------

        /// <summary>
        /// 動く図の舞台。area いっぱいに縦横比 aspect の枠（AspectRatioFitter）を置き、HelpDemo を付ける。
        /// 座標は枠の割合（x・y 0〜1）、大きさは枠の高さを 1 とした単位（正方形が正方形に見える）。
        /// </summary>
        private sealed class Stage
        {
            private readonly List<HelpDemo.Track> _tracks = new List<HelpDemo.Track>();

            public Stage(RectTransform area, float aspect)
            {
                Aspect = aspect;
                Rect = HelpRebuildSetup.Rect("Stage", area, Vector2.zero, Vector2.one);
                var fitter = Rect.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = aspect;
                // 広がる輪などが枠の外（文字ありの文など）に描かれないよう切り抜く。
                Rect.gameObject.AddComponent<RectMask2D>();
            }

            public RectTransform Rect { get; }

            public float Aspect { get; }

            public Vector2 Size(float w, float h) => new Vector2(w / Aspect, h);

            private RectTransform Place(string name, float x, float y, float w, float h)
            {
                var half = Size(w, h) * 0.5f;
                var center = new Vector2(x, y);
                return HelpRebuildSetup.Rect(name, Rect, center - half, center + half);
            }

            public Image Box(string name, float x, float y, float w, float h, Color color)
            {
                var image = Place(name, x, y, w, h).gameObject.AddComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
                return image;
            }

            public Image Ball(string name, float x, float y, float d, Color color)
            {
                var image = Box(name, x, y, d, d, color);
                image.sprite = _knob;
                return image;
            }

            public TriangleGraphic Tri(string name, float x, float y, float w, float h, Color color, Direction pointing)
            {
                var triangle = Place(name, x, y, w, h).gameObject.AddComponent<TriangleGraphic>();
                triangle.color = color;
                triangle.Pointing = pointing;
                triangle.raycastTarget = false;
                return triangle;
            }

            public TextMeshProUGUI Text(string name, string value, float x, float y, float size, Color color)
            {
                var text = Place(name, x, y, size, size).gameObject.AddComponent<TextMeshProUGUI>();
                text.font = _numbers;
                text.text = value;
                text.color = color;
                text.enableAutoSizing = true;
                text.fontSizeMin = 10f;
                text.fontSizeMax = 200f;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                return text;
            }

            /// <summary>いくつかの絵をまとめる入れ物（CanvasGroup で一緒に消せる）。</summary>
            public RectTransform Group(string name, float x, float y, float w, float h)
            {
                var rect = Place(name, x, y, w, h);
                rect.gameObject.AddComponent<CanvasGroup>();
                return rect;
            }

            /// <summary>味方（盤面の見た目の影絵と緑のゲージ）。</summary>
            public Actor Ally(string name, int index, out Actor gauge)
            {
                var group = Group(name, 0f, 0f, 0.36f, 0.36f);
                // 足元の青い台とゲージで味方と分かるようにする（敵は赤）。
                Img("Base", group, new Vector2(-0.05f, -0.1f), new Vector2(1.05f, 0.08f), UiStyle.Ally, _knob);
                UnitShape(HelpRebuildSetup.Rect("Art", group, Vector2.zero, Vector2.one), index);
                gauge = Gauge(group, UiStyle.Ally);
                return Animate(group, 0.36f, 0.36f);
            }

            /// <summary>敵（盤面の見た目に合わせた図形と赤いゲージ）。</summary>
            public Actor Enemy(string name, Color color, float size, out Actor gauge, bool air = false, bool ranged = false, bool shield = false)
            {
                var group = Group(name, 0f, 0f, size, size);
                if (!air)
                    Img("Base", group, new Vector2(-0.05f, -0.1f), new Vector2(1.05f, 0.08f), UiStyle.Danger, _knob);
                if (air)
                    Img("Body", group, Vector2.zero, Vector2.one, color, _knob);
                else if (ranged)
                    Cylinder(group, color);
                else
                    Img("Body", group, new Vector2(0.1f, 0f), new Vector2(0.9f, 0.85f), color);
                if (shield)
                    Img("Shield", group, new Vector2(-0.08f, 0.05f), new Vector2(0.06f, 0.65f), Shield);
                gauge = Gauge(group, UiStyle.Danger);
                return Animate(group, size, size);
            }

            private Actor Gauge(RectTransform group, Color color)
            {
                var back = Img("Gauge", group, new Vector2(0.05f, 1.04f), new Vector2(0.95f, 1.14f), new Color(0.05f, 0.05f, 0.06f));
                var fill = Img("Fill", back.rectTransform, Vector2.zero, Vector2.one, color);
                return Raw(fill).Bar(0f, 1f);
            }

            /// <summary>位置と大きさを舞台の単位で動かす。</summary>
            public Actor Animate(Graphic graphic, float w, float h) => Add(graphic.rectTransform, graphic, null, w, h, false);

            public Actor Animate(RectTransform group, float w, float h) =>
                Add(group, null, group.GetComponent<CanvasGroup>(), w, h, false);

            /// <summary>親の矩形の割合のまま動かす（入れ物の中の部品）。</summary>
            public Actor Raw(Graphic graphic) => Add(graphic.rectTransform, graphic, null, 1f, 1f, true);

            public Actor Raw(RectTransform rect) => Add(rect, null, null, 1f, 1f, true);

            private Actor Add(RectTransform target, Graphic graphic, CanvasGroup group, float w, float h, bool raw)
            {
                var track = new HelpDemo.Track { target = target, graphic = graphic, group = group };
                _tracks.Add(track);
                return new Actor(this, track, w, h, raw, graphic != null ? graphic.color : Color.white);
            }

            public void Finish(float period) => Rect.gameObject.AddComponent<HelpDemo>().Configure(period, _tracks.ToArray());
        }

        private sealed class Actor
        {
            private readonly Stage _stage;
            private readonly HelpDemo.Track _track;
            private readonly List<HelpDemo.Key> _keys = new List<HelpDemo.Key>();
            private readonly float _w;
            private readonly float _h;
            private readonly bool _raw;
            private Color _color;
            private float _angle;

            public Actor(Stage stage, HelpDemo.Track track, float w, float h, bool raw, Color color)
            {
                _stage = stage;
                _track = track;
                _w = w;
                _h = h;
                _raw = raw;
                _color = color;
            }

            public RectTransform Target => _track.target;

            /// <summary>以降のキーの色。</summary>
            public Actor Tint(Color color)
            {
                _color = color;
                return this;
            }

            /// <summary>以降のキーの回転。</summary>
            public Actor Angle(float angle)
            {
                _angle = angle;
                return this;
            }

            /// <summary>時刻 t に中心 (x, y)、大きさ ×scale、透明度 ×alpha。</summary>
            public Actor At(float t, float x, float y, float scale = 1f, float alpha = 1f)
            {
                var size = _raw ? new Vector2(_w * scale, _h * scale) : _stage.Size(_w * scale, _h * scale);
                var color = _color;
                color.a *= alpha;
                return Push(t, new Vector2(x, y), size, color);
            }

            /// <summary>親の割合で、時刻 t に中心 (x, y)・大きさ (w, h)・透明度 alpha。</summary>
            public Actor Span(float t, float x, float y, float w, float h, float alpha = 1f)
            {
                var color = _color;
                color.a *= alpha;
                return Push(t, new Vector2(x, y), new Vector2(w, h), color);
            }

            /// <summary>ゲージの中身: 時刻 t に左から fraction の長さ。</summary>
            public Actor Bar(float t, float fraction)
            {
                if (_keys.Count > 0 && _keys[_keys.Count - 1].time >= t)
                {
                    _keys.Clear();
                }

                return Push(t, new Vector2(fraction * 0.5f, 0.5f), new Vector2(fraction, 1f), _color);
            }

            private Actor Push(float t, Vector2 position, Vector2 size, Color color)
            {
                _keys.Add(new HelpDemo.Key { time = t, position = position, size = size, color = color, angle = _angle });
                _track.keys = _keys.ToArray();
                return this;
            }
        }
    }
}
