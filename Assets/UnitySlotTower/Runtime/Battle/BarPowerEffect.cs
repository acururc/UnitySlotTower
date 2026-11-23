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
        [Tooltip("足元の縮まる魔法陣（味方に付いて動く、spec 008 FR-004）。位置と大きさは実行時に毎フレーム入れる。")]
        [SerializeField] private ParticleSystem chargeRing;
        [Tooltip("足元の膨らむ光の芯（味方に付いて動く）。位置と大きさは実行時に毎フレーム入れる。")]
        [SerializeField] private ParticleSystem chargeCore;
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
        [Tooltip("爆発と攻撃の閃光（放射状の光）。")]
        [SerializeField] private ParticleSystem burstFlash;
        [Tooltip("爆発で味方から立つ炎の柱。")]
        [SerializeField] private ParticleSystem burstPillar;
        [Tooltip("効果の間に舞い上がる火の粉。")]
        [SerializeField] private ParticleSystem auraEmbers;

        [Header("溜め（足元の輪）")]
        [Tooltip("輪の大きさ（マス = 1）。揃った瞬間 → 爆発の瞬間。")]
        [SerializeField] private Vector2 chargeRingSize = new Vector2(4f, 1.2f);
        [SerializeField] private Color chargeRingColor = new Color(1f, 0.8f, 0.3f, 1f);
        [Tooltip("溜めの間に魔法陣が回る回数（爆発に近づくほど速い）。")]
        [SerializeField, Min(0f)] private float chargeCircleTurns = 1.5f;
        [Tooltip("足元の光の芯の大きさ（爆発の直前、マス = 1）。溜めにつれて膨らむ。")]
        [SerializeField, Min(0f)] private float chargeCoreSize = 1.6f;
        [SerializeField] private Color chargeCoreColor = new Color(1f, 0.95f, 0.7f, 1f);
        [Tooltip("吸い込まれる粒の数（毎秒、味方1体あたり）。爆発の直前ほど増える。")]
        [SerializeField, Min(0f)] private float motesPerSecond = 18f;
        [Tooltip("粒が集まってくる距離（マス = 1）。溜めの始め → 爆発の直前（近くから遠くへ広げる）。")]
        [SerializeField] private Vector2 moteReach = new Vector2(0.8f, 2.6f);
        [Tooltip("粒が芯へ入るときの渦の強さ。")]
        [SerializeField, Min(0f)] private float moteSwirl = 1.2f;

        [Header("爆発")]
        [SerializeField, Min(0)] private int flamesPerUnit = 30;
        [SerializeField, Min(0)] private int sparksPerUnit = 28;
        [Tooltip("炎が昇りながら巻く回数と、横向きの速さ（渦の強さ）。")]
        [SerializeField, Min(0f)] private float flameTurns = 2f;
        [SerializeField, Min(0f)] private float flameSwirl = 2.5f;
        [Tooltip("閃光の大きさ（マス = 1）。")]
        [SerializeField, Min(0f)] private float flashSize = 4.5f;
        [Tooltip("炎の柱の幅と高さ（マス = 1）。")]
        [SerializeField] private Vector2 pillarSize = new Vector2(1.6f, 5f);

        [Header("オーラ")]
        [Tooltip("オーラの光の大きさ（ワールド、マス = 1）。")]
        [SerializeField, Min(0f)] private float auraSize = 1.7f;
        [Tooltip("オーラの中心の、味方の足元からの高さ。")]
        [SerializeField] private float auraHeight = 0.5f;
        [SerializeField] private Color auraColor = new Color(1f, 0.55f, 0.15f, 0.7f);
        [Tooltip("立ちのぼる炎の数（毎秒、味方1体あたり）。続く効果なので、爆発より控えめにして戦闘の邪魔をしない。")]
        [SerializeField, Min(0f)] private float auraFlamesPerSecond = 16f;
        [Tooltip("舞い上がる火の粉の数（毎秒、味方1体あたり）。")]
        [SerializeField, Min(0f)] private float embersPerSecond = 8f;
        [Tooltip("攻撃したときの火花の数。")]
        [SerializeField, Min(0)] private int sparksPerAttack = 12;
        [Tooltip("攻撃したときの閃光の大きさ（マス = 1）。")]
        [SerializeField, Min(0f)] private float attackFlashSize = 1.6f;

        [Header("揺れ")]
        [SerializeField, Min(0f)] private float shakeSeconds = 0.2f;
        [Tooltip("揺れの幅（ワールド）。黄7と同じくらい（spec 007 D-04）。")]
        [SerializeField, Min(0f)] private float shakeAmplitude = 0.06f;

        private readonly ParticleSystem.Particle[] _ringParticles = new ParticleSystem.Particle[MaxUnits];
        private readonly ParticleSystem.Particle[] _coreParticles = new ParticleSystem.Particle[MaxUnits];
        private readonly ParticleSystem.Particle[] _auraParticles = new ParticleSystem.Particle[MaxUnits];
        private readonly Vector3[] _ringPositions = new Vector3[MaxUnits];
        private readonly Vector3[] _auraPositions = new Vector3[MaxUnits];
        private int _ringCount;
        private int _auraCount;
        private bool _auraShown;
        private float _flameDebt;
        private float _emberDebt;
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
            Count(chargeRing) + Count(chargeCore) + Count(chargeMotes) + Count(burstFlames) + Count(burstRing) + Count(burstSparks) +
            Count(aura) + Count(auraFlames) + Count(attackSparks) + Count(burstFlash) + Count(burstPillar) + Count(auraEmbers);

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

                    // 吸い込まれる粒: 始めは近くから、溜まるにつれて遠くから集まり、渦を巻いて芯へ入る。爆発の直前ほど多い。
                    moteDebt += motesPerSecond * (0.5f + 1.5f * progress) * _ringCount * Time.deltaTime;
                    var reach = Mathf.Lerp(moteReach.x, moteReach.y, progress);
                    for (; moteDebt >= 1f && _ringCount > 0; moteDebt -= 1f)
                    {
                        var at = _ringPositions[UnityEngine.Random.Range(0, _ringCount)];
                        var side = UnityEngine.Random.insideUnitCircle.normalized * (reach * UnityEngine.Random.Range(0.8f, 1f));
                        var from = at + new Vector3(side.x, UnityEngine.Random.Range(0.1f, 0.6f), side.y);
                        var swirl = new Vector3(-side.y, 0f, side.x) * moteSwirl;
                        Emit(chargeMotes, from, (at + Vector3.up * 0.4f - from) * 2.2f + swirl);
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
            // 芯: 溜めにつれて膨らみ、爆発の直前は速く脈打つ。
            var throb = 1f + 0.15f * progress * Mathf.Sin(Time.time * (12f + 30f * progress));
            var coreSize = chargeCoreSize * eased * throb;
            var coreColor = chargeCoreColor;
            coreColor.a *= Mathf.Lerp(0.3f, 1f, progress);
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
                // 魔法陣（spec 008 FR-004）: 縮まりながら回り、爆発に近づくほど速く回る。
                _ringParticles[_ringCount] = new ParticleSystem.Particle
                {
                    position = position,
                    startSize = size,
                    rotation = _ringCount * 41f + progress * progress * chargeCircleTurns * 360f,
                    startColor = color,
                    startLifetime = 100f,
                    remainingLifetime = 100f,
                };
                _coreParticles[_ringCount] = new ParticleSystem.Particle
                {
                    position = position + Vector3.up * 0.01f,
                    startSize = coreSize,
                    rotation = -_ringCount * 41f - progress * 360f,
                    startColor = coreColor,
                    startLifetime = 100f,
                    remainingLifetime = 100f,
                };
                _ringCount++;
            }

            SetLayer(chargeRing, _ringParticles, _ringCount);
            SetLayer(chargeCore, _coreParticles, _ringCount);
        }

        private static void SetLayer(ParticleSystem system, ParticleSystem.Particle[] particles, int count)
        {
            if (system == null)
            {
                return;
            }

            if (!system.isPlaying)
            {
                system.Play();
            }

            system.SetParticles(particles, count);
        }

        private void HideRings()
        {
            chargeRing.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (chargeCore != null)
            {
                chargeCore.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

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
                EmitSized(burstFlash, at + Vector3.up * 0.6f, flashSize);
                burstPillar.Emit(new ParticleSystem.EmitParams
                {
                    position = at + Vector3.up * (pillarSize.y * 0.45f),
                    startSize3D = new Vector3(pillarSize.x, pillarSize.y, 1f),
                    applyShapeToPosition = false,
                }, 1);
                // 炎は渦を巻いて昇る（竜巻のように、太い所と細い所を作ってシルエットを単調にしない）。
                for (var k = 0; k < flamesPerUnit; k++)
                {
                    var angle = k * (Mathf.PI * 2f * flameTurns / Mathf.Max(1, flamesPerUnit)) + UnityEngine.Random.Range(-0.3f, 0.3f);
                    var radius = 0.35f + 0.3f * Mathf.Abs(Mathf.Sin(k * 0.7f));
                    var out1 = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var tangent = new Vector3(-out1.z, 0f, out1.x);
                    Emit(burstFlames, at + out1 * radius + Vector3.up * 0.2f,
                        Vector3.up * UnityEngine.Random.Range(3.5f, 7f) + tangent * flameSwirl + out1 * UnityEngine.Random.Range(0.2f, 1f));
                }

                for (var k = 0; k < sparksPerUnit; k++)
                {
                    var side = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(2.5f, 5f);
                    Emit(burstSparks, at + Vector3.up * 0.5f, new Vector3(side.x, UnityEngine.Random.Range(2f, 5.5f), side.y));
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
            EmitSized(burstFlash, at, attackFlashSize);
            for (var k = 0; k < sparksPerAttack; k++)
            {
                var side = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(1.5f, 3.2f);
                Emit(attackSparks, at, new Vector3(side.x, UnityEngine.Random.Range(1.5f, 3.5f), side.y));
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
                var flicker = 1f + 0.12f * Mathf.Sin(Time.time * 11f + _auraCount * 1.7f) + 0.06f * Mathf.Sin(Time.time * 23f + _auraCount);
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
                Emit(auraFlames, at + new Vector3(offset.x, -0.35f, offset.y), Vector3.up * UnityEngine.Random.Range(1.4f, 2.4f));
            }

            // 舞い上がる火の粉。
            _emberDebt += embersPerSecond * _auraCount * Time.deltaTime;
            for (; _emberDebt >= 1f && _auraCount > 0; _emberDebt -= 1f)
            {
                var at = _auraPositions[UnityEngine.Random.Range(0, _auraCount)];
                var offset = UnityEngine.Random.insideUnitCircle * 0.5f;
                Emit(auraEmbers, at + new Vector3(offset.x, 0f, offset.y),
                    new Vector3(offset.x * 0.6f, UnityEngine.Random.Range(1.5f, 3f), offset.y * 0.6f));
            }
        }

        private void HideAura()
        {
            aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _auraShown = false;
            _auraCount = 0;
            _flameDebt = 0f;
            _emberDebt = 0f;
        }

        private static void EmitSized(ParticleSystem system, Vector3 position, float size) =>
            system.Emit(new ParticleSystem.EmitParams
            {
                position = position,
                startSize = size,
                rotation = UnityEngine.Random.Range(0f, 360f),
                applyShapeToPosition = false,
            }, 1);

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
