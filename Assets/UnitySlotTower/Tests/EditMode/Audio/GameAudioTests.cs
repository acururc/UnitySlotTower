using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnitySlotTower.Core.Audio;

namespace UnitySlotTower.Tests.EditMode.Audio
{
    public sealed class GameAudioTests
    {
        private GameObject _go;
        private SoundCatalog _catalog;
        private AudioClip _clip;
        private readonly List<SoundId> _requested = new List<SoundId>();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("GameAudio Test");
            _catalog = ScriptableObject.CreateInstance<SoundCatalog>();
            _clip = AudioClip.Create("beep", 44100, 1, 44100, false);
            _requested.Clear();
            GameAudio.Requested += OnRequested;
        }

        [TearDown]
        public void TearDown()
        {
            GameAudio.Requested -= OnRequested;
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_clip);
        }

        private void OnRequested(SoundId id) => _requested.Add(id);

        [Test]
        public void Without_Catalog_Nothing_Plays_And_No_Error()
        {
            var audio = _go.AddComponent<GameAudio>();

            Assert.That(audio.PlaySe(SoundId.Lever), Is.False);
            audio.StartLoop(SoundId.ReelSpin);
            audio.ChangeBgm(BgmTrack.Battle);

            Assert.That(_requested, Is.EqualTo(new[] { SoundId.Lever, SoundId.ReelSpin }));
            Assert.That(audio.IsLooping, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Unassigned_Or_Empty_Entries_Do_Not_Play()
        {
            _catalog.SetEntries(
                new[] { new SoundCatalog.SoundEntry { id = SoundId.Hit, clips = new AudioClip[] { null } } },
                new[] { new SoundCatalog.BgmEntry { track = BgmTrack.Title, clip = null } });
            var audio = _go.AddComponent<GameAudio>();
            audio.SetCatalog(_catalog);

            Assert.That(audio.PlaySe(SoundId.Hit), Is.False);
            Assert.That(audio.PlaySe(SoundId.Lever), Is.False);
            Assert.That(_catalog.TryGet(BgmTrack.Title, out _), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Assigned_Sound_Plays_Up_To_Its_Voice_Limit()
        {
            _catalog.SetEntries(
                new[] { new SoundCatalog.SoundEntry { id = SoundId.Hit, clips = new[] { _clip }, maxVoices = 1 } },
                new SoundCatalog.BgmEntry[0]);
            var audio = _go.AddComponent<GameAudio>();
            audio.SetCatalog(_catalog);

            Assert.That(audio.PlaySe(SoundId.Hit), Is.True);
            Assert.That(audio.PlaySe(SoundId.Hit), Is.False);
        }

        [Test]
        public void Paused_Se_Does_Not_Play()
        {
            _catalog.SetEntries(
                new[] { new SoundCatalog.SoundEntry { id = SoundId.Hit, clips = new[] { _clip } } },
                new SoundCatalog.BgmEntry[0]);
            var audio = _go.AddComponent<GameAudio>();
            audio.SetCatalog(_catalog);

            audio.SetSePaused(true);

            Assert.That(audio.PlaySe(SoundId.Hit), Is.False);
            audio.SetSePaused(false);
            Assert.That(audio.PlaySe(SoundId.Hit), Is.True);
        }

        [Test]
        public void Entry_Cycles_Through_Clips_Skipping_Null()
        {
            var other = AudioClip.Create("boop", 4410, 1, 44100, false);
            try
            {
                var entry = new SoundCatalog.SoundEntry { clips = new[] { _clip, null, other } };

                Assert.That(new[] { entry.Next(), entry.Next(), entry.Next() }, Is.EqualTo(new[] { _clip, other, _clip }));
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }
    }
}
