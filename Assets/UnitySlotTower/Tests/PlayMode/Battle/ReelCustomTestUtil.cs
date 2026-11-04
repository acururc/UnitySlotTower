using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    /// <summary>Game シーンの開始時に開くリールのカスタム画面（T322）を、空きマスをすべてコインで埋めて確定し閉じる。</summary>
    internal static class ReelCustomTestUtil
    {
        /// <summary>BattleController の Start の後（Run ができた後）に呼ぶ。確定したフレームは ▶ を受け付けないので1フレーム進める。</summary>
        public static IEnumerator CloseWithDefault(BattleController battle)
        {
            var screen = Object.FindAnyObjectByType<ReelCustomScreen>();
            Assert.That(screen != null && screen.IsOpen && battle.IsCustomizing, Is.True, "開始時にカスタム画面が開いていない");
            FillEmpty(screen.Customizer, SlotSymbol.Coin);
            Assert.That(screen.Confirm(), Is.True, "空きを埋めても確定できない");
            yield return null;
        }

        /// <summary>空きマスをすべて symbol で埋める。</summary>
        public static void FillEmpty(ReelCustomizer customizer, SlotSymbol symbol)
        {
            for (var i = 0; i < customizer.Length; i++)
            {
                if (customizer.At(i) == null)
                {
                    Assert.That(customizer.Place(i, symbol), Is.True, $"マス {i} に置けない");
                }
            }
        }
    }
}
