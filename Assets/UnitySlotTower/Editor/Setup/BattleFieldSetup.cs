using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>Game シーンの仮の地面（M0）を、マップ CSV から作る戦闘フィールド（T201）に置き換える。</summary>
    internal static class BattleFieldSetup
    {
        private const string TableFolder = "Assets/UnitySlotTower/Data/Tables/";
        private const string PlaceholderName = "Battle Field (Graybox)";

        [MenuItem("Tools/UnitySlotTower/Setup/Build Battle Field")]
        private static void Build()
        {
            if (Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include) != null)
            {
                Debug.LogWarning("[SlotTower Setup] 戦闘フィールドは既にあるため作成しない。");
                return;
            }

            var placeholder = GameObject.Find(PlaceholderName);
            if (placeholder != null)
            {
                Object.DestroyImmediate(placeholder);
            }

            var field = new GameObject("Battle Field").AddComponent<BattleFieldView>();
            var serialized = new SerializedObject(field);
            serialized.FindProperty("mapTable").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(TableFolder + BattleMap.MapFileName("w1"));
            serialized.FindProperty("pathsTable").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(TableFolder + BattleMap.PathsFileName);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(field.gameObject.scene);
            EditorSceneManager.SaveScene(field.gameObject.scene);
            Debug.Log("[SlotTower Setup] 戦闘フィールドを作成した。");
        }
    }
}
