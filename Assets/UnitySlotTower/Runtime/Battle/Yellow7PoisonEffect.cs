using System;
using UnityEngine;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 黄7の毒の演出（spec 006 FR-001・FR-002・FR-004、plan 3章）。<see cref="Blue7FreezeEffect"/> と同じく 3D のフィールドの上の
    /// パーティクルを戦闘カメラで写す。揃うと盤面の地面のあちこちと敵の足元（敵に付いて動く）に毒の沼が湧き、泡がぼこぼこ立つ。Core が毒を始めた瞬間
    /// （<see cref="BattleController.YellowBurst"/>）に沼が弾けて飛沫と毒の霧が上がり、カメラが弱く揺れる（ヒットストップなし）。
    /// 毒の間は敵ごとに紫のオーラ（粒）を付け、毒のダメージ（<see cref="BattleController.PoisonTicked"/>）ごとに点滅させる。
    /// 毒が切れたらオーラを消す。時間は戦闘の時間（Time.deltaTime）。パーティクルは Setup（Build Yellow7 Poison Effect）が作る。
    /// </summary>
    public sealed class Yellow7PoisonEffect : MonoBehaviour
    {
        // 弾けるのが来ないとき（ステージが先に終わった）に沼を止めるまでの、待ち時間を過ぎてからの余裕（秒）。
        private const float BurstGrace = 0.5f;

        // 沼とオーラの上限。
        private const int MaxSwamps = 24;
        private const int MaxAuras = 64;

        [SerializeField] private SlotCabinet cabinet;
        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private Camera battleCamera;

        [Header("パーティクル（Setup が作る）")]
        [SerializeField] private ParticleSystem swamp;
        [Tooltip("敵の足元の沼（敵に付いて動く）。位置と大きさは実行時に毎フレーム入れる。")]
        [SerializeField] private ParticleSystem enemySwamp;
        [SerializeField] private ParticleSystem bubbles;
        [SerializeField] private ParticleSystem splash;
        [SerializeField] private ParticleSystem ring;
        [SerializeField] private ParticleSystem fumes;
        [SerializeField] private ParticleSystem aura;
        [SerializeField] private ParticleSystem tickBubbles;

        [Header("沼（揃ってから弾けるまでの待ち時間に対する割合）")]
        [Tooltip("盤面に湧く沼の数。")]
        [SerializeField, Range(1, MaxSwamps)] private int swampCount = 10;
        [Tooltip("最後の沼が湧く時点。沼はここまでに順に湧く。")]
        [SerializeField, Range(0.05f, 1f)] private float swampSpreadEnd = 0.6f;
        [Tooltip("沼の大きさ（マス = 1）。")]
        [SerializeField] private Vector2 swampSize = new Vector2(2.2f, 3.4f);
        [Tooltip("敵の足元の沼の大きさ（マス = 1）。")]
        [SerializeField, Min(0f)] private float enemySwampSize = 5f;
        [Tooltip("敵の足元の沼が広がりきるまでの秒数。")]
        [SerializeField, Min(0.01f)] private float enemySwampGrowSeconds = 0.35f;
        [SerializeField] private Color enemySwampColor = new Color(0.45f, 0.12f, 0.6f, 0.9f);
        [Tooltip("泡の数（毎秒、沼1つあたり）。弾ける直前ほど増える。")]
        [SerializeField, Min(0f)] private float bubblesPerSecond = 6f;

        [Header("弾ける")]
        [SerializeField, Min(0)] private int splashPerSwamp = 10;
        [SerializeField, Min(0)] private int fumesPerSwamp = 2;

        [Header("オーラ")]
        [Tooltip("オーラの大きさ（ワールド、マス = 1）。")]
        [SerializeField, Min(0f)] private float auraSize = 1.1f;
        [Tooltip("オーラの中心の地面（敵の足元）からの高さ。")]
        [SerializeField] private float auraHeight = 0.4f;
        [SerializeField] private Color auraColor = new Color(0.65f, 0.2f, 0.95f, 0.55f);
        [SerializeField] private Color auraFlashColor = new Color(0.95f, 0.6f, 1f, 0.95f);
        [Tooltip("毒のダメージで点滅している秒数。")]
        [SerializeField, Min(0.01f)] private float flashSeconds = 0.2f;
        [Tooltip("点滅の瞬間の大きさの倍率。")]
        [SerializeField, Min(1f)] private float flashScale = 1.35f;
        [SerializeField, Min(0)] private int tickBubblesPerEnemy = 3;

        [Header("揺れ")]
        [SerializeField, Min(0f)] private float shakeSeconds = 0.2f;
        [Tooltip("揺れの幅（ワールド）。青7より弱く（spec 006 D-04）。")]
        [SerializeField, Min(0f)] private float shakeAmplitude = 0.06f;

        private readonly ParticleSystem.Particle[] _auraParticles = new ParticleSystem.Particle[MaxAuras];
        private readonly Vector3[] _auraPositions = new Vector3[MaxAuras];
        private readonly Vector3[] _swampPositions = new Vector3[MaxSwamps];
        private readonly ParticleSystem.Particle[] _enemySwampParticles = new ParticleSystem.Particle[MaxAuras];
        private readonly Vector3[] _enemySwampPositions = new Vector3[MaxAuras];
        private int _swampsShown;
        private int _enemySwampCount;
        private int _auraCount;
        private bool _auraShown;
        private float _flashAt = float.NegativeInfinity;
        private int _bubbling;
        private int _expectedBursts;
        private BattleCameraShake _shake;

        /// <summary>黄7で湧かせた沼の回数（テスト用）。</summary>
        public int PlayCount { get; private set; }

        /// <summary>沼が弾けた数（テスト用）。</summary>
        public int BurstCount { get; private set; }

        /// <summary>毒のダメージでオーラが点滅した回数（テスト用）。</summary>
        public int FlashCount { get; private set; }

        /// <summary>毒が切れてオーラが消えた回数（テスト用）。</summary>
        public int FadeCount { get; private set; }

        /// <summary>沼が湧いて泡が立っている（弾ける前）。</summary>
        public bool IsBubbling => _bubbling > 0;

        /// <summary>出ている敵の足元の沼の数（沼が湧いている間だけ 0 より大きい）。</summary>
        public int EnemySwampCount => IsBubbling ? _enemySwampCount : 0;

        /// <summary>出ているオーラの数（毒の間だけ 0 より大きい）。</summary>
        public int AuraCount => _auraShown ? _auraCount : 0;

        /// <summary>カメラが揺れている。</summary>
        public bool IsShaking => _shake != null && _shake.IsShaking;

        /// <summary>演出中か（沼・オーラ・揺れ・残っている粒のどれか）。</summary>
        public bool IsPlaying => IsBubbling || _auraShown || IsShaking || ActiveParticleCount > 0;

        /// <summary>出ている粒の数（テスト用。演出が終われば 0）。</summary>
        public int ActiveParticleCount =>
            Count(swamp) + Count(enemySwamp) + Count(bubbles) + Count(splash) + Count(ring) + Count(fumes) + Count(aura) + Count(tickBubbles);

        private void Awake() => _shake = BattleCameraShake.Of(battleCamera);

        private void OnEnable()
        {
            cabinet.Aligned += OnAligned;
            battle.YellowBurst += OnBurst;
            battle.PoisonTicked += OnTicked;
            battle.NodeBegan += OnNodeBegan;
        }

        private void OnDisable()
        {
            cabinet.Aligned -= OnAligned;
            battle.YellowBurst -= OnBurst;
            battle.PoisonTicked -= OnTicked;
            battle.NodeBegan -= OnNodeBegan;
        }

        // 前のステージで来なかった弾けるのを待たない。前のステージのオーラは消す。
        private void OnNodeBegan()
        {
            _expectedBursts = BurstCount;
            HideAura();
            HideEnemySwamps();
        }

        private void OnAligned(SlotSymbol symbol)
        {
            var field = battle.Field;
            if (symbol != SlotSymbol.Yellow7 || field?.Effects == null || fieldView.Map == null)
            {
                return;
            }

            PlayCount++;
            _expectedBursts++;
            var delay = (float)field.Effects.YellowDelaySteps / SimulationClock.StepsPerSecond;
            // 待ちが 0 なら弾けるのは揃いと同時（ApplySlot の中）なので沼は湧かせない。
            if (delay > 0f)
            {
                _ = Bubble(delay, _expectedBursts);
            }
        }

        private async Awaitable Bubble(float delay, int burstIndex)
        {
            _bubbling++;
            _swampsShown = 0;
            var token = destroyCancellationToken;
            try
            {
                var map = fieldView.Map;
                var lifetime = delay + BurstGrace;
                var bubbleDebt = 0f;
                for (var t = 0f; BurstCount < burstIndex && t < delay + BurstGrace; t += Time.deltaTime)
                {
                    // 沼を順に湧かせる（最初の1つは揃った瞬間）。
                    var due = Mathf.Min(swampCount, 1 + Mathf.FloorToInt(swampCount * t / Mathf.Max(0.01f, swampSpreadEnd * delay)));
                    while (_swampsShown < due)
                    {
                        var position = RandomOnBoard(map) + Vector3.up * 0.03f;
                        _swampPositions[_swampsShown++] = position;
                        swamp.Emit(new ParticleSystem.EmitParams
                        {
                            position = position,
                            startSize = UnityEngine.Random.Range(swampSize.x, swampSize.y),
                            startLifetime = Mathf.Max(0.2f, lifetime - t),
                            applyShapeToPosition = false,
                        }, 1);
                    }

                    PlaceEnemySwamps(Mathf.Clamp01(t / enemySwampGrowSeconds));

                    // 泡: 弾ける直前ほど多く立つ。盤面の沼と敵の足元の沼の両方から。
                    var rise = 0.4f + 1.6f * Mathf.Clamp01(t / delay);
                    var swamps = _swampsShown + _enemySwampCount;
                    bubbleDebt += bubblesPerSecond * rise * swamps * Time.deltaTime;
                    for (; bubbleDebt >= 1f; bubbleDebt -= 1f)
                    {
                        var pick = UnityEngine.Random.Range(0, swamps);
                        var at = pick < _swampsShown ? _swampPositions[pick] : _enemySwampPositions[pick - _swampsShown];
                        var offset = UnityEngine.Random.insideUnitCircle * 0.5f;
                        Emit(bubbles, at + new Vector3(offset.x, 0.05f, offset.y), Vector3.up * UnityEngine.Random.Range(0.3f, 0.8f));
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
                _bubbling--;
            }

            if (_bubbling == 0)
            {
                // 弾けなかった（ステージが先に終わった）: 沼はそっと消す。
                swamp.Clear();
                HideEnemySwamps();
            }
        }

        // 生きている敵の足元に沼を置く（敵に付いて動く）。grow は広がり具合（0〜1）。
        private void PlaceEnemySwamps(float grow)
        {
            var field = battle.Field;
            _enemySwampCount = 0;
            if (field == null || fieldView.Map == null)
            {
                return;
            }

            var size = enemySwampSize * Mathf.Lerp(0.2f, 1f, grow);
            foreach (var enemy in field.Enemies)
            {
                if (_enemySwampCount >= MaxAuras)
                {
                    break;
                }

                if (!enemy.IsAlive)
                {
                    continue;
                }

                // 空の敵も真下の地面に置く。
                var position = fieldView.PositionToWorld(enemy.X, enemy.Y, false) + Vector3.up * 0.04f;
                _enemySwampPositions[_enemySwampCount] = position;
                _enemySwampParticles[_enemySwampCount] = new ParticleSystem.Particle
                {
                    position = position,
                    startSize = size * (1f + 0.05f * Mathf.Sin(Time.time * 7f + _enemySwampCount * 1.9f)),
                    rotation = _enemySwampCount * 53f,
                    startColor = enemySwampColor,
                    startLifetime = 100f,
                    remainingLifetime = 100f,
                };
                _enemySwampCount++;
            }

            if (!enemySwamp.isPlaying)
            {
                enemySwamp.Play();
            }

            enemySwamp.SetParticles(_enemySwampParticles, _enemySwampCount);
        }

        private void HideEnemySwamps()
        {
            enemySwamp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _enemySwampCount = 0;
        }

        // Core が毒を始めた: 沼を弾けさせ、飛沫と毒の霧を上げて弱く揺らす。
        private void OnBurst()
        {
            BurstCount++;
            if (battle.Field == null || fieldView.Map == null)
            {
                return;
            }

            swamp.Clear();
            bubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            // 待ちが 0 で沼がないときは盤面のあちこちで弾ける。
            if (_swampsShown == 0)
            {
                var map = fieldView.Map;
                for (var i = 0; i < swampCount; i++)
                {
                    _swampPositions[i] = RandomOnBoard(map) + Vector3.up * 0.03f;
                }

                _swampsShown = swampCount;
            }

            for (var i = 0; i < _swampsShown; i++)
            {
                BurstAt(_swampPositions[i]);
            }

            // 敵の足元の沼は、その敵の今いる場所で弾ける（待ちが 0 でも足元で弾ける）。
            PlaceEnemySwamps(1f);
            for (var i = 0; i < _enemySwampCount; i++)
            {
                BurstAt(_enemySwampPositions[i]);
            }

            HideEnemySwamps();
            _swampsShown = 0;
            _shake.Shake(shakeSeconds, shakeAmplitude);
        }

        private void BurstAt(Vector3 at)
        {
            Emit(ring, at, Vector3.zero);
            for (var k = 0; k < splashPerSwamp; k++)
            {
                var side = UnityEngine.Random.insideUnitCircle * 1.6f;
                Emit(splash, at + Vector3.up * 0.1f, new Vector3(side.x, UnityEngine.Random.Range(2.5f, 4.5f), side.y));
            }

            for (var k = 0; k < fumesPerSwamp; k++)
            {
                Emit(fumes, at + Vector3.up * 0.3f, Vector3.up * UnityEngine.Random.Range(0.3f, 0.7f));
            }
        }

        // 毒のダメージが入った: オーラを点滅させ、敵ごとに泡を立てる。
        private void OnTicked()
        {
            FlashCount++;
            _flashAt = Time.time;
            for (var i = 0; i < AuraCount; i++)
            {
                for (var k = 0; k < tickBubblesPerEnemy; k++)
                {
                    var offset = UnityEngine.Random.insideUnitCircle * 0.3f;
                    Emit(tickBubbles, _auraPositions[i] + new Vector3(offset.x, 0.2f, offset.y), Vector3.up * UnityEngine.Random.Range(0.6f, 1.2f));
                }
            }
        }

        // 毒の間は毎フレーム生きている敵にオーラを置き、毒が切れたら消す。
        private void LateUpdate()
        {
            var field = battle.Field;
            var poisoned = field?.Effects != null && field.Effects.IsPoisoned && fieldView.Map != null;
            if (poisoned)
            {
                PlaceAura(field);
            }
            else if (_auraShown)
            {
                FadeCount++;
                for (var i = 0; i < _auraCount; i++)
                {
                    Emit(fumes, _auraPositions[i], Vector3.up * 0.3f);
                }

                HideAura();
            }
        }

        private void PlaceAura(Battlefield field)
        {
            // 点滅: ダメージの瞬間に明るく大きくなり、flashSeconds で戻る。普段はゆっくり脈打つ。
            var flash = 1f - Mathf.Clamp01((Time.time - _flashAt) / flashSeconds);
            var color = Color.Lerp(auraColor, auraFlashColor, flash);
            _auraCount = 0;
            foreach (var enemy in field.Enemies)
            {
                if (_auraCount >= MaxAuras)
                {
                    break;
                }

                if (!enemy.IsAlive)
                {
                    continue;
                }

                var position = fieldView.PositionToWorld(enemy.X, enemy.Y, enemy.Definition.IsAir) + Vector3.up * auraHeight;
                var pulse = 1f + 0.06f * Mathf.Sin(Time.time * 6f + _auraCount * 1.3f);
                _auraPositions[_auraCount] = position;
                _auraParticles[_auraCount] = new ParticleSystem.Particle
                {
                    position = position,
                    startSize = auraSize * pulse * Mathf.Lerp(1f, flashScale, flash),
                    rotation = _auraCount * 37f,
                    startColor = color,
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
        }

        private void HideAura()
        {
            aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _auraShown = false;
            _auraCount = 0;
        }

        private Vector3 RandomOnBoard(BattleMap map) =>
            fieldView.PositionToWorld(UnityEngine.Random.Range(-0.5f, map.Width - 0.5f),
                UnityEngine.Random.Range(-0.5f, map.Height - 0.5f), false);

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
