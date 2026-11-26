using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// spec 009（T1303、plan 3章）: 自作の金貨・きらめき・光の絵を作り、開いている Game シーンの UI Canvas にコインの噴水の演出の層
    /// （<see cref="CoinFountainEffect"/>）を足す。揃った時の演出の層と同じく、ヘルプ・リザルトより下に置く。何度実行しても同じ結果になる
    /// （Inspector の調整値は初期値に戻る）。
    /// </summary>
    internal static class CoinFountainEffectSetup
    {
        private const string LayerName = "Coin Fountain Effect";

        [MenuItem("Tools/UnitySlotTower/Setup/Build Coin Fountain Effect (spec 009)")]
        private static void Build()
        {
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var fieldView = Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include);
            var status = Object.FindAnyObjectByType<OperationBarStatus>(FindObjectsInactive.Include);
            var camera = fieldView != null
                ? new SerializedObject(fieldView).FindProperty("battleCamera").objectReferenceValue as Camera
                : null;
            var canvas = status != null ? status.GetComponentInParent<Canvas>(true)?.rootCanvas : null;
            if (cabinet == null || battle == null || fieldView == null || status == null || camera == null || canvas == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの筐体・戦闘・フィールド・操作バー・戦闘カメラ・UI Canvas が見つからない。");
                return;
            }

            var coin = CoinFountainTextures.Coin();
            var bill = CoinFountainTextures.Bill();
            var sparkle = CoinFountainTextures.Sparkle();
            var glow = CoinFountainTextures.Glow();

            // 縦長の画面の親（Portrait Root）の中、早見表（Help）のすぐ前。ヘルプ・リザルトはこの演出より上に出る。
            var help = Object.FindAnyObjectByType<HelpScreen>(FindObjectsInactive.Include);
            var root = help != null ? (RectTransform)help.transform.parent : (RectTransform)canvas.transform;
            foreach (var parent in new[] { (Transform)canvas.transform, root })
            {
                var old = parent.Find(LayerName);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            var rect = new GameObject(LayerName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = root.gameObject.layer;
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetSiblingIndex(help != null ? help.transform.GetSiblingIndex() : root.childCount - 1);

            var effect = rect.gameObject.AddComponent<CoinFountainEffect>();
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("cabinet").objectReferenceValue = cabinet;
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("fieldView").objectReferenceValue = fieldView;
            serialized.FindProperty("status").objectReferenceValue = status;
            serialized.FindProperty("battleCamera").objectReferenceValue = camera;
            serialized.FindProperty("coinSprite").objectReferenceValue = coin;
            serialized.FindProperty("billSprite").objectReferenceValue = bill;
            serialized.FindProperty("sparkleSprite").objectReferenceValue = sparkle;
            serialized.FindProperty("glowSprite").objectReferenceValue = glow;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var scene = root.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] コインの噴水の演出の層を足した（spec 009、{root.name} の {rect.GetSiblingIndex()} 番目、絵 {(coin != null && bill != null && sparkle != null && glow != null ? "4枚" : "不足")}）。");
        }
    }
}
