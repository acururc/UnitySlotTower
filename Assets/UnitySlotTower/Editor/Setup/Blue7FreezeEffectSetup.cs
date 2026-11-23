using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnitySlotTower.Battle;
using UnitySlotTower.Slot;
using Object = UnityEngine.Object;
using R = UnitySlotTower.Editor.Setup.Red7BreathEffectSetup;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// spec 004（T803）: 開いている Game シーンに青7の凍結の演出（<see cref="Blue7FreezeEffect"/>）とパーティクルを作る。
    /// 材質の作り方と部品の作り方は赤7の Setup（<see cref="Red7BreathEffectSetup"/>）と同じ。
    /// パーティクルの値はここが初期値で、作った後は Inspector で調整してよい（もう一度実行すると初期値に戻る）。何度実行しても同じ結果になる。
    /// </summary>
    internal static class Blue7FreezeEffectSetup
    {
        private const string RootName = "Blue7 Freeze Effect";
        private const string ShaderName = "UnitySlotTower/VFX Particle";

        private static readonly Color Frost = new Color(0.6f, 0.88f, 1f);
        private static readonly Color DeepIce = new Color(0.25f, 0.55f, 1f);

        [MenuItem("Tools/UnitySlotTower/Setup/Build Blue7 Freeze Effect (spec 004)")]
        private static void Build()
        {
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>(FindObjectsInactive.Include);
            var battle = Object.FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
            var fieldView = Object.FindAnyObjectByType<BattleFieldView>(FindObjectsInactive.Include);
            var camera = fieldView != null
                ? new SerializedObject(fieldView).FindProperty("battleCamera").objectReferenceValue as Camera
                : null;
            if (cabinet == null || battle == null || fieldView == null || camera == null)
            {
                Debug.LogError("[SlotTower Setup] Game シーンの筐体・戦闘・フィールド・戦闘カメラが見つからない。");
                return;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[SlotTower Setup] シェーダー {ShaderName} が見つからない。");
                return;
            }

            var glow = R.Material(shader, "Blue7 Glow", "circle_05", additive: true);
            var mistMaterial = R.Material(shader, "Blue7 Mist", "smoke_04", additive: false);
            var crack = R.Material(shader, "Blue7 Crack", "spark_04", additive: true);
            var sparkle = R.Material(shader, "Blue7 Sparkle", "star_06", additive: true);
            var flare = R.Material(shader, "Blue7 Flare", "star_08", additive: true);
            // 氷の塊は絵を使わない（白 × 粒の色）。半透明。
            var iceMaterial = R.Material(shader, "Blue7 Ice", "", additive: false);

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
            var effect = root.AddComponent<Blue7FreezeEffect>();

            var warning = R.Create(root.transform, "Warning", glow, layer, 200, true, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                main.startColor = new Color(0.55f, 0.85f, 1f, 0.8f);
                var emission = ps.emission;
                emission.rateOverTime = 80f;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                R.Fade(ps, Color.white, Color.white, 0.25f);
                R.Grow(ps, 0.6f, 1.3f);
            });

            // 冷気の前線: 低く広がる白い霧。前線の帯（横幅は実行時に盤面に合わせる）から出して手前へ少し流す。
            var mist = R.Create(root.transform, "Mist", mistMaterial, layer, 300, true, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.85f, 0.95f, 1f, 1f);
                var emission = ps.emission;
                emission.rateOverTime = 120f;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(1f, 0.3f, 0.6f);
                var color = ps.colorOverLifetime;
                color.enabled = true;
                color.color = R.Gradient(new[] { (Color.white, 0f), (Frost, 1f) }, new[] { (0f, 0f), (0.6f, 0.2f), (0f, 1f) });
                R.Grow(ps, 0.6f, 1.4f);
                var rotation = ps.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            });

            // 雪の粒: 前線の上から舞い落ちる。
            var snow = R.Create(root.transform, "Snow", glow, layer, 400, true, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                main.startColor = Color.white;
                main.gravityModifier = 0.25f;
                var emission = ps.emission;
                emission.rateOverTime = 260f;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(1f, 1.2f, 1f);
                R.Fade(ps, Color.white, Frost, 0.15f);
            });

            // 地面の霜: 前線が通った所に平らなひびの模様を残し、凍結の後まで薄く残る。
            var groundFrost = R.Create(root.transform, "Ground Frost", crack, layer, 300, true, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.5f, 0.8f, 1f, 0.7f);
                var emission = ps.emission;
                emission.rateOverTime = 90f;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(1f, 0.02f, 0.3f);
                R.Fade(ps, Color.white, DeepIce, 0.1f);
                Flat(ps);
            });

            var flash = R.Create(root.transform, "Flash", flare, layer, 4, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 0.3f;
                main.startSize = 12f;
                main.startColor = new Color(0.8f, 0.95f, 1f, 1f);
                R.Fade(ps, Color.white, Frost, 0.02f);
                R.Grow(ps, 0.5f, 1.2f);
            });

            // 凍結の瞬間に盤面へ走る大きなひび。
            var cracks = R.Create(root.transform, "Cracks", crack, layer, 64, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
                main.startSize = new ParticleSystem.MinMaxCurve(2.2f, 3.5f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.75f, 0.93f, 1f, 1f);
                R.Fade(ps, Color.white, DeepIce, 0.03f);
                R.Grow(ps, 0.7f, 1.05f);
                Flat(ps);
            });

            var sparkles = R.Create(root.transform, "Sparkles", sparkle, layer, 128, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                main.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
                main.startColor = new Color(0.85f, 0.97f, 1f, 1f);
                R.Fade(ps, Color.white, Frost, 0.1f);
                R.Grow(ps, 0.2f, 1f);
            });

            // 氷の塊: 立方体のメッシュの粒。位置・大きさ・傾きは実行時に毎フレーム入れる（放出はしない）。
            var ice = R.Create(root.transform, "Ice", iceMaterial, layer, 64, false, ps =>
            {
                var main = ps.main;
                main.startSize3D = true;
                main.startRotation3D = true;
                main.startLifetime = 100f;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                renderer.alignment = ParticleSystemRenderSpace.World;
            });

            var shards = R.Create(root.transform, "Shards", glow, layer, 512, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
                main.startColor = new Color(0.8f, 0.95f, 1f, 1f);
                main.gravityModifier = 1.2f;
                R.Fade(ps, Color.white, Frost, 0.01f);
                // 飛ぶ向きに伸ばして破片らしくする。
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.08f;
                renderer.lengthScale = 1.5f;
            });

            var thawMist = R.Create(root.transform, "Thaw Mist", mistMaterial, layer, 64, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.9f, 0.97f, 1f, 1f);
                var color = ps.colorOverLifetime;
                color.enabled = true;
                color.color = R.Gradient(new[] { (Color.white, 0f), (Color.white, 1f) }, new[] { (0.7f, 0f), (0f, 1f) });
                R.Grow(ps, 0.6f, 1.5f);
            });

            var serialized = new SerializedObject(effect);
            serialized.FindProperty("cabinet").objectReferenceValue = cabinet;
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("fieldView").objectReferenceValue = fieldView;
            serialized.FindProperty("battleCamera").objectReferenceValue = camera;
            serialized.FindProperty("warning").objectReferenceValue = warning;
            serialized.FindProperty("mist").objectReferenceValue = mist;
            serialized.FindProperty("snow").objectReferenceValue = snow;
            serialized.FindProperty("groundFrost").objectReferenceValue = groundFrost;
            serialized.FindProperty("flash").objectReferenceValue = flash;
            serialized.FindProperty("cracks").objectReferenceValue = cracks;
            serialized.FindProperty("sparkles").objectReferenceValue = sparkles;
            serialized.FindProperty("ice").objectReferenceValue = ice;
            serialized.FindProperty("shards").objectReferenceValue = shards;
            serialized.FindProperty("thawMist").objectReferenceValue = thawMist;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] 青7の凍結の演出を作った（spec 004、レイヤー {LayerMask.LayerToName(layer)}、カメラ {camera.name}）。");
        }

        // 地面に平らに置く（上から見た模様）。
        private static void Flat(ParticleSystem ps)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            // 霜は粒より下（奥）に描く。
            renderer.sortingFudge = 5f;
        }
    }
}
