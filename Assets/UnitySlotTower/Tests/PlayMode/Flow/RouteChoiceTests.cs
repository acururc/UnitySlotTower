using System.Collections;
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
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("T350b (FR-010, FR-017, D-75): 戦闘マスの後にルート画面が開き、筐体のリールで次のマスを選ぶ（コイン・回数固定は減らない）")]
    public sealed class RouteChoiceTests
    {
        // どの戦闘マスも各ウェーブ近接の敵1体だけ。
        private const string ShortWaves =
            "encounter_id,wave,time,enemy_id,spawn,path_id,is_boss\n" +
            "first,1,0,melee,S1,first_g1,0\nfirst,2,0,melee,S1,first_g1,0\n" +
            "battle_a,1,0,melee,S1,battle_a_g1,0\nbattle_a,2,0,melee,S1,battle_a_g1,0\n" +
            "battle_b,1,0,melee,S1,battle_b_g1,0\nbattle_b,2,0,melee,S1,battle_b_g1,0\n" +
            "battle_c,1,0,melee,S1,battle_c_g1,0\nbattle_c,2,0,melee,S1,battle_c_g1,0\n" +
            "elite_a,1,0,melee,S1,elite_a_g1,0\nelite_a,2,0,melee,S1,elite_a_g1,0\n" +
            "boss_a,1,0,melee,S1,boss_a_g1,0\nboss_a,2,0,melee,S1,boss_a_g1,0\n";

        private bool _runInBackground;
        private RunController _run;
        private BattleController _battle;
        private SlotCabinet _cabinet;
        private RouteScreen _screen;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            RunController.SeedOverride = 12345;
            SceneManager.sceneLoaded += ReplaceWaves;
            try
            {
                yield return SceneManager.LoadSceneAsync("Game");
            }
            finally
            {
                SceneManager.sceneLoaded -= ReplaceWaves;
            }

            _run = Object.FindAnyObjectByType<RunController>();
            _battle = Object.FindAnyObjectByType<BattleController>();
            _cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            _screen = Object.FindAnyObjectByType<RouteScreen>(FindObjectsInactive.Include);
            for (var i = 0; i < 60 && (_battle == null || _battle.Run == null); i++)
            {
                yield return null;
            }

            Assert.That((_run != null, _battle != null && _battle.Run != null, _screen != null), Is.EqualTo((true, true, true)),
                "Game シーンに RunController かルート画面がないか、最初の戦闘マスに入らない");
            Assert.That((_screen.IsOpen, _cabinet.IsChoosingRoute), Is.EqualTo((false, false)), "戦闘中にルート画面が開いている");
        }

        [TearDown]
        public void TearDown()
        {
            RunController.SeedOverride = null;
            Time.timeScale = 1f;
            Application.runInBackground = _runInBackground;
        }

        [UnityTest]
        public IEnumerator Route_Screen_Opens_After_A_Battle_With_The_Reels_Of_The_Next_Nodes()
        {
            yield return FinishBattle();
            var state = _run.State;
            var current = state.Current;
            Assert.That((_run.IsChoosingRoute, _screen.IsOpen, _cabinet.IsChoosingRoute, _battle.IsInputBlocked),
                Is.EqualTo((true, true, true, true)), "戦闘マスの後にルート画面が開かない／戦闘の操作を止めない");
            Assert.That(_screen.NodeViewCount, Is.EqualTo(state.Route.All.Count), "マップに全マスが並ばない");

            for (var reel = 0; reel < 3; reel++)
            {
                var next = current.NextOnReel(reel);
                Assert.That((_cabinet.HasRouteDirection(reel), Dim(reel).activeSelf, PickButton(reel).gameObject.activeSelf,
                        _screen.ArrowButtons[reel].interactable),
                    Is.EqualTo((next != null, next == null, true, next != null)), $"リール {reel} の行き先の表示が合わない");
                if (next != null)
                {
                    Assert.That(_screen.IsHighlighted(next), Is.True, $"リール {reel} の行き先が光らない");
                }
            }

            // ルート画面では ▶ を押しても次のウェーブは始まらない。
            var phase = _battle.Run.Phase;
            _battle.StartWave();
            Assert.That(_battle.Run.Phase, Is.EqualTo(phase));
        }

        [UnityTest]
        public IEnumerator Lever_Spins_Without_Coins_And_The_Clicked_Reel_Enters_That_Node()
        {
            yield return FinishBattle();
            var state = _run.State;
            var current = state.Current;
            var coins = state.Coins.Coins;
            var fixedSpins = _cabinet.FixedSpinsLeft;
            var spins = _cabinet.SpinCount;

            // 回す前のリールのクリックは効かない。
            var reel = RouteTestUtil.PreferredReel(current);
            PickButton(reel).onClick.Invoke();
            Assert.That(_cabinet.RoutePick, Is.Null, "回す前にリールが止まった");

            _cabinet.LeverRect.GetComponent<Button>().onClick.Invoke();
            Assert.That((_cabinet.IsRouteSpinning, _cabinet.IsSpinning), Is.EqualTo((true, true)), "レバーでリールが回らない");
            // 通常の自動停止（約1.8秒）を過ぎても止まらない。
            yield return WaitReal(2.5f);
            Assert.That((_cabinet.IsSpinning, _run.IsChoosingRoute), Is.EqualTo((true, true)), "ルート選びのリールが自動で止まった");
            // 回数固定は次の戦闘マスの出現で置き換わるので、マスに入る前に確かめる。
            Assert.That((state.Coins.Coins, _cabinet.FixedSpinsLeft), Is.EqualTo((coins, fixedSpins)), "レバーでコインか回数固定が減った");

            var target = current.NextOnReel(reel);
            PickButton(reel).onClick.Invoke();
            Assert.That(_cabinet.RoutePick, Is.EqualTo(reel), "行き先のあるリールのクリックで止まらない");
            var limit = Time.realtimeSinceStartup + 5f;
            while (state.Current != target && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            Assert.That((state.Current, _run.Visited[_run.Visited.Count - 1]), Is.EqualTo((target, target)),
                "止めたリールの方向のマスに進まない");
            Assert.That((state.Coins.Coins, _cabinet.SpinCount), Is.EqualTo((coins, spins)),
                "ルート選びでコイン・回した回数が変わった");
            if (RouteNodeKinds.Get(target.Kind).IsBattle)
            {
                Assert.That((_screen.IsOpen, _battle.Floor, _battle.Run.Phase), Is.EqualTo((false, target.Floor, WavePhase.Preparing)),
                    "戦闘マスに入ったのにルート画面が閉じない");
            }
            else
            {
                // 戦闘以外のマスはその画面を開く（T350c）。
                Assert.That((_screen.IsOpen, _run.OpenScreen != null && _run.OpenScreen.Kind == target.Kind),
                    Is.EqualTo((false, true)), "戦闘以外のマスでその画面が開かない");
            }
        }

        [UnityTest]
        public IEnumerator Reel_Without_A_Destination_Does_Not_Stop()
        {
            // 筐体のモードだけを確かめる（RunController は自分が始めたルート選びにしか反応しない）。
            var state = _run.State;
            var node = state.Current;
            var coins = state.Coins.Coins;
            var fixedSpins = _cabinet.FixedSpinsLeft;
            var chosen = -1;
            void OnChosen(int reel) => chosen = reel;
            _cabinet.RouteReelChosen += OnChosen;
            try
            {
                _cabinet.BeginRouteChoice(new[] { false, true, false });
                Assert.That((Dim(0).activeSelf, Dim(1).activeSelf, Dim(2).activeSelf), Is.EqualTo((true, false, true)));
                _cabinet.LeverRect.GetComponent<Button>().onClick.Invoke();
                Assert.That(_cabinet.IsRouteSpinning, Is.True);

                PickButton(0).onClick.Invoke();
                PickButton(2).onClick.Invoke();
                yield return WaitReal(0.5f);
                Assert.That((_cabinet.RoutePick, _cabinet.IsSpinning, chosen), Is.EqualTo(((int?)null, true, -1)),
                    "行き先のないリールのクリックで止まった");

                PickButton(1).onClick.Invoke();
                var limit = Time.realtimeSinceStartup + 5f;
                while (chosen < 0 && Time.realtimeSinceStartup < limit)
                {
                    yield return null;
                }

                Assert.That((chosen, _cabinet.IsChoosingRoute, _cabinet.IsSpinning, Dim(0).activeSelf, PickButton(1).gameObject.activeSelf),
                    Is.EqualTo((1, false, false, false, false)), "行き先のあるリールで止まり終えない");
                Assert.That((state.Coins.Coins, _cabinet.FixedSpinsLeft, state.Current, _run.IsChoosingRoute),
                    Is.EqualTo((coins, fixedSpins, node, false)));
            }
            finally
            {
                _cabinet.RouteReelChosen -= OnChosen;
            }
        }

        // 最初の戦闘マスを早送りで終え、ルート画面が開くまで待つ。
        private IEnumerator FinishBattle()
        {
            var first = _battle.Run;
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 30f;
            while (!_run.IsChoosingRoute && first.Phase != WavePhase.GameOver && Time.realtimeSinceStartup < limit)
            {
                _battle.StartWave();
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(_run.IsChoosingRoute, Is.True, "最初の戦闘マスを終えてもルート選びにならない");
            yield return null;
        }

        private Button PickButton(int reel) => ((Button[])Field("routeReelButtons"))[reel];

        private GameObject Dim(int reel) => ((GameObject[])Field("routeReelDims"))[reel];

        private object Field(string name) =>
            typeof(SlotCabinet).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_cabinet);

        private static IEnumerator WaitReal(float seconds)
        {
            var until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        private static void ReplaceWaves(Scene scene, LoadSceneMode mode)
        {
            var battle = Object.FindAnyObjectByType<BattleController>();
            typeof(BattleController)
                .GetField("wavesTable", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(battle, new TextAsset(ShortWaves));
        }
    }
}
