using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Game シーンに文字なし説明（T403）を組む一回限りの手順。Portrait Root のリザルトのすぐ下（カスタム画面より前面）に重ね、
    /// 操作バー・筐体・カスタム画面・フィールドに結ぶ。既にあれば何もしない。以後の変更は Editor でシーンを直接編集する。
    /// </summary>
    internal static class GuideOverlaySetup
    {
        private const string RootName = "Guide";
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color FrameColor = new Color(1f, 0.85f, 0.2f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        [MenuItem("Tools/UnitySlotTower/Setup/Build Guide Overlay (T403)")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<GuideOverlay>(FindObjectsInactive.Include) != null)
            {
                Debug.LogWarning("[SlotTower Setup] 文字なし説明は既にあるため作成しない。");
                return;
            }

            var portrait = GameObject.Find("Portrait Root")?.GetComponent<RectTransform>();
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var input = Object.FindAnyObjectByType<UnitPlacementInput>(FindObjectsInactive.Include);
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var custom = Object.FindAnyObjectByType<ReelCustomScreen>(FindObjectsInactive.Include);
            var field = Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include);
            var status = Object.FindAnyObjectByType<OperationBarStatus>(FindObjectsInactive.Include);
            if (portrait == null || battle == null || input == null || cabinet == null || custom == null || field == null
                || status == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの Portrait Root・戦闘・操作バー・筐体・カスタム画面のどれかが見つからない。");
                return;
            }

            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // 画面いっぱい。カスタム画面より前、リザルトより後ろ。
            var host = CreateRect(RootName, portrait, Vector2.zero, Vector2.one);
            var result = Object.FindAnyObjectByType<ResultScreen>(FindObjectsInactive.Include);
            if (result != null && result.transform.parent == portrait)
            {
                host.SetSiblingIndex(result.transform.GetSiblingIndex());
            }
            else
            {
                host.SetAsLastSibling();
            }

            var guide = host.gameObject.AddComponent<GuideOverlay>();
            var root = CreateRect("Overlay", host, Vector2.zero, Vector2.one);

            // 暗幕（上・下・左・右）。クリックを遮る。
            var dims = new RectTransform[4];
            string[] dimNames = { "Dim Top", "Dim Bottom", "Dim Left", "Dim Right" };
            for (var i = 0; i < dims.Length; i++)
            {
                var dim = CreateFloating(dimNames[i], root, Vector2.zero).gameObject.AddComponent<Image>();
                dim.color = DimColor;
                dim.raycastTarget = true;
                dims[i] = dim.rectTransform;
            }

            // 光る枠（4辺）。
            var frame = CreateFloating("Frame", root, Vector2.zero);
            var frameGroup = frame.gameObject.AddComponent<CanvasGroup>();
            frameGroup.blocksRaycasts = false;
            frameGroup.interactable = false;
            CreateEdge("Top", frame, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -8f), Vector2.zero);
            CreateEdge("Bottom", frame, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 8f));
            CreateEdge("Left", frame, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(8f, 0f));
            CreateEdge("Right", frame, new Vector2(1f, 0f), Vector2.one, new Vector2(-8f, 0f), Vector2.zero);

            // 向き選びの矢印（右・上・左・下の順。回転は実行時に付ける）。
            var arrows = new RectTransform[4];
            for (var i = 0; i < arrows.Length; i++)
            {
                var arrow = CreateFloating($"Facing Arrow {i + 1}", root, Center);
                var graphic = arrow.gameObject.AddComponent<TriangleGraphic>();
                graphic.color = FrameColor;
                graphic.raycastTarget = false;
                arrow.gameObject.SetActive(false);
                arrows[i] = arrow;
            }

            // 押した所に広がる輪。
            var ripple = CreateFloating("Ripple", root, Center);
            var rippleGroup = ripple.gameObject.AddComponent<CanvasGroup>();
            rippleGroup.blocksRaycasts = false;
            rippleGroup.interactable = false;
            var rippleImage = ripple.gameObject.AddComponent<Image>();
            rippleImage.sprite = knob;
            rippleImage.color = new Color(1f, 1f, 1f, 0.8f);
            rippleImage.raycastTarget = false;

            // 手: 指先（pivot）を左上に向けた白い三角と、その縁取り。
            var hand = CreateFloating("Hand", root, Center);
            hand.sizeDelta = Vector2.zero;
            CreatePointer("Outline", hand, new Vector2(104f, 76f), new Color(0.1f, 0.1f, 0.12f));
            CreatePointer("Tip", hand, new Vector2(88f, 60f), Color.white);

            var serialized = new SerializedObject(guide);
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("placementInput").objectReferenceValue = input;
            serialized.FindProperty("slotCabinet").objectReferenceValue = cabinet;
            serialized.FindProperty("reelCustomScreen").objectReferenceValue = custom;
            serialized.FindProperty("fieldView").objectReferenceValue = field;
            serialized.FindProperty("battleCamera").objectReferenceValue =
                new SerializedObject(input).FindProperty("battleCamera").objectReferenceValue;
            SetSlotTargets(serialized.FindProperty("unitIcons"), Object.FindObjectsByType<UnitBarIcon>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            SetSlotTargets(serialized.FindProperty("upgradeIcons"),
                Object.FindObjectsByType<UnitUpgradeIcon>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            var retreat = (Component)new SerializedObject(status).FindProperty("retreatButton").objectReferenceValue;
            serialized.FindProperty("retreatButton").objectReferenceValue = retreat.transform;
            serialized.FindProperty("root").objectReferenceValue = root;
            SetArray(serialized.FindProperty("dims"), dims);
            serialized.FindProperty("frame").objectReferenceValue = frame;
            serialized.FindProperty("frameGroup").objectReferenceValue = frameGroup;
            serialized.FindProperty("hand").objectReferenceValue = hand;
            serialized.FindProperty("ripple").objectReferenceValue = ripple;
            serialized.FindProperty("rippleGroup").objectReferenceValue = rippleGroup;
            SetArray(serialized.FindProperty("facingArrows"), arrows);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 実行時は説明があるときだけ開く。
            root.gameObject.SetActive(false);
            var scene = portrait.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SlotTower Setup] 文字なし説明を作成した。");
        }

        // 各アイコンの slot（units.csv の並び）の位置に入れる。
        private static void SetSlotTargets(SerializedProperty property, MonoBehaviour[] icons)
        {
            property.arraySize = 4;
            foreach (var icon in icons)
            {
                var slot = new SerializedObject(icon).FindProperty("slot").intValue;
                if (slot >= 0 && slot < property.arraySize)
                {
                    property.GetArrayElementAtIndex(slot).objectReferenceValue = icon.transform;
                }
            }
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void CreatePointer(string name, RectTransform hand, Vector2 size, Color color)
        {
            // ▶ の先（右の中央）を pivot にして 135 度回すと、先が左上を向いて手の位置を指す。
            var rect = CreateRect(name, hand, Center, Center);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = name == "Outline" ? new Vector2(-6f, 6f) : Vector2.zero;
            rect.localEulerAngles = new Vector3(0f, 0f, 135f);
            var graphic = rect.gameObject.AddComponent<TriangleGraphic>();
            graphic.color = color;
            graphic.raycastTarget = false;
        }

        private static void CreateEdge(string name, RectTransform frame, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = CreateRect(name, frame, anchorMin, anchorMax);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = FrameColor;
            image.raycastTarget = false;
        }

        // 親の中心に置き、位置と大きさを実行時に決める部品。
        private static RectTransform CreateFloating(string name, RectTransform parent, Vector2 pivot)
        {
            var rect = CreateRect(name, parent, Center, Center);
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(100f, 100f);
            return rect;
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
    }
}
