using System;
using UnityEngine;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 赤7の息吹の演出（spec 003 FR-001・FR-002、plan 3章）。3D のフィールドの上に置いたパーティクルを戦闘カメラで写すので、
    /// 戦闘モニターの枠で切れる。揃うと 予兆（盤面の奥の端が赤く光る）→ 息吹（奥の上空から炎を吹き下ろし、狙いを奥から手前へ動かす）
    /// と進み、Core が赤7のダメージを入れた瞬間（<see cref="BattleController.RedStruck"/>）に盤面全体が爆発してカメラが揺れる。
    /// 時間は戦闘の時間（Time.deltaTime）。パーティクルは Setup（Build Red7 Breath Effect）が作るので Inspector で調整できる。
    /// </summary>
    public sealed class Red7BreathEffect : MonoBehaviour
    {
        // 爆発が来ないとき（ステージが先に終わった）に息吹を止めるまでの、待ち時間を過ぎてからの余裕（秒）。
        private const float StrikeGrace = 0.5f;

        [SerializeField] private SlotCabinet cabinet;
        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private Camera battleCamera;

        [Header("パーティクル（Setup が作る）")]
        [SerializeField] private ParticleSystem warning;
        [SerializeField] private ParticleSystem breath;
        [SerializeField] private ParticleSystem groundFire;
        [SerializeField] private ParticleSystem flash;
        [SerializeField] private ParticleSystem blasts;
        [SerializeField] private ParticleSystem hits;
        [SerializeField] private ParticleSystem embers;
        [SerializeField] private ParticleSystem smoke;

        [Header("流れ（揃ってから爆発までの待ち時間に対する割合）")]
        [Tooltip("予兆の光を出し終える時点。")]
        [SerializeField, Range(0f, 1f)] private float warningEnd = 0.4f;
        [Tooltip("息吹を吹き始める時点。")]
        [SerializeField, Range(0f, 0.95f)] private float breathStart = 0.25f;
        [Tooltip("揃った瞬間に予兆の光をまとめて出す数。")]
        [SerializeField, Min(0)] private int warningBurst = 16;

        [Header("息吹")]
        [Tooltip("吹き出し口の高さと、盤面の奥の端から後ろへの距離（ワールド、マス = 1）。")]
        [SerializeField] private float nozzleHeight = 6f;
        [SerializeField] private float nozzleBack = 3f;
        [Tooltip("炎が吹き出し口から着地点まで飛ぶ秒数（粒の速さを決める）。")]
        [SerializeField, Min(0.05f)] private float flightSeconds = 0.3f;

        [Header("爆発")]
        [SerializeField, Min(0)] private int blastCount = 16;
        [SerializeField, Min(0)] private int hitsPerEnemy = 2;
        [SerializeField, Min(0)] private int emberCount = 80;
        [SerializeField, Min(0)] private int smokeCount = 14;
        [Tooltip("閃光の大きさ（盤面の長い辺に対する倍率）。")]
        [SerializeField, Min(0f)] private float flashScale = 1.6f;

        [Header("揺れ")]
        [SerializeField, Min(0f)] private float shakeSeconds = 0.4f;
        [Tooltip("揺れの幅（ワールド）。")]
        [SerializeField, Min(0f)] private float shakeAmplitude = 0.25f;

        private int _breathing;
        private int _expectedStrikes;
        private BattleCameraShake _shake;

        /// <summary>赤7で始めた息吹の数（テスト用）。</summary>
        public int PlayCount { get; private set; }

        /// <summary>爆発した数（テスト用）。</summary>
        public int StrikeCount { get; private set; }

        /// <summary>息吹を吹いている（予兆を含む）。</summary>
        public bool IsBreathing => _breathing > 0;

        /// <summary>カメラが揺れている。</summary>
        public bool IsShaking => _shake != null && _shake.IsShaking;

        /// <summary>演出中か（息吹・揺れ・残っている粒のどれか）。</summary>
        public bool IsPlaying => IsBreathing || IsShaking || ActiveParticleCount > 0;

        /// <summary>出ている粒の数（テスト用。演出が終われば 0）。</summary>
        public int ActiveParticleCount =>
            Count(warning) + Count(breath) + Count(groundFire) + Count(flash) + Count(blasts) + Count(hits) + Count(embers) + Count(smoke);

        private void Awake() => _shake = BattleCameraShake.Of(battleCamera);

        private void OnEnable()
        {
            cabinet.Aligned += OnAligned;
            battle.RedStruck += OnStruck;
            battle.NodeBegan += OnNodeBegan;
        }

        private void OnDisable()
        {
            cabinet.Aligned -= OnAligned;
            battle.RedStruck -= OnStruck;
            battle.NodeBegan -= OnNodeBegan;
        }

        // 前のステージで来なかった爆発を待たない。
        private void OnNodeBegan() => _expectedStrikes = StrikeCount;

        private void OnAligned(SlotSymbol symbol)
        {
            var field = battle.Field;
            if (symbol != SlotSymbol.Red7 || field?.Effects == null || fieldView.Map == null)
            {
                return;
            }

            PlayCount++;
            var delay = (float)field.Effects.RedDelaySteps / SimulationClock.StepsPerSecond;
            _ = Breathe(delay, ++_expectedStrikes);
        }

        private async Awaitable Breathe(float delay, int strikeIndex)
        {
            _breathing++;
            var token = destroyCancellationToken;
            try
            {
                Measure(out var far, out var near, out var right, out var forward, out var width, out _);
                warning.transform.SetPositionAndRotation(far + Vector3.up * 0.2f, Quaternion.LookRotation(forward, Vector3.up));
                var warningShape = warning.shape;
                warningShape.scale = new Vector3(width, 0.1f, 0.4f);
                warning.Play();
                warning.Emit(warningBurst); // 揃った瞬間から光る。

                var nozzle = far + forward * nozzleBack + Vector3.up * nozzleHeight;
                var groundShape = groundFire.shape;
                groundShape.scale = new Vector3(width, 0.6f, 0.05f);
                var breathing = false;
                for (var t = 0f; StrikeCount < strikeIndex && t < delay + StrikeGrace; t += Time.deltaTime)
                {
                    if (t >= warningEnd * delay && warning.isEmitting)
                    {
                        warning.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }

                    if (t >= breathStart * delay)
                    {
                        // 着地点を奥の端から手前の端へ動かす（はじめ速く、最後はゆっくり）。
                        var s = delay > 0f ? Mathf.Clamp01((t - breathStart * delay) / ((1f - breathStart) * delay)) : 1f;
                        s = 1f - (1f - s) * (1f - s);
                        var target = Vector3.Lerp(far, near, s);
                        var line = target - nozzle;
                        breath.transform.SetPositionAndRotation(nozzle, Quaternion.LookRotation(line, Vector3.up));
                        var main = breath.main;
                        main.startSpeed = line.magnitude / flightSeconds;
                        groundFire.transform.SetPositionAndRotation(target, Quaternion.LookRotation(Vector3.up, forward));
                        if (!breathing)
                        {
                            breathing = true;
                            breath.Play();
                            groundFire.Play();
                        }
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
                _breathing--;
            }

            if (_breathing == 0)
            {
                warning.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                breath.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                groundFire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        // Core が赤7のダメージを入れた（倒した敵はまだ消えていない）: 盤面全体を爆発させる。
        private void OnStruck()
        {
            StrikeCount++;
            var field = battle.Field;
            if (field == null || fieldView.Map == null)
            {
                return;
            }

            breath.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            groundFire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            warning.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            Measure(out _, out _, out _, out _, out var width, out var depth);
            var map = fieldView.Map;
            var center = fieldView.PositionToWorld((map.Width - 1) * 0.5, (map.Height - 1) * 0.5, false);
            flash.Emit(new ParticleSystem.EmitParams
            {
                position = center + Vector3.up,
                startSize = Mathf.Max(width, depth) * flashScale,
                applyShapeToPosition = false,
            }, 1);

            for (var i = 0; i < blastCount; i++)
            {
                Emit(blasts, RandomOnBoard(map) + Vector3.up * 0.3f, Vector3.up * UnityEngine.Random.Range(0.2f, 0.8f));
            }

            foreach (var enemy in field.Enemies)
            {
                var position = fieldView.PositionToWorld(enemy.X, enemy.Y, enemy.Definition.IsAir) + Vector3.up * 0.3f;
                for (var i = 0; i < hitsPerEnemy; i++)
                {
                    Emit(hits, position, UnityEngine.Random.insideUnitSphere * 0.4f);
                }
            }

            for (var i = 0; i < emberCount; i++)
            {
                Emit(embers, RandomOnBoard(map),
                    UnityEngine.Random.insideUnitSphere * 2.5f + Vector3.up * UnityEngine.Random.Range(2.5f, 5f));
            }

            for (var i = 0; i < smokeCount; i++)
            {
                Emit(smoke, RandomOnBoard(map) + Vector3.up * 0.5f, Vector3.up * UnityEngine.Random.Range(0.3f, 0.8f));
            }

            _shake.Shake(shakeSeconds, shakeAmplitude);
        }

        // 盤面の奥の端の中心・手前の端の中心（地面の高さ）、右と奥の向き、横幅と奥行き（ワールド）。
        private void Measure(out Vector3 far, out Vector3 near, out Vector3 right, out Vector3 forward, out float width, out float depth)
        {
            var map = fieldView.Map;
            var midX = (map.Width - 1) * 0.5;
            far = fieldView.PositionToWorld(midX, -0.5, false);
            near = fieldView.PositionToWorld(midX, map.Height - 0.5, false);
            var left = fieldView.PositionToWorld(-0.5, -0.5, false);
            var rightEdge = fieldView.PositionToWorld(map.Width - 0.5, -0.5, false);
            right = (rightEdge - left).normalized;
            forward = (far - near).normalized;
            width = Vector3.Distance(left, rightEdge);
            depth = Vector3.Distance(far, near);
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
