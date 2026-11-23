using System;
using UnityEngine;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 青7の凍結の演出（spec 004 FR-001・FR-002・FR-004、plan 3章）。<see cref="Red7BreathEffect"/> と同じく 3D のフィールドの上の
    /// パーティクルを戦闘カメラで写す。揃うと 予兆（盤面の奥の端が青白く光る）→ 冷気（霜と雪の帯が奥から手前へ進み、地面に霜を残す）
    /// と進み、Core が停止を始めた瞬間（<see cref="BattleController.BlueFroze"/>）に盤面全体が凍りついてカメラが小さく揺れる。
    /// 停止の間は敵ごとに氷の塊（メッシュのパーティクル）を付け、停止が切れた瞬間に砕く。時間は戦闘の時間（Time.deltaTime）。
    /// パーティクルは Setup（Build Blue7 Freeze Effect）が作るので Inspector で調整できる。
    /// </summary>
    public sealed class Blue7FreezeEffect : MonoBehaviour
    {
        // 凍結が来ないとき（ステージが先に終わった）に冷気を止めるまでの、待ち時間を過ぎてからの余裕（秒）。
        private const float FreezeGrace = 0.5f;

        // 氷の塊の上限（plan 5章）。
        private const int MaxIce = 64;

        [SerializeField] private SlotCabinet cabinet;
        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private Camera battleCamera;

        [Header("パーティクル（Setup が作る）")]
        [SerializeField] private ParticleSystem warning;
        [SerializeField] private ParticleSystem mist;
        [SerializeField] private ParticleSystem snow;
        [SerializeField] private ParticleSystem groundFrost;
        [SerializeField] private ParticleSystem flash;
        [SerializeField] private ParticleSystem cracks;
        [SerializeField] private ParticleSystem sparkles;
        [SerializeField] private ParticleSystem ice;
        [SerializeField] private ParticleSystem shards;
        [SerializeField] private ParticleSystem thawMist;

        [Header("流れ（揃ってから凍結までの待ち時間に対する割合）")]
        [Tooltip("予兆の光を出し終える時点。")]
        [SerializeField, Range(0f, 1f)] private float warningEnd = 0.4f;
        [Tooltip("冷気が奥の端を出る時点。")]
        [SerializeField, Range(0f, 0.95f)] private float chillStart = 0.2f;
        [Tooltip("冷気が手前の端に着く時点（これより後は手前の端に留まる）。")]
        [SerializeField, Range(0.05f, 1f)] private float chillEnd = 0.9f;
        [Tooltip("揃った瞬間に予兆の光をまとめて出す数。")]
        [SerializeField, Min(0)] private int warningBurst = 16;

        [Header("凍結")]
        [SerializeField, Min(0)] private int crackCount = 10;
        [SerializeField, Min(0)] private int sparkleCount = 40;
        [Tooltip("閃光の大きさ（盤面の長い辺に対する倍率）。")]
        [SerializeField, Min(0f)] private float flashScale = 1.4f;

        [Header("氷の塊")]
        [Tooltip("氷の塊の大きさ（ワールド、マス = 1）。")]
        [SerializeField] private Vector3 iceSize = new Vector3(0.75f, 0.9f, 0.75f);
        [Tooltip("氷の塊の中心の地面（敵の足元）からの高さ。")]
        [SerializeField] private float iceHeight = 0.35f;
        [Tooltip("凍結の瞬間に氷の塊が大きくなりきるまでの秒数。")]
        [SerializeField, Min(0.01f)] private float icePopSeconds = 0.12f;
        [SerializeField] private Color iceColor = new Color(0.65f, 0.9f, 1f, 0.55f);

        [Header("砕ける")]
        [SerializeField, Min(0)] private int shardsPerIce = 8;
        [SerializeField, Min(0)] private int thawMistPerIce = 1;

        [Header("揺れ")]
        [SerializeField, Min(0f)] private float shakeSeconds = 0.25f;
        [Tooltip("揺れの幅（ワールド）。赤7より弱く（spec 004 D-06）。")]
        [SerializeField, Min(0f)] private float shakeAmplitude = 0.1f;

        private readonly ParticleSystem.Particle[] _iceParticles = new ParticleSystem.Particle[MaxIce];
        private readonly Vector3[] _icePositions = new Vector3[MaxIce];
        private int _iceCount;
        private bool _iceShown;
        private float _frozeAt;
        private int _chilling;
        private int _expectedFreezes;
        private BattleCameraShake _shake;

        /// <summary>青7で始めた冷気の数（テスト用）。</summary>
        public int PlayCount { get; private set; }

        /// <summary>凍結した数（テスト用）。</summary>
        public int FreezeCount { get; private set; }

        /// <summary>氷が砕けた回数（テスト用）。</summary>
        public int ShatterCount { get; private set; }

        /// <summary>冷気を出している（予兆を含む）。</summary>
        public bool IsChilling => _chilling > 0;

        /// <summary>出ている氷の塊の数（停止中だけ 0 より大きい）。</summary>
        public int IceCount => _iceShown ? _iceCount : 0;

        /// <summary>カメラが揺れている。</summary>
        public bool IsShaking => _shake != null && _shake.IsShaking;

        /// <summary>演出中か（冷気・氷の塊・揺れ・残っている粒のどれか）。</summary>
        public bool IsPlaying => IsChilling || _iceShown || IsShaking || ActiveParticleCount > 0;

        /// <summary>出ている粒の数（テスト用。演出が終われば 0）。</summary>
        public int ActiveParticleCount =>
            Count(warning) + Count(mist) + Count(snow) + Count(groundFrost) + Count(flash) + Count(cracks) + Count(sparkles) +
            Count(ice) + Count(shards) + Count(thawMist);

        private void Awake() => _shake = BattleCameraShake.Of(battleCamera);

        private void OnEnable()
        {
            cabinet.Aligned += OnAligned;
            battle.BlueFroze += OnFroze;
            battle.NodeBegan += OnNodeBegan;
        }

        private void OnDisable()
        {
            cabinet.Aligned -= OnAligned;
            battle.BlueFroze -= OnFroze;
            battle.NodeBegan -= OnNodeBegan;
        }

        // 前のステージで来なかった凍結を待たない。前のステージの氷は砕かずに消す。
        private void OnNodeBegan()
        {
            _expectedFreezes = FreezeCount;
            HideIce();
        }

        private void OnAligned(SlotSymbol symbol)
        {
            var field = battle.Field;
            if (symbol != SlotSymbol.Blue7 || field?.Effects == null || fieldView.Map == null)
            {
                return;
            }

            PlayCount++;
            _expectedFreezes++;
            var delay = (float)field.Effects.BlueDelaySteps / SimulationClock.StepsPerSecond;
            // 待ちが 0 なら凍結は揃いと同時（ApplySlot の中）なので冷気は出さない。
            if (delay > 0f)
            {
                _ = Chill(delay, _expectedFreezes);
            }
        }

        private async Awaitable Chill(float delay, int freezeIndex)
        {
            _chilling++;
            var token = destroyCancellationToken;
            try
            {
                Measure(out var far, out var near, out var forward, out var width);
                warning.transform.SetPositionAndRotation(far + Vector3.up * 0.2f, Quaternion.LookRotation(forward, Vector3.up));
                var warningShape = warning.shape;
                warningShape.scale = new Vector3(width, 0.1f, 0.4f);
                warning.Play();
                warning.Emit(warningBurst); // 揃った瞬間から光る。

                SetFrontWidth(mist, width);
                SetFrontWidth(snow, width);
                SetFrontWidth(groundFrost, width);
                var chilling = false;
                for (var t = 0f; FreezeCount < freezeIndex && t < delay + FreezeGrace; t += Time.deltaTime)
                {
                    if (t >= warningEnd * delay && warning.isEmitting)
                    {
                        warning.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }

                    if (t >= chillStart * delay)
                    {
                        // 冷気の前線を奥の端から手前の端へ動かす（一定の速さで、最後は手前の端に留まる）。
                        var s = Mathf.Clamp01((t - chillStart * delay) / Mathf.Max(0.01f, (chillEnd - chillStart) * delay));
                        var front = Vector3.Lerp(far, near, s);
                        var rotation = Quaternion.LookRotation(forward, Vector3.up);
                        mist.transform.SetPositionAndRotation(front + Vector3.up * 0.3f, rotation);
                        snow.transform.SetPositionAndRotation(front + Vector3.up * 1.6f, rotation);
                        groundFrost.transform.SetPositionAndRotation(front + Vector3.up * 0.02f, rotation);
                        if (!chilling)
                        {
                            chilling = true;
                            mist.Play();
                            snow.Play();
                            groundFrost.Play();
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
                _chilling--;
            }

            if (_chilling == 0)
            {
                StopChill();
            }
        }

        // Core が停止を始めた: 盤面全体を凍らせ、氷の塊を出す。
        private void OnFroze()
        {
            FreezeCount++;
            if (battle.Field == null || fieldView.Map == null)
            {
                return;
            }

            StopChill();
            Measure(out _, out _, out _, out var width);
            var map = fieldView.Map;
            var center = fieldView.PositionToWorld((map.Width - 1) * 0.5, (map.Height - 1) * 0.5, false);
            var depth = Vector3.Distance(fieldView.PositionToWorld(0, -0.5, false), fieldView.PositionToWorld(0, map.Height - 0.5, false));
            flash.Emit(new ParticleSystem.EmitParams
            {
                position = center + Vector3.up,
                startSize = Mathf.Max(width, depth) * flashScale,
                applyShapeToPosition = false,
            }, 1);

            for (var i = 0; i < crackCount; i++)
            {
                Emit(cracks, RandomOnBoard(map) + Vector3.up * 0.03f, Vector3.zero);
            }

            for (var i = 0; i < sparkleCount; i++)
            {
                Emit(sparkles, RandomOnBoard(map) + Vector3.up * UnityEngine.Random.Range(0.2f, 1.2f), Vector3.up * 0.3f);
            }

            _frozeAt = Time.time;
            _shake.Shake(shakeSeconds, shakeAmplitude);
        }

        // 停止の間は毎フレーム生きている敵に氷の塊を置き、停止が切れたら砕く。
        private void LateUpdate()
        {
            var field = battle.Field;
            var stopped = field?.Effects != null && field.Effects.IsStopped && fieldView.Map != null;
            if (stopped)
            {
                PlaceIce(field);
            }
            else if (_iceShown)
            {
                Shatter();
            }
        }

        private void PlaceIce(Battlefield field)
        {
            var pop = Mathf.Clamp01((Time.time - _frozeAt) / icePopSeconds);
            // 少し大きくなりすぎてから戻る（ピキーンと固まる感じ）。
            var scale = pop < 1f ? Mathf.Lerp(0.3f, 1.15f, pop) : 1f;
            _iceCount = 0;
            foreach (var enemy in field.Enemies)
            {
                if (_iceCount >= MaxIce)
                {
                    break;
                }

                if (!enemy.IsAlive)
                {
                    continue;
                }

                var position = fieldView.PositionToWorld(enemy.X, enemy.Y, enemy.Definition.IsAir) + Vector3.up * iceHeight;
                // 敵ごとに傾きを変える（並びで決めるので毎フレーム同じ）。
                var tilt = new Vector3(8f * Mathf.Sin(_iceCount * 2.1f), 37f * _iceCount, 8f * Mathf.Cos(_iceCount * 1.7f));
                _icePositions[_iceCount] = position;
                _iceParticles[_iceCount] = new ParticleSystem.Particle
                {
                    position = position,
                    startSize3D = iceSize * scale,
                    rotation3D = tilt,
                    startColor = iceColor,
                    startLifetime = 100f,
                    remainingLifetime = 100f,
                };
                _iceCount++;
            }

            if (!ice.isPlaying)
            {
                ice.Play();
            }

            ice.SetParticles(_iceParticles, _iceCount);
            _iceShown = true;
        }

        private void Shatter()
        {
            ShatterCount++;
            for (var i = 0; i < _iceCount; i++)
            {
                for (var k = 0; k < shardsPerIce; k++)
                {
                    Emit(shards, _icePositions[i] + UnityEngine.Random.insideUnitSphere * 0.25f,
                        UnityEngine.Random.insideUnitSphere * 2.2f + Vector3.up * UnityEngine.Random.Range(1.5f, 3f));
                }

                for (var k = 0; k < thawMistPerIce; k++)
                {
                    Emit(thawMist, _icePositions[i], Vector3.up * 0.4f);
                }
            }

            HideIce();
        }

        private void HideIce()
        {
            ice.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _iceShown = false;
            _iceCount = 0;
        }

        private void StopChill()
        {
            warning.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            mist.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            snow.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            groundFrost.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private static void SetFrontWidth(ParticleSystem system, float width)
        {
            var shape = system.shape;
            var scale = shape.scale;
            shape.scale = new Vector3(width, scale.y, scale.z);
        }

        // 盤面の奥の端の中心・手前の端の中心（地面の高さ）、奥の向き、横幅（ワールド）。
        private void Measure(out Vector3 far, out Vector3 near, out Vector3 forward, out float width)
        {
            var map = fieldView.Map;
            var midX = (map.Width - 1) * 0.5;
            far = fieldView.PositionToWorld(midX, -0.5, false);
            near = fieldView.PositionToWorld(midX, map.Height - 0.5, false);
            forward = (far - near).normalized;
            width = Vector3.Distance(fieldView.PositionToWorld(-0.5, -0.5, false), fieldView.PositionToWorld(map.Width - 0.5, -0.5, false));
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
