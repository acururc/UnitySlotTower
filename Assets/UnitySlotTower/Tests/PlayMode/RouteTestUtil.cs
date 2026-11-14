using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode
{
    /// <summary>
    /// ルート画面（T350b）を通り抜けるテスト用の手順。戦闘を早送りするループで毎フレーム <see cref="Step"/> を呼ぶ。
    /// </summary>
    internal static class RouteTestUtil
    {
        /// <summary>
        /// ルート選び中なら、レバーを引き、回り出したら戦闘マスを優先して行き先のあるリールを止める（旧 T350a の自動進行と同じ選び方）。
        /// </summary>
        public static void Step()
        {
            var run = Object.FindAnyObjectByType<RunController>();
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            if (run == null || cabinet == null || !run.IsChoosingRoute || !cabinet.IsChoosingRoute ||
                cabinet.RoutePick.HasValue || cabinet.IsSpinning && !cabinet.IsRouteSpinning)
            {
                return;
            }

            if (!cabinet.IsRouteSpinning)
            {
                cabinet.LeverRect.GetComponent<Button>().onClick.Invoke();
                return;
            }

            cabinet.PickRouteReel(PreferredReel(run.State.Current));
        }

        /// <summary>行き先に戦闘マスがあればその最初（列の順）のリール、なければ最初の行き先のリール。</summary>
        public static int PreferredReel(RouteNode current)
        {
            foreach (var next in current.Next)
            {
                if (RouteNodeKinds.Get(next.Kind).IsBattle)
                {
                    return current.ReelOf(next);
                }
            }

            return current.ReelOf(current.Next[0]);
        }
    }
}
