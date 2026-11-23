using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnitySlotTower.Battle;
using UnitySlotTower.Slot;
using Object = UnityEngine.Object;

namespace UnitySlotTower.Editor.Setup
{
    /// <summary>
    /// spec 003（T704）: 開いている Game シーンに赤7の息吹の演出（<see cref="Red7BreathEffect"/>）とパーティクルを作る。
    /// 材質は Art/VFX に作る（Kenney Particle Pack の絵と自作シェーダー UnitySlotTower/VFX Particle）。
    /// パーティクルの値はここが初期値で、作った後は Inspector で調整してよい（もう一度実行すると初期値に戻る）。何度実行しても同じ結果になる。
    /// </summary>
    internal static class Red7BreathEffectSetup
    {
        private const string RootName = "Red7 Breath Effect";
        private const string VfxFolder = "Assets/UnitySlotTower/Art/VFX";
        private const string ShaderName = "UnitySlotTower/VFX Particle";

        [MenuItem("Tools/UnitySlotTower/Setup/Build Red7 Breath Effect (spec 003)")]
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

            var flame = Material(shader, "Red7 Flame", "flame_01", additive: true);
            var glow = Material(shader, "Red7 Glow", "circle_05", additive: true);
            var fire = Material(shader, "Red7 Fire", "fire_01", additive: true);
            var burst = Material(shader, "Red7 Burst", "scorch_02", additive: true);
            var smokeMaterial = Material(shader, "Red7 Smoke", "smoke_04", additive: false);

