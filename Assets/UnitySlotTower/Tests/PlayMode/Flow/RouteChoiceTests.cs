using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("T350b (FR-010, FR-017, D-75): 戦闘マスの後にルート画面が開き、筐体のリールで次のマスを選ぶ（コインは減らない）")]
    public sealed class RouteChoiceTests
    {
        // どの戦闘マスも各ウェーブ近接の敵1体だけ。
        private const string ShortWaves =
            "stage,time,enemy_id,spawn,path_id,is_boss\n" +
            "1,0,melee,S1,first_g1,0\n1,3,melee,S1,first_g1,0\n" +
            "2,0,melee,S1,battle_a_g1,0\n2,3,melee,S1,battle_a_g1,0\n" +
            "3,0,melee,S1,battle_b_g1,0\n3,3,melee,S1,battle_b_g1,0\n" +
            "5,0,melee,S1,battle_c_g1,0\n6,0,melee,S1,battle_d_g1,0\n7,0,melee,S1,battle_e_g1,0\n5,3,melee,S1,battle_c_g1,0\n6,3,melee,S1,battle_d_g1,0\n7,3,melee,S1,battle_e_g1,0\n" +
            "4,0,melee,S1,elite_a_g1,0\n4,3,melee,S1,elite_a_g1,0\n" +
            "8,0,melee,S1,boss_a_g1,0\n8,3,melee,S1,boss_a_g1,0\n";

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
            // 開始の演出中は筐体のレバーを受け付けない（AC-016）。
            yield return RouteTestUtil.WaitForIntro(_battle);
        }

        [TearDown]
        public void TearDown()
        {
            RunController.SeedOverride = null;
            GameSession.SetTextMode(false);
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
            AssertRouteLooks(state, _run.Visited);
            // 入った時点でリールが回っている（FR-064）。
            yield return RouteTestUtil.WaitForRouteSpin(_cabinet);
            Assert.That(_cabinet.IsSpinning, Is.True, "ルート画面に入ってもリールが回っていない");

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
        }

        // T386 (FR-073, AC-054): 今のマスからの線だけが Next、通ってきた道は Path、ほかは Faint。今からたどれない上の階のマスは暗い。
        private void AssertRouteLooks(RunState state, System.Collections.Generic.IReadOnlyList<RouteNode> visited)
        {
            var current = state.Current;
            var reachable = new System.Collections.Generic.HashSet<RouteNode>();
            var walk = new System.Collections.Generic.Stack<RouteNode>();
            walk.Push(current);
            while (walk.Count > 0)
            {
                foreach (var next in walk.Pop().Next)
                {
                    if (reachable.Add(next))
                    {
                        walk.Push(next);
                    }
                }
            }

            foreach (var node in state.Route.All)
            {
                foreach (var next in node.Next)
                {
                    var step = false;
                    for (var i = 0; i + 1 < visited.Count; i++)
                    {
                        step |= visited[i] == node && visited[i + 1] == next;
                    }

                    var expected = node == current ? RouteScreen.LineLook.Next
                        : step ? RouteScreen.LineLook.Path
                        : RouteScreen.LineLook.Faint;
                    Assert.That(_screen.LookOf(node, next), Is.EqualTo(expected),
                        $"線 {node.Floor}-{node.Column}→{next.Floor}-{next.Column} の見た目が合わない");
                }

                var dark = node.Floor > current.Floor && !reachable.Contains(node);
                Assert.That(_screen.IsUnreachable(node), Is.EqualTo(dark), $"マス {node.Floor}-{node.Column} の暗さが合わない");
                var visitedMark = false;
                for (var i = 0; i < visited.Count; i++)
                {
                    visitedMark |= node != current && visited[i] == node;
                }

                Assert.That(_screen.HasVisitedMark(node), Is.EqualTo(visitedMark), $"マス {node.Floor}-{node.Column} の通った印が合わない");
            }
        }

        [UnityTest]
        [Description("T367 (FR-064, AC-018): 入った時点でリールが回り、止めたリールが最初に、残りが左から順に止まり、移動のアニメの後にそのマスに入る")]
        public IEnumerator Reels_Spin_On_Entry_And_The_Clicked_Reel_Stops_First_Then_The_Move_Animates_Into_That_Node()
        {
            yield return FinishBattle();
            var state = _run.State;
            var current = state.Current;
            var coins = state.Coins.Coins;
            var spins = _cabinet.SpinCount;

            yield return RouteTestUtil.WaitForRouteSpin(_cabinet);
            // レバーは何もしない（コインも回数も使わない）。通常の自動停止（約1.8秒）を過ぎても止まらない。
            _cabinet.LeverRect.GetComponent<Button>().onClick.Invoke();
            yield return WaitReal(2.5f);
            Assert.That((_cabinet.IsSpinning, _run.IsChoosingRoute), Is.EqualTo((true, true)), "ルート選びのリールが自動で止まった");
            Assert.That((state.Coins.Coins, _cabinet.SpinCount), Is.EqualTo((coins, spins)),
                "ルート選びでコインか回した回数が変わった");

            var reels = (SlotReelView[])Field("reels");
            var reel = RouteTestUtil.PreferredReel(current);
            var target = current.NextOnReel(reel);
            PickButton(reel).onClick.Invoke();
            Assert.That(_cabinet.RoutePick, Is.EqualTo(reel), "行き先のあるリールのクリックで止まらない");

            // 各リールが止まった順を記録する。
            var stopOrder = new System.Collections.Generic.List<int>();
            var limit = Time.realtimeSinceStartup + 5f;
            while (stopOrder.Count < 3 && Time.realtimeSinceStartup < limit)
            {
                for (var i = 0; i < 3; i++)
                {
                    if (!reels[i].IsSpinning && !stopOrder.Contains(i))
                    {
                        stopOrder.Add(i);
                    }
                }

                yield return null;
            }

            var expectedOrder = new System.Collections.Generic.List<int> { reel };
            for (var i = 0; i < 3; i++)
            {
                if (i != reel)
                {
                    expectedOrder.Add(i);
                }
            }

            Assert.That(stopOrder, Is.EqualTo(expectedOrder), "止めたリールが最初に、残りが左から順に止まらない");

            // 止まった後は移動のアニメ。まだマスに入らず、印が今のマスから行き先へ進む。
            var stoppedAt = Time.realtimeSinceStartup;
            while (!_screen.IsMoving && Time.realtimeSinceStartup < stoppedAt + 1f)
            {
                yield return null;
            }

            Assert.That((_screen.IsMoving, _screen.IsOpen, state.Current, _screen.Marker.gameObject.activeSelf),
                Is.EqualTo((true, true, current, true)), "止まった後に移動のアニメにならない");
            Assert.That(_screen.ArrowButtons[reel].interactable, Is.False, "移動のアニメ中に行き先を押せる");
            var startPos = _screen.Marker.position;
            yield return WaitReal(0.3f);
            Assert.That(Vector3.Distance(_screen.Marker.position, startPos), Is.GreaterThan(1f), "移動のアニメで印が動かない");
            // アニメ中のレバーで筐体が回らない。
            _cabinet.LeverRect.GetComponent<Button>().onClick.Invoke();
            Assert.That(_cabinet.IsSpinning, Is.False, "移動のアニメ中にレバーで回った");

            limit = Time.realtimeSinceStartup + 5f;
            while (state.Current != target && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            Assert.That(Time.realtimeSinceStartup - stoppedAt, Is.GreaterThan(0.7f), "移動のアニメを見せずに次の画面に移った");
            Assert.That((state.Current, _run.Visited[_run.Visited.Count - 1], _screen.IsMoving), Is.EqualTo((target, target, false)),
                "止めたリールの方向のマスに進まない");
            Assert.That((state.Coins.Coins, _cabinet.SpinCount), Is.EqualTo((coins, spins)),
                "ルート選びでコイン・回した回数が変わった");
            if (RouteNodeKinds.Get(target.Kind).IsBattle)
            {
                Assert.That((_screen.IsOpen, _battle.Floor, _battle.Run.Phase), Is.EqualTo((false, target.Floor, WavePhase.Intro)),
                    "戦闘マスに入ったのにルート画面が閉じないか、開始の演出から始まらない");
            }
            else
            {
                // 戦闘以外のマスはその画面を開く（T350c）。
                Assert.That((_screen.IsOpen, _run.OpenScreen != null && _run.OpenScreen.Kind == target.Kind),
                    Is.EqualTo((false, true)), "戦闘以外のマスでその画面が開かない");
            }

            // T386 (AC-054): 2階から見たルート。通ってきた線は Path、端の列からは反対の端の上の階に行けない。
            _screen.Show(state, _run.Visited);
            AssertRouteLooks(state, _run.Visited);
            Assert.That(_screen.LookOf(current, target), Is.EqualTo(RouteScreen.LineLook.Path), "通ってきた線が細い線にならない");
            if (target.Column != 1)
            {
                foreach (var far in state.Route.NodesOn(target.Floor + 1))
                {
                    if (far.Column == 2 - target.Column)
                    {
                        Assert.That(_screen.IsUnreachable(far), Is.True, "端の列から反対の端の上のマスが暗くならない");
                    }
                }
            }

            _screen.Hide();
        }

        [UnityTest]
        [Description("T359d (FR-006, D-76): 文字あり版のルート画面。開いたまま切り替えるとマップが新しい枠で作り直され、行き先の札に方向・名前・説明、凡例が出て、札のクリックで進める")]
        public IEnumerator Text_Mode_Route_Shows_Cards_And_Legend_And_A_Card_Click_Enters_The_Node()
        {
            yield return FinishBattle();
            var state = _run.State;
            var current = state.Current;
            var map = (RectTransform)typeof(RouteScreen).GetField("mapArea", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_screen);
            var noTextSize = map.rect.size;

            GameSession.SetTextMode(true);
            // 作り直しは次の Update、古いマスと線の破棄はそのフレームの終わり。
            yield return null;
            yield return null;
            Assert.That(map.rect.size, Is.Not.EqualTo(noTextSize), "文字あり版でマップの枠が変わらない");
            // 今のマスから行き先への線の長さが、作り直した後のマスの間の距離と合う。
            var next = current.Next[0];
            var from = map.Find($"Nodes/Node {current.Floor}-{current.Column} {current.Kind}");
            var to = map.Find($"Nodes/Node {next.Floor}-{next.Column} {next.Kind}");
            var line = (RectTransform)map.Find($"Lines/Line {current.Floor}-{current.Column} {next.Floor}-{next.Column}");
            Assert.That((from != null, to != null, line != null), Is.EqualTo((true, true, true)), "マップのマスか線がない");
            Assert.That(line.sizeDelta.x, Is.EqualTo(Vector2.Distance(from.localPosition, to.localPosition)).Within(1f),
                "切り替えた後の線が作り直したマスにつながらない");

            var legend = _screen.transform.Find("Panel/Text Mode Legend");
            Assert.That(legend != null && legend.GetComponent<CanvasGroup>().alpha > 0.99f, Is.True, "文字あり版で凡例が出ない");

            var names = (TextModeLabel[])Private("directionLabels");
            var descriptions = (TextModeLabel[])Private("directionDescriptions");
            for (var reel = 0; reel < 3; reel++)
            {
                var target = current.NextOnReel(reel);
                Assert.That((names[reel].IsShown, descriptions[reel].IsShown), Is.EqualTo((target != null, target != null)),
                    $"リール {reel} の札の名前・説明の表示が行き先と合わない");
                if (target != null)
                {
                    TextModeLabel.TryResolve(TableOf(names[reel]), "node_" + target.Kind + "_desc", out var expected);
                    Assert.That(descriptions[reel].Text.text, Is.EqualTo(expected), $"リール {reel} の札の説明が行き先と合わない");
                }
            }

            // 札の真ん中のクリックが札に当たる（凡例や他の部品に隠れない）。
            var reelToPick = RouteTestUtil.PreferredReel(current);
            var card = _screen.ArrowButtons[reelToPick].transform;
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(null, card.position) }, hits);
            Assert.That(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(card), Is.True,
                "文字あり版の札の真ん中のクリックが札に当たらない（いちばん上: " + (hits.Count > 0 ? hits[0].gameObject.name : "なし") + "）");

            var chosen = current.NextOnReel(reelToPick);
            yield return RouteTestUtil.WaitForRouteSpin(_cabinet);
            _screen.ArrowButtons[reelToPick].onClick.Invoke();
            var limit = Time.realtimeSinceStartup + 5f;
            while (state.Current != chosen && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            Assert.That(state.Current, Is.EqualTo(chosen), "文字あり版で札のクリックの方向のマスに進まない");
        }

        [UnityTest]
        public IEnumerator Reel_Without_A_Destination_Does_Not_Stop()
        {
            // 筐体のモードだけを確かめる（RunController は自分が始めたルート選びにしか反応しない）。
            var state = _run.State;
            var node = state.Current;
            var coins = state.Coins.Coins;
            var chosen = -1;
            void OnChosen(int reel) => chosen = reel;
            _cabinet.RouteReelChosen += OnChosen;
            try
            {
                _cabinet.BeginRouteChoice(new[] { false, true, false });
                Assert.That((Dim(0).activeSelf, Dim(1).activeSelf, Dim(2).activeSelf), Is.EqualTo((true, false, true)));
                yield return RouteTestUtil.WaitForRouteSpin(_cabinet);

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
                Assert.That((state.Coins.Coins, state.Current, _run.IsChoosingRoute),
                    Is.EqualTo((coins, node, false)));
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
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(_run.IsChoosingRoute, Is.True, "最初の戦闘マスを終えてもルート選びにならない");
            yield return null;
        }

        private object Private(string name) =>
            typeof(RouteScreen).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_screen);

        private static TextAsset TableOf(TextModeLabel label) =>
            (TextAsset)typeof(TextModeLabel).GetField("table", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(label);

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
