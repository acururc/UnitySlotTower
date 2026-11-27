using NUnit.Framework;
using UnitySlotTower.Core.Audio;

namespace UnitySlotTower.Tests.EditMode.Audio
{
    public sealed class VoiceLimiterTests
    {
        [Test]
        public void Refuses_Beyond_Max_While_Earlier_Voices_Still_Play()
        {
            var limiter = new VoiceLimiter();

            Assert.That(limiter.TryStart(1, 0, 1, 2), Is.True);
            Assert.That(limiter.TryStart(1, 0.1, 1, 2), Is.True);
            Assert.That(limiter.TryStart(1, 0.2, 1, 2), Is.False);
            Assert.That(limiter.Playing(1, 0.2), Is.EqualTo(2));
        }

        [Test]
        public void Accepts_Again_After_A_Voice_Ends()
        {
            var limiter = new VoiceLimiter();
            limiter.TryStart(1, 0, 1, 1);

            Assert.That(limiter.TryStart(1, 0.5, 1, 1), Is.False);
            Assert.That(limiter.TryStart(1, 1.0, 1, 1), Is.True);
        }

        [Test]
        public void Ids_Are_Counted_Separately()
        {
            var limiter = new VoiceLimiter();
            limiter.TryStart(1, 0, 1, 1);

            Assert.That(limiter.TryStart(2, 0, 1, 1), Is.True);
        }

        [Test]
        public void Zero_Max_Means_No_Limit()
        {
            var limiter = new VoiceLimiter();
            for (var i = 0; i < 20; i++)
            {
                Assert.That(limiter.TryStart(1, 0, 1, 0), Is.True);
            }
        }

        [Test]
        public void Clear_Forgets_Playing_Voices()
        {
            var limiter = new VoiceLimiter();
            limiter.TryStart(1, 0, 5, 1);

            limiter.Clear();

            Assert.That(limiter.TryStart(1, 0.1, 5, 1), Is.True);
        }
    }
}
