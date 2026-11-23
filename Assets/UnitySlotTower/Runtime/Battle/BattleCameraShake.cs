using System;
using UnityEngine;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 戦闘カメラの揺れ（spec 003 FR-002・spec 004 FR-002）。出目の演出が共有するので、揺れが重なっても戻す位置は1つにまとまる。
    /// 揺れの最中にカメラが動かされた（マップが変わった、<see cref="BattleFieldView"/> の FitCamera）ら、そこを新しい元の位置にする。
    /// 時間は戦闘の時間（Time.deltaTime）。
    /// </summary>
    public sealed class BattleCameraShake : MonoBehaviour
    {
        private int _shaking;
        private Vector3 _base;
        private Vector3 _set;
        private float _amplitude;

        /// <summary>揺れている。</summary>
        public bool IsShaking => _shaking > 0;

        /// <summary>カメラに付いた揺れの部品を返す（無ければ付ける）。</summary>
        public static BattleCameraShake Of(Camera camera) =>
            camera.TryGetComponent(out BattleCameraShake shake) ? shake : camera.gameObject.AddComponent<BattleCameraShake>();

        /// <summary><paramref name="seconds"/> 秒かけて幅 <paramref name="amplitude"/>（ワールド）から弱めながら揺らし、元の位置に戻す。</summary>
        public void Shake(float seconds, float amplitude)
        {
            if (seconds > 0f && amplitude > 0f)
            {
                _ = Run(seconds, amplitude);
            }
        }

        private async Awaitable Run(float seconds, float amplitude)
        {
            var camera = transform;
            if (_shaking++ == 0)
            {
                _base = camera.position;
                _set = _base;
            }

            var token = destroyCancellationToken;
            try
            {
                for (var t = 0f; t < seconds; t += Time.deltaTime)
                {
                    var k = 1f - t / seconds;
                    // 重なったら、そのフレームで一番強い揺れを使う（足し合わせると赤7と青7が同時のとき揺れすぎる）。
                    _amplitude = Mathf.Max(_amplitude, amplitude * k * k);
                    await Awaitable.NextFrameAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                _shaking--;
            }

            if (_shaking == 0 && camera.position == _set)
            {
                camera.position = _base;
            }
        }

        // 揺れの位置は1フレームに1回だけ決める（重なった揺れがそれぞれ位置を書くと戻す位置がずれる）。
        private void LateUpdate()
        {
            if (_shaking == 0)
            {
                _amplitude = 0f;
                return;
            }

            var camera = transform;
            if (camera.position != _set)
            {
                _base = camera.position;
            }

            var offset = UnityEngine.Random.insideUnitCircle * _amplitude;
            _set = _base + camera.right * offset.x + camera.up * offset.y;
            camera.position = _set;
            _amplitude = 0f;
        }
    }
}
