using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Flow;
using Object = UnityEngine.Object;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    /// <summary>
    /// 全画面を文字なし・文字ありの両方で開いて PNG に保存する撮影用の手順（T359、plan 16.13）。判定はしない。
    /// 通常のテスト実行では動かない（Explicit）。Editor の <c>Tools/UnitySlotTower/Automation/Capture Screens (T359)</c> で実行し、
    /// <c>Artifacts/Automation/Editor/t359/&lt;日時&gt;/</c> に <c>{notext|text}-{画面}.png</c> を書く。
    /// </summary>
    [Explicit("撮影用。Capture Screens (T359) メニューから実行する")]
    [Category(Category)]
    public sealed class ScreenCaptureTests
    {
        public const string Category = "ScreenCapture";

        // どの戦闘マスも各ウェーブ近接の敵1体だけ（最初の戦闘を早く終える）。
        private const string ShortWaves =
            "encounter_id,time,enemy_id,spawn,path_id,is_boss\n" +
            "first,0,melee,S1,first_g1,0\nfirst,3,melee,S1,first_g1,0\n" +
            "battle_a,0,melee,S1,battle_a_g1,0\nbattle_a,3,melee,S1,battle_a_g1,0\n" +
            "battle_b,0,melee,S1,battle_b_g1,0\nbattle_b,3,melee,S1,battle_b_g1,0\n" +
            "battle_c,0,melee,S1,battle_c_g1,0\nbattle_d,0,melee,S1,battle_d_g1,0\nbattle_e,0,melee,S1,battle_e_g1,0\nbattle_c,3,melee,S1,battle_c_g1,0\nbattle_d,3,melee,S1,battle_d_g1,0\nbattle_e,3,melee,S1,battle_e_g1,0\n" +
            "elite_a,0,melee,S1,elite_a_g1,0\nelite_a,3,melee,S1,elite_a_g1,0\n" +
            "boss_a,0,melee,S1,boss_a_g1,0\nboss_a,3,melee,S1,boss_a_g1,0\n";

        private const float Settle = 0.4f;

        private bool _runInBackground;
        private string _folder;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            _folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "Automation", "Editor", "t359",
                DateTime.Now.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(_folder);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.SetTextMode(false);
            GuideOverlay.DisabledForTests = true;
            RunController.SeedOverride = null;
            Time.timeScale = 1f;
            Application.runInBackground = _runInBackground;
            Debug.Log($"[ScreenCapture] {_folder}");
        }

        [UnityTest]
        public IEnumerator Capture_All_Screens_In_Both_Modes()
        {
            foreach (var textMode in new[] { false, true })
            {
                yield return CaptureMode(textMode);
            }
        }

        private IEnumerator CaptureMode(bool textMode)
        {
            var prefix = textMode ? "text" : "notext";
            GameSession.SetTextMode(textMode);

            // タイトルと難易度選択。
            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);
            yield return Capture(prefix + "-title");
            GameObject.Find("Start Button").GetComponent<Button>().onClick.Invoke();
            yield return Capture(prefix + "-difficulty");

            // 戦闘（最初の説明が出ている状態）と早見表。
            RunController.SeedOverride = 1;
            GuideOverlay.DisabledForTests = false;
            var battle = default(BattleController);
            yield return LoadGame(b => battle = b);
            yield return Capture(prefix + "-game");
            battle.SetPaused(true);
            var help = Object.FindAnyObjectByType<HelpScreen>();
            help.Open();
            yield return Capture(prefix + "-help");
            help.Close();
            battle.SetPaused(false);

            // 最初の戦闘を終えてルート、そこからショップ・回復・リザルトを直接開く。
            GuideOverlay.DisabledForTests = true;
            yield return LoadGame(b => battle = b);
            var run = Object.FindAnyObjectByType<RunController>();
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 30f;
            while (!run.IsChoosingRoute && battle.Run.Phase != WavePhase.GameOver && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(run.IsChoosingRoute, Is.True, "最初の戦闘マスを終えてもルート選びにならない");
            yield return Capture(prefix + "-route");

            yield return CaptureNodeScreen<ShopScreen>(run, RouteNodeKinds.Shop, prefix + "-shop");
            yield return CaptureNodeScreen<RestScreen>(run, RouteNodeKinds.Rest, prefix + "-rest");

            var result = Object.FindAnyObjectByType<ResultScreen>();
            typeof(ResultScreen).GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(result, new object[] { battle.Run });
            yield return Capture(prefix + "-result");
        }

        private IEnumerator CaptureNodeScreen<T>(RunController run, string kind, string name)
            where T : NodeScreen
        {
            var node = FindNode(run.State.Route, kind);
            Assert.That(node, Is.Not.Null, $"ルートに {kind} のマスがない");
            var screen = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            screen.Open(run, node);
            yield return Capture(name);
            screen.Close();
            yield return null;
        }

        private static RouteNode FindNode(RouteMap route, string kind)
        {
            for (var floor = 1; floor <= route.Floors; floor++)
            {
                foreach (var node in route.NodesOn(floor))
                {
                    if (node.Kind == kind)
                    {
                        return node;
                    }
                }
            }

            return null;
        }

        private IEnumerator Capture(string name)
        {
            var until = Time.realtimeSinceStartup + Settle;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(Path.Combine(_folder, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                Object.Destroy(texture);
            }
        }

        private static IEnumerator LoadGame(Action<BattleController> found)
        {
            SceneManager.sceneLoaded += ReplaceWaves;
            try
            {
                yield return SceneManager.LoadSceneAsync(GameSession.GameScene);
            }
            finally
            {
                SceneManager.sceneLoaded -= ReplaceWaves;
            }

            var battle = Object.FindAnyObjectByType<BattleController>();
            for (var i = 0; i < 300 && battle.Run == null; i++)
            {
                yield return null;
            }

            Assert.That(battle.Run, Is.Not.Null, "Game シーンの進行が作られない");
            found(battle);
        }

        private static void ReplaceWaves(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameSession.GameScene)
            {
                return;
            }

            var battle = Object.FindAnyObjectByType<BattleController>();
            typeof(BattleController)
                .GetField("wavesTable", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(battle, new TextAsset(ShortWaves));
        }
    }
}
