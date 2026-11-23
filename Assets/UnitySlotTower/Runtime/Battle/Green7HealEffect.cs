using System;
using UnityEngine;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 緑7の回復の演出（spec 005 FR-001、plan 3章）。3D のフィールドの上に置いたパーティクルを戦闘カメラで写す。
    /// 揃うと 予兆（盤面の奥の端が緑に光る）→ 光の粒が盤面の上空から降り注ぐ と進み、Core が回復を入れた瞬間
    /// （<see cref="BattleController.GreenHealed"/>）に、生きている味方ごとに光の柱と足元の光の輪が立ち、粒が上がって消える。
    /// 揺れとヒットストップはない（FR-003）。時間は戦闘の時間（Time.deltaTime）。
    /// パーティクルは Setup（Build Green7 Heal Effect）が作るので Inspector で調整できる。
    /// </summary>
    public sealed class Green7HealEffect : MonoBehaviour
    {
        // 柱が来ないとき（ステージが先に終わった）に降る光を止めるまでの、待ち時間を過ぎてからの余裕（秒）。
        private const float HealGrace = 0.5f;

        // 1回の柱で光を出す味方の上限（粒の数を抑える）。
        private const int MaxUnits = 16;

        [SerializeField] private SlotCabinet cabinet;
        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;

        [Header("パーティクル（Setup が作る）")]
        [SerializeField] private ParticleSystem warning;
        [SerializeField] private ParticleSystem rain;
        [SerializeField] private ParticleSystem flash;
        [SerializeField] private ParticleSystem pillars;
        [SerializeField] private ParticleSystem rings;
        [SerializeField] private ParticleSystem motes;
        [Tooltip("盤面全体の魔法陣の外・中・内の層（spec 008 FR-003）。位置と大きさは実行時に入れる。")]
        [SerializeField] private ParticleSystem circleOuter;
        [SerializeField] private ParticleSystem circleMiddle;
        [SerializeField] private ParticleSystem circleInner;

        [Header("盤面全体の魔法陣（spec 008）")]
        [Tooltip("魔法陣の大きさ（盤面の長い辺に対する倍率）。")]
        [SerializeField, Min(0f)] private float circleScale = 1.05f;
        [Tooltip("層ごとの現れ始めと現れ終わり（待ち時間に対する割合）。外・中・内の順。")]
        [SerializeField] private Vector2 outerReveal = new Vector2(0f, 0.35f);
        [SerializeField] private Vector2 middleReveal = new Vector2(0.25f, 0.6f);
        [SerializeField] private Vector2 innerReveal = new Vector2(0.5f, 0.85f);
        [Tooltip("層ごとの回る速さ（度/秒）。外・中・内。向きを変えて重ねる。")]
        [SerializeField] private Vector3 circleSpin = new Vector3(18f, -32f, 55f);
        [SerializeField] private Color circleOuterColor = new Color(0.35f, 1f, 0.5f, 0.8f);
        [SerializeField] private Color circleMiddleColor = new Color(0.55f, 1f, 0.65f, 0.8f);
        [SerializeField] private Color circleInnerColor = new Color(0.85f, 1f, 0.85f, 0.95f);
        [Tooltip("回復の瞬間に強く光るときの色（広がりながら消える）。")]
        [SerializeField] private Color circleFlashColor = new Color(0.85f, 1f, 0.85f, 1f);
        [Tooltip("強く光って消えるまでの秒数。")]
        [SerializeField, Min(0.05f)] private float circleFlashSeconds = 0.6f;

        [Header("流れ（揃ってから光の柱までの待ち時間に対する割合）")]
        [Tooltip("予兆の光を出し終える時点。")]
        [SerializeField, Range(0f, 1f)] private float warningEnd = 0.4f;
        [Tooltip("光が降り始める時点。")]
        [SerializeField, Range(0f, 0.95f)] private float rainStart = 0.2f;
        [Tooltip("揃った瞬間に予兆の光をまとめて出す数。")]
        [SerializeField, Min(0)] private int warningBurst = 16;

        [Header("降り注ぐ光")]
        [Tooltip("光が降り始める高さ（ワールド、マス = 1）。")]
        [SerializeField] private float rainHeight = 5f;

        [Header("光の柱")]
        [Tooltip("柱の高さと太さ（ワールド）。")]
        [SerializeField, Min(0f)] private float pillarHeight = 4f;
        [SerializeField, Min(0f)] private float pillarWidth = 0.9f;
        [Tooltip("足元の光の輪の大きさ（ワールド）。")]
        [SerializeField, Min(0f)] private float ringSize = 1.6f;
        [SerializeField, Min(0)] private int motesPerUnit = 8;
        [Tooltip("閃光の大きさ（盤面の長い辺に対する倍率）。")]
        [SerializeField, Min(0f)] private float flashScale = 1.4f;

        private readonly ParticleSystem.Particle[] _circleParticle = new ParticleSystem.Particle[1];
        private int _showering;
        private int _expectedHeals;
        private float _circleAngle;
        private bool _circleShown;

        /// <summary>魔法陣が出ている（回復を待っている間）。</summary>
        public bool IsCircleShown => _circleShown;

        /// <summary>回復の瞬間に魔法陣が強く光った回数（テスト用）。</summary>
        public int CircleFlashCount { get; private set; }

        /// <summary>緑7で始めた演出の数（テスト用）。</summary>
        public int PlayCount { get; private set; }

        /// <summary>光の柱が立った数（テスト用）。</summary>
        public int HealCount { get; private set; }

        /// <summary>光が降っている（予兆を含む）。</summary>
        public bool IsShowering => _showering > 0;

        /// <summary>演出中か（降る光・残っている粒のどれか）。</summary>
        public bool IsPlaying => IsShowering || ActiveParticleCount > 0;

        /// <summary>出ている粒の数（テスト用。演出が終われば 0）。</summary>
        public int ActiveParticleCount =>
            Count(warning) + Count(rain) + Count(flash) + Count(pillars) + Count(rings) + Count(motes) +
            Count(circleOuter) + Count(circleMiddle) + Count(circleInner);

        /// <summary>出ている光の柱の数（テスト用）。</summary>
        public int PillarCount => Count(pillars);

        private void OnEnable()
        {
            cabinet.Aligned += OnAligned;
            battle.GreenHealed += OnHealed;
            battle.NodeBegan += OnNodeBegan;
        }

        private void OnDisable()
        {
            cabinet.Aligned -= OnAligned;
            battle.GreenHealed -= OnHealed;
            battle.NodeBegan -= OnNodeBegan;
        }

        // 前のステージで来なかった柱を待たない。
        private void OnNodeBegan() => _expectedHeals = HealCount;

        private void OnAligned(SlotSymbol symbol)
        {
            var field = battle.Field;
            if (symbol != SlotSymbol.Green7 || field?.Effects == null || fieldView.Map == null)
            {
                return;
            }

            PlayCount++;
            var delay = (float)field.Effects.GreenDelaySteps / SimulationClock.StepsPerSecond;
            _ = Shower(delay, ++_expectedHeals);
        }

        private async Awaitable Shower(float delay, int healIndex)
        {
            _showering++;
            var token = destroyCancellationToken;
            try
            {
                Measure(out var far, out var center, out var forward, out var width, out var depth);
                warning.transform.SetPositionAndRotation(far + Vector3.up * 0.2f, Quaternion.LookRotation(forward, Vector3.up));
                var warningShape = warning.shape;
                warningShape.scale = new Vector3(width, 0.1f, 0.4f);
                warning.Play();
                warning.Emit(warningBurst); // 揃った瞬間から光る。

                rain.transform.SetPositionAndRotation(center + Vector3.up * rainHeight, Quaternion.LookRotation(forward, Vector3.up));
                var rainShape = rain.shape;
                rainShape.scale = new Vector3(width, 0.1f, depth);
                var raining = false;
                _circleAngle = 0f;
                for (var t = 0f; HealCount < healIndex && t < delay + HealGrace; t += Time.deltaTime)
                {
                    PlaceCircles(center, Mathf.Max(width, depth) * circleScale, delay > 0f ? Mathf.Clamp01(t / delay) : 1f);

                    if (t >= warningEnd * delay && warning.isEmitting)
                    {
                        warning.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }

                    if (!raining && t >= rainStart * delay)
                    {
                        raining = true;
                        rain.Play();
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
                _showering--;
            }

            if (_showering == 0)
            {
                warning.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (_circleShown)
                {
                    // 柱が来なかった（ステージが先に終わった）: 魔法陣はそっと消す。
                    HideCircles();
                }
            }
        }

        // 盤面の中心に魔法陣の層を置く。progress は待ちの進み具合（0〜1）。外→中→内の順に現れ、層ごとに逆向きに回る。
        private void PlaceCircles(Vector3 center, float size, float progress)
        {
            _circleAngle += Time.deltaTime * (1f + progress);
            var bright = Mathf.Lerp(0.6f, 1f, progress);
            var at = center + Vector3.up * 0.06f;
            PlaceLayer(circleOuter, at, size, Reveal(progress, outerReveal), circleSpin.x, circleOuterColor, bright, 100f);
            PlaceLayer(circleMiddle, at + Vector3.up * 0.01f, size, Reveal(progress, middleReveal), circleSpin.y, circleMiddleColor, bright, 100f);
            PlaceLayer(circleInner, at + Vector3.up * 0.02f, size, Reveal(progress, innerReveal), circleSpin.z, circleInnerColor, bright, 100f);
            _circleShown = true;
        }

        private static float Reveal(float progress, Vector2 window) =>
            Mathf.Clamp01((progress - window.x) / Mathf.Max(0.01f, window.y - window.x));

        // 層の粒を1つ置く。appear（0〜1）で 1.15 倍から縮んで定まり、濃くなる。lifetime が短いと、置いた後は大きく広がりながら消える（Setup の色と大きさの変化）。
        private void PlaceLayer(ParticleSystem layer, Vector3 at, float size, float appear, float spin, Color color, float bright, float lifetime)
        {
            if (layer == null)
            {
                return;
            }

            var eased = 1f - (1f - appear) * (1f - appear);
            color.a *= appear * bright;
            _circleParticle[0] = new ParticleSystem.Particle
            {
                position = at,
                startSize = size * Mathf.Lerp(1.15f, 1f, eased),
                rotation = spin * _circleAngle,
                startColor = color,
                startLifetime = lifetime,
                remainingLifetime = lifetime,
            };
            if (!layer.isPlaying)
            {
                layer.Play();
            }

            layer.SetParticles(_circleParticle, 1);
        }

        private void HideCircles()
        {
            foreach (var layer in new[] { circleOuter, circleMiddle, circleInner })
            {
                if (layer != null)
                {
                    layer.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            _circleShown = false;
        }

        // 回復の瞬間: 魔法陣が強く光り、広がりながら消える（待ちが 0 でも出す）。
        private void FlashCircles(Vector3 center, float size)
        {
            CircleFlashCount++;
            var at = center + Vector3.up * 0.06f;
            PlaceLayer(circleOuter, at, size, 1f, circleSpin.x, circleFlashColor, 1f, circleFlashSeconds);
            PlaceLayer(circleMiddle, at + Vector3.up * 0.01f, size, 1f, circleSpin.y, circleFlashColor, 1f, circleFlashSeconds);
            PlaceLayer(circleInner, at + Vector3.up * 0.02f, size, 1f, circleSpin.z, circleFlashColor, 1f, circleFlashSeconds);
            _circleShown = false;
        }

        // Core が回復を入れた: 生きている味方ごとに光の柱を立てる。
        private void OnHealed()
        {
            HealCount++;
            var field = battle.Field;
            if (field == null || fieldView.Map == null)
            {
                return;
            }

            warning.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            Measure(out _, out var center, out _, out var width, out var depth);
            FlashCircles(center, Mathf.Max(width, depth) * circleScale);
            flash.Emit(new ParticleSystem.EmitParams
            {
                position = center + Vector3.up,
                startSize = Mathf.Max(width, depth) * flashScale,
                applyShapeToPosition = false,
            }, 1);

            var shown = 0;
            foreach (var unit in field.Units)
            {
                if (!unit.IsAlive || shown++ >= MaxUnits)
                {
                    continue;
                }

                var ground = fieldView.CellToWorld(unit.Cell);
                pillars.Emit(new ParticleSystem.EmitParams
                {
                    position = ground + Vector3.up * (pillarHeight * 0.5f),
                    startSize3D = new Vector3(pillarWidth, pillarHeight, 1f),
                    applyShapeToPosition = false,
                }, 1);
                rings.Emit(new ParticleSystem.EmitParams
                {
                    position = ground + Vector3.up * 0.05f,
                    startSize = ringSize,
                    applyShapeToPosition = false,
                }, 1);
                for (var i = 0; i < motesPerUnit; i++)
                {
                    var side = UnityEngine.Random.insideUnitCircle * 0.4f;
                    motes.Emit(new ParticleSystem.EmitParams
                    {
                        position = ground + new Vector3(side.x, UnityEngine.Random.Range(0.1f, 0.8f), side.y),
                        velocity = Vector3.up * UnityEngine.Random.Range(1f, 2.2f),
                        applyShapeToPosition = false,
                    }, 1);
                }
            }
        }

        // 盤面の奥の端の中心と盤面の中心（地面の高さ）、奥の向き、横幅と奥行き（ワールド）。
        private void Measure(out Vector3 far, out Vector3 center, out Vector3 forward, out float width, out float depth)
        {
            var map = fieldView.Map;
            var midX = (map.Width - 1) * 0.5;
            far = fieldView.PositionToWorld(midX, -0.5, false);
            var near = fieldView.PositionToWorld(midX, map.Height - 0.5, false);
            center = (far + near) * 0.5f;
            var left = fieldView.PositionToWorld(-0.5, -0.5, false);
            var rightEdge = fieldView.PositionToWorld(map.Width - 0.5, -0.5, false);
            forward = (far - near).normalized;
            width = Vector3.Distance(left, rightEdge);
            depth = Vector3.Distance(far, near);
        }

        private static int Count(ParticleSystem system) => system != null ? system.particleCount : 0;
    }
}
