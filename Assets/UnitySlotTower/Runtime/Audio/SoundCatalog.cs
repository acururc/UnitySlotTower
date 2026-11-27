using System;
using System.Collections.Generic;
using UnityEngine;
using UnitySlotTower.Core.Audio;

namespace UnitySlotTower
{
    /// <summary>
    /// 出来事（<see cref="SoundId"/>）と BGM（<see cref="BgmTrack"/>）に使う音の一覧（spec 014 FR-007）。
    /// 差し替えはこのアセットを直すだけ。割り当てのない出来事は鳴らさない。
    /// </summary>
    [CreateAssetMenu(menuName = "UnitySlotTower/Sound Catalog", fileName = "SoundCatalog")]
    public sealed class SoundCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class SoundEntry
        {
            public SoundId id;

            [Tooltip("複数あれば順に使う")]
            public AudioClip[] clips = Array.Empty<AudioClip>();

            [Range(0f, 1f)] public float volume = 1f;

            [Tooltip("同時に鳴る数の上限（0 で上限なし）")]
            public int maxVoices = 4;

            [NonSerialized] private int _next;

            /// <summary>次に使うクリップ。null を飛ばし、無ければ null。</summary>
            public AudioClip Next()
            {
                if (clips == null)
                {
                    return null;
                }

                for (var i = 0; i < clips.Length; i++)
                {
                    var clip = clips[(_next + i) % clips.Length];
                    if (clip != null)
                    {
                        _next = (_next + i + 1) % clips.Length;
                        return clip;
                    }
                }

                return null;
            }
        }

        [Serializable]
        public sealed class BgmEntry
        {
            public BgmTrack track;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
        }

        [SerializeField] private List<SoundEntry> sounds = new List<SoundEntry>();
        [SerializeField] private List<BgmEntry> bgms = new List<BgmEntry>();

        public IReadOnlyList<SoundEntry> Sounds => sounds;
        public IReadOnlyList<BgmEntry> Bgms => bgms;

        /// <summary>Editor ツールとテスト用。</summary>
        public void SetEntries(IEnumerable<SoundEntry> newSounds, IEnumerable<BgmEntry> newBgms)
        {
            sounds = new List<SoundEntry>(newSounds);
            bgms = new List<BgmEntry>(newBgms);
        }

        public bool TryGet(SoundId id, out SoundEntry entry)
        {
            foreach (var e in sounds)
            {
                if (e != null && e.id == id)
                {
                    entry = e;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public bool TryGet(BgmTrack track, out BgmEntry entry)
        {
            foreach (var e in bgms)
            {
                if (e != null && e.track == track && e.clip != null)
                {
                    entry = e;
                    return true;
                }
            }

            entry = null;
            return false;
        }
    }
}
