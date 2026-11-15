using UnityEngine;

namespace UnitySlotTower.Ui
{
    /// <summary>
    /// 矢印を向きの方向へ小さく行き来させて「こちらへ流れる」と絵で伝える部品（plan 16.10 のショップの絵による説明）。
    /// 動かす方向は RectTransform の右（回転込み）。<see cref="Playing"/> を切るとその場で元の位置に戻って止まる。
    /// 一時停止中も画面の絵として動くよう実時間で進める。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class NudgeArrow : MonoBehaviour
    {
        [Tooltip("行き来の幅（矢印の幅に対する割合）。")]
        [SerializeField, Range(0f, 2f)] private float amplitude = 0.35f;

        [Tooltip("1秒あたりの往復の回数。")]
        [SerializeField, Min(0.01f)] private float frequency = 1.2f;

        private RectTransform _rect;
        private Vector2 _origin;
        private bool _playing = true;
        private float _phase;

        /// <summary>動いているか（止めると元の位置に戻る）。</summary>
        public bool Playing
        {
            get => _playing;
            set
            {
                if (_playing == value)
                {
                    return;
                }

                _playing = value;
                if (!value && _rect != null)
                {
                    _rect.anchoredPosition = _origin;
                }
            }
        }

        /// <summary>元の位置からのずれ（テスト用）。</summary>
        public Vector2 Offset => _rect != null ? _rect.anchoredPosition - _origin : Vector2.zero;

        /// <summary>コードで作った矢印の幅と速さを決める。</summary>
        public void Configure(float newAmplitude, float newFrequency)
        {
            amplitude = Mathf.Max(0f, newAmplitude);
            frequency = Mathf.Max(0.01f, newFrequency);
        }

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _origin = _rect.anchoredPosition;
        }

        private void OnDisable()
        {
            if (_rect != null)
            {
                _rect.anchoredPosition = _origin;
            }

            _phase = 0f;
        }

        private void Update()
        {
            if (!_playing)
            {
                return;
            }

            // 0 → 前へ → 0 を繰り返す（後ろには下がらない）。幅は矢印の大きさに合わせるので画面の解像度に依らない。
            _phase = Mathf.Repeat(_phase + Time.unscaledDeltaTime * frequency, 1f);
            var distance = (1f - Mathf.Cos(_phase * 2f * Mathf.PI)) * 0.5f * amplitude * _rect.rect.width;
            var direction = (Vector2)(_rect.localRotation * Vector3.right);
            _rect.anchoredPosition = _origin + direction * distance;
        }
    }
}
