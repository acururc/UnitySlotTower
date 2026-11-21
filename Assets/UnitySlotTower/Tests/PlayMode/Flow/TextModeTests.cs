using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("AC-005 / FR-006 (T352): 起動時は文字なし。タイトルの隠しボタンで文字ありになり主な画面にラベルが出る。戻すと消え、配置は変わらない")]
    public sealed class TextModeTests
    {
        // どの戦闘マスも各ウェーブ近接の敵1体だけ（最初の戦闘を早く終える）。
        private const string ShortWaves =
            "stage,time,enemy_id,spawn,path_id,is_boss\n" +
            "1,0,melee,S1,first_g1,0\n1,3,melee,S1,first_g1,0\n" +
            "2,0,melee,S1,battle_a_g1,0\n2,3,melee,S1,battle_a_g1,0\n" +
            "3,0,melee,S1,battle_b_g1,0\n3,3,melee,S1,battle_b_g1,0\n" +
            "5,0,melee,S1,battle_c_g1,0\n6,0,melee,S1,battle_d_g1,0\n7,0,melee,S1,battle_e_g1,0\n5,3,melee,S1,battle_c_g1,0\n6,3,melee,S1,battle_d_g1,0\n7,3,melee,S1,battle_e_g1,0\n" +
            "4,0,melee,S1,elite_a_g1,0\n4,3,melee,S1,elite_a_g1,0\n" +
            "8,0,melee,S1,boss_a_g1,0\n8,3,melee,S1,boss_a_g1,0\n";

        private bool _runInBackground;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            GameSession.SetTextMode(false);
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
        }

        [UnityTest]
        public IEnumerator Title_Hidden_Button_Toggles_Text_Mode_And_Shows_The_Mark()
        {
            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);
            yield return null;
            var title = Object.FindAnyObjectByType<TitleScreen>();
            Assert.That((GameSession.TextMode, ShownIds().Count), Is.EqualTo((false, 0)), "起動時に文字あり版になっている");
            Assert.That(title.TextModeButton, Is.Not.Null, "隠しボタンがない");
            Assert.That(title.TextModeButton.navigation.mode, Is.EqualTo(Navigation.Mode.None), "隠しボタンがキーボードで選べる");

            var before = Layout();
            title.TextModeButton.onClick.Invoke();
            var after = Layout();
            yield return null;
            Assert.That(GameSession.TextMode, Is.True, "隠しボタンで文字あり版にならない");
            Assert.That(ShownIds(), Does.Contain("title_text_mode").And.Contain("title_start"), "「あ」の印か ▶ のラベルが出ない");
            AssertSameLayout(before, after);

            title.TextModeButton.onClick.Invoke();
            yield return null;
            Assert.That((GameSession.TextMode, ShownIds().Count), Is.EqualTo((false, 0)), "もう一度押しても文字なし版に戻らない");
        }

        [UnityTest]
        public IEnumerator Game_Bar_Cabinet_Guide_And_Help_Show_Labels_Only_In_Text_Mode()
        {
            GuideOverlay.DisabledForTests = false;
            var battle = default(BattleController);
            yield return LoadGame(b => battle = b);
            var guide = Object.FindAnyObjectByType<GuideOverlay>();
            for (var i = 0; i < 30 && guide.Current == Core.Guide.GuideHint.None; i++)
            {
                yield return null;
            }

            Assert.That(ShownIds(), Is.Empty, "文字なし版でラベルが出ている");

            // 同じフレームで切り替え、守る枠（戦闘カメラの Viewport と筐体）が動かないことを確かめる（AC-005、plan 16.13）。
            // ほかの部品は文字あり版で配置を差し替える（D-76）。
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>().transform;
            var camera = GameObject.Find("Battle Camera").GetComponent<Camera>();
            var before = Layout(cabinet);
            var viewport = camera.rect;
            GameSession.SetTextMode(true);
            AssertSameLayout(before, Layout(cabinet));
            Assert.That(camera.rect, Is.EqualTo(viewport), "文字あり版で戦闘カメラの Viewport が変わった");
            yield return null;

            var shown = ShownIds();
            Assert.That(shown, Does.Contain("unit_tank").And.Contain("unit_buffer").And.Contain("bar_upgrade").And.Contain("life")
                .And.Contain("bar_pause").And.Contain("cabinet_lever").And.Contain("cabinet_credit")
                .And.Contain("bar_remaining").And.Contain("bar_speed"), "操作バーか筐体にラベルが出ない");
            Assert.That(shown, Does.Contain(GuideOverlay.CaptionId(guide.Current)), "説明の文が出ない");

            battle.SetPaused(true);
            var help = Object.FindAnyObjectByType<HelpScreen>();
            help.Open();
            yield return null;
            Assert.That(help.IsOpen, Is.True);
            Assert.That(ShownIds(), Does.Contain("help_title").And.Contain("help_close").And.Contain("help_tab_slot")
                .And.Contain("help_tab_units").And.Contain("slot_red7").And.Contain("help_slot_heading").And.Contain("help_col_chance"));
            Assert.That(ShownIds(), Does.Not.Contain("help_units_heading"));
            help.UnitTab.onClick.Invoke();
            yield return null;
            Assert.That(ShownIds(), Does.Contain("help_units_heading").And.Contain("help_col_range").And.Contain("stat_hp").And.Contain("skill_sword"));
            Assert.That(ShownIds(), Does.Not.Contain("help_slot_heading"));
            help.CloseButton.onClick.Invoke();
            yield return null;
            Assert.That((help.IsOpen, help.OpenButton.gameObject.activeInHierarchy, ShownIds().Contains("help_open")),
                Is.EqualTo((false, true, true)), "文字あり版で閉じられないか、「?」に名前が出ない");
            help.OpenButton.onClick.Invoke();
            yield return null;
            Assert.That(help.IsOpen, Is.True, "文字あり版で開き直せない");
            var skillPictures = 0;
            foreach (var rect in help.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == "Help Skill Picture") skillPictures++;
            Assert.That(skillPictures, Is.EqualTo(4), "早見表を開き直すとスキルの絵が増えるか、職の絵が足りない");

            GameSession.SetTextMode(false);
            yield return null;
            Assert.That(ShownIds(), Is.Empty, "文字なし版に戻してもラベルが消えない");
            help.Close();
            battle.SetPaused(false);
        }

        [UnityTest]
        [Description("T359 (plan 16.13): 文字あり版にして戻すと、全部品の配置・字の大きさ・色・表示が元と同じ（文字なし版の見た目が変わらない）")]
        public IEnumerator Switching_Back_Restores_Every_Part_Of_The_No_Text_Screen()
        {
            var battle = default(BattleController);
            yield return LoadGame(b => battle = b);
            battle.SetPaused(true);
            var help = Object.FindAnyObjectByType<HelpScreen>();
            help.Open();
            yield return null;

            // アニメの影響を受けないように同じフレームで切り替えて戻す。
            var before = Layout();
            var looks = Looks();
            GameSession.SetTextMode(true);
            GameSession.SetTextMode(false);
            AssertSameLayout(before, Layout());
            var changed = new List<string>();
            foreach (var pair in Looks())
            {
                if (!looks.TryGetValue(pair.Key, out var look) || look != pair.Value)
                {
                    changed.Add(pair.Key.name);
                }
            }

            Assert.That(changed, Is.Empty, "戻しても見た目が元に戻らない: " + string.Join(", ", changed));
            help.Close();
            battle.SetPaused(false);
        }

        [UnityTest]
        [Description("T359g (plan 16.13, AC-005): 文字あり版の全画面で、表示中の日本語は 36 以上・画面からはみ出さない・省略されない・同じ親の中で重ならない")]
        public IEnumerator Every_Screen_Shows_Japanese_Text_Large_Inside_And_Apart()
        {
            GameSession.SetTextMode(true);
            var problems = new List<string>();

            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);
            yield return Settle();
            Readable("タイトル", problems);

            // 戦闘（最初の説明が出ている状態）、一時停止中の「?」、早見表。
            RunController.SeedOverride = 1;
            GuideOverlay.DisabledForTests = false;
            var battle = default(BattleController);
            yield return LoadGame(b => battle = b);
            yield return Settle();
            Readable("戦闘", problems);
            battle.SetPaused(true);
            yield return Settle();
            Assert.That(ShownIds(), Does.Contain("help_open").And.Contain("pause_home"));
            Readable("一時停止", problems);
            var quit = Object.FindAnyObjectByType<QuitToTitle>();
            quit.HomeButton.onClick.Invoke();
            yield return Settle();
            Assert.That(ShownIds(), Does.Contain("pause_question").And.Contain("pause_yes").And.Contain("pause_no"));
            Readable("タイトル確認", problems);
            quit.CancelButton.onClick.Invoke();
            yield return Settle();
            var skillHelp = Object.FindAnyObjectByType<SkillHelpPanel>();
            GameObject.Find("Skill Gauge 1").GetComponent<Button>().onClick.Invoke();
            yield return Settle();
            Assert.That(skillHelp.IsOpen, Is.True, "タンクのスキル説明が開かない");
            Assert.That(ShownIds(), Does.Contain("skill_tank").And.Contain("skill_help_off").And.Contain("skill_help_on"));
            Readable("スキルの説明", problems);
            skillHelp.Close();
            yield return Settle();
            var help = Object.FindAnyObjectByType<HelpScreen>();
            help.Open();
            yield return Settle();
            Readable("早見表 スロット", problems);
            help.UnitTab.onClick.Invoke();
            yield return Settle();
            Readable("早見表 味方", problems);
            help.Close();
            battle.SetPaused(false);

            // リザルトを直接開く（ルート・ショップ・回復はない: spec 002 FR-101）。
            GuideOverlay.DisabledForTests = true;
            yield return LoadGame(b => battle = b);
            var result = Object.FindAnyObjectByType<ResultScreen>();
            typeof(ResultScreen).GetMethod("Open", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(result, new object[] { battle.Run });
            yield return Settle();
            Readable("リザルト", problems);

            Assert.That(problems, Is.Empty, "読みにくい文字:\n" + string.Join("\n", problems));
        }

        [UnityTest]
        public IEnumerator Visible_Text_Mode_Labels_Use_Styled_Backgrounds_In_Title_And_Battle()
        {
            GameSession.SetTextMode(true);
            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);
            yield return Settle();
            AssertStyledBackgrounds("Title");
            var battle = default(BattleController);
            yield return LoadGame(b => battle = b);
            yield return Settle();
            AssertStyledBackgrounds("Game");
        }

        private static void AssertStyledBackgrounds(string screen)
        {
            var shown = 0;
            foreach (var label in Object.FindObjectsByType<TextModeLabel>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!label.IsShown) continue;
                shown++;
                Assert.That(label.Background == null || label.Background is UiPanel, Is.True,
                    $"{screen}: {label.name} に平らな下地が残っている");
            }
            Assert.That(shown, Is.GreaterThan(0), $"{screen}: 表示中のラベルがない");
        }

        private static void Readable(string screen, List<string> problems) =>
            Assert.That(TextReadability.Collect(screen, problems), Is.GreaterThan(0), $"{screen}: 表示中の日本語のテキストが見つからない");

        // 開く動きが落ち着くまで実時間で待つ。
        private static IEnumerator Settle()
        {
            var until = Time.realtimeSinceStartup + 0.4f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        // ラベル以外の文字の大きさ・絵の色・CanvasGroup の濃さ（部品のパスごと）。
        private static Dictionary<Graphic, string> Looks()
        {
            var looks = new Dictionary<Graphic, string>();
            foreach (var graphic in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (graphic.GetComponentInParent<TextModeLabel>(true) != null)
                {
                    continue;
                }

                var size = graphic is TMPro.TMP_Text text ? $"{text.fontSize}/{text.enableAutoSizing}/{text.fontSizeMin}/{text.fontSizeMax}" : "";
                var group = graphic.GetComponent<CanvasGroup>();
                looks[graphic] = $"{graphic.color}|{size}|{(group != null ? group.alpha : 1f)}";
            }

            return looks;
        }

        // いま描かれているラベルの id（文言を実行中に入れたものは最初の id）。
        private static List<string> ShownIds()
        {
            var ids = new List<string>();
            foreach (var label in Object.FindObjectsByType<TextModeLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label.isActiveAndEnabled && label.IsShown)
                {
                    ids.Add(label.Id);
                }
            }

            return ids;
        }

        // ラベル以外の全 UI 部品の画面上の矩形（within を渡せばその子孫だけ）。
        private static Dictionary<RectTransform, Rect> Layout(Transform within = null)
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            var layout = new Dictionary<RectTransform, Rect>();
            foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (rect.GetComponentInParent<TextModeLabel>(true) != null || (within != null && !rect.IsChildOf(within)))
                {
                    continue;
                }

                rect.GetWorldCorners(corners);
                layout[rect] = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            }

            return layout;
        }

        private static void AssertSameLayout(Dictionary<RectTransform, Rect> before, Dictionary<RectTransform, Rect> after)
        {
            var moved = new List<string>();
            foreach (var pair in before)
            {
                if (!after.TryGetValue(pair.Key, out var rect) || rect != pair.Value)
                {
                    moved.Add(pair.Key.name);
                }
            }

            Assert.That((moved.Count, after.Count), Is.EqualTo((0, before.Count)), "切り替えで部品が動いた: " + string.Join(", ", moved));
        }

        private static IEnumerator LoadGame(System.Action<BattleController> found)
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
            yield return StageTestUtil.WaitForIntro(battle);
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
