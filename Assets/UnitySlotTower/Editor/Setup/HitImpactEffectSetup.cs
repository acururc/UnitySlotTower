using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySlotTower.Battle;
using Object = UnityEngine.Object;
using R = UnitySlotTower.Editor.Setup.Red7BreathEffectSetup;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// spec 008（T1204・T1205）: 開いている Game シーンに当たりの衝撃（<see cref="HitImpactEffect"/>）とパーティクルを作り、
    /// <see cref="BattleActorsView"/> に弾の尾の材質を入れる。新しい素材は使わない（Kenney を流用、尾は白の加算）。
    /// 値はここが初期値で、作った後は Inspector で調整してよい（もう一度実行すると初期値に戻る）。何度実行しても同じ結果になる。
    /// </summary>
    internal static class HitImpactEffectSetup
    {
        private const string RootName = "Hit Impact Effect";
        private const string ShaderName = "UnitySlotTower/VFX Particle";

        [MenuItem("Tools/UnitySlotTower/Setup/Build Hit Impact Effect (spec 008)")]
        private static void Build()
        {
            var actors = Object.FindAnyObjectByType<BattleActorsView>(FindObjectsInactive.Include);
            var fieldView = Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include);
            if (actors == null || fieldView == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの味方と敵の表示・フィールドが見つからない。");
                return;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[SlotTower Setup] シェーダー {ShaderName} が見つからない。");
                return;
            }

            var flashMaterial = R.Material(shader, "Hit Flash", "scorch_02", additive: true);
            var ringMaterial = R.Material(shader, "Hit Ring", "circle_05", additive: true);
            var sparkMaterial = R.Material(shader, "Hit Spark", "spark_04", additive: true);
            // 尾: テクスチャなし（白）の加算。色と太さは TrailRenderer が持つ。
            var trailMaterial = R.Material(shader, "Shot Trail", "none", additive: true);

            var scene = fieldView.gameObject.scene;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == RootName)
                {
                    Object.DestroyImmediate(go);
                }
            }

            var root = new GameObject(RootName);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var layer = fieldView.gameObject.layer;
            root.layer = layer;
            var effect = root.AddComponent<HitImpactEffect>();

            // 閃光: 中心から外へ一瞬広がって消える。
            var flash = R.Create(root.transform, "Flash", flashMaterial, layer, 128, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 0.15f;
                R.Fade(ps, Color.white, Color.white, 0.01f);
                R.Grow(ps, 0.4f, 1.3f);
            });

            // 輪: カメラを向いて外へ広がる。
            var ring = R.Create(root.transform, "Ring", ringMaterial, layer, 128, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 0.22f;
                R.Fade(ps, Color.white, Color.white, 0.01f);
                R.Grow(ps, 0.3f, 1.4f);
            });

            // 火花: 当たった所から飛び散って落ちる。
            var sparks = R.Create(root.transform, "Sparks", sparkMaterial, layer, 512, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
                main.gravityModifier = 1f;
                R.Fade(ps, Color.white, Color.white, 0.01f);
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.05f;
                renderer.lengthScale = 1.2f;
            });

            var serialized = new SerializedObject(effect);
            serialized.FindProperty("actors").objectReferenceValue = actors;
            serialized.FindProperty("flash").objectReferenceValue = flash;
            serialized.FindProperty("ring").objectReferenceValue = ring;
            serialized.FindProperty("sparks").objectReferenceValue = sparks;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var actorsSerialized = new SerializedObject(actors);
            actorsSerialized.FindProperty("shotTrailMaterial").objectReferenceValue = trailMaterial;
            actorsSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] 当たりの衝撃と弾の尾を作った（spec 008、レイヤー {LayerMask.LayerToName(layer)}）。");
        }
    }
}