            var scene = fieldView.gameObject.scene;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == RootName)
                {
                    Object.DestroyImmediate(go);
                }
            }

            var root = new GameObject(RootName);
            SceneManager_Move(root, scene);
            var layer = fieldView.gameObject.layer;
            root.layer = layer;
            var effect = root.AddComponent<Red7BreathEffect>();

            var warning = Create(root.transform, "Warning", glow, layer, 200, true, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                main.startColor = new Color(1f, 0.25f, 0.08f, 0.8f);
                var emission = ps.emission;
                emission.rateOverTime = 80f;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                Fade(ps, Color.white, Color.white, 0.25f);
                Grow(ps, 0.6f, 1.3f);
            });

            var breath = Create(root.transform, "Breath", flame, layer, 700, true, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.55f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
                main.startSpeed = 20f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var emission = ps.emission;
                emission.rateOverTime = 320f;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 9f;
                shape.radius = 0.3f;
                var color = ps.colorOverLifetime;
                color.enabled = true;
                color.color = Gradient(
                    new[] { (new Color(1f, 0.95f, 0.75f), 0f), (new Color(1f, 0.6f, 0.15f), 0.35f), (new Color(0.9f, 0.18f, 0.05f), 1f) },
                    new[] { (0f, 0f), (1f, 0.08f), (0.85f, 0.6f), (0f, 1f) });
                Grow(ps, 0.4f, 2.4f);
                var rotation = ps.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            });

            // 地面をなめる炎: 息吹と同じゆらぐ炎の絵を回しながら低く広げる（縦長の絵は水滴に見えたので使わない）。
            var groundFire = Create(root.transform, "Ground Fire", flame, layer, 400, true, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var rotation = ps.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
                var emission = ps.emission;
                emission.rateOverTime = 160f;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                var color = ps.colorOverLifetime;
                color.enabled = true;
                color.color = Gradient(
                    new[] { (new Color(1f, 0.9f, 0.5f), 0f), (new Color(1f, 0.45f, 0.1f), 0.5f), (new Color(0.7f, 0.1f, 0.03f), 1f) },
                    new[] { (0f, 0f), (1f, 0.1f), (0f, 1f) });
                Grow(ps, 0.6f, 1.3f);
            });

            var flash = Create(root.transform, "Flash", glow, layer, 4, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 0.35f;
                main.startSize = 12f;
                main.startSpeed = 0f;
                main.startColor = new Color(1f, 0.85f, 0.6f, 1f);
                Fade(ps, Color.white, new Color(1f, 0.5f, 0.2f), 0.02f);
                Grow(ps, 0.6f, 1.2f);
            });

            var blasts = Create(root.transform, "Blasts", fire, layer, 64, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                FireColor(ps);
                Grow(ps, 0.3f, 1.4f);
            });

            var hits = Create(root.transform, "Hits", burst, layer, 128, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                FireColor(ps);
                Grow(ps, 0.4f, 1.3f);
            });

            var embers = Create(root.transform, "Embers", glow, layer, 300, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
                main.gravityModifier = 0.8f;
                var color = ps.colorOverLifetime;
                color.enabled = true;
                color.color = Gradient(
                    new[] { (new Color(1f, 0.8f, 0.3f), 0f), (new Color(1f, 0.3f, 0.05f), 1f) },
                    new[] { (1f, 0f), (1f, 0.6f), (0f, 1f) });
            });

            var smoke = Create(root.transform, "Smoke", smokeMaterial, layer, 64, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(2f, 3.5f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.16f, 0.1f, 0.09f, 1f);
                var color = ps.colorOverLifetime;
                color.enabled = true;
                color.color = Gradient(
                    new[] { (Color.white, 0f), (Color.white, 1f) },
                    new[] { (0f, 0f), (0.55f, 0.2f), (0f, 1f) });
                Grow(ps, 0.6f, 1.5f);
                // 煙は炎より奥に描く。
                ps.GetComponent<ParticleSystemRenderer>().sortingFudge = 10f;
            });

            var serialized = new SerializedObject(effect);
            serialized.FindProperty("cabinet").objectReferenceValue = cabinet;
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("fieldView").objectReferenceValue = fieldView;
            serialized.FindProperty("battleCamera").objectReferenceValue = camera;
            serialized.FindProperty("warning").objectReferenceValue = warning;
            serialized.FindProperty("breath").objectReferenceValue = breath;
            serialized.FindProperty("groundFire").objectReferenceValue = groundFire;
            serialized.FindProperty("flash").objectReferenceValue = flash;
            serialized.FindProperty("blasts").objectReferenceValue = blasts;
            serialized.FindProperty("hits").objectReferenceValue = hits;
            serialized.FindProperty("embers").objectReferenceValue = embers;
            serialized.FindProperty("smoke").objectReferenceValue = smoke;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] 赤7の息吹の演出を作った（spec 003、レイヤー {LayerMask.LayerToName(layer)}、カメラ {camera.name}）。");
        }

        private static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene) =>
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

        // 材質を作るか、あれば値を上書きする（GUID を保つ）。
        internal static Material Material(Shader shader, string name, string texture, bool additive)
        {
            var path = $"{VfxFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{VfxFolder}/Kenney/{texture}.png");
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        internal static ParticleSystem Create(Transform parent, string name, Material material, int layer, int maxParticles,
            bool looping, Action<ParticleSystem> configure)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.layer = layer;
            go.transform.SetParent(parent, false);
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = looping;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.startSpeed = 0f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 5f;
            configure(ps);
            return ps;
        }

        // 色は始め→終わりに変わり、透明度は fadeIn までに上がって最後に消える。
        internal static void Fade(ParticleSystem ps, Color from, Color to, float fadeIn)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = Gradient(new[] { (from, 0f), (to, 1f) }, new[] { (0f, 0f), (1f, fadeIn), (0f, 1f) });
        }

        // 火の玉: 白い芯 → 橙 → 暗い赤で消える。
        private static void FireColor(ParticleSystem ps)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = Gradient(
                new[] { (new Color(1f, 0.95f, 0.8f), 0f), (new Color(1f, 0.55f, 0.12f), 0.3f), (new Color(0.45f, 0.08f, 0.03f), 1f) },
                new[] { (1f, 0f), (0.9f, 0.5f), (0f, 1f) });
        }

        internal static void Grow(ParticleSystem ps, float from, float to)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
        }

        internal static ParticleSystem.MinMaxGradient Gradient((Color color, float time)[] colors, (float alpha, float time)[] alphas)
        {
            var gradient = new Gradient();
            var colorKeys = new GradientColorKey[colors.Length];
            for (var i = 0; i < colors.Length; i++) colorKeys[i] = new GradientColorKey(colors[i].color, colors[i].time);
            var alphaKeys = new GradientAlphaKey[alphas.Length];
            for (var i = 0; i < alphas.Length; i++) alphaKeys[i] = new GradientAlphaKey(alphas[i].alpha, alphas[i].time);
            gradient.SetKeys(colorKeys, alphaKeys);
            return new ParticleSystem.MinMaxGradient(gradient);
        }
    }
}
