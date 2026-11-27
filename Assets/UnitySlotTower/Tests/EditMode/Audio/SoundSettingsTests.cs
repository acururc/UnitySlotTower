using System.Collections.Generic;
using NUnit.Framework;
using UnitySlotTower.Core.Audio;

namespace UnitySlotTower.Tests.EditMode.Audio
{
    public sealed class SoundSettingsTests
    {
        private sealed class MemoryStore : ISoundSettingsStore
        {
            public readonly Dictionary<string, float> Values = new Dictionary<string, float>();
            public bool TryGet(string key, out float value) => Values.TryGetValue(key, out value);
            public void Set(string key, float value) => Values[key] = value;
        }

        [Test]
        public void Defaults_Are_Bgm70_Se80_When_Nothing_Saved()
        {
            var settings = new SoundSettings(new MemoryStore());

            Assert.That((settings.Bgm, settings.Se), Is.EqualTo((0.5f, 0.5f)));
        }

        [Test]
        public void Values_Are_Clamped_To_0_1()
        {
            var settings = new SoundSettings();

            settings.Bgm = 1.5f;
            settings.Se = -0.2f;

            Assert.That((settings.Bgm, settings.Se), Is.EqualTo((1f, 0f)));
            settings.Se = float.NaN;
            Assert.That(settings.Se, Is.EqualTo(0f));
        }

        [Test]
        public void Changed_Values_Are_Saved_And_Loaded_Next_Time()
        {
            var store = new MemoryStore();
            var first = new SoundSettings(store) { Bgm = 0.25f, Se = 0f };

            var next = new SoundSettings(store);

            Assert.That((next.Bgm, next.Se), Is.EqualTo((0.25f, 0f)));
            Assert.That(first.Bgm, Is.EqualTo(0.25f));
        }

        [Test]
        public void Saved_Values_Out_Of_Range_Are_Clamped_On_Load()
        {
            var store = new MemoryStore();
            store.Values[SoundSettings.BgmKey] = 3f;
            store.Values[SoundSettings.SeKey] = -1f;

            var settings = new SoundSettings(store);

            Assert.That((settings.Bgm, settings.Se), Is.EqualTo((1f, 0f)));
        }

        [Test]
        public void Changed_Fires_Only_When_Value_Changes()
        {
            var settings = new SoundSettings();
            var count = 0;
            settings.Changed += () => count++;

            settings.Bgm = SoundSettings.DefaultBgm;
            settings.Bgm = 0.1f;
            settings.Se = 2f;
            settings.Se = 1f;

            Assert.That(count, Is.EqualTo(2));
        }
    }
}
