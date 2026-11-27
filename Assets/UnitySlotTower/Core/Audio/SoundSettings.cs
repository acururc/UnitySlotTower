using System;

namespace UnitySlotTower.Core.Audio
{
    /// <summary>音量の保存先（Runtime では PlayerPrefs）。</summary>
    public interface ISoundSettingsStore
    {
        bool TryGet(string key, out float value);
        void Set(string key, float value);
    }

    /// <summary>BGM と効果音の音量（0〜1、spec 014 FR-006）。</summary>
    public sealed class SoundSettings
    {
        public const float DefaultBgm = 0.7f;
        public const float DefaultSe = 0.8f;
        public const string BgmKey = "sound.bgm";
        public const string SeKey = "sound.se";

        private readonly ISoundSettingsStore _store;
        private float _bgm = DefaultBgm;
        private float _se = DefaultSe;

        public SoundSettings(ISoundSettingsStore store = null)
        {
            _store = store;
            if (_store == null)
            {
                return;
            }

            if (_store.TryGet(BgmKey, out var bgm)) _bgm = Clamp(bgm);
            if (_store.TryGet(SeKey, out var se)) _se = Clamp(se);
        }

        /// <summary>値が変わったときに呼ぶ。</summary>
        public event Action Changed;

        public float Bgm
        {
            get => _bgm;
            set => Apply(ref _bgm, value, BgmKey);
        }

        public float Se
        {
            get => _se;
            set => Apply(ref _se, value, SeKey);
        }

        private void Apply(ref float field, float value, string key)
        {
            var v = Clamp(value);
            if (v == field)
            {
                return;
            }

            field = v;
            _store?.Set(key, v);
            Changed?.Invoke();
        }

        /// <summary>0〜1 に丸める。NaN は 0。</summary>
        public static float Clamp(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
    }
}
