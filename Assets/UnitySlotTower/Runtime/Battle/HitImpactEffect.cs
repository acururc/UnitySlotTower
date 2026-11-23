using UnityEngine;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 攻撃の当たりの衝撃（spec 008 FR-001、plan 4章）。<see cref="BattleActorsView.Impact"/> のたびに、当たった位置に
    /// 閃光（中心から外へ広がる）・地面に平らな輪・飛び散る火花を出す。味方の攻撃は白〜金、敵の攻撃は赤。
    /// 普段の攻撃は回数が多いので小さく短く、揺れとヒットストップはない。時間は戦闘の時間（Time.deltaTime、パーティクルの既定）。
    /// パーティクルは Setup（Build Hit Impact Effect）が作る。
    /// </summary>
    public sealed class HitImpactEffect : MonoBehaviour
    {
        [SerializeField] private BattleActorsView actors;

        [Header("パーティクル（Setup が作る）")]
        [SerializeField] private ParticleSystem flash;
        [SerializeField] private ParticleSystem ring;
        [SerializeField] private ParticleSystem sparks;

        [Header("大きさと数")]
        [Tooltip("閃光の大きさ（マス = 1）。")]
        [SerializeField, Min(0f)] private float flashSize = 0.9f;
        [Tooltip("輪の大きさ（マス = 1）。")]
        [SerializeField, Min(0f)] private float ringSize = 0.9f;
        [SerializeField, Min(0)] private int sparksPerHit = 6;
        [Tooltip("火花の速さ（ワールド/秒）。")]
        [SerializeField] private Vector2 sparkSpeed = new Vector2(1.5f, 3f);

        [Header("色")]
        [SerializeField] private Color unitHitColor = new Color(1f, 0.9f, 0.55f, 1f);
        [SerializeField] private Color enemyHitColor = new Color(1f, 0.3f, 0.25f, 1f);

        /// <summary>衝撃を出した数（テスト用）。</summary>
        public int PlayCount { get; private set; }

        /// <summary>演出中か（残っている粒があるか）。</summary>
        public bool IsPlaying => ActiveParticleCount > 0;

        /// <summary>出ている粒の数（テスト用。演出が終われば 0）。</summary>
        public int ActiveParticleCount => Count(flash) + Count(ring) + Count(sparks);

        private void OnEnable() => actors.Impact += OnImpact;

        private void OnDisable() => actors.Impact -= OnImpact;

        private void OnImpact(Vector3 at, bool byUnit)
        {
            PlayCount++;
            var color = byUnit ? unitHitColor : enemyHitColor;
            flash.Emit(new ParticleSystem.EmitParams
            {
                position = at,
                startSize = flashSize,
                startColor = color,
                rotation = Random.Range(0f, 360f),
                applyShapeToPosition = false,
            }, 1);
            ring.Emit(new ParticleSystem.EmitParams
            {
                position = at,
                startSize = ringSize,
                startColor = color,
                applyShapeToPosition = false,
            }, 1);
            for (var i = 0; i < sparksPerHit; i++)
            {
                var direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.8f + 0.2f;
                sparks.Emit(new ParticleSystem.EmitParams
                {
                    position = at,
                    velocity = direction * Random.Range(sparkSpeed.x, sparkSpeed.y),
                    startColor = color,
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        private static int Count(ParticleSystem system) => system != null ? system.particleCount : 0;
    }
}
