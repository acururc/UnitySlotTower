using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
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
        /// 戦闘マスの開始の演出（FR-019、既定 2 秒の実時間）が終わるまで待つ。演出中は配置などの操作を受け付けないので、
        /// 戦闘マスに入った直後に操作するテストはこれを挟む。
        /// </summary>
        public static System.Collections.IEnumerator WaitForIntro(UnitySlotTower.Battle.BattleController battle)
        {
            var limit = Time.realtimeSinceStartup + 10f;
            while (battle.IsInIntro && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            Assert.That(battle.IsInIntro, Is.False, "開始の演出が終わらない");
        }

        /// <summary>
        /// ルート選びのリール（入った時点で自動で回る: D-90）が回り出すまで待つ。戦闘の最後の回転が止まり終わる前にルート選びに
        /// 入ると、止まり終えてから回り出す。
        /// </summary>
        public static System.Collections.IEnumerator WaitForRouteSpin(SlotCabinet cabinet)
        {
            var limit = Time.realtimeSinceStartup + 5f;
            while (!cabinet.IsRouteSpinning && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            Assert.That(cabinet.IsRouteSpinning, Is.True, "ルート選びに入ってもリールが回り出さない");
        }

        /// <summary>
        /// ルート選び中なら、リールが回り出したら戦闘マスを優先して行き先のあるリールを止める（旧 T350a の自動進行と同じ選び方）。
        /// ショップ・回復の画面（T350c）が開いていれば ◀ で閉じる。
        /// </summary>
        public static void Step()
        {
            var run = Object.FindAnyObjectByType<RunController>();
            if (run != null && run.OpenScreen != null)
            {
                run.OpenScreen.BackButton.onClick.Invoke();
                return;
            }

            var cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            if (run == null || cabinet == null || !run.IsChoosingRoute || !cabinet.IsChoosingRoute ||
                cabinet.RoutePick.HasValue || !cabinet.IsRouteSpinning)
            {
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

        /// <summary>
        /// 2階にその種類のマスがあるシード（<see cref="RunController.SeedOverride"/> 用）。RunController はシードの乱数で最初にルートを
        /// 作るので、同じ表とシードなら同じルートになる。表はプロジェクトの route_rules.csv（Editor で実行するテストだけで使う）。
        /// </summary>
        public static ulong SeedWithSecondFloor(string kind)
        {
            var path = Path.Combine(Application.dataPath, "UnitySlotTower/Data/Tables", RouteRules.FileName);
            var rules = RouteRules.Load(CsvParser.Parse(RouteRules.FileName, File.ReadAllText(path)));
            for (ulong seed = 1; seed < 5000; seed++)
            {
                if (ReelTo(RouteGenerator.Generate(rules, new SeededRandom(seed)).Start, kind) >= 0)
                {
                    return seed;
                }
            }

            Assert.Fail($"2階に {kind} のあるシードが見つからない");
            return 0;
        }

        /// <summary>今のマスから、その種類の行き先に止めるリールの番号。なければ -1。</summary>
        public static int ReelTo(RouteNode current, string kind)
        {
            for (var reel = 0; reel < 3; reel++)
            {
                if (current.NextOnReel(reel)?.Kind == kind)
                {
                    return reel;
                }
            }

            return -1;
        }
    }
}
