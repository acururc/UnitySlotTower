using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// Game シーンの仮の地面（M0）を、マップ CSV から作る戦闘フィールド（T201）に置き換える。
    /// ステージごとのマップ（T311）は既存のフィールドにも Assign Stage Maps で設定できる。
    /// </summary>
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
            AssignTables(field);
            Debug.Log("[SlotTower Setup] 戦闘フィールドを作成した。");
        }

        [MenuItem("Tools/UnitySlotTower/Setup/Assign Stage Maps")]
        private static void AssignStageMaps()
        {
            var field = Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include);
            if (field == null)
            {
                Debug.LogError("[SlotTower Setup] 戦闘フィールドが見つからない。");
                return;
            }

            AssignTables(field);
            Debug.Log("[SlotTower Setup] 戦闘フィールドに maps/ のマップをすべて設定した。");
        }

        /// <summary>maps/ の CSV をすべて名前順に設定する（マップ id はファイル名。戦闘マスが id で選ぶ: plan 16.6）。</summary>
        private static void AssignTables(BattleFieldView field)
        {
            var serialized = new SerializedObject(field);
            var maps = serialized.FindProperty("mapTables");
            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { TableFolder + "maps" });
            var paths = new string[guids.Length];
            for (var i = 0; i < guids.Length; i++)
            {
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            }

            System.Array.Sort(paths, System.StringComparer.Ordinal);
            maps.arraySize = paths.Length;
            for (var i = 0; i < paths.Length; i++)
            {
                maps.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(paths[i]);
            }

            serialized.FindProperty("pathsTable").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(TableFolder + BattleMap.PathsFileName);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(field.gameObject.scene);
            EditorSceneManager.SaveScene(field.gameObject.scene);
        }
    }
}
