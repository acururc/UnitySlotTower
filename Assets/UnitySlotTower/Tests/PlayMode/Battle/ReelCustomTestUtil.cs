using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    /// <summary>Game シーンの開始時に開くリールのカスタム画面（T316）を、既定配列のまま確定して閉じる。</summary>
    internal static class ReelCustomTestUtil
    {
        /// <summary>BattleController の Start の後（Run ができた後）に呼ぶ。確定したフレームは ▶ を受け付けないので1フレーム進める。</summary>
        public static IEnumerator CloseWithDefault(BattleController battle)
        {
            var screen = Object.FindAnyObjectByType<ReelCustomScreen>();
            Assert.That(screen != null && screen.IsOpen && battle.IsCustomizing, Is.True, "開始時にカスタム画面が開いていない");
            Assert.That(screen.Confirm(), Is.True, "既定配列で確定できない");
            yield return null;
        }
    }
}
