using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    [Description("FR-034, plan 17.11: 群れで同時に約20体を超える敵がいても、Editor のフレーム時間が 30fps を下回らない")]
    public sealed class CrowdFrameTimeTests
    {
        // 最初の戦闘に3レーンから5体ずつ×5群れ（25体）を2.5秒以内に出す。道は10マスで、先頭がゴールに着くのは約10秒後。
        // 一番多いウェーブ（boss_a）の同時に出ている敵の数（20体ほど）より多い。最初の戦闘の中央レーンの決まりは検証器の対象で、ここでは使わない。
        private const string CrowdWaves =
            "encounter_id,time,enemy_id,spawn,path_id,is_boss,count,interval\n" +
            "first,0,melee,S1,first_g1,0,5,0.5\n" +
            "first,0,melee,S2,first_g2,0,5,0.5\n" +
            "first,0,melee,S3,first_g3,0,5,0.5\n" +
            "first,0.25,ranged,S1,first_g1,0,5,0.5\n" +
            "first,0.25,ranged,S3,first_g3,0,5,0.5\n";

        private const int Crowd = 20;
        private const int Frames = 120;

        private bool _runInBackground;

        [SetUp]
        public void SetUp()
        {
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Application.runInBackground = _runInBackground;
        }

        [UnityTest]
        public IEnumerator Frame_Time_Stays_Under_30fps_With_A_Crowd_Of_Enemies()
        {
            SceneManager.sceneLoaded += ReplaceWaves;
            try
            {
                yield return SceneManager.LoadSceneAsync("Game");
            }
            finally
            {
                SceneManager.sceneLoaded -= ReplaceWaves;
            }

            var battle = Object.FindAnyObjectByType<BattleController>();
            for (var i = 0; i < 60 && battle.Run == null; i++)
            {
                yield return null;
            }

            Assert.That(battle.Run, Is.Not.Null, "Game シーンの進行が作られない");
            yield return RouteTestUtil.WaitForIntro(battle);

            var limit = Time.realtimeSinceStartup + 15f;
            while (battle.Field.Enemies.Count < Crowd && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            Assert.That(battle.Field.Enemies.Count, Is.GreaterThanOrEqualTo(Crowd), "群れの敵がそろわない");

            var total = 0f;
            var worst = 0f;
            var fewest = int.MaxValue;
            for (var i = 0; i < Frames; i++)
            {
                yield return null;
                var dt = Time.unscaledDeltaTime;
                total += dt;
                worst = Mathf.Max(worst, dt);
                fewest = Mathf.Min(fewest, battle.Field.Enemies.Count);
            }

            var average = total / Frames;
            var report = $"[CrowdFrameTime] 敵 {fewest} 体以上で {Frames} フレーム: 平均 {average * 1000f:F1} ms・最大 {worst * 1000f:F1} ms";
            Debug.Log(report);
            // Console を読めない環境でも測定値を残す（Artifacts は Git の対象外）。
            var folder = System.IO.Path.Combine(Application.dataPath, "..", "Artifacts", "Automation", "Editor", "t362");
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "crowd-frame-time.txt"), report + "\n");
            Assert.That(battle.Run.Phase, Is.EqualTo(WavePhase.Fighting), "測定中に戦闘が終わった");
            Assert.That(fewest, Is.GreaterThanOrEqualTo(Crowd - 2), "測定中に敵が減りすぎた");
            Assert.That(average, Is.LessThan(1f / 30f), $"平均フレーム時間 {average * 1000f:F1} ms が 30fps を下回る");
        }

        private static void ReplaceWaves(Scene scene, LoadSceneMode mode)
        {
            var battle = Object.FindAnyObjectByType<BattleController>();
            typeof(BattleController)
                .GetField("wavesTable", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(battle, new TextAsset(CrowdWaves));
        }
    }
}
