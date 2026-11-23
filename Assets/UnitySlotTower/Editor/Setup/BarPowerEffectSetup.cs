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
    /// spec 007（T1103）: 開いている Game シーンにバーの闘気の演出（<see cref="BarPowerEffect"/>）とパーティクルを作る。
    /// 材質の作り方と部品の作り方は赤7の Setup（<see cref="Red7BreathEffectSetup"/>）と同じ。新しい素材は使わない（Kenney を流用）。
    /// パーティクルの値はここが初期値で、作った後は Inspector で調整してよい（もう一度実行すると初期値に戻る）。何度実行しても同じ結果になる。
    /// </summary>
    internal static class BarPowerEffectSetup
    {
        private const string RootName = "Bar Power Effect";
        private const string ShaderName = "UnitySlotTower/VFX Particle";

        private static readonly Color Gold = new Color(1f, 0.85f, 0.35f);
        private static readonly Color Ember = new Color(1f, 0.35f, 0.05f);

        [MenuItem("Tools/UnitySlotTower/Setup/Build Bar Power Effect (spec 007)")]
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

            var glow = R.Material(shader, "Bar Glow", "circle_05", additive: true);
            var flame = R.Material(shader, "Bar Flame", "flame_01", additive: true);
            var spark = R.Material(shader, "Bar Spark", "spark_04", additive: true);
            var flash = R.Material(shader, "Bar Flash", "scorch_02", additive: true);

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
            var effect = root.AddComponent<BarPowerEffect>();

            // 足元の縮まる輪と膨らむ芯（味方1体に2つ）: 位置・大きさ・色は実行時に毎フレーム入れる（放出はしない）。
            var chargeRing = R.Create(root.transform, "Charge Ring", glow, layer, 128, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 100f;
                Flat(ps);
            });

            // 溜めの粒: 輪の外から足元へ吸い込まれる。
            var chargeMotes = R.Create(root.transform, "Charge Motes", glow, layer, 800, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 0.45f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
                main.startColor = new Color(1f, 0.85f, 0.45f, 1f);
                R.Fade(ps, Color.white, Gold, 0.2f);
                R.Grow(ps, 1f, 0.4f);
            });

            // 爆発の炎: 味方から上へ噴き上がる。
            var burstFlames = R.Create(root.transform, "Burst Flames", flame, layer, 1500, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                main.startSize = new ParticleSystem.MinMaxCurve(1f, 1.8f);
                main.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
                main.startColor = new Color(1f, 0.7f, 0.25f, 1f);
                main.gravityModifier = -0.3f;
                R.Fade(ps, Color.white, Ember, 0.02f);
                R.Grow(ps, 0.6f, 1.4f);
            });

            // 爆発の輪: 足元から地面に広がる光の輪。
            var burstRing = R.Create(root.transform, "Burst Ring", glow, layer, 64, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 0.45f;
                main.startSize = 5f;
                main.startColor = new Color(1f, 0.85f, 0.45f, 1f);
                R.Fade(ps, Color.white, Ember, 0.02f);
                R.Grow(ps, 0.2f, 1.4f);
                Flat(ps);
            });

            // 爆発の火花: 四方へ飛んで落ちる。
            var burstSparks = R.Create(root.transform, "Burst Sparks", spark, layer, 1500, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
                main.startColor = new Color(1f, 0.9f, 0.5f, 1f);
                main.gravityModifier = 1.2f;
                R.Fade(ps, Color.white, Ember, 0.01f);
                Stretch(ps);
            });

            // オーラの光: 味方ごとの橙の光。位置・大きさ・色は実行時に毎フレーム入れる（放出はしない）。
            var aura = R.Create(root.transform, "Aura", glow, layer, 64, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 100f;
            });

            // オーラから立ちのぼる炎。
            var auraFlames = R.Create(root.transform, "Aura Flames", flame, layer, 1500, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
                main.startRotation = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
                main.startColor = new Color(1f, 0.65f, 0.2f, 0.95f);
                R.Fade(ps, Color.white, Ember, 0.1f);
                R.Grow(ps, 0.9f, 0.3f);
            });

            // 攻撃したときの火花。
            var attackSparks = R.Create(root.transform, "Attack Sparks", spark, layer, 800, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.3f);
                main.startColor = new Color(1f, 0.85f, 0.4f, 1f);
                main.gravityModifier = 0.8f;
                R.Fade(ps, Color.white, Ember, 0.01f);
                Stretch(ps);
            });

            // 閃光: 爆発と攻撃の瞬間の放射状の光。大きさは実行時に入れる。
            var burstFlash = R.Create(root.transform, "Burst Flash", flash, layer, 256, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 0.22f;
                main.startColor = new Color(1f, 0.9f, 0.6f, 1f);
                R.Fade(ps, Color.white, Ember, 0.01f);
                R.Grow(ps, 0.5f, 1.3f);
            });

            // 炎の柱: 爆発で味方から縦に立つ。大きさは実行時に入れる（縦長）。
            var burstPillar = R.Create(root.transform, "Burst Pillar", flame, layer, 64, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 0.7f;
                main.startSize3D = true;
                main.startSizeX = 1.6f;
                main.startSizeY = 5f;
                main.startSizeZ = 1f;
                var color = ps.colorOverLifetime;
                color.enabled = true;
                color.color = R.Gradient(
                    new[] { (Color.white, 0f), (Gold, 0.3f), (Ember, 1f) },
                    new[] { (0f, 0f), (1f, 0.06f), (0.8f, 0.45f), (0f, 1f) });
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.separateAxes = true;
                size.x = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1.3f), new Keyframe(1f, 0.4f)));
                size.y = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.2f, 1f), new Keyframe(1f, 1.2f)));
                size.z = 1f;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
            });

            // 火の粉: 効果の間、味方から舞い上がる。
            var auraEmbers = R.Create(root.transform, "Aura Embers", spark, layer, 600, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
                main.startColor = new Color(1f, 0.8f, 0.35f, 1f);
                R.Fade(ps, Color.white, Ember, 0.05f);
                Stretch(ps);
            });

            var serialized = new SerializedObject(effect);
            serialized.FindProperty("cabinet").objectReferenceValue = cabinet;
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("fieldView").objectReferenceValue = fieldView;
            serialized.FindProperty("battleCamera").objectReferenceValue = camera;
            serialized.FindProperty("chargeRing").objectReferenceValue = chargeRing;
            serialized.FindProperty("chargeMotes").objectReferenceValue = chargeMotes;
            serialized.FindProperty("burstFlames").objectReferenceValue = burstFlames;
            serialized.FindProperty("burstRing").objectReferenceValue = burstRing;
            serialized.FindProperty("burstSparks").objectReferenceValue = burstSparks;
            serialized.FindProperty("aura").objectReferenceValue = aura;
            serialized.FindProperty("auraFlames").objectReferenceValue = auraFlames;
            serialized.FindProperty("attackSparks").objectReferenceValue = attackSparks;
            serialized.FindProperty("burstFlash").objectReferenceValue = burstFlash;
            serialized.FindProperty("burstPillar").objectReferenceValue = burstPillar;
            serialized.FindProperty("auraEmbers").objectReferenceValue = auraEmbers;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] バーの闘気の演出を作った（spec 007、レイヤー {LayerMask.LayerToName(layer)}、カメラ {camera.name}）。");
        }

        // 地面に平らに置く（上から見た模様）。粒より下（奥）に描く。
        private static void Flat(ParticleSystem ps)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sortingFudge = 5f;
        }

        // 飛ぶ向きに伸ばす（火花）。
        private static void Stretch(ParticleSystem ps)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1.4f;
        }
    }
}
