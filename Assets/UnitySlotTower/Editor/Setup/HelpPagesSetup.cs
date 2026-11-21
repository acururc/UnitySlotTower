using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnitySlotTower.Flow;
using UnitySlotTower.Ui;
using static UnitySlotTower.Editor.Setup.HelpScreenSetup;

namespace UnitySlotTower.Editor.Setup
{
    internal static class HelpPagesSetup
    {
        [MenuItem("Tools/UnitySlotTower/Setup/Help Pages (T424)")]
        private static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            var portrait = GameObject.Find("Portrait Root");
            if (Application.isPlaying || scene.name != GameSession.GameScene || portrait == null || portrait.scene != scene)
            {
                Debug.LogError("文字なしの保存状態で Game シーンを開いてください。");
                return;
            }

            var root = (RectTransform)portrait.transform;
            var help = root.GetComponentInChildren<HelpScreen>(true);
            var bar = root.Find("Operation Bar");
            if (help == null || bar == null)
            {
                Debug.LogError("早見表または操作バーがありません。");
                return;
            }
            var fields = new SerializedObject(help);
            var panel = (GameObject)fields.FindProperty("panel").objectReferenceValue;
            if (panel == null) return;
            var area = (RectTransform)panel.transform;
            foreach (var name in new[] { SlotRowsName, "Divider", "Unit 1", "Unit 2", "Unit 3", "Unit 4",
                "Slot Page", "Unit Page", "Page Tabs", "Text Label help_slot_heading", "Text Label help_units_heading",
                "Text Label help_col_symbol", "Text Label help_col_chance", "Text Label help_col_effect",
                "Text Label help_col_job", "Text Label help_col_range", "Text Label help_col_stats" })
            {
                var child = area.Find(name);
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }
            LoadAssets();
            BuildPages(area, bar, fields);
            fields.ApplyModifiedPropertiesWithoutUndo();
            RestyleUnits();
            TextModeSetup.AddHelpLabels(root);
            panel.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        internal static void BuildPages(RectTransform area, Transform bar, SerializedObject fields)
        {
            var slot = CreateRect("Slot Page", area, Vector2.zero, Vector2.one);
            var units = CreateRect("Unit Page", area, Vector2.zero, Vector2.one);
            BuildSlot(slot, fields, 0.84f, 0.09f);
            BuildUnits(units, bar, fields, 0.84f, 0.20f);
            fields.FindProperty("slotPage").objectReferenceValue = slot.gameObject;
            fields.FindProperty("unitPage").objectReferenceValue = units.gameObject;
            slot.gameObject.SetActive(false);
            units.gameObject.SetActive(false);

            var tabs = CreateRect("Page Tabs", area, new Vector2(0f, 0.865f), new Vector2(1f, 0.925f));
            RectTransform Tab(string name, string field, float left, float right)
            {
                var button = CreateButton(name, tabs, new Vector2(left, 0f), new Vector2(right, 1f), UiStyle.ButtonFill);
                button.targetGraphic = button.GetComponent<Image>();
                fields.FindProperty(field).objectReferenceValue = button;
                return (RectTransform)button.transform;
            }

            Chevron(Tab("Previous", "previousButton", 0.17f, 0.27f), true);
            // タブの絵は Icon（文字なし版は中央、文字あり版は TextModeLayoutSetup が上半分へ）の中の縦横比を保つ Art に描く。
            var reels = TabArt(Tab("Slot Tab", "slotTab", 0.30f, 0.48f));
            for (var i = 0; i < 3; i++)
                CreateImage("Reel " + (i + 1), reels, new Vector2(0.22f + i * 0.2f, 0f), new Vector2(0.38f + i * 0.2f, 1f), Color.white, null);
            var unit = TabArt(Tab("Unit Tab", "unitTab", 0.52f, 0.70f));
            var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            CreateImage("Head", unit, new Vector2(0.40f, 0.62f), new Vector2(0.60f, 0.94f), Color.white, circle);
            CreateImage("Body", unit, new Vector2(0.30f, 0f), new Vector2(0.70f, 0.56f), Color.white, null);
            Chevron(Tab("Next", "nextButton", 0.73f, 0.83f), false);
        }

        /// <summary>タブの絵の置き場所（文字なし版の位置）。</summary>
        internal static readonly Vector2 TabIconNoTextY = new Vector2(0.18f, 0.82f);

        private static RectTransform TabArt(RectTransform tab)
        {
            var icon = CreateRect("Icon", tab, new Vector2(0f, TabIconNoTextY.x), new Vector2(1f, TabIconNoTextY.y));
            var art = CreateRect("Art", icon, Vector2.zero, Vector2.one);
            var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1.6f;
            return art;
        }

        private static void Chevron(RectTransform button, bool left)
        {
            var angle = left ? 40f : -40f;
            for (var i = 0; i < 2; i++)
            {
                var y = i == 0 ? 0.59f : 0.41f;
                var stroke = CreateImage("Stroke " + i, button, new Vector2(0.38f, y - 0.04f),
                    new Vector2(0.62f, y + 0.04f), Color.white, null);
                stroke.rectTransform.localEulerAngles = new Vector3(0f, 0f, i == 0 ? angle : -angle);
            }
        }
    }
}
