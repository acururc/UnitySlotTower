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
    /// spec 006（T1003）: 開いている Game シーンに黄7の毒の演出（<see cref="Yellow7PoisonEffect"/>）とパーティクルを作る。
    /// 材質の作り方と部品の作り方は赤7の Setup（<see cref="Red7BreathEffectSetup"/>）と同じ。新しい素材は使わない。
    /// パーティクルの値はここが初期値で、作った後は Inspector で調整してよい（もう一度実行すると初期値に戻る）。何度実行しても同じ結果になる。
    /// </summary>
    internal static class Yellow7PoisonEffectSetup
    {
        private const string RootName = "Yellow7 Poison Effect";
        private const string ShaderName = "UnitySlotTower/VFX Particle";

        private static readonly Color Toxic = new Color(0.75f, 0.35f, 1f);
        private static readonly Color DeepPoison = new Color(0.3f, 0.05f, 0.45f);
        private static readonly Color Slime = new Color(0.55f, 0.95f, 0.3f);

        [MenuItem("Tools/UnitySlotTower/Setup/Build Yellow7 Poison Effect (spec 006)")]
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

            var glow = R.Material(shader, "Yellow7 Glow", "circle_05", additive: true);
            var blob = R.Material(shader, "Yellow7 Swamp", "circle_05", additive: false);
            var fumeMaterial = R.Material(shader, "Yellow7 Fumes", "smoke_04", additive: false);

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
            var effect = root.AddComponent<Yellow7PoisonEffect>();

            // 毒の沼: 地面に平らな濃い紫の丸。湧いてから広がり、弾けるまで残る（寿命は実行時に入れる）。
            var swamp = R.Create(root.transform, "Swamp", blob, layer, 32, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 2f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.45f, 0.12f, 0.6f, 0.9f);
                var color = ps.colorOverLifetime;
                color.enabled = true;
                color.color = R.Gradient(new[] { (Color.white, 0f), (Color.white, 1f) }, new[] { (0f, 0f), (1f, 0.25f), (1f, 1f) });
                // ぼこぼこ広がる: すぐ大きくなり、その後ゆっくり膨らむ。
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.2f), new Keyframe(0.2f, 0.85f), new Keyframe(1f, 1.1f)));
                Flat(ps);
            });

            // 敵の足元の沼: 位置・大きさ・色は実行時に毎フレーム入れる（放出はしない）。
            var enemySwamp = R.Create(root.transform, "Enemy Swamp", blob, layer, 64, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 100f;
                Flat(ps);
            });

            // 泡: 沼から上がって大きくなり、弾けて消える。
            var bubbles = R.Create(root.transform, "Bubbles", glow, layer, 300, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                main.startColor = new Color(0.7f, 0.4f, 1f, 0.9f);
                R.Fade(ps, Color.white, Slime, 0.1f);
                R.Grow(ps, 0.4f, 1.4f);
            });

            // 弾けた飛沫: 上へ飛んで落ちる。
            var splash = R.Create(root.transform, "Splash", glow, layer, 512, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
                main.startColor = new Color(0.8f, 0.45f, 1f, 1f);
                main.gravityModifier = 1.4f;
                R.Fade(ps, Color.white, Slime, 0.01f);
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.06f;
                renderer.lengthScale = 1.4f;
            });

            // 弾けた輪: 沼の位置から地面に広がる光の輪。
            var ring = R.Create(root.transform, "Ring", glow, layer, 32, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 0.35f;
                main.startSize = 3f;
                main.startColor = new Color(0.85f, 0.5f, 1f, 1f);
                R.Fade(ps, Color.white, Toxic, 0.02f);
                R.Grow(ps, 0.4f, 1.3f);
                Flat(ps);
            });

            // 毒の霧: 弾けた所とオーラが消えた所から立ちのぼる。
            var fumes = R.Create(root.transform, "Fumes", fumeMaterial, layer, 160, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
                main.startSize = new ParticleSystem.MinMaxCurve(1f, 1.8f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.6f, 0.25f, 0.85f, 1f);
                var color = ps.colorOverLifetime;
                color.enabled = true;
                color.color = R.Gradient(new[] { (Color.white, 0f), (DeepPoison, 1f) }, new[] { (0f, 0f), (0.6f, 0.15f), (0f, 1f) });
                R.Grow(ps, 0.6f, 1.6f);
            });

            // オーラ: 敵ごとの紫の光。位置・大きさ・色は実行時に毎フレーム入れる（放出はしない）。
            var aura = R.Create(root.transform, "Aura", glow, layer, 64, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = 100f;
            });

            // ダメージごとの泡: 敵から小さく上がる。
            var tickBubbles = R.Create(root.transform, "Tick Bubbles", glow, layer, 256, false, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
                main.startColor = new Color(0.85f, 0.55f, 1f, 1f);
                R.Fade(ps, Color.white, Toxic, 0.05f);
                R.Grow(ps, 0.5f, 1.3f);
            });

            var serialized = new SerializedObject(effect);
            serialized.FindProperty("cabinet").objectReferenceValue = cabinet;
            serialized.FindProperty("battle").objectReferenceValue = battle;
            serialized.FindProperty("fieldView").objectReferenceValue = fieldView;
            serialized.FindProperty("battleCamera").objectReferenceValue = camera;
            serialized.FindProperty("swamp").objectReferenceValue = swamp;
            serialized.FindProperty("enemySwamp").objectReferenceValue = enemySwamp;
            serialized.FindProperty("bubbles").objectReferenceValue = bubbles;
            serialized.FindProperty("splash").objectReferenceValue = splash;
            serialized.FindProperty("ring").objectReferenceValue = ring;
            serialized.FindProperty("fumes").objectReferenceValue = fumes;
            serialized.FindProperty("aura").objectReferenceValue = aura;
            serialized.FindProperty("tickBubbles").objectReferenceValue = tickBubbles;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SlotTower Setup] 黄7の毒の演出を作った（spec 006、レイヤー {LayerMask.LayerToName(layer)}、カメラ {camera.name}）。");
        }

        // 地面に平らに置く（上から見た模様）。粒より下（奥）に描く。
        private static void Flat(ParticleSystem ps)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sortingFudge = 5f;
        }
    }
}
