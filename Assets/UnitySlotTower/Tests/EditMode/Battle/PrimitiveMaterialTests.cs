using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySlotTower.Battle;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("プレイヤービルドで戦闘マスがマゼンタになる不具合: 実行時に作る箱や球へ、ビルドに含まれる URP のマテリアルを渡す")]
    public sealed class PrimitiveMaterialTests
    {
        private const string ScenePath = "Assets/UnitySlotTower/Scenes/Game.unity";
        private const string MaterialPath = "Assets/UnitySlotTower/Art/Materials/Greybox.mat";

        [Test]
        public void Greybox_Material_Uses_Urp_Lit_With_Base_Color()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            Assert.That(material, Is.Not.Null, MaterialPath);
            Assert.That((material.shader.name, material.HasProperty("_BaseColor"), material.enableInstancing),
                Is.EqualTo(("Universal Render Pipeline/Lit", true, true)));
        }

        [Test]
        public void Views_That_Create_Primitives_In_Game_Scene_Have_The_Greybox_Material()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var wasLoaded = scene.isLoaded;
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            var found = new List<string>();
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    Collect<BattleFieldView>(root, material, found);
                    Collect<BattleActorsView>(root, material, found);
                    Collect<UnitPlacementView>(root, material, found);
                }
            }
            finally
            {
                if (!wasLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            Assert.That(found, Is.EquivalentTo(new[] { "BattleFieldView: ok", "BattleActorsView: ok", "UnitPlacementView: ok" }));
        }

        private static void Collect<T>(GameObject root, Material expected, List<string> found) where T : Component
        {
            foreach (var view in root.GetComponentsInChildren<T>(true))
            {
                var assigned = new SerializedObject(view).FindProperty("primitiveMaterial")?.objectReferenceValue;
                found.Add($"{typeof(T).Name}: {(assigned != null && assigned == expected ? "ok" : "missing")}");
            }
        }
    }
}
