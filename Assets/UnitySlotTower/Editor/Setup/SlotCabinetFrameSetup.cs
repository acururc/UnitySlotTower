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
    /// <see cref="PortraitLayout.ContentScale"/> 倍の大きさに縮め（文字の大きさは変えない）、空いた周りに外枠・ネオン管・
    /// 角の大きなランプ・LED の列とそのにじみを置き、リール窓のまわりにも LED の列を置く（光らせるのは
    /// <see cref="SlotCabinetLights"/>）。戦闘カメラは <see cref="PortraitLayout.BattleView"/> が同じ割合で縮める。
    /// 外枠は UI Canvas の中で Portrait Root のすぐ後（上）に、窓の LED はリール窓のすぐ前（下）に、それぞれ自分の Canvas を
    /// 持たせて置く（LED の色の更新をそこだけに閉じる）。見た目は仮置き（本番は M5）。何度実行しても同じ結果になる。
    /// </summary>
    internal static class SlotCabinetFrameSetup
    {
        private const string FrameName = "Cabinet Frame";
        private const string WindowLightsName = "Window Lights";
        private const string RootName = "Portrait Root";
        private const float LedSize = 26f;
        private const float LedSpacing = 31f;
        private const float GlowSize = 70f;
        private const float JewelSize = 46f;
        private const float JewelGlowSize = 130f;
        private const float OuterLine = 6f;
        private const float InnerTrim = 5f;
        private const float WindowMargin = 15f;
        private const float WindowLedSize = 15f;
        private const float WindowLedSpacing = 25f;
        private const float WindowGlowSize = 42f;

        private static readonly Color BodyColor = new Color(0.16f, 0.03f, 0.08f);
        private static readonly Color TrimColor = new Color(0.95f, 0.75f, 0.3f);
        private static readonly Color TrimShadeColor = new Color(0.5f, 0.32f, 0.08f);
        private static readonly Color LedOffColor = new Color(0.28f, 0.12f, 0.12f);
        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        [MenuItem("Tools/UnitySlotTower/Setup/Build Cabinet Frame (T614)")]
        private static void Build()
        {
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var root = cabinet != null ? FindAncestor(cabinet.transform, RootName) : null;
            var canvas = root != null ? root.parent as RectTransform : null;
            var window = cabinet != null ? cabinet.transform.Find("Reel Window") as RectTransform : null;
            if (canvas == null || canvas.GetComponent<Canvas>() == null || window == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの筐体・Reel Window・Portrait Root・UI Canvas が見つからない。");
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

            var oldWindow = window.parent.Find(WindowLightsName);
            if (oldWindow != null)
            {
                Object.DestroyImmediate(oldWindow.gameObject);
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
            var neon = Ring("Neon", frame, half, new Vector2(OuterLine, OuterLine), TrimColor);
            Ring("Trim Shade", frame, inner + new Vector2(InnerTrim + 2f, InnerTrim + 2f), new Vector2(2f, 2f), TrimShadeColor);
            Ring("Trim", frame, inner + new Vector2(InnerTrim, InnerTrim), new Vector2(InnerTrim, InnerTrim), TrimColor);

            // LED は外側の線と内側の縁の間の帯の真ん中に、左上の角から時計回りに並べる（角は大きなランプ）。
            var path = (half - new Vector2(OuterLine, OuterLine) + inner + new Vector2(InnerTrim, InnerTrim)) * 0.5f;
            var glowGroup = Group("Glows", frame);
            var ledGroup = Group("LEDs", frame);
            var corners = new[]
            {
                new Vector2(-path.x, path.y), new Vector2(path.x, path.y),
                new Vector2(path.x, -path.y), new Vector2(-path.x, -path.y),
            };
            var (lights, glows) = CreateRing(ledGroup, glowGroup, corners, LedSpacing, LedSize, GlowSize, true);

            var jewelGroup = Group("Corner Lamps", frame);
            var sprite = Knob();
            var accents = new Graphic[neon.Length + corners.Length];
            var accentGlows = new Image[corners.Length];
            neon.CopyTo(accents, 0);
            for (var c = 0; c < corners.Length; c++)
            {
                accentGlows[c] = Dot(glowGroup, $"Corner Glow {c}", corners[c], JewelGlowSize, null, Clear);
                accents[neon.Length + c] = Dot(jewelGroup, $"Corner Lamp {c}", corners[c], JewelSize, sprite, TrimColor);
            }

            var (windowLights, windowGlows) = BuildWindowLights(window);

            var component = frame.gameObject.AddComponent<SlotCabinetLights>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("cabinet").objectReferenceValue = cabinet;
            serialized.FindProperty("waveOrigin").objectReferenceValue = window;
            SetArray(serialized.FindProperty("lights"), lights);
            SetArray(serialized.FindProperty("glows"), glows);
            SetArray(serialized.FindProperty("windowLights"), windowLights);
            SetArray(serialized.FindProperty("windowGlows"), windowGlows);
            SetArray(serialized.FindProperty("accents"), accents);
            SetArray(serialized.FindProperty("accentGlows"), accentGlows);

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
            Debug.Log($"[SlotTower Setup] スロット台の外枠と LED {lights.Length} 個・窓の LED {windowLights.Length} 個を置いた" +
                      $"（T614、中身 {content.x}×{content.y}）。");
        }

        // リール窓を WindowMargin だけ広げた枠に LED を並べる。窓のすぐ前（下）に置くので、にじみは窓の下に隠れる。
        private static (Image[] lights, Image[] glows) BuildWindowLights(RectTransform window)
        {
            Canvas.ForceUpdateCanvases();
            var size = window.rect.size;
            var group = new GameObject(WindowLightsName, typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
            group.gameObject.layer = window.gameObject.layer;
            group.SetParent(window.parent, false);
            group.SetSiblingIndex(window.GetSiblingIndex());
            group.anchorMin = window.anchorMin;
            group.anchorMax = window.anchorMax;
            group.pivot = window.pivot;
            group.offsetMin = window.offsetMin - new Vector2(WindowMargin, WindowMargin);
            group.offsetMax = window.offsetMax + new Vector2(WindowMargin, WindowMargin);

            var half = size * 0.5f + new Vector2(WindowMargin, WindowMargin);
            var corners = new[]
            {
                new Vector2(-half.x, half.y), new Vector2(half.x, half.y),
                new Vector2(half.x, -half.y), new Vector2(-half.x, -half.y),
            };
            var glowGroup = Group("Glows", group);
            var ledGroup = Group("LEDs", group);
            return CreateRing(ledGroup, glowGroup, corners, WindowLedSpacing, WindowLedSize, WindowGlowSize, false);
        }

        // 4つの角（左上から時計回り）を結ぶ線上に LED を並べる。skipCorners なら角は空ける（大きなランプを置く）。
        private static (Image[] lights, Image[] glows) CreateRing(RectTransform ledParent, RectTransform glowParent,
            Vector2[] corners, float spacing, float ledSize, float glowSize, bool skipCorners)
        {
            var sprite = Knob();
            var across = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(corners[0], corners[1]) / spacing));
            var down = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(corners[1], corners[2]) / spacing));
            var first = skipCorners ? 1 : 0;
            var total = (across + down - first * 2) * 2;
            var lights = new Image[total];
            var glows = new Image[total];
            var n = 0;
            for (var side = 0; side < 4; side++)
            {
                var count = side % 2 == 0 ? across : down;
                var from = corners[side];
                var to = corners[(side + 1) % 4];
                for (var k = first; k < count; k++)
                {
                    var at = Vector2.Lerp(from, to, k / (float)count);
                    glows[n] = Dot(glowParent, $"Glow {n}", at, glowSize, null, Clear);
                    lights[n] = Dot(ledParent, $"LED {n}", at, ledSize, sprite, LedOffColor);
                    n++;
                }
            }

            return (lights, glows);
        }

        private static Image Dot(RectTransform parent, string name, Vector2 at, float size, Sprite sprite, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.gameObject.layer = parent.gameObject.layer;
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = at;
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite Knob() => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        private static void SetArray(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static RectTransform Group(string name, RectTransform parent)
        {
            var group = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            group.gameObject.layer = parent.gameObject.layer;
            group.SetParent(parent, false);
            Stretch(group);
            return group;
        }

        // 中心から外側の半分の大きさ outerHalf、太さ thickness（横・縦）の枠を4本の帯で作る。
        private static Graphic[] Ring(string name, RectTransform parent, Vector2 outerHalf, Vector2 thickness, Color color)
        {
            var group = Group(name, parent);
            var innerHalf = outerHalf - thickness;
            return new Graphic[]
            {
                Strip(group, "Top", new Vector2(-outerHalf.x, innerHalf.y), new Vector2(outerHalf.x, outerHalf.y), color),
                Strip(group, "Bottom", new Vector2(-outerHalf.x, -outerHalf.y), new Vector2(outerHalf.x, -innerHalf.y), color),
                Strip(group, "Left", new Vector2(-outerHalf.x, -innerHalf.y), new Vector2(-innerHalf.x, innerHalf.y), color),
                Strip(group, "Right", new Vector2(innerHalf.x, -innerHalf.y), new Vector2(outerHalf.x, innerHalf.y), color),
            };
        }

        private static Image Strip(RectTransform parent, string name, Vector2 min, Vector2 max, Color color)
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
            return image;
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
