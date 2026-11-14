using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Ui;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;
using UnitySlotTower.Tests.PlayMode.Battle;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("AC-001 / FR-003: 実行中にコードが入れる数字も含め、タイトルのロゴ以外の画面に文字がなく、数字専用フォントで描く（T406）")]
    public sealed class ScreenTextTests
    {
        private const string NumericFontName = "NumericOnly SDF";

        // ゲームオーバー用: S1-W1 に近接の敵 12 体（ライフ 10 より多い）。
        private const string LosingWaves =
            "encounter_id,wave,time,enemy_id,spawn,path_id,is_boss\n" +
            "first,1,0,melee,S1,first_g1,0\nfirst,1,1,melee,S1,first_g1,0\nfirst,1,2,melee,S1,first_g1,0\nfirst,1,3,melee,S1,first_g1,0\n" +
            "first,1,4,melee,S1,first_g1,0\nfirst,1,5,melee,S1,first_g1,0\nfirst,1,6,melee,S1,first_g1,0\nfirst,1,7,melee,S1,first_g1,0\n" +
            "first,1,8,melee,S1,first_g1,0\nfirst,1,9,melee,S1,first_g1,0\nfirst,1,10,melee,S1,first_g1,0\nfirst,1,11,melee,S1,first_g1,0\n" +
            "first,2,0,melee,S1,first_g1,0\nbattle_a,1,0,melee,S1,battle_a_g1,0\nbattle_b,1,0,melee,S1,battle_b_g1,0\nbattle_c,1,0,melee,S1,battle_c_g1,0\nbattle_a,2,0,melee,S1,battle_a_g1,0\nbattle_b,2,0,melee,S1,battle_b_g1,0\nbattle_c,2,0,melee,S1,battle_c_g1,0\n" +
            "boss_a,1,0,melee,S1,boss_a_g1,0\nboss_a,2,0,melee,S1,boss_a_g1,0\n";

        // クリア用: 全6ウェーブとも近接の敵1体だけ。
        private const string WinningWaves =
            "encounter_id,wave,time,enemy_id,spawn,path_id,is_boss\n" +
            "first,1,0,melee,S1,first_g1,0\nfirst,2,0,melee,S1,first_g1,0\nbattle_a,1,0,melee,S1,battle_a_g1,0\nbattle_b,1,0,melee,S1,battle_b_g1,0\nbattle_c,1,0,melee,S1,battle_c_g1,0\nbattle_a,2,0,melee,S1,battle_a_g1,0\nbattle_b,2,0,melee,S1,battle_b_g1,0\nbattle_c,2,0,melee,S1,battle_c_g1,0\n" +
            "elite_a,1,0,melee,S1,elite_a_g1,0\nelite_a,2,0,melee,S1,elite_a_g1,0\n" +
            "boss_a,1,0,melee,S1,boss_a_g1,0\nboss_a,2,0,melee,S1,boss_a_g1,0\n";

        private static string _waves;
        private HitRandom _random;

        [SetUp]
        public void SetUp()
        {
            _random = new HitRandom();
            SlotCabinet.RandomOverride = _random;
            SceneManager.sceneLoaded += ReplaceWaves;
        }

        [TearDown]
        public void TearDown()
        {
            SceneManager.sceneLoaded -= ReplaceWaves;
            _waves = null;
            SlotCabinet.RandomOverride = null;
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator Title_And_Difficulty_Show_Only_The_Logo_As_Letters()
        {
            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);
            yield return null;
            AssertNoLetters("タイトル");

            GameObject.Find("Start Button").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(Object.FindAnyObjectByType<TitleScreen>().IsChoosingDifficulty, Is.True);
            AssertNoLetters("難易度選択");
        }

        [UnityTest]
        public IEnumerator Game_Screens_Up_To_Game_Over_Show_No_Letters()
        {
            _waves = LosingWaves;
            var battle = default(BattleController);
            yield return LoadGame(b => battle = b);
            AssertNoLetters("開始時");

            yield return ReelCustomTestUtil.CloseWithDefault(battle);
            AssertNoLetters("準備");

            // バーを揃えて効果の秒数を出す（T342 までのつなぎで、バーは旧の攻撃力アップ）。
            battle.StartWave();
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            _random.Index = -1;
            for (var i = 0; i < cabinet.Patterns.Count && _random.Index < 0; i++)
            {
                if (cabinet.Patterns[i] == Core.Slot.SlotSymbol.Bar)
                {
                    _random.Index = i;
                }
            }

            Assert.That(_random.Index, Is.GreaterThanOrEqualTo(0), "抽選表にバーがない");
            yield return SpinAndStopAll(cabinet);
            yield return null;
            AssertNoLetters("戦闘（効果中）");

            battle.SetPaused(true);
            var help = Object.FindAnyObjectByType<HelpScreen>();
            help.Open();
            yield return null;
            Assert.That(help.IsOpen, Is.True);
            AssertNoLetters("一時停止と早見表");
            battle.SetPaused(false);

            yield return PlayUntilFinished(battle);
            Assert.That(battle.Run.Phase, Is.EqualTo(WavePhase.GameOver));
            AssertNoLetters("リザルト（ゲームオーバー）");
        }

        [UnityTest]
        public IEnumerator Clear_Result_Shows_No_Letters()
        {
            _waves = WinningWaves;
            var battle = default(BattleController);
            yield return LoadGame(b => battle = b);
            yield return ReelCustomTestUtil.CloseWithDefault(battle);
            // ルートは最長7戦（T350a）で、敵1体ずつでもライフ 10 では足りないため、戦闘マスごとにライフを戻す。
            var run = Object.FindAnyObjectByType<RunController>();
            void Refill() => run.State.Heal(run.State.MaxLife);
            battle.NodeBegan += Refill;

            yield return PlayUntilFinished(battle);
            battle.NodeBegan -= Refill;
            Assert.That(battle.Run.Phase, Is.EqualTo(WavePhase.Completed));
            AssertNoLetters("リザルト（クリア）");
        }

        // 表示中かどうかに関わらず、読み込まれた全シーンの文字部品を調べる（隠れた部品も後で出るため）。
        private static void AssertNoLetters(string screen)
        {
            var violations = new List<string>();
            var onTitle = SceneManager.GetActiveScene().name == GameSession.TitleScene;
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (onTitle && text.name == "Logo")
                {
                    continue;
                }

                var index = NumericText.FindInvalid(text.text);
                if (index >= 0)
                {
                    violations.Add($"{PathOf(text.transform)}: '{text.text}' の '{text.text[index]}'");
                }

                if (text.font == null || text.font.name != NumericFontName)
                {
                    violations.Add($"{PathOf(text.transform)}: フォント {(text.font != null ? text.font.name : "なし")}");
                }
            }

            Assert.That(violations, Is.Empty, $"{screen}: " + string.Join("\n", violations));
        }

        private static string PathOf(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }

        private static IEnumerator LoadGame(System.Action<BattleController> found)
        {
            yield return SceneManager.LoadSceneAsync(GameSession.GameScene);
            var battle = Object.FindAnyObjectByType<BattleController>();
            for (var i = 0; i < 300 && battle.Run == null; i++)
            {
                yield return null;
            }

            Assert.That(battle.Run, Is.Not.Null, "Game シーンの進行が作られない");
            yield return null;
            found(battle);
        }

        // レバーを押し、リールが自動で止まるまで待つ。
        private static IEnumerator SpinAndStopAll(SlotCabinet cabinet)
        {
            var lever = cabinet.LeverRect.GetComponent<Button>();
            var until = Time.realtimeSinceStartup + 5f;
            while (!cabinet.IsSpinning && Time.realtimeSinceStartup < until)
            {
                lever.onClick.Invoke();
                yield return null;
            }

            Assert.That(cabinet.IsSpinning, Is.True, "レバーで回らない");
            // 自動停止は実時間（約2秒）なので、フレームが速くても足りるよう時間で待つ。
            until = Time.realtimeSinceStartup + 5f;
            while (cabinet.IsSpinning && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(cabinet.IsSpinning, Is.False, "リールが止まらない");
        }

        private static IEnumerator PlayUntilFinished(BattleController battle)
        {
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 90f;
            while (!battle.IsFinished && Time.realtimeSinceStartup < limit)
            {
                battle.StartWave();
                RouteTestUtil.Step();
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(battle.IsFinished, Is.True, "時間内にゲームが終わらない");
            yield return null;
        }

        private static void ReplaceWaves(Scene scene, LoadSceneMode mode)
        {
            if (_waves == null || scene.name != GameSession.GameScene)
            {
                return;
            }

            var battle = Object.FindAnyObjectByType<BattleController>();
            typeof(BattleController)
                .GetField("wavesTable", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(battle, new TextAsset(_waves));
        }

        // 20パターンの Index 番目を選ぶ（ハズレのときの止まり方は Index 番目の組み合わせ）。
        private sealed class HitRandom : IRandom
        {
            public int Index;

            public int NextInt(int minInclusive, int maxExclusive) => Index;

            public double NextDouble() => 0.999;
        }
    }
}
