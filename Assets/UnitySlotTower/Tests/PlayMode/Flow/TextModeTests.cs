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
            "encounter_id,wave,time,enemy_id,spawn,path_id,is_boss\n" +
            "first,1,0,melee,S1,first_g1,0\nfirst,2,0,melee,S1,first_g1,0\n" +
            "battle_a,1,0,melee,S1,battle_a_g1,0\nbattle_a,2,0,melee,S1,battle_a_g1,0\n" +
            "battle_b,1,0,melee,S1,battle_b_g1,0\nbattle_b,2,0,melee,S1,battle_b_g1,0\n" +
            "battle_c,1,0,melee,S1,battle_c_g1,0\nbattle_c,2,0,melee,S1,battle_c_g1,0\n" +
            "elite_a,1,0,melee,S1,elite_a_g1,0\nelite_a,2,0,melee,S1,elite_a_g1,0\n" +
            "boss_a,1,0,melee,S1,boss_a_g1,0\nboss_a,2,0,melee,S1,boss_a_g1,0\n";

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

            // 難易度選択の画面に進んでもラベルが出る。
            GameObject.Find("Start Button").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(ShownIds(), Does.Contain("title_difficulty"));
            title.GetType().GetMethod("Show", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(title, new object[] { false });
            yield return null;

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

            // 同じフレームで切り替え、ラベル以外の部品が動かないことを確かめる（アニメの影響を受けないように）。
            var before = Layout();
            GameSession.SetTextMode(true);
            var after = Layout();
            AssertSameLayout(before, after);
            yield return null;

            var shown = ShownIds();
            Assert.That(shown, Does.Contain("unit_tank").And.Contain("unit_buffer").And.Contain("bar_upgrade").And.Contain("life")
                .And.Contain("bar_start").And.Contain("cabinet_lever").And.Contain("cabinet_credit"), "操作バーか筐体にラベルが出ない");
            Assert.That(shown, Does.Contain(GuideOverlay.CaptionId(guide.Current)), "説明の文が出ない");

            battle.SetPaused(true);
            var help = Object.FindAnyObjectByType<HelpScreen>();
            help.Open();
            yield return null;
            Assert.That(help.IsOpen, Is.True);
            Assert.That(ShownIds(), Does.Contain("help_title").And.Contain("stat_hp").And.Contain("skill_sword").And.Contain("slot_red7"),
                "早見表にラベルが出ない");

            GameSession.SetTextMode(false);
            yield return null;
            Assert.That(ShownIds(), Is.Empty, "文字なし版に戻してもラベルが消えない");
            help.Close();
            battle.SetPaused(false);
        }

        [UnityTest]
        public IEnumerator Route_And_Shop_Show_Labels_For_What_They_Offer()
        {
            GameSession.SetTextMode(true);
            RunController.SeedOverride = 1;
            var battle = default(BattleController);
            yield return LoadGame(b => battle = b);
            var run = Object.FindAnyObjectByType<RunController>();
            var seed = default(ulong?);
            for (ulong s = 1; s < 5000 && seed == null; s++)
            {
                foreach (var next in RouteGenerator.Generate(run.Rules, new SeededRandom(s)).Start.Next)
                {
                    if (next.Kind == RouteNodeKinds.Shop)
                    {
                        seed = s;
                        break;
                    }
                }
            }

            Assert.That(seed, Is.Not.Null, "2階にショップのあるシードが見つからない");
            RunController.SeedOverride = seed;
            yield return LoadGame(b => battle = b);
            run = Object.FindAnyObjectByType<RunController>();
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>();

            var first = battle.Run;
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 30f;
            while (!run.IsChoosingRoute && first.Phase != WavePhase.GameOver && Time.realtimeSinceStartup < limit)
            {
                battle.StartWave();
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(run.IsChoosingRoute, Is.True, "最初の戦闘マスを終えてもルート選びにならない");
            yield return null;
            var shown = ShownIds();
            Assert.That(shown, Does.Contain("route_title").And.Contain("node_shop"), "ルート画面に題と行き先の種類が出ない");

            var reel = -1;
            for (var r = 0; r < 3 && reel < 0; r++)
            {
                if (run.State.Current.NextOnReel(r)?.Kind == RouteNodeKinds.Shop)
                {
                    reel = r;
                }
            }

            cabinet.LeverRect.GetComponent<Button>().onClick.Invoke();
            Assert.That(cabinet.PickRouteReel(reel), Is.True, "行き先のリールを止められない");
            limit = Time.realtimeSinceStartup + 5f;
            while (run.OpenScreen == null && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            var shop = run.OpenScreen as ShopScreen;
            Assert.That(shop != null && shop.IsOpen, Is.True, "ショップの画面が開かない");
            yield return null;
            var offerLabels = 0;
            foreach (var button in shop.OfferButtons)
            {
                var label = button.GetComponentInChildren<TextModeLabel>();
                if (label != null && label.IsShown && label.Text.text.Length > 0)
                {
                    offerLabels++;
                }
            }

            Assert.That(offerLabels, Is.EqualTo(shop.OfferButtons.Count), "品の札に名前が出ない");
            Assert.That(ShownIds(), Does.Contain("shop_title").And.Contain("shop_buy_coins").And.Contain("back"));
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

        // ラベル以外の全 UI 部品の画面上の矩形。
        private static Dictionary<RectTransform, Rect> Layout()
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            var layout = new Dictionary<RectTransform, Rect>();
            foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (rect.GetComponentInParent<TextModeLabel>(true) != null)
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
            yield return null;
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
