using System.Collections;
using UnitySlotTower.Battle;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    /// <summary>
    /// 旧: Game シーンの開始時に開くリールのカスタム画面を閉じる手順。カスタムは廃止（2026-11-14、T341 で画面を削除）したので
    /// 1フレーム進めるだけ。呼び出し側のテストの書き直し（T348〜T353）で消す。
    /// </summary>
    internal static class ReelCustomTestUtil
    {
        /// <summary>BattleController の Start の後（Run ができた後）に呼ぶ。</summary>
        public static IEnumerator CloseWithDefault(BattleController battle)
        {
            yield return null;
        }
    }
}
