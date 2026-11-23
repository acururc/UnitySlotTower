using System;
using UnityEngine;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// バーの闘気の演出（spec 007 FR-001・FR-002・FR-004、plan 3章）。<see cref="Yellow7PoisonEffect"/> と同じく 3D のフィールドの上の
    /// パーティクルを戦闘カメラで写す。揃うと味方ごとに足元へ光の輪が縮まっていき（溜め）、Core が攻撃力アップを始めた瞬間
    /// （<see cref="BattleController.BarBurst"/>）に味方ごとに炎のような闘気が爆発して、カメラが弱く揺れる（ヒットストップなし）。
    /// 攻撃力アップの間は味方ごとに闘気のオーラ（光と立ちのぼる炎）を付け、味方が攻撃する（<see cref="BattleController.UnitAttacked"/>）たびに
    /// その味方から火花を出す。効果が切れたらオーラを消す。時間は戦闘の時間（Time.deltaTime）。パーティクルは Setup（Build Bar Power Effect）が作る。
    /// </summary>
    public sealed class BarPowerEffect : MonoBehaviour
    {
        // 爆発が来ないとき（ステージが先に終わった）に輪を止めるまでの、待ち時間を過ぎてからの余裕（秒）。
        private const float BurstGrace = 0.5f;

        // 輪とオーラの上限（味方の数）。
        private const int MaxUnits = 64;

        [SerializeField] private SlotCabinet cabinet;
        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private Camera battleCamera;

        [Header("パーティクル（Setup が作る）")]
        [Tooltip("足元の縮まる輪（味方に付いて動く）。位置と大きさは実行時に毎フレーム入れる。")]
        [SerializeField] private ParticleSystem chargeRing;
        [Tooltip("溜めの間に足元へ吸い込まれる光の粒。")]
        [SerializeField] private ParticleSystem chargeMotes;
        [SerializeField] private ParticleSystem burstFlames;
        [SerializeField] private ParticleSystem burstRing;
        [SerializeField] private ParticleSystem burstSparks;
        [Tooltip("闘気のオーラの光（味方に付いて動く）。位置と大きさは実行時に毎フレーム入れる。")]
        [SerializeField] private ParticleSystem aura;
        [Tooltip("オーラから立ちのぼる炎。")]
        [SerializeField] private ParticleSystem auraFlames;
        [SerializeField] private ParticleSystem attackSparks;

        [Header("溜め（足元の輪）")]
        [Tooltip("輪の大きさ（マス = 1）。揃った瞬間 → 爆発の瞬間。")]
        [SerializeField] private Vector2 chargeRingSize = new Vector2(3.2f, 0.6f);
        [SerializeField] private Color chargeRingColor = new Color(1f, 0.75f, 0.3f, 0.9f);
        [Tooltip("吸い込まれる粒の数（毎秒、味方1体あたり）。爆発の直前ほど増える。")]
        [SerializeField, Min(0f)] private float motesPerSecond = 8f;

        [Header("爆発")]
        [SerializeField, Min(0)] private int flamesPerUnit = 14;
        [SerializeField, Min(0)] private int sparksPerUnit = 12;

        [Header("オーラ")]
        [Tooltip("オーラの光の大きさ（ワールド、マス = 1）。")]
        [SerializeField, Min(0f)] private float auraSize = 1.2f;
        [Tooltip("オーラの中心の、味方の足元からの高さ。")]
        [SerializeField] private float auraHeight = 0.45f;
        [SerializeField] private Color auraColor = new Color(1f, 0.55f, 0.15f, 0.5f);
        [Tooltip("立ちのぼる炎の数（毎秒、味方1体あたり）。")]
        [SerializeField, Min(0f)] private float auraFlamesPerSecond = 10f;
        [Tooltip("攻撃したときの火花の数。")]
        [SerializeField, Min(0)] private int sparksPerAttack = 6;

        [Header("揺れ")]
        [SerializeField, Min(0f)] private float shakeSeconds = 0.2f;
        [Tooltip("揺れの幅（ワールド）。黄7と同じくらい（spec 007 D-04）。")]
        [SerializeField, Min(0f)] private float shakeAmplitude = 0.06f;

        private readonly ParticleSystem.Particle[] _ringParticles = new ParticleSystem.Particle[MaxUnits];
        private readonly ParticleSystem.Particle[] _auraParticles = new ParticleSystem.Particle[MaxUnits];
        private readonly Vector3[] _ringPositions = new Vector3[MaxUnits];
        private readonly Vector3[] _auraPositions = new Vector3[MaxUnits];
        private int _ringCount;
        private int _auraCount;
        private bool _auraShown;
        private float _flameDebt;
        private int _charging;
        private int _expectedBursts;
        private BattleCameraShake _shake;

        /// <summary>バーで溜めを始めた回数（テスト用）。</summary>
        public int PlayCount { get; private set; }

        /// <summary>闘気が爆発した数（テスト用）。</summary>
        public int BurstCount { get; private set; }

        /// <summary>攻撃で火花を出した回数（テスト用）。</summary>
        public int SparkCount { get; private set; }

        /// <summary>効果が切れてオーラが消えた回数（テスト用）。</summary>
        public int FadeCount { get; private set; }

        /// <summary>足元の輪が縮まっている（爆発の前）。</summary>
        public bool IsCharging => _charging > 0;

        /// <summary>出ている足元の輪の数（溜めの間だけ 0 より大きい）。</summary>
        public int RingCount => IsCharging ? _ringCount : 0;

        /// <summary>出ているオーラの数（攻撃力アップの間だけ 0 より大きい）。</summary>
        public int AuraCount => _auraShown ? _auraCount : 0;

        /// <summary>カメラが揺れている。</summary>
        public bool IsShaking => _shake != null && _shake.IsShaking;

        /// <summary>演出中か（輪・オーラ・揺れ・残っている粒のどれか）。</summary>
        public bool IsPlaying => IsCharging || _auraShown || IsShaking || ActiveParticleCount > 0;

        /// <summary>出ている粒の数（テスト用。演出が終われば 0）。</summary>
        public int ActiveParticleCount =>
            Count(chargeRing) + Count(chargeMotes) + Count(burstFlames) + Count(burstRing) + Count(burstSparks) +
            Count(aura) + Count(auraFlames) + Count(attackSparks);

        private void Awake() => _shake = BattleCameraShake.Of(battleCamera);

        private void OnEnable()
        {
            cabinet.Aligned += OnAligned;
            battle.BarBurst += OnBurst;
            battle.UnitAttacked += OnUnitAttacked;
            battle.NodeBegan += OnNodeBegan;
        }

        private void OnDisable()
        {
            cabinet.Aligned -= OnAligned;
            battle.BarBurst -= OnBurst;
            battle.UnitAttacked -= OnUnitAttacked;
            battle.NodeBegan -= OnNodeBegan;
        }

        // 前のステージで来なかった爆発を待たない。前のステージのオーラと輪は消す。
        private void OnNodeBegan()
        {
            _expectedBursts = BurstCount;
            HideAura();
            HideRings();
        }

        private void OnAligned(SlotSymbol symbol)
        {
            var field = battle.Field;
            if (symbol != SlotSymbol.Bar || field?.Effects == null || fieldView.Map == null)
            {
                return;
            }

            PlayCount++;
            _expectedBursts++;
            var delay = (float)field.Effects.BarDelaySteps / SimulationClock.StepsPerSecond;
            // 待ちが 0 なら爆発は揃いと同時（ApplySlot の中）なので輪は出さない。
            if (delay > 0f)
            {
                _ = Charge(delay, _expectedBursts);
            }
        }

        private async Awaitable Charge(float delay, int burstIndex)
        {
            _charging++;
            var token = destroyCancellationToken;
            try
            {
                var moteDebt = 0f;
                for (var t = 0f; BurstCount < burstIndex && t < delay + BurstGrace; t += Time.deltaTime)
                {
                    var progress = Mathf.Clamp01(t / delay);
                    PlaceRings(progress);

                    // 吸い込まれる粒: 輪の外から足元へ集まる。爆発の直前ほど多い。
                    moteDebt += motesPerSecond * (0.5f + 1.5f * progress) * _ringCount * Time.deltaTime;
                    for (; moteDebt >= 1f && _ringCount > 0; moteDebt -= 1f)
                    {
                        var at = _ringPositions[UnityEngine.Random.Range(0, _ringCount)];
                        var side = UnityEngine.Random.insideUnitCircle.normalized * (chargeRingSize.x * 0.5f);
                        var from = at + new Vector3(side.x, 0.1f, side.y);
                        Emit(chargeMotes, from, (at + Vector3.up * 0.4f - from) * 2.2f);
                    }

                    await Awaitable.NextFrameAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
                // シーンを離れた: 粒ごと消えるので片付けない。
                return;
            }
            finally
            {
                _charging--;
            }

            if (_charging == 0)
            {
                // 爆発しなかった（ステージが先に終わった）か爆発した: 輪を消す。
                HideRings();
            }
        }

        // 生きている味方の足元に輪を置く（味方に付いて動く）。progress は溜めの進み具合（0〜1）。
        private void PlaceRings(float progress)
        {
            var field = battle.Field;
            _ringCount = 0;
            if (field == null || fieldView.Map == null)
            {
                return;
            }

            var eased = progress * progress;
            var size = Mathf.Lerp(chargeRingSize.x, chargeRingSize.y, eased);
            var color = chargeRingColor;
            color.a *= Mathf.Lerp(0.5f, 1f, progress);
            foreach (var unit in field.Units)
            {
                if (_ringCount >= MaxUnits)
                {
                    break;
                }

                if (!unit.IsAlive)
                {
                    continue;
                }

                var position = fieldView.CellTopToWorld(unit.Cell) + Vector3.up * 0.04f;
                _ringPositions[_ringCount] = position;
                _ringParticles[_ringCount] = new ParticleSystem.Particle
                {
                    position = position,
                    startSize = size,
                    rotation = _ringCount * 41f + progress * 120f,
                    startColor = color,
                    startLifetime = 100f,
                    remainingLifetime = 100f,
                };
                _ringCount++;
            }

            if (!chargeRing.isPlaying)
            {
                chargeRing.Play();
            }

            chargeRing.SetParticles(_ringParticles, _ringCount);
        }

        private void HideRings()
        {
            chargeRing.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _ringCount = 0;
        }

        // Core が攻撃力アップを始めた: 味方ごとに闘気を爆発させ、弱く揺らす。
        private void OnBurst()
        {
            BurstCount++;
            var field = battle.Field;
            if (field == null || fieldView.Map == null)
            {
                return;
            }

            HideRings();
            chargeMotes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var shown = 0;
            foreach (var unit in field.Units)
            {
                if (!unit.IsAlive || shown++ >= MaxUnits)
                {
                    continue;
                }

                var at = fieldView.CellTopToWorld(unit.Cell);
                Emit(burstRing, at + Vector3.up * 0.05f, Vector3.zero);
                for (var k = 0; k < flamesPerUnit; k++)
                {
                    var side = UnityEngine.Random.insideUnitCircle * 0.9f;
                    Emit(burstFlames, at + new Vector3(side.x * 0.3f, 0.2f, side.y * 0.3f),
                        new Vector3(side.x, UnityEngine.Random.Range(2.5f, 4.5f), side.y));
                }

                for (var k = 0; k < sparksPerUnit; k++)
                {
                    var side = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(1.5f, 3f);
                    Emit(burstSparks, at + Vector3.up * 0.4f, new Vector3(side.x, UnityEngine.Random.Range(1.5f, 3.5f), side.y));
                }
            }

            _shake.Shake(shakeSeconds, shakeAmplitude);
        }

        // 味方が攻撃した: 攻撃力アップの間だけ、その味方から火花を出す。
        private void OnUnitAttacked(BattleUnit unit, BattleEnemy enemy)
        {
            var field = battle.Field;
            if (field?.Effects == null || !field.Effects.IsAttackUp || fieldView.Map == null)
            {
                return;
            }

            SparkCount++;
            var at = fieldView.CellTopToWorld(unit.Cell) + Vector3.up * auraHeight;
            for (var k = 0; k < sparksPerAttack; k++)
            {
                var side = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(1f, 2.2f);
                Emit(attackSparks, at, new Vector3(side.x, UnityEngine.Random.Range(1f, 2.5f), side.y));
            }
        }

        // 攻撃力アップの間は毎フレーム生きている味方にオーラを置き、切れたら消す。
        private void LateUpdate()
        {
            var field = battle.Field;
            var powered = field?.Effects != null && field.Effects.IsAttackUp && fieldView.Map != null;
            if (powered)
            {
                PlaceAura(field);
            }
            else if (_auraShown)
            {
                FadeCount++;
                for (var i = 0; i < _auraCount; i++)
                {
                    Emit(auraFlames, _auraPositions[i], Vector3.up * 1.2f);
                }

                HideAura();
            }
        }

        private void PlaceAura(Battlefield field)
        {
            _auraCount = 0;
            foreach (var unit in field.Units)
            {
                if (_auraCount >= MaxUnits)
                {
                    break;
                }

                if (!unit.IsAlive)
                {
                    continue;
                }

                var position = fieldView.CellTopToWorld(unit.Cell) + Vector3.up * auraHeight;
                var flicker = 1f + 0.08f * Mathf.Sin(Time.time * 11f + _auraCount * 1.7f);
                _auraPositions[_auraCount] = position;
                _auraParticles[_auraCount] = new ParticleSystem.Particle
                {
                    position = position,
                    startSize = auraSize * flicker,
                    rotation = _auraCount * 37f,
                    startColor = auraColor,
                    startLifetime = 100f,
                    remainingLifetime = 100f,
                };
                _auraCount++;
            }

            if (!aura.isPlaying)
            {
                aura.Play();
            }

            aura.SetParticles(_auraParticles, _auraCount);
            _auraShown = true;

            // 立ちのぼる炎。
            _flameDebt += auraFlamesPerSecond * _auraCount * Time.deltaTime;
            for (; _flameDebt >= 1f && _auraCount > 0; _flameDebt -= 1f)
            {
                var at = _auraPositions[UnityEngine.Random.Range(0, _auraCount)];
                var offset = UnityEngine.Random.insideUnitCircle * 0.3f;
                Emit(auraFlames, at + new Vector3(offset.x, -0.3f, offset.y), Vector3.up * UnityEngine.Random.Range(0.8f, 1.5f));
            }
        }

        private void HideAura()
        {
            aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _auraShown = false;
            _auraCount = 0;
            _flameDebt = 0f;
        }

        private static void Emit(ParticleSystem system, Vector3 position, Vector3 velocity) =>
            system.Emit(new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                applyShapeToPosition = false,
            }, 1);

        private static int Count(ParticleSystem system) => system != null ? system.particleCount : 0;
    }
}
