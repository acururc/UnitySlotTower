using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Flow;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// T361（FR-019、plan 17.2）: 開いている Game シーンに開始の演出（<see cref="BattleIntroView"/>）を足し、
    /// 準備フェーズの廃止でいらなくなった操作バーの ▶（Start Button）とウェーブ n/2（Wave）を消す。何度実行しても同じ結果になる。
    /// </summary>
    internal static class BattleIntroSetup
    {
        private const string RootName = "Battle Intro";

        [MenuItem("Tools/UnitySlotTower/Setup/Build Battle Intro (T361)")]
        private static void Build()
        {
            var portrait = GameObject.Find("Portrait Root")?.GetComponent<RectTransform>();
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var field = Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include);
            var status = Object.FindAnyObjectByType<OperationBarStatus>(FindObjectsInactive.Include);
            var camera = GameObject.Find("Battle Camera")?.GetComponent<Camera>();
            if (portrait == null || battle == null || field == null || status == null || camera == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの Portrait Root・戦闘・フィールド・操作バー・Battle Camera のどれかが見つからない。");
                return;
            }

            var removed = 0;
            foreach (var name in new[] { "Start Button", "Wave" })
            {
                var child = status.transform.Find(name);
                if (child != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                    removed++;
                }
            }

            var old = portrait.Find(RootName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            // 画面いっぱいの入れ物。早見表（HelpScreen）より後ろ、戦闘の UI より前。
            var host = CreateRect(RootName, portrait, Vector2.zero, Vector2.one);
            var help = Object.FindAnyObjectByType<HelpScreen>(FindObjectsInactive.Include);
            if (help != null && help.transform.parent == portrait)
            {
                host.SetSiblingIndex(help.transform.GetSiblingIndex());
            }
            else
            {
                host.SetAsLastSibling();
            }

            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var play = CreateRect("Play", host, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            play.sizeDelta = new Vector2(360f, 360f);
            var group = play.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var mark = CreateRect("Mark", play, Vector2.zero, Vector2.one);
            var disc = mark.gameObject.AddComponent<Image>();
            disc.sprite = knob;
            disc.color = new Color(0.08f, 0.1f, 0.14f, 0.7f);
            disc.raycastTarget = false;
            var ring = CreateRect("Ring", mark, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f));
            var ringImage = ring.gameObject.AddComponent<Image>();
            ringImage.sprite = knob;
            ringImage.color = new Color(0.3f, 0.85f, 0.4f, 0.9f);
            ringImage.raycastTarget = false;
            var inner = CreateRect("Inner", ring, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
            var innerImage = inner.gameObject.AddComponent<Image>();
            innerImage.sprite = knob;
            innerImage.color = new Color(0.08f, 0.1f, 0.14f, 0.85f);
            innerImage.raycastTarget = false;
            var triangle = CreateRect("Triangle", mark, new Vector2(0.36f, 0.25f), new Vector2(0.72f, 0.75f));
            var graphic = triangle.gameObject.AddComponent<TriangleGraphic>();
            graphic.color = Color.white;
            graphic.raycastTarget = false;
            play.gameObject.SetActive(false);

            var view = host.gameObject.AddComponent<BattleIntroView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("fieldView").objectReferenceValue = field;
            serialized.FindProperty("battleCamera").objectReferenceValue = camera;
            serialized.FindProperty("primitiveMaterial").objectReferenceValue =
                new SerializedObject(field).FindProperty("primitiveMaterial").objectReferenceValue;
            serialized.FindProperty("playGroup").objectReferenceValue = group;
            serialized.FindProperty("playMark").objectReferenceValue = mark;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var scene = host.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] 開始の演出を足し、操作バーの部品を {removed} 個消した（T361）。");
        }

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
    }
}
