using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// 早見表の行の動く図（spec 016 FR-002・D3）。くり返す短い演出を、キー（時刻・位置・大きさ・色・回転）の補間で描く。
    /// 位置と大きさは親の矩形の割合なので、文字あり版で図の枠が縮んでも絵は崩れない。
    /// 一時停止中も動くよう実時間（<see cref="Time.unscaledTime"/>）で進め、非表示のページでは Update が走らないので止まる。
    /// キーは Editor の Setup（Build Help Pages (spec 016)）が置く。
    /// </summary>
    public sealed class HelpDemo : MonoBehaviour
    {
        [Serializable]
        public struct Key
        {
            [Tooltip("ループの中の時刻（0〜1）。")]
            public float time;
            [Tooltip("中心の位置（親の矩形の割合）。")]
            public Vector2 position;
            [Tooltip("大きさ（親の矩形の割合）。")]
            public Vector2 size;
            public Color color;
            [Tooltip("Z 回転（度）。")]
            public float angle;
        }

        [Serializable]
        public sealed class Track
        {
            public RectTransform target;
            [Tooltip("色を変える Graphic（なくてもよい）。")]
            public Graphic graphic;
            [Tooltip("透明度（キーの色の a）を変える CanvasGroup（なくてもよい）。いくつかの絵をまとめた部品に使う。")]
            public CanvasGroup group;
            [Tooltip("時刻の順に並べる。最初より前は最初、最後より後は最後の値。")]
            public Key[] keys = Array.Empty<Key>();
        }

        [Tooltip("1回の長さ（秒）。")]
        [SerializeField] private float period = 2f;
        [SerializeField] private Track[] tracks = Array.Empty<Track>();

        private float _start;

        public float Period => period;

        public Track[] Tracks => tracks;

        /// <summary>Setup から中身を置く。</summary>
        public void Configure(float loopSeconds, Track[] demoTracks)
        {
            period = loopSeconds;
            tracks = demoTracks;
            Evaluate(0f);
        }

        private void OnEnable()
        {
            _start = Time.unscaledTime;
            Evaluate(0f);
        }

        private void Update()
        {
            if (period <= 0f)
            {
                return;
            }

            Evaluate(Mathf.Repeat(Time.unscaledTime - _start, period) / period);
        }

        /// <summary>ループの位置 t（0〜1）の姿にする。</summary>
        public void Evaluate(float t)
        {
            for (var i = 0; i < tracks.Length; i++)
            {
                var track = tracks[i];
                if (track == null || track.target == null || track.keys.Length == 0)
                {
                    continue;
                }

                var key = Sample(track.keys, t);
                var half = key.size * 0.5f;
                track.target.anchorMin = key.position - half;
                track.target.anchorMax = key.position + half;
                track.target.offsetMin = track.target.offsetMax = Vector2.zero;
                track.target.localEulerAngles = new Vector3(0f, 0f, key.angle);
                if (track.graphic != null)
                {
                    track.graphic.color = key.color;
                }

                if (track.group != null)
                {
                    track.group.alpha = key.color.a;
                }
            }
        }

        /// <summary>キーの列の時刻 t の値（前後のキーの線形補間）。</summary>
        public static Key Sample(Key[] keys, float t)
        {
            if (t <= keys[0].time)
            {
                return keys[0];
            }

            for (var i = 1; i < keys.Length; i++)
            {
                var b = keys[i];
                if (t > b.time)
                {
                    continue;
                }

                var a = keys[i - 1];
                var span = b.time - a.time;
                var u = span > 0f ? (t - a.time) / span : 1f;
                return new Key
                {
                    time = t,
                    position = Vector2.LerpUnclamped(a.position, b.position, u),
                    size = Vector2.LerpUnclamped(a.size, b.size, u),
                    color = Color.LerpUnclamped(a.color, b.color, u),
                    angle = Mathf.LerpUnclamped(a.angle, b.angle, u),
                };
            }

            return keys[keys.Length - 1];
        }
    }
}
