using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Flow;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Setup
{
    internal static class PauseMenuSetup
    {
        [MenuItem("Tools/UnitySlotTower/Setup/Pause Menu (T423)")]
        private static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            var root = GameObject.Find("Portrait Root")?.transform as RectTransform;
            var status = root?.Find("Top Band/Status") as RectTransform;
            var help = root?.GetComponentInChildren<HelpScreen>(true);
            var speed = status?.Find("Speed Button");
            var battle = Object.FindAnyObjectByType<BattleController>();
            var input = Object.FindAnyObjectByType<UnitPlacementInput>();
            if (scene.name != GameSession.GameScene || root == null || root.gameObject.scene != scene ||
                help == null || help.OpenButton == null || speed == null || battle == null || input == null)
            {
                Debug.LogError("[SlotTower Setup] 帯と早見表のある Game シーンを開いてください。");
                return;
            }

            StyleHelpButton(status, help.OpenButton);
            RemoveChild(status, "Home Button");
            var home = ButtonAt(status, "Home Button", 0f, 0f, 1f, 1f);
            // Rates ends at 980 px in the icon-only HUD.
            BattleHudRestyleSetup.CornerButton(status, home.name, 990f, 1110f);
            var house = Rect(home.transform, "House", 0.24f, 0.12f, 0.76f, 0.88f);
            var roof = Rect(house, "Roof", 0f, 0.48f, 1f, 1f).gameObject.AddComponent<TriangleGraphic>();
            roof.Pointing = TriangleGraphic.Direction.Up;
            roof.raycastTarget = false;
            ImageAt(house, "Walls", 0.17f, 0f, 0.83f, 0.52f, Color.white);
            ImageAt(house, "Door", 0.42f, 0f, 0.58f, 0.29f, UiStyle.PanelFill);

            RemoveChild(root, "Quit Confirmation");
            var shade = ImageAt(root, "Quit Confirmation", 0f, 0f, 1f, 1f, new Color(0f, 0f, 0f, 0.72f));
            shade.raycastTarget = true;
            // 下の札が透けて文字と混ざらないよう、板とボタンは不透明（T423）。
            var board = ImageAt(shade.transform, "Board", 0.15f, 0.38f, 0.85f, 0.62f, UiStyle.ScreenFill);
            UiSkin.Apply(board, UiStyle.ScreenFill, UiStyle.PanelBorder);
            var yes = ButtonAt(board.transform, "Confirm", 0.12f, 0.10f, 0.46f, 0.62f);
            var no = ButtonAt(board.transform, "Cancel", 0.54f, 0.10f, 0.88f, 0.62f);
            UiSkin.Apply(yes.GetComponent<Image>(), UiStyle.SectionFill, UiStyle.PanelBorder);
            UiSkin.Apply(no.GetComponent<Image>(), UiStyle.SectionFill, UiStyle.PanelBorder);
            Stroke(yes.transform, new Vector2(-32f, -5f), 58f, -45f);
            Stroke(yes.transform, new Vector2(15f, 8f), 98f, 48f);
            Stroke(no.transform, Vector2.zero, 110f, 45f);
            Stroke(no.transform, Vector2.zero, 110f, -45f);

            var menu = help.GetComponent<QuitToTitle>();
            if (menu == null) menu = help.gameObject.AddComponent<QuitToTitle>();
            var fields = new SerializedObject(menu);
            fields.FindProperty("battle").objectReferenceValue = battle;
            fields.FindProperty("help").objectReferenceValue = help;

            fields.FindProperty("panel").objectReferenceValue = shade.gameObject;
            fields.FindProperty("homeButton").objectReferenceValue = home;
            fields.FindProperty("confirmButton").objectReferenceValue = yes;
            fields.FindProperty("cancelButton").objectReferenceValue = no;
            fields.ApplyModifiedPropertiesWithoutUndo();
            var placement = new SerializedObject(input);
            placement.FindProperty("quitToTitle").objectReferenceValue = menu;
            placement.ApplyModifiedPropertiesWithoutUndo();
            TextModeSetup.AddPauseLabels(root, help);
            home.gameObject.SetActive(false);
            help.OpenButton.gameObject.SetActive(false);
            shade.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        internal static void StyleHelpButton(RectTransform status, Button open)
        {
            open.transform.SetParent(status, false);
            RemoveChild(open.transform, UiSkin.SkinName);
            open.targetGraphic = open.GetComponent<Image>();
            BattleHudRestyleSetup.CornerButton(status, open.name, 1188f, 1306f);
        }

        private static void RemoveChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static RectTransform Rect(Transform parent, string name, float left, float bottom, float right, float top)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image ImageAt(Transform parent, string name, float left, float bottom, float right, float top, Color color)
        {
            var image = Rect(parent, name, left, bottom, right, top).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Button ButtonAt(Transform parent, string name, float left, float bottom, float right, float top)
        {
            var image = ImageAt(parent, name, left, bottom, right, top, UiStyle.PanelFill);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static void Stroke(Transform parent, Vector2 offset, float length, float angle)
        {
            var line = ImageAt(parent, "Stroke", 0.5f, 0.63f, 0.5f, 0.63f, Color.white).rectTransform;
            line.sizeDelta = new Vector2(length, 14f);
            line.anchoredPosition = offset;
            line.localEulerAngles = new Vector3(0f, 0f, angle);
        }
    }
}
