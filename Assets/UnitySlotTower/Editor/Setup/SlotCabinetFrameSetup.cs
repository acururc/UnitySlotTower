using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnitySlotTower.Core.Layout;
using UnitySlotTower.Layout;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// T614（FR-116・E-29）: 開いている Game シーンの画面全体をスロット台の 3D モデルで囲む。3:4 の中身（Portrait Root）を
    /// <see cref="PortraitLayout.ContentScale"/> 倍に縮めて左右の中央・下寄せに置き（文字の大きさは変えない）、空いた周り（上のアーチ・
    /// 左右の柱・下の縁）と、中身の中の透明にした筐体（ボタン台・リールの下パネル）の後ろに、箱と円柱で組んだ仮の台を置く
    /// （実機の台の形を参考にした場所どり。本番のモデルに差し替える）。台は戦闘のマップから離れた所に置き、専用の平行投影の
    /// カメラ（<see cref="PortraitScreenLayout"/> が 3:4 の領域に合わせる）で、戦闘カメラより先に描く。1 単位 = 基準解像度の 100 px。
    /// 台の光の帯とリール窓まわりの UI の LED は <see cref="SlotCabinetLights"/> が光らせる。何度実行しても同じ結果になる。
    /// </summary>
    internal static class SlotCabinetFrameSetup
    {
        private const string ModelName = "Cabinet Model";
        private const string OldFrameName = "Cabinet Frame";
        private const string WindowLightsName = "Window Lights";
        private const string ButtonDeckName = "Button Deck";
        private const string RootName = "Portrait Root";
        private const string MaterialFolder = "Assets/UnitySlotTower/Art/Materials/Cabinet";
        private const float WindowMargin = 15f;
        private const float WindowLedSize = 15f;
        private const float WindowLedSpacing = 25f;
        private const float WindowGlowSize = 42f;
        private const float StripSegment = 40f;
        private const float StripWidth = 16f;

        // 台は戦闘のマップ（原点付近）から遠く離し、台のカメラの奥行きも短くして互いに写り込まないようにする。
        private static readonly Vector3 ModelOrigin = new Vector3(0f, 0f, -1000f);

        private static readonly Color LedOffColor = new Color(0.05f, 0.1f, 0.2f);
        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        private static Material _body;
        private static Material _gloss;
        private static Material _chrome;
        private static Material _gunmetal;
        private static Material _grille;
        private static Material _light;

        [MenuItem("Tools/UnitySlotTower/Setup/Build Cabinet Frame (T614)")]
        private static void Build()
        {
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var root = cabinet != null ? FindAncestor(cabinet.transform, RootName) : null;
            var canvas = root != null ? root.parent as RectTransform : null;
            var window = cabinet != null ? cabinet.transform.Find("Reel Window") as RectTransform : null;
            var layout = Object.FindAnyObjectByType<PortraitScreenLayout>(FindObjectsInactive.Include);
            if (canvas == null || canvas.GetComponent<Canvas>() == null || window == null || layout == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの筐体・Reel Window・Portrait Root・UI Canvas・PortraitScreenLayout が見つからない。");
                return;
            }

            var full = new Vector2(PortraitLayout.ReferenceWidth, PortraitLayout.ReferenceHeight);
            var content = full * (float)PortraitLayout.ContentScale;
            var bottomBand = full.y * (float)PortraitLayout.ContentBottomRatio;
            var archBand = full.y - content.y - bottomBand;
            var center = new Vector2(0f, (bottomBand - archBand) * 0.5f); // 中身の中心（画面の中心から）
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = center;
            root.sizeDelta = content;

            // 旧 UI の外枠（板・ネオン・LED）とボタン台の板は 3D の台に置き換える。筐体の背景は透明にして後ろの台を見せる。
            DestroyChild(canvas, OldFrameName);
            var slotArea = (RectTransform)window.parent;
            DestroyChild(slotArea, ButtonDeckName);
            DestroyChild(slotArea, WindowLightsName);
            var slotImage = slotArea.GetComponent<Image>();
            if (slotImage != null)
            {
                slotImage.color = Clear;
            }

            var old = GameObject.Find(ModelName);
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            LoadMaterials();
            var model = new GameObject(ModelName).transform;
            model.position = ModelOrigin;
            var camera = CreateCamera(model, layout);
            var strips = new List<Renderer>();
            BuildCabinet(model, content, center, slotArea, window, strips);
            var (windowLights, windowGlows) = BuildWindowLights(window);

            var lights = model.gameObject.AddComponent<SlotCabinetLights>();
            var serialized = new SerializedObject(lights);
            serialized.FindProperty("cabinet").objectReferenceValue = cabinet;
            serialized.FindProperty("cabinetCamera").objectReferenceValue = camera;
            serialized.FindProperty("waveOrigin").objectReferenceValue = window;
            SetArray(serialized.FindProperty("strips"), strips.ToArray());
            SetArray(serialized.FindProperty("windowLights"), windowLights);
            SetArray(serialized.FindProperty("windowGlows"), windowGlows);
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
            Debug.Log($"[SlotTower Setup] スロット台の仮モデル（光の帯 {strips.Count} 個）と窓の LED {windowLights.Length} 個を置いた" +
                      $"（T614、中身 {content.x}×{content.y}、カメラの深さ {camera.depth}）。");
        }

        private static Camera CreateCamera(Transform model, PortraitScreenLayout layout)
        {
            var layoutSerialized = new SerializedObject(layout);
            var battle = (Camera)layoutSerialized.FindProperty("battleCamera").objectReferenceValue;

            var go = new GameObject("Cabinet Camera", typeof(Camera));
            go.transform.SetParent(model, false);
            go.transform.localPosition = new Vector3(0f, 0f, -30f);
            go.transform.localRotation = Quaternion.identity;
            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = PortraitLayout.ReferenceHeight / 200f;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.depth = battle != null ? battle.depth - 0.5f : -0.5f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            layoutSerialized.FindProperty("cabinetCamera").objectReferenceValue = camera;
            layoutSerialized.ApplyModifiedPropertiesWithoutUndo();
            var box = PortraitLayout.Letterbox(Screen.width, Screen.height);
            camera.rect = new Rect((float)box.X, (float)box.Y, (float)box.Width, (float)box.Height);
            return camera;
        }

        // 実機の台（黒い本体・上の銀のアーチと V 字の飾り・左右の柱の青い光の帯・銀のボタン台・リールを囲む角ばった光の枠）を
        // 参考にした仮の形。座標は画面の中心を原点にした基準解像度の px（上が +y）、z は手前がマイナス。
        private static void BuildCabinet(Transform model, Vector2 content, Vector2 center, RectTransform slotArea, RectTransform window,
            List<Renderer> strips)
        {
            const float w = 720f;
            const float h = 960f;
            var left = -content.x * 0.5f;
            var right = content.x * 0.5f;
            var top = center.y + content.y * 0.5f;
            var bottom = center.y - content.y * 0.5f;
            var scale = (float)PortraitLayout.ContentScale;
            var slotHeight = PortraitLayout.ReferenceHeight * PortraitLayout.SlotAreaTop; // 筐体の中の px（縮める前）
            float SlotY(float y) => bottom + y * scale;
            var deckBottom = SlotY(526f);
            var deckTop = SlotY(714f);
            var lcdBottom = SlotY(slotHeight);

            var windowMin = window.anchorMin;
            var windowMax = window.anchorMax;
            var winL = left + windowMin.x * content.x;
            var winR = left + windowMax.x * content.x;
            var winB = SlotY(windowMin.y * slotHeight);
            var winT = SlotY(windowMax.y * slotHeight);

            var body = Group(model, "Body");
            Box(body, "Back", new Vector2(-w, -h), new Vector2(w, h), 0.6f, 0.5f, _body);
            Box(body, "Pillar Left", new Vector2(-w, -h + 40f), new Vector2(left, top), -0.3f, 0.9f, _gloss);
            Box(body, "Pillar Right", new Vector2(right, -h + 40f), new Vector2(w, top), -0.3f, 0.9f, _gloss);
            Box(body, "Pillar Edge Left", new Vector2(left - 14f, bottom), new Vector2(left - 4f, top), -0.8f, 0.2f, _chrome);
            Box(body, "Pillar Edge Right", new Vector2(right + 4f, bottom), new Vector2(right + 14f, top), -0.8f, 0.2f, _chrome);

            // 上のアーチ: 銀の帯の上に黒い冠（ロゴの場所）、左右から真ん中へ下がる V 字の銀の飾り、その下に青い光の帯。
            var arch = Group(model, "Arch");
            Box(arch, "Arch Base", new Vector2(-w, top), new Vector2(w, h), -0.4f, 1.2f, _chrome);
            Box(arch, "Crown", new Vector2(-380f, top + 20f), new Vector2(380f, h - 22f), -1.1f, 0.4f, _gloss);
            Cylinder(arch, "Crown Dome", new Vector2(0f, h - 30f), new Vector2(820f, 120f), -1.2f, _gloss);
            Bar(arch, "V Left", new Vector2(-w + 10f, h - 20f), new Vector2(-210f, top + 18f), 34f, -1.3f, 0.3f, _chrome);
            Bar(arch, "V Right", new Vector2(w - 10f, h - 20f), new Vector2(210f, top + 18f), 34f, -1.3f, 0.3f, _chrome);
            Speaker(arch, "Speaker Left", new Vector2(-520f, top + 90f));
            Speaker(arch, "Speaker Right", new Vector2(520f, top + 90f));

            var lights = Group(model, "Light Strips");
            Strip(lights, strips, new Vector2(-w + 30f, h - 60f), new Vector2(-230f, top + 4f), -1.4f);
            Strip(lights, strips, new Vector2(w - 30f, h - 60f), new Vector2(230f, top + 4f), -1.4f);
            Strip(lights, strips, new Vector2(-230f, top + 4f), new Vector2(230f, top + 4f), -1.4f);

            // 柱: 外側に縦の光の帯（上でアーチへ向かって内に曲がる）、下の段（リールの下パネル）の横は角ばった光の枠。
            foreach (var side in new[] { -1f, 1f })
            {
                Strip(lights, strips, new Vector2(side * (w - 22f), deckTop), new Vector2(side * (w - 22f), top - 60f), -0.8f);
                Strip(lights, strips, new Vector2(side * (w - 22f), top - 60f), new Vector2(side * (w - 60f), top + 30f), -0.8f);
                Strip(lights, strips, new Vector2(side * (w - 20f), deckBottom - 10f), new Vector2(side * (w - 56f), deckBottom - 80f), -0.8f);
                Strip(lights, strips, new Vector2(side * (w - 56f), deckBottom - 80f), new Vector2(side * (w - 56f), bottom + 70f), -0.8f);
                Strip(lights, strips, new Vector2(side * (w - 56f), bottom + 70f), new Vector2(side * (w - 20f), bottom + 10f), -0.8f);
            }

            // 液晶のふち（戦闘画面と札の外側、柱の内側の銀の縁の上端と下端）。
            var lcd = Group(model, "LCD Bezel");
            Box(lcd, "Top", new Vector2(left - 14f, top), new Vector2(right + 14f, top + 12f), -1.0f, 0.2f, _gunmetal);
            Box(lcd, "Bottom", new Vector2(left - 14f, lcdBottom - 6f), new Vector2(right + 14f, lcdBottom + 4f), -1.0f, 0.2f, _gunmetal);

            // ボタン台: 手前に張り出した銀の台（レバー・コインの UI がこの上に載る）と前の縁。
            var deck = Group(model, "Button Deck");
            Box(deck, "Deck", new Vector2(left - 30f, deckBottom), new Vector2(right + 30f, deckTop), -1.6f, 1.3f, _chrome);
            Box(deck, "Deck Face", new Vector2(left - 10f, deckBottom + 16f), new Vector2(right + 10f, deckTop - 14f), -2.0f, 0.1f, _gunmetal);
            Box(deck, "Deck Lip", new Vector2(left - 40f, deckBottom - 18f), new Vector2(right + 40f, deckBottom), -1.8f, 1.0f, _gloss);

            // リールの下パネル: 黒い板にリール窓の銀のふちと、左右の角ばった光の枠。
            var lower = Group(model, "Lower Panel");
            Box(lower, "Panel", new Vector2(left, bottom), new Vector2(right, deckBottom - 18f), -0.5f, 0.3f, _gloss);
            const float bezel = 32f;
            const float rim = 18f;
            Box(lower, "Bezel Top", new Vector2(winL - bezel - rim, winT + bezel), new Vector2(winR + bezel + rim, winT + bezel + rim), -0.9f, 0.3f, _chrome);
            Box(lower, "Bezel Bottom", new Vector2(winL - bezel - rim, winB - bezel - rim), new Vector2(winR + bezel + rim, winB - bezel), -0.9f, 0.3f, _chrome);
            Box(lower, "Bezel Left", new Vector2(winL - bezel - rim, winB - bezel), new Vector2(winL - bezel, winT + bezel), -0.9f, 0.3f, _chrome);
            Box(lower, "Bezel Right", new Vector2(winR + bezel, winB - bezel), new Vector2(winR + bezel + rim, winT + bezel), -0.9f, 0.3f, _chrome);
            foreach (var side in new[] { -1f, 1f })
            {
                var inner = side < 0f ? winL - bezel - rim - 40f : winR + bezel + rim + 40f;
                var outer = inner + side * 70f;
                Strip(lights, strips, new Vector2(outer, winT + 20f), new Vector2(inner, winT - 40f), -0.9f);
                Strip(lights, strips, new Vector2(inner, winT - 40f), new Vector2(inner, winB + 40f), -0.9f);
                Strip(lights, strips, new Vector2(inner, winB + 40f), new Vector2(outer, winB - 20f), -0.9f);
            }

            // 下の縁と足（スピーカーの網）。
            var foot = Group(model, "Foot");
            Box(foot, "Base", new Vector2(-w, -h), new Vector2(w, bottom), -0.4f, 0.8f, _gloss);
            Box(foot, "Grille", new Vector2(-300f, -h + 8f), new Vector2(300f, bottom - 8f), -0.9f, 0.2f, _grille);
        }

        private static void Speaker(Transform parent, string name, Vector2 at)
        {
            Cylinder(parent, name, at, new Vector2(120f, 120f), -1.3f, _grille);
        }

        // 光の帯を StripSegment ごとの短い部品に分けて並べる（波が帯を伝わって見えるように）。
        private static void Strip(Transform parent, List<Renderer> strips, Vector2 from, Vector2 to, float z)
        {
            var count = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(from, to) / StripSegment));
            for (var k = 0; k < count; k++)
            {
                var a = Vector2.Lerp(from, to, k / (float)count);
                var b = Vector2.Lerp(from, to, (k + 1) / (float)count);
                strips.Add(Bar(parent, $"Strip {strips.Count}", a, b, StripWidth, z, 0.1f, _light));
            }
        }

        // from から to への太さ thickness の棒（z は手前の面、depth は奥行き。いずれも単位）。
        private static Renderer Bar(Transform parent, string name, Vector2 from, Vector2 to, float thickness, float z, float depth, Material material)
        {
            var go = Primitive(PrimitiveType.Cube, parent, name, material);
            var dir = to - from;
            go.transform.localPosition = new Vector3((from.x + to.x) * 0.005f, (from.y + to.y) * 0.005f, z + depth * 0.5f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            go.transform.localScale = new Vector3(dir.magnitude / 100f + thickness / 100f, thickness / 100f, depth);
            return go.GetComponent<Renderer>();
        }

        private static Renderer Box(Transform parent, string name, Vector2 min, Vector2 max, float z, float depth, Material material)
        {
            var go = Primitive(PrimitiveType.Cube, parent, name, material);
            go.transform.localPosition = new Vector3((min.x + max.x) * 0.005f, (min.y + max.y) * 0.005f, z + depth * 0.5f);
            go.transform.localScale = new Vector3((max.x - min.x) / 100f, (max.y - min.y) / 100f, depth);
            return go.GetComponent<Renderer>();
        }

        // 正面を向けた円柱（size は横・縦の px、楕円にもなる）。
        private static void Cylinder(Transform parent, string name, Vector2 at, Vector2 size, float z, Material material)
        {
            var go = Primitive(PrimitiveType.Cylinder, parent, name, material);
            go.transform.localPosition = new Vector3(at.x / 100f, at.y / 100f, z + 0.15f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size.x / 100f, 0.15f, size.y / 100f);
        }

        private static GameObject Primitive(PrimitiveType type, Transform parent, string name, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // 戦闘のクリック判定に関わらせない
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name).transform;
            go.SetParent(parent, false);
            return go;
        }

        private static void LoadMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/UnitySlotTower/Art/Materials", "Cabinet");
            }

            _body = LitMaterial("Cabinet Body", new Color(0.03f, 0.03f, 0.04f), 0f, 0.3f);
            _gloss = LitMaterial("Cabinet Gloss", new Color(0.06f, 0.06f, 0.08f), 0.2f, 0.85f);
            _chrome = LitMaterial("Cabinet Chrome", new Color(0.78f, 0.8f, 0.84f), 0.9f, 0.8f);
            _gunmetal = LitMaterial("Cabinet Gunmetal", new Color(0.3f, 0.32f, 0.36f), 0.8f, 0.7f);
            _grille = LitMaterial("Cabinet Grille", new Color(0.12f, 0.12f, 0.13f), 0.3f, 0.4f);
            _light = UnlitMaterial("Cabinet Light", new Color(0.25f, 0.6f, 1f));
            AssetDatabase.SaveAssets();
        }

        private static Material LitMaterial(string name, Color color, float metallic, float smoothness)
        {
            var material = LoadOrCreate(name, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material UnlitMaterial(string name, Color color)
        {
            var material = LoadOrCreate(name, "Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreate(string name, string shader)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
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
            var glowGroup = UiGroup("Glows", group);
            var ledGroup = UiGroup("LEDs", group);
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var across = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(corners[0], corners[1]) / WindowLedSpacing));
            var down = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(corners[1], corners[2]) / WindowLedSpacing));
            var total = (across + down) * 2;
            var lights = new Image[total];
            var glows = new Image[total];
            var n = 0;
            for (var side = 0; side < 4; side++)
            {
                var count = side % 2 == 0 ? across : down;
                for (var k = 0; k < count; k++)
                {
                    var at = Vector2.Lerp(corners[side], corners[(side + 1) % 4], k / (float)count);
                    glows[n] = Dot(glowGroup, $"Glow {n}", at, WindowGlowSize, null, Clear);
                    lights[n] = Dot(ledGroup, $"LED {n}", at, WindowLedSize, sprite, LedOffColor);
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

        private static RectTransform UiGroup(string name, RectTransform parent)
        {
            var group = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            group.gameObject.layer = parent.gameObject.layer;
            group.SetParent(parent, false);
            group.anchorMin = Vector2.zero;
            group.anchorMax = Vector2.one;
            group.offsetMin = group.offsetMax = Vector2.zero;
            return group;
        }

        private static void SetArray(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void DestroyChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
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
