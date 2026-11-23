using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Slot;
using Object = UnityEngine.Object;
using R = UnitySlotTower.Editor.Setup.Red7BreathEffectSetup;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// spec 005（T903）: 開いている Game シーンに緑7の回復の演出（<see cref="Green7HealEffect"/>）とパーティクルを作る。
    /// 材質の作り方と部品の作り方は赤7の Setup（<see cref="Red7BreathEffectSetup"/>）と同じ。
    /// パーティクルの値はここが初期値で、作った後は Inspector で調整してよい（もう一度実行すると初期値に戻る）。何度実行しても同じ結果になる。
    /// </summary>
    internal static class Green7HealEffectSetup
    {
        private const string RootName = "Green7 Heal Effect";
        private const string ShaderName = "UnitySlotTower/VFX Particle";

        [MenuItem("Tools/UnitySlotTower/Setup/Build Green7 Heal Effect (spec 005)")]
        private static void Build()
        {
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var fieldView = Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include);
            if (cabinet == null || battle == null || fieldView == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの筐体・戦闘・フィールドが見つからない。");
                return;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[SlotTower Setup] シェーダー {ShaderName} が見つからない。");
                return;
            }

            var glow = R.Material(shader, "Green7 Glow", "circle_05", additive: true);
            var sparkle = R.Material(shader, "Green7 Sparkle", "star_06", additive: true);
            var flare = R.Material(shader, "Green7 Flare", "star_08", additive: true);

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
            var effect = root.AddComponent<Green7HealEffect>();

            var warning = R.Create(root.transform, "Warning", glow, layer, 200, true, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                main.startColor = new Color(0.35f, 1f, 0.45f, 0.8f);
                var emission = ps.emission;
                emission.rateOverTime = 80f;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                R.Fade(ps, Color.white, Color.white, 0.25f);
                R.Grow(ps, 0.6f, 1.3f);
            });

            // 降り注ぐ光: 盤面の上空の板から、きらめく粒がゆっくり降る。
            var rain = R.Create(root.transform, "Rain", sparkle, layer, 400, true, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var emission = ps.emission;
                emission.rateOverTime = 140f;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
                velocity.y = new ParticleSystem.MinMaxCurve(-4.5f, -3f);
                velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
                var rotation = ps.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
                var color = ps.colorOverLifetime;
                color.enabled = true;
                color.color = R.Gradient(
                    new[] { (new Color(0.85f, 1f, 0.85f), 0f), (new Color(0.4f, 1f, 0.5f), 1f) },
                    new[] { (0f, 0f), (1f, 0.2f), (0.9f, 0.8f), (0f, 1f) });
            });

            var flash = R.Create(root.transform, "Flash", glow, layer, 4, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 0.45f;
                main.startSize = 10f;
                main.startColor = new Color(0.75f, 1f, 0.75f, 0.7f);
                R.Fade(ps, Color.white, new Color(0.4f, 1f, 0.5f), 0.05f);
                R.Grow(ps, 0.7f, 1.2f);
            });

            // 光の柱: 縦に引き伸ばした光をカメラへ向けたまま立てる（大きさは演出が味方ごとに決める）。
            var pillars = R.Create(root.transform, "Pillars", glow, layer, 64, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 0.9f;
                main.startSize3D = true;
                main.startSizeX = 0.9f;
                main.startSizeY = 4f;
                main.startSizeZ = 1f;
                var color = ps.colorOverLifetime;
                color.enabled = true;
                color.color = R.Gradient(
                    new[] { (Color.white, 0f), (new Color(0.55f, 1f, 0.6f), 0.4f), (new Color(0.3f, 0.9f, 0.4f), 1f) },
                    new[] { (0f, 0f), (1f, 0.08f), (0.8f, 0.5f), (0f, 1f) });
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.separateAxes = true;
                size.x = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1.3f, 1f, 0.2f));
                size.y = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.15f));
                size.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
                ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.VerticalBillboard;
            });

            // 足元の光の輪: 地面に寝かせた光が広がって消える。
            var rings = R.Create(root.transform, "Rings", glow, layer, 64, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 0.7f;
                main.startSize = 1.6f;
                main.startColor = new Color(0.5f, 1f, 0.55f, 0.9f);
                R.Fade(ps, Color.white, new Color(0.3f, 0.9f, 0.4f), 0.1f);
                R.Grow(ps, 0.3f, 1.4f);
                ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            });

            // 余韻: 味方の足元から光の粒がふわっと上がって消える。
            var motes = R.Create(root.transform, "Motes", flare, layer, 300, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var color = ps.colorOverLifetime;
                color.enabled = true;
                color.color = R.Gradient(
                    new[] { (Color.white, 0f), (new Color(0.45f, 1f, 0.55f), 1f) },
                    new[] { (0f, 0f), (1f, 0.15f), (0f, 1f) });
                R.Grow(ps, 1f, 0.4f);
            });

            var serialized = new SerializedObject(effect);
            serialized.FindProperty("cabinet").objectReferenceValue = cabinet;
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("fieldView").objectReferenceValue = fieldView;
            serialized.FindProperty("warning").objectReferenceValue = warning;
            serialized.FindProperty("rain").objectReferenceValue = rain;
            serialized.FindProperty("flash").objectReferenceValue = flash;
            serialized.FindProperty("pillars").objectReferenceValue = pillars;
            serialized.FindProperty("rings").objectReferenceValue = rings;
            serialized.FindProperty("motes").objectReferenceValue = motes;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] 緑7の回復の演出を作った（spec 005、レイヤー {LayerMask.LayerToName(layer)}）。");
        }
    }
}
