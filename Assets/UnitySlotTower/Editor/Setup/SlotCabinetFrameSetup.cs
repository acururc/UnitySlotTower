using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Layout;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// T614（FR-116・E-29）: 開いている Game シーンの画面全体をスロット台の外枠で囲む。3:4 の中身（Portrait Root）を
    /// <see cref="PortraitLayout.ContentScale"/> 倍の大きさに縮め（文字の大きさは変えない）、空いた周りに外枠と LED の列
    /// （<see cref="SlotCabinetLights"/>）を置く。戦闘カメラは <see cref="PortraitLayout.BattleView"/> が同じ割合で縮める。
    /// 外枠は UI Canvas の中で Portrait Root のすぐ後（上）に、自分の Canvas を持たせて置く（LED の色の更新を外枠だけに閉じる）。
    /// 見た目は仮置き（本番は M5）。何度実行しても同じ結果になる。
    /// </summary>
    internal static class SlotCabinetFrameSetup
    {
        private const string FrameName = "Cabinet Frame";
        private const string RootName = "Portrait Root";
        private const float LedSize = 20f;
        private const float LedSpacing = 36f;
        private const float OuterLine = 4f;
        private const float InnerTrim = 5f;

        private static readonly Color BodyColor = new Color(0.2f, 0.05f, 0.07f);
        private static readonly Color OuterLineColor = new Color(0.5f, 0.16f, 0.16f);
        private static readonly Color TrimColor = new Color(0.85f, 0.66f, 0.25f);

        [MenuItem("Tools/UnitySlotTower/Setup/Build Cabinet Frame (T614)")]
        private static void Build()
        {
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var root = cabinet != null ? FindAncestor(cabinet.transform, RootName) : null;
            var canvas = root != null ? root.parent as RectTransform : null;
            if (canvas == null || canvas.GetComponent<Canvas>() == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの筐体・Portrait Root・UI Canvas が見つからない。");
                return;
            }

            var full = new Vector2(PortraitLayout.ReferenceWidth, PortraitLayout.ReferenceHeight);
            var content = full * (float)PortraitLayout.ContentScale;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = content;

            var old = canvas.Find(FrameName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var frame = new GameObject(FrameName, typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
            frame.gameObject.layer = canvas.gameObject.layer;
            frame.SetParent(canvas, false);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = full;
            frame.SetSiblingIndex(root.GetSiblingIndex() + 1);

            var half = full * 0.5f;
            var inner = content * 0.5f;
            var band = half - inner;
            Ring("Body", frame, half, band, BodyColor);
            Ring("Outer Line", frame, half, new Vector2(OuterLine, OuterLine), OuterLineColor);
            Ring("Trim", frame, inner + new Vector2(InnerTrim, InnerTrim), new Vector2(InnerTrim, InnerTrim), TrimColor);

            // LED は外側の線と内側の縁の間の帯の真ん中に、左上の角から時計回りに並べる。
            var path = (half - new Vector2(OuterLine, OuterLine) + inner + new Vector2(InnerTrim, InnerTrim)) * 0.5f;
            var leds = new GameObject("LEDs", typeof(RectTransform)).GetComponent<RectTransform>();
            leds.gameObject.layer = frame.gameObject.layer;
            leds.SetParent(frame, false);
            Stretch(leds);
            var lights = CreateLeds(leds, path);

            var component = frame.gameObject.AddComponent<SlotCabinetLights>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("cabinet").objectReferenceValue = cabinet;
            var array = serialized.FindProperty("lights");
            array.arraySize = lights.Length;
            for (var i = 0; i < lights.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            }

            var colors = serialized.FindProperty("symbolColors");
            var symbolColors = new[]
            {
                SlotSymbolArt.SevenColors[0], SlotSymbolArt.SevenColors[1], SlotSymbolArt.SevenColors[2],
                SlotSymbolArt.SevenColors[3], new Color(0.95f, 0.95f, 1f), SlotSymbolArt.CoinColor,
            };
            colors.arraySize = symbolColors.Length;
            for (var i = 0; i < symbolColors.Length; i++)
            {
                colors.GetArrayElementAtIndex(i).colorValue = symbolColors[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            var scene = canvas.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] スロット台の外枠と LED {lights.Length} 個を置いた（T614、中身 {content.x}×{content.y}）。");
        }

        private static Image[] CreateLeds(RectTransform parent, Vector2 path)
        {
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var across = Mathf.Max(1, Mathf.RoundToInt(path.x * 2f / LedSpacing));
            var down = Mathf.Max(1, Mathf.RoundToInt(path.y * 2f / LedSpacing));
            var corners = new[]
            {
                new Vector2(-path.x, path.y), new Vector2(path.x, path.y),
                new Vector2(path.x, -path.y), new Vector2(-path.x, -path.y),
            };
            var lights = new Image[(across + down) * 2];
            var n = 0;
            for (var side = 0; side < 4; side++)
            {
                var count = side % 2 == 0 ? across : down;
                var from = corners[side];
                var to = corners[(side + 1) % 4];
                for (var k = 0; k < count; k++)
                {
                    var image = new GameObject($"LED {n}", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    image.gameObject.layer = parent.gameObject.layer;
                    var rect = image.rectTransform;
                    rect.SetParent(parent, false);
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(LedSize, LedSize);
                    rect.anchoredPosition = Vector2.Lerp(from, to, k / (float)count);
                    image.sprite = sprite;
                    image.color = new Color(0.32f, 0.16f, 0.12f);
                    image.raycastTarget = false;
                    lights[n++] = image;
                }
            }

            return lights;
        }

        // 中心から外側の半分の大きさ outerHalf、太さ thickness（横・縦）の枠を4本の帯で作る。
        private static void Ring(string name, RectTransform parent, Vector2 outerHalf, Vector2 thickness, Color color)
        {
            var group = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            group.gameObject.layer = parent.gameObject.layer;
            group.SetParent(parent, false);
            Stretch(group);
            var innerHalf = outerHalf - thickness;
            Strip(group, "Top", new Vector2(-outerHalf.x, innerHalf.y), new Vector2(outerHalf.x, outerHalf.y), color);
            Strip(group, "Bottom", new Vector2(-outerHalf.x, -outerHalf.y), new Vector2(outerHalf.x, -innerHalf.y), color);
            Strip(group, "Left", new Vector2(-outerHalf.x, -innerHalf.y), new Vector2(-innerHalf.x, innerHalf.y), color);
            Strip(group, "Right", new Vector2(innerHalf.x, -innerHalf.y), new Vector2(outerHalf.x, innerHalf.y), color);
        }

        private static void Strip(RectTransform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.gameObject.layer = parent.gameObject.layer;
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = max - min;
            rect.anchoredPosition = (min + max) * 0.5f;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static RectTransform FindAncestor(Transform from, string name)
        {
            for (var t = from; t != null; t = t.parent)
            {
                if (t.name == name)
                {
                    return t as RectTransform;
                }
            }

            return null;
        }
    }
}
