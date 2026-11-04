using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnitySlotTower.Core.Layout;
using UnitySlotTower.Layout;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// M0 の Game シーン骨組み（T006）と、縦長ウィンドウのプレイヤー設定を作る一回限りの手順。
    /// 既存のシーンは上書きしない。以後の変更は Editor でシーンを直接編集する。
    /// </summary>
    internal static class GameSceneSetup
    {
        public const string ScenePath = "Assets/UnitySlotTower/Scenes/Game.unity";
        private const string CatalogPath = "Assets/UnitySlotTower/Data/PresentationCatalog.asset";
        private const string MenuRoot = "Tools/UnitySlotTower/Setup/";

        [MenuItem(MenuRoot + "Create Game Scene")]
        private static void CreateGameScene()
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

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var background = CreateCamera("Background Camera", -100);
            background.cullingMask = 0;
            background.backgroundColor = Color.black;

            var battle = CreateCamera("Battle Camera", 0);
            battle.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
            battle.fieldOfView = 40f;
            battle.transform.SetPositionAndRotation(new Vector3(0f, 7.8f, -5.45f), Quaternion.Euler(55f, 0f, 0f));
            battle.gameObject.tag = "MainCamera";

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var field = GameObject.CreatePrimitive(PrimitiveType.Plane);
            field.name = "Battle Field (Graybox)";
            field.transform.localScale = new Vector3(0.9f, 1f, 0.6f);

            var scaler = CreateCanvas(out var root);
            CreatePanel("Operation Bar", root, new Vector2(0f, SlotAreaTop), new Vector2(1f, 1f - (float)PortraitLayout.BattleHeightRatio),
                new Color(0.12f, 0.12f, 0.14f));
            var slot = CreatePanel("Slot Area", root, Vector2.zero, new Vector2(1f, SlotAreaTop), new Color(0.35f, 0.08f, 0.1f));
            CreatePanel("Reel Window", slot, new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.9f), new Color(0.92f, 0.92f, 0.9f));

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var layout = new GameObject("Portrait Screen Layout").AddComponent<PortraitScreenLayout>();
            var serialized = new SerializedObject(layout);
            serialized.FindProperty("battleCamera").objectReferenceValue = battle;
            serialized.FindProperty("canvasScaler").objectReferenceValue = scaler;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild();
            CreateCatalog();
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[SlotTower Setup] {ScenePath} を作成した。");
        }

        /// <summary>
        /// 3:4 領域の 0〜SlotAreaTop がスロット筐体、SlotAreaTop〜(1 − 戦闘の割合) が操作バー（T317、plan 2章）。
        /// 操作バーは T308 と同じ高さ（基準 154 px）を保つ。
        /// </summary>
        private const float SlotAreaTop = 0.32f;

        /// <summary>
        /// 既にある Game シーンを戦闘 60%・下部 40% に詰め直す（T317）。操作バーは高さを保って移し、
        /// 筐体はリール窓の縦横比を保ったまま中央に置き、レバー・ボーナス表示を左、クレジット・払い出しを右下に寄せる。
        /// 何度実行しても同じ配置になる。
        /// </summary>
        [MenuItem(MenuRoot + "Apply 6:4 Layout (T317)")]
        private static void ApplySixFourLayout()
        {
            var bar = GameObject.Find("Operation Bar")?.GetComponent<RectTransform>();
            var slot = GameObject.Find("Slot Area")?.GetComponent<RectTransform>();
            var camera = GameObject.Find("Battle Camera")?.GetComponent<Camera>();
            if (bar == null || slot == null || camera == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの Operation Bar / Slot Area / Battle Camera が見つからない。");
                return;
            }

            SetAnchors(bar, new Vector2(0f, SlotAreaTop), new Vector2(1f, 1f - (float)PortraitLayout.BattleHeightRatio));
            SetAnchors(slot, Vector2.zero, new Vector2(1f, SlotAreaTop));

            // 戦闘の領域が 1440×1152 px（5:4）になり横の画角が狭まるため、9 列が左右に収まるよう引いて俯角を深くする。
            // 注視点はマップの中心（原点）のまま。
            camera.fieldOfView = 45f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 10.6f, -4.94f), Quaternion.Euler(65f, 0f, 0f));

            // 筐体の中は 1440×614 px。リール窓は T315 と同じ縦横比（約 2.03:1）で 842×414 px。
            var windowMin = new Vector2(0.208f, 0.293f);
            var windowMax = new Vector2(0.792f, 0.967f);
            var placed = new (string Name, Vector2 Min, Vector2 Max)[]
            {
                ("Reel Window", windowMin, windowMax),
                ("Stop Button 1", new Vector2(0.255f, 0.08f), new Vector2(0.371f, 0.25f)),
                ("Stop Button 2", new Vector2(0.442f, 0.08f), new Vector2(0.558f, 0.25f)),
                ("Stop Button 3", new Vector2(0.629f, 0.08f), new Vector2(0.745f, 0.25f)),
                ("Bonus Lamp", new Vector2(0.028f, 0.847f), new Vector2(0.181f, 0.966f)),
                ("Bonus Games", new Vector2(0.014f, 0.69f), new Vector2(0.175f, 0.825f)),
                ("Lever Button", new Vector2(0.03f, 0.065f), new Vector2(0.18f, 0.267f)),
                ("Coin Icon", new Vector2(0.76f, 0.261f), new Vector2(0.819f, 0.362f)),
                ("Credit", new Vector2(0.823f, 0.252f), new Vector2(0.986f, 0.371f)),
                ("Payout", new Vector2(0.823f, 0.098f), new Vector2(0.986f, 0.217f)),
            };
            foreach (var (name, min, max) in placed)
            {
                if (slot.Find(name) is not RectTransform rect)
                {
                    Debug.LogError($"[SlotTower Setup] Slot Area / {name} が見つからない。");
                    return;
                }

                SetAnchors(rect, min, max);
            }

            // 有効ラインの印は SlotCabinetSetup と同じ式で、リール窓の中段の左右に置く。
            var y = (windowMin.y + windowMax.y) / 2f;
            var half = (windowMax.y - windowMin.y) * 0.08f;
            SetAnchors((RectTransform)slot.Find("Payline Mark Left"), new Vector2(windowMin.x - 0.035f, y - half),
                new Vector2(windowMin.x - 0.005f, y + half));
            SetAnchors((RectTransform)slot.Find("Payline Mark Right"), new Vector2(windowMax.x + 0.005f, y - half),
                new Vector2(windowMax.x + 0.035f, y + half));

            EditorSceneManager.MarkSceneDirty(slot.gameObject.scene);
            EditorSceneManager.SaveScene(slot.gameObject.scene);
            Debug.Log("[SlotTower Setup] Game シーンを戦闘 60%・下部 40% の配置にした。");
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Camera CreateCamera(string name, int priority)
        {
            var camera = new GameObject(name).AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.depth = priority;
            var data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = false;
            return camera;
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

            // 基準解像度の 3:4 領域。CanvasScaler が短い辺に合わせるため、常に黒帯の内側に収まる。
            root = new GameObject("Portrait Root", typeof(RectTransform)).GetComponent<RectTransform>();
            root.gameObject.layer = canvasObject.layer;
            root.SetParent(canvasObject.transform, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(PortraitLayout.ReferenceWidth, PortraitLayout.ReferenceHeight);
            return scaler;
        }

        private static RectTransform CreatePanel(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax,
            Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.gameObject.layer = parent.gameObject.layer;
            panel.SetParent(parent, false);
            panel.anchorMin = anchorMin;
            panel.anchorMax = anchorMax;
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static void AddSceneToBuild()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path != ScenePath)
                {
                    // テンプレートの SampleScene などは残すがビルドからは外す。
                    scenes.Add(new EditorBuildSettingsScene(existing.path, false));
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void CreateCatalog()
        {
            if (AssetDatabase.LoadAssetAtPath<PresentationCatalog>(CatalogPath) == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PresentationCatalog>(), CatalogPath);
            }
        }

        [MenuItem(MenuRoot + "Apply Portrait Player Settings")]
        private static void ApplyPlayerSettings()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 1280;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            Debug.Log("[SlotTower Setup] プレイヤー設定を縦長 960×1280 ウィンドウにした。");
        }
    }
}
