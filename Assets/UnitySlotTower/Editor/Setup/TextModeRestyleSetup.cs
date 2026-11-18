using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Setup
{
    internal static class TextModeRestyleSetup
    {
        [MenuItem("Tools/UnitySlotTower/Setup/Restyle Text Mode (T389)")]
        private static void Restyle()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "Title" && scene.name != "Game") return;
            Canvas.ForceUpdateCanvases();
            var inside = 0;
            var standalone = 0;
            var legends = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var label in root.GetComponentsInChildren<TextModeLabel>(true))
                {
                    var serialized = new SerializedObject(label);
                    var property = serialized.FindProperty("background");
                    var old = property.objectReferenceValue as Graphic;
                    if (old == null) continue;
                    var target = old.gameObject;
                    var isInside = IsInsidePanel(label.transform);
                    if (old is UiPanel && !isInside) continue;
                    Object.DestroyImmediate(old);
                    property.objectReferenceValue = isInside ? null : AddBackground(target);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    if (isInside) inside++; else standalone++;
                }

                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image.name != TextModeSetup.RouteLegendName) continue;
                    UiSkin.Apply(image, UiStyle.SectionFill, UiStyle.PanelBorder);
                    legends++;
                }
            }

            HelpScreenSetup.RestyleUnits();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[T389] 中: {inside}, 単独: {standalone}, 凡例: {legends}");
        }

        internal static UiPanel AddBackground(GameObject target)
        {
            var panel = target.AddComponent<UiPanel>();
            panel.color = UiStyle.PanelFill;
            panel.SetBorder(UiStyle.PanelBorder);
            panel.raycastTarget = false;
            panel.enabled = false;
            return panel;
        }

        internal static bool IsInsidePanel(Transform label)
        {
            var canvas = label.GetComponentInParent<Canvas>(true);
            if (canvas == null) return false;
            for (var parent = label.parent; parent != null && parent != canvas.transform; parent = parent.parent)
            {
                if (parent.GetComponent<UiPanel>() != null || parent.Find(UiSkin.SkinName) != null) return true;
                var image = parent.GetComponent<Image>();
                // 不透明な地（画面全体の地も含む）の上なら文字だけで読める。地が要るのは盤面など透けた所の上だけ。
                if (image != null && image.enabled && image.color.a >= 0.5f) return true;
            }
            return false;
        }
    }
}
