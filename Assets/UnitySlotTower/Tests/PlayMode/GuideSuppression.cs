using NUnit.Framework;
using UnitySlotTower.Flow;

namespace UnitySlotTower.Tests.PlayMode
{
    /// <summary>
    /// 文字なし説明（T403）は戦闘の時間を止め、暗幕で操作バーのクリックを遮るので、PlayMode テストでは既定で出さない。
    /// 説明そのものを確かめるテスト（GuideTests）だけが SetUp で有効にし、TearDown で戻す。
    /// </summary>
    [SetUpFixture]
    public sealed class GuideSuppression
    {
        [OneTimeSetUp]
        public void DisableGuide() => GuideOverlay.DisabledForTests = true;

        [OneTimeTearDown]
        public void RestoreGuide() => GuideOverlay.DisabledForTests = false;
    }
}
