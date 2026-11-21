using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Layout;
using UnitySlotTower.Flow;
using UnitySlotTower.Layout;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Title シーン（T401、plan 4章）を作る一回限りの手順。ロゴ（文字はここだけ可: FR-003）と ▶、設定1〜6の筐体プレートと ◀ を置き、
    /// ビルドの先頭に登録する。既存のシーンは上書きしない。以後の変更は Editor でシーンを直接編集する。
    /// </summary>
    internal static class TitleSceneSetup
    {
        public const string ScenePath = "Assets/UnitySlotTower/Scenes/Title.unity";
        private const string NumericFontPath = "Assets/UnitySlotTower/Art/Fonts/NumericOnly SDF.asset";
        private const string LogoFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private static Sprite _box;

        [MenuItem("Tools/UnitySlotTower/Setup/Create Title Scene")]
        private static void CreateTitleScene()
        {
            if (File.Exists(ScenePath))
            {
                Debug.LogWarning($"[SlotTower Setup] {ScenePath} は既にあるため作成しない。");
                return;
            }

            // 自動化から呼ぶため保存ダイアログは出さず、未保存の変更があれば中止する。
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("[SlotTower Setup] 未保存のシーンがあるため中止した。保存してから再実行する。");
                    return;
                }
            }

            var numericFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NumericFontPath);
            var logoFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LogoFontPath);
            if (numericFont == null || logoFont == null)
            {
                Debug.LogError("[SlotTower Setup] フォントが見つからない。");
                return;
            }

            _box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Background Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.gameObject.tag = "MainCamera";
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;

            var scaler = CreateCanvas(out var root);
            var backdrop = CreateImage("Backdrop", root, Vector2.zero, Vector2.one, new Color(0.1f, 0.08f, 0.14f));
            backdrop.raycastTarget = true;

            // タイトル: ロゴと ▶。
            var title = CreateRect("Title Panel", root, Vector2.zero, Vector2.one);
            var logo = CreateRect("Logo", title, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.82f))
                .gameObject.AddComponent<TextMeshProUGUI>();
            logo.font = logoFont;
            logo.text = "SLOT TOWER";
            logo.color = new Color(1f, 0.85f, 0.3f);
            logo.fontStyle = FontStyles.Bold;
            logo.enableAutoSizing = true;
            logo.fontSizeMin = 10f;
            logo.fontSizeMax = 300f;
            logo.alignment = TextAlignmentOptions.Center;
            logo.raycastTarget = false;

            var start = CreateButton("Start Button", title, new Vector2(0.35f, 0.3f), new Vector2(0.65f, 0.42f),
                new Color(0.25f, 0.6f, 0.3f));
            CreateRect("Play", start.GetComponent<RectTransform>(), new Vector2(0.4f, 0.2f), new Vector2(0.64f, 0.8f))
                .gameObject.AddComponent<TriangleGraphic>().raycastTarget = false;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<EventSystem>().firstSelectedGameObject = start.gameObject;

            var layout = new GameObject("Portrait Screen Layout").AddComponent<PortraitScreenLayout>();
            Assign(layout, "canvasScaler", scaler);

            var screen = new GameObject("Title Screen").AddComponent<TitleScreen>();
            Assign(screen, "startButton", start);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildFirst();
            AssetDatabase.SaveAssets();
            Debug.Log($"[SlotTower Setup] {ScenePath} を作成した。");
        }

        private static CanvasScaler CreateCanvas(out RectTransform root)
        {
            var canvasObject = new GameObject("UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(PortraitLayout.ReferenceWidth, PortraitLayout.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            root = new GameObject("Portrait Root", typeof(RectTransform)).GetComponent<RectTransform>();
            root.gameObject.layer = canvasObject.layer;
            root.SetParent(canvasObject.transform, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(PortraitLayout.ReferenceWidth, PortraitLayout.ReferenceHeight);
            return scaler;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image CreateImage(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var image = CreateRect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Button CreateButton(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var image = CreateImage(name, parent, anchorMin, anchorMax, color);
            image.sprite = _box;
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            // キーボードで選んでいるボタンが分かるように、選んでいないボタンを暗くする。
            var colors = button.colors;
            colors.normalColor = new Color(0.65f, 0.65f, 0.65f);
            colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            return button;
        }

        private static void Assign(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildFirst()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path != ScenePath)
                {
                    scenes.Add(existing);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
