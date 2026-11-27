using UnityEngine;
using UnitySlotTower.Core.Audio;

namespace UnitySlotTower
{
    /// <summary>音量を PlayerPrefs に置く（spec 014 FR-006）。書き出しは <see cref="Flush"/> か終了時。</summary>
    public sealed class PlayerPrefsSoundStore : ISoundSettingsStore
    {
        public bool TryGet(string key, out float value)
        {
            if (PlayerPrefs.HasKey(key))
            {
                value = PlayerPrefs.GetFloat(key);
                return true;
            }

            value = 0f;
            return false;
        }

        public void Set(string key, float value) => PlayerPrefs.SetFloat(key, value);

        public static void Flush() => PlayerPrefs.Save();
    }
}
