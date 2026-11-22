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
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Ui;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("AC-001 / FR-003: 実行中にコードが入れる数字も含め、タイトルのロゴ以外の画面（戦闘・リザルト）に" +
                 "文字がなく、数字専用フォントで描く（T406, T353）")]
    public sealed class ScreenTextTests
    {
        private const string NumericFontName = "NumericOnly SDF";

        // ゲームオーバー用: S1-W1 に近接の敵 12 体（ライフ 10 より多い）。
        private const string LosingWaves =
            "stage,time,enemy_id,spawn,path_id,is_boss\n" +
            "1,0,melee,S1,first_g1,0\n1,1,melee,S1,first_g1,0\n1,2,melee,S1,first_g1,0\n1,3,melee,S1,first_g1,0\n" +
            "1,4,melee,S1,first_g1,0\n1,5,melee,S1,first_g1,0\n1,6,melee,S1,first_g1,0\n1,7,melee,S1,first_g1,0\n" +
            "1,8,melee,S1,first_g1,0\n1,9,melee,S1,first_g1,0\n1,10,melee,S1,first_g1,0\n1,11,melee,S1,first_g1,0\n" +
            "1,3,melee,S1,first_g1,0\n2,0,melee,S1,battle_a_g1,0\n3,0,melee,S1,battle_b_g1,0\n5,0,melee,S1,battle_c_g1,0\n6,0,melee,S1,battle_d_g1,0\n7,0,melee,S1,battle_e_g1,0\n2,3,melee,S1,battle_a_g1,0\n3,3,melee,S1,battle_b_g1,0\n5,3,melee,S1,battle_c_g1,0\n6,3,melee,S1,battle_d_g1,0\n7,3,melee,S1,battle_e_g1,0\n" +
            "8,0,melee,S1,boss_a_g1,0\n8,3,melee,S1,boss_a_g1,0\n";

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
        public IEnumerator Title_Shows_Only_The_Logo_As_Letters()
        {
            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);
            yield return null;
            AssertNoLetters("タイトル");
        }

        [UnityTest]
        public IEnumerator Game_Screens_Up_To_Game_Over_Show_No_Letters()
        {
            _waves = LosingWaves;
            var battle = default(BattleController);
            yield return LoadGame(b => battle = b);
            AssertNoLetters("開始時（準備）");

            // バーを揃えて操作バーに攻撃力の倍率を出す。
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
            _waves = StageTestUtil.ShortWaves;
            var battle = default(BattleController);
            yield return LoadGame(b => battle = b);
            // どのステージも敵1体だけなので、味方を置かなくてもライフ 10 のうち 8 を失ってクリアする。
            yield return PlayUntilFinished(battle);
            Assert.That(battle.Run.Phase, Is.EqualTo(WavePhase.Completed));
            AssertNoLetters("リザルト（クリア）");
        }

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

                // 文字あり版のラベルは対象外。代わりに文字なし版では描かれないことを確かめる（T352、plan 16.9）。
                if (text.GetComponentInParent<TextModeLabel>(true) != null)
                {
                    if (text.isActiveAndEnabled)
                    {
                        violations.Add($"{PathOf(text.transform)}: 文字なし版で文字あり版のラベルが出ている");
                    }

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
            var limit = Time.realtimeSinceStartup + 90f + 7 * BattlePresentationTiming.VictoryBand;
            while (!battle.IsFinished && Time.realtimeSinceStartup < limit)
            {
                if (!battle.IsShowingVictory && (battle.IsInIntro || battle.Run.Phase == WavePhase.Fighting))
                    Time.timeScale = 20f;
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
