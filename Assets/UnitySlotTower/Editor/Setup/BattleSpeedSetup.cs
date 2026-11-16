using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// T365（FR-009, FR-011、plan 17.5）: 開いている Game シーンの操作バーに残りの敵の数（敵の絵と数字）と倍速のボタン（×1）を足し、
    /// <see cref="OperationBarStatus"/> につなぐ。文字あり版の位置（帯の空き）も置く。名前のラベルは T370。何度実行しても同じ結果になる。
    /// </summary>
    internal static class BattleSpeedSetup
    {
        private const string FontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private static readonly string[] PartNames = { "Enemy Icon", "Remaining", "Speed Button" };

        // 文字あり版の帯の値の段・名前の段（TextModeLayoutSetup の ValueBottom・ValueTop・NameTop と同じ）。
        private const float BandValueBottom = 0.04f;
        private const float BandValueTop = 0.54f;
        private const float BandNameTop = 0.98f;

        [MenuItem("Tools/UnitySlotTower/Setup/Build Speed And Remaining (T365)")]
        private static void Build()
        {
            var status = Object.FindAnyObjectByType<OperationBarStatus>(FindObjectsInactive.Include);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (status == null || font == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの操作バー（OperationBarStatus）か数字のフォントが見つからない。");
                return;
            }

            var root = (RectTransform)status.transform;
            foreach (var name in PartNames)
            {
                var old = root.Find(name);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            var box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // 上の段の右（ウェーブ n/2 があった所）: 敵の顔（赤紫の箱に白い目）と残りの数。
            var icon = CreateImage("Enemy Icon", root, new Vector2(0.73f, 0.58f), new Vector2(0.82f, 0.9f),
                new Color(0.6f, 0.15f, 0.3f), box);
            icon.type = Image.Type.Sliced;
            icon.preserveAspect = false;
            CreateImage("Eye Left", icon.rectTransform, new Vector2(0.2f, 0.45f), new Vector2(0.42f, 0.75f), Color.white, knob)
                .preserveAspect = true;
            CreateImage("Eye Right", icon.rectTransform, new Vector2(0.58f, 0.45f), new Vector2(0.8f, 0.75f), Color.white, knob)
                .preserveAspect = true;
            var remaining = CreateNumber("Remaining", root, new Vector2(0.83f, 0.52f), new Vector2(0.98f, 0.96f), font, "0");

            // 下の段の左（撤退・一時停止の並び）: 倍速のボタン。
            var speedImage = CreateImage("Speed Button", root, new Vector2(0.04f, 0.06f), new Vector2(0.34f, 0.46f),
                new Color(0.25f, 0.4f, 0.55f), box);
            speedImage.type = Image.Type.Sliced;
            speedImage.raycastTarget = true;
            var speed = speedImage.gameObject.AddComponent<Button>();
            speed.navigation = new Navigation { mode = Navigation.Mode.None };
            var speedText = CreateNumber("Speed", speedImage.rectTransform, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.9f),
                font, "×1");

            var serialized = new SerializedObject(status);
            serialized.FindProperty("remainingText").objectReferenceValue = remaining;
            serialized.FindProperty("speedButton").objectReferenceValue = speed;
            serialized.FindProperty("speedText").objectReferenceValue = speedText;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 文字あり版: 帯の所持金と倍率の間に残りの数、撤退の左に倍速（TextModeLayoutSetup の BuildStatusBand の空き）。
            // T370 から名前のラベルは Add Text Mode Labels、最終的な位置は Build Text Mode Layout が置く（このメニューの後に両方を実行し直す）。
            SetTextRect(icon.rectTransform, new Vector2(0.285f, 0.10f), new Vector2(0.319f, 0.48f));
            SetTextRect(remaining.rectTransform, new Vector2(0.321f, BandValueBottom), new Vector2(0.40f, BandValueTop));
            SetTextRect(speedImage.rectTransform, new Vector2(0.63f, BandValueBottom), new Vector2(0.74f, BandNameTop));

            var scene = root.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] 操作バーに残りの敵の数と倍速のボタンを足した（T365）。");
        }

        private static void SetTextRect(RectTransform target, Vector2 anchorMin, Vector2 anchorMax) =>
            TextModeLayoutSetup.Set(target.gameObject, TextModeLayout.Parts.Rect, s =>
            {
                s.anchorMin = anchorMin;
                s.anchorMax = anchorMax;
                s.offsetMin = Vector2.zero;
                s.offsetMax = Vector2.zero;
                return s;
            });

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image CreateImage(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color,
            Sprite sprite)
        {
            var image = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateNumber(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
            TMP_FontAsset font, string initial)
        {
            var text = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = initial;
            text.color = Color.white;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 200f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }
    }
}
