using NUnit.Framework;
using UnitySlotTower.Battle;

namespace UnitySlotTower.Tests.PlayMode
{
    /// <summary>
    /// PlayMode テスト全体の前後処理。ステージ1の見せ場（spec 015 FR-001、ウェーブ1で止まってレバーを待つ）は既定で飛ばす。
    /// 見せ場を見るテスト（<c>SlotBetShowcaseTests</c>・<c>ScreenCaptureTests</c>）は自分の SetUp で false にし、TearDown で true に戻す。
    /// </summary>
    [SetUpFixture]
    public sealed class PlayModeTestSetup
    {
        [OneTimeSetUp]
        public void SkipShowcase() => BattleController.SkipShowcase = true;

        [OneTimeTearDown]
        public void RestoreShowcase() => BattleController.SkipShowcase = false;
    }
}
