using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    [Description("FR-010, FR-019, T312: 戦闘マスが終わると戦闘フィールドが次の戦闘マスのマップに替わり、開始の演出の後すぐ戦闘になる（AC-016）")]
    public sealed class StageTransitionTests
    {
        // 近接の敵だけ（最初の戦闘は2体、通常の戦闘は1体）。味方を置かないので敵はゴールに着き、ライフ1ずつ減って戦闘が終わる。
        private const string ShortWaves =
            "encounter_id,time,enemy_id,spawn,path_id,is_boss\n" +
            "first,0,melee,S1,first_g1,0\n" +
            "first,3,melee,S1,first_g1,0\n" +
            "battle_a,0,melee,S1,battle_a_g1,0\nbattle_b,0,melee,S1,battle_b_g1,0\nbattle_c,0,melee,S1,battle_c_g1,0\nbattle_d,0,melee,S1,battle_d_g1,0\nbattle_e,0,melee,S1,battle_e_g1,0\n" +
            "elite_a,0,melee,S1,elite_a_g1,0\n";

        private bool _runInBackground;
        private BattleController _battle;
        private BattleFieldView _fieldView;
        private bool _textMode;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runInBackground = Application.runInBackground;
            _textMode = GameSession.TextMode;
            Application.runInBackground = true;

            // sceneLoaded は Awake の後・Start の前に呼ばれるので、BattleController が表を読む前に差し替えられる。
            SceneManager.sceneLoaded += ReplaceWaves;
            try
            {
                yield return SceneManager.LoadSceneAsync("Game");
            }
            finally
            {
                SceneManager.sceneLoaded -= ReplaceWaves;
            }

            _battle = Object.FindAnyObjectByType<BattleController>();
            _fieldView = Object.FindAnyObjectByType<BattleFieldView>();
            for (var i = 0; i < 60 && _battle.Run == null; i++)
            {
                yield return null;
            }

            Assert.That(_battle.Run, Is.Not.Null, "Game シーンの進行が作られない");
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Application.runInBackground = _runInBackground;
            GameSession.SetTextMode(_textMode);
        }

        [UnityTest]
        [Description("AC-058: 文字なし版の開始・出現・到達・勝利と次画面への遷移")]
        public IEnumerator Battle_Presentation_Reveals_Board_Flashes_And_Holds_Victory() => Presentation(false);

        [UnityTest]
        [Description("AC-058: 文字あり版でも同じ演出を行い、開始の名前と戦闘終了を表示")]
        public IEnumerator Text_Battle_Presentation_Shows_Names_And_Holds_Victory() => Presentation(true);

        // AC-059: 光っている間も操作できる（縁の光はクリックを受けず、画面のどこを押しても下の部品に届く）。
        private static void AssertEdgeFlashPassesClicks(ScreenEdgeFlash edge)
        {
            Assert.That((edge.Graphic.raycastTarget, edge.GetComponentInChildren<GraphicRaycaster>(true) == null),
                Is.EqualTo((false, true)), "縁の光がクリックを受ける");
            var events = EventSystem.current;
            Assert.That(events, Is.Not.Null, "EventSystem がない");
            var hits = new List<RaycastResult>();
            var points = new[]
            {
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                new Vector2(8f, 8f), new Vector2(Screen.width - 8f, 8f),
                new Vector2(8f, Screen.height - 8f), new Vector2(Screen.width - 8f, Screen.height - 8f),
            };
            foreach (var point in points)
            {
                hits.Clear();
                events.RaycastAll(new PointerEventData(events) { position = point }, hits);
                foreach (var hit in hits)
                    Assert.That(hit.gameObject.transform.IsChildOf(edge.transform), Is.False, $"{point} で縁の光が押される");
            }
        }

        private IEnumerator Presentation(bool textMode)
        {
            GameSession.SetTextMode(textMode);
            var intro = Object.FindAnyObjectByType<BattleIntroView>();
            var status = Object.FindAnyObjectByType<OperationBarStatus>();
            var flow = Object.FindAnyObjectByType<RunController>();
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            var map = _battle.Run.Map;
            var path = map.Paths[0];
            var exit = path.Cells[0];
            var goal = path.Cells[path.Cells.Count - 1];
            yield return null;
            Assert.That((intro.IsShowing, intro.BackgroundAlpha, intro.IconAlpha, intro.IsNodeNameVisible),
                Is.EqualTo((true, 1f, 1f, textMode)), "暗い背景とマスの絵（文字あり版は名前）が出ない");
            for (var y = 0; y < map.Height; y++)
                for (var x = 0; x < map.Width; x++)
                {
                    var cell = new GridPoint(x, y);
                    if (map.TileAt(cell) == MapTile.Spawn)
                        Assert.That(_fieldView.HasExitMarker(cell), Is.True, $"出口 {x},{y} に枠がない");
                }
            Assert.That(_fieldView.IsExitFlashing(exit), Is.False);

            var limit = Time.realtimeSinceStartup + 10f;
            while (_battle.Run.IntroProgress < 0.65 && Time.realtimeSinceStartup < limit) yield return null;
            yield return null;
            Assert.That((intro.BackgroundAlpha, intro.IconAlpha), Is.EqualTo((0f, 1f)),
                "絵を残して盤面を先に見せない");
            yield return RouteTestUtil.WaitForIntro(_battle);
            yield return null;
            Assert.That((intro.IsShowing, intro.BackgroundAlpha, intro.IconAlpha), Is.EqualTo((false, 0f, 0f)));

            limit = Time.realtimeSinceStartup + 5f;
            while (!_fieldView.IsExitFlashing(exit) && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That(_fieldView.IsExitFlashing(exit), Is.True, "出現時に出口が光らない");
            _battle.SetPaused(true);
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That((_fieldView.HasExitMarker(exit), _fieldView.IsExitFlashing(exit)), Is.EqualTo((true, false)),
                "光った後に常設の枠が残らない");
            _battle.SetPaused(false);
            Time.timeScale = 4f;
            limit = Time.realtimeSinceStartup + 15f;
            while (!_fieldView.IsBaseFlashing(goal) && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That(_fieldView.IsBaseFlashing(goal), Is.True, "到達時に本拠地が光らない");
            _battle.SetPaused(true);
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.That((status.IsLifeFlashing, status.LifeNumberScale > 1f), Is.EqualTo((true, true)),
                "ライフが赤く光って大きくならない");
            var edge = _battle.EdgeFlash;
            var edgeAlpha = edge.Alpha;
            Assert.That((edge.IsFlashing, edgeAlpha > 0f, edgeAlpha <= ScreenEdgeFlash.StartAlpha + 0.001f),
                Is.EqualTo((true, true, true)), "AC-059: 画面の縁が赤く光らない");
            AssertEdgeFlashPassesClicks(edge);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That((_fieldView.IsBaseFlashing(goal), status.IsLifeFlashing, status.LifeNumberScale),
                Is.EqualTo((false, false, 1f)), "到達の演出が戻らない");
            Assert.That(edge.Alpha, Is.EqualTo(edgeAlpha), "一時停止中に縁の光が進む");
            _battle.SetPaused(false);
            limit = Time.realtimeSinceStartup + 5f;
            while (edge.IsFlashing && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That((edge.IsFlashing, edge.Graphic.enabled), Is.EqualTo((false, false)), "縁の光が消えない");
            Time.timeScale = 4f;
            limit = Time.realtimeSinceStartup + 15f;
            while (!_battle.IsShowingVictory && Time.realtimeSinceStartup < limit) yield return null;
            yield return null;
            Assert.That((_battle.IsShowingVictory, intro.IsVictoryBandVisible, intro.IsVictoryNameVisible,
                Time.timeScale, _battle.IsInputBlocked, cabinet.InputLocked, flow.IsChoosingRoute, intro.BackgroundAlpha),
                Is.EqualTo((true, true, textMode, 0f, true, true, false, 0f)), "盤面を残す勝利の帯で時間と入力が止まらない");
            var captions = intro.GetComponentInParent<Canvas>().GetComponentsInChildren<TextModeLabel>(true);
            TextModeLabel victoryLabel = null;
            foreach (var caption in captions)
                if (caption.Id == "battle_end") victoryLabel = caption;
            Assert.That(victoryLabel, Is.Not.Null, "勝利の帯が battle_end を使っていない");
            var table = (TextAsset)typeof(TextModeLabel).GetField("table", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(victoryLabel);
            Assert.That(TextModeLabel.TryResolve(table, "battle_end", out var expectedVictory), Is.True);
            Assert.That(victoryLabel.Text.text, Is.EqualTo(expectedVictory), "勝利の帯の文言が表と違う");
            var tick = _battle.Field.Tick;
            var speed = _battle.BattleSpeed;
            _battle.TogglePause();
            _battle.CycleBattleSpeed();
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That((_battle.IsShowingVictory, _battle.Field.Tick, _battle.IsPaused, _battle.BattleSpeed),
                Is.EqualTo((true, tick, false, speed)), "勝利の帯の間に戦闘か操作が進んだ");
            yield return RouteTestUtil.WaitForVictory(_battle);
            yield return null;
            Assert.That((intro.IsVictoryBandVisible, flow.IsChoosingRoute, Time.timeScale, cabinet.InputLocked),
                Is.EqualTo((false, true, 1f, false)),
                "勝利の帯の後にルートへ移らない");
        }

        [UnityTest]
        public IEnumerator Clearing_The_First_Node_Shows_The_Next_Map_And_Starts_After_The_Intro()
        {
            yield return null;
            Assert.That((_fieldView.Map.Id, TileRoots()), Is.EqualTo(("first", 1)));

            // 敵が歩き切るまで速める（1体あたり実時間で約1秒）。
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 30f;
            while (_battle.Floor == 1 && _battle.Run.Phase != WavePhase.GameOver &&
                   Time.realtimeSinceStartup < limit)
            {
                RouteTestUtil.Step();
                yield return null;
            }

            Time.timeScale = 1f;
            // 次の戦闘マスはルート画面で選ぶ（ショップ・回復を挟むと3階以上: T350b）。
            Assert.That((_battle.Floor >= 2, _battle.Run.Phase, _battle.IsInputBlocked),
                Is.EqualTo((true, WavePhase.Intro, true)), "次の戦闘マスが開始の演出から始まらない（演出中は操作できない）");

            // 古いマスの Destroy はフレームの終わりに効く。
            yield return null;
            var map = _fieldView.Map;
            var tiles = _fieldView.transform.Find("Tiles");
            // 出現は種類と階が合う行からランダムに選ばれる（plan 16.6）。
            Assert.That((map.Id == _battle.Run.Encounter.MapId, map == _battle.Run.Map, TileRoots(), tiles.childCount),
                Is.EqualTo((true, true, 1, map.Width * map.Height)), map.Id);

            // 演出のあいだは戦闘の時間が進まず、終わるとすぐ戦闘になる（準備フェーズはない: D-79）。
            var cabinet = Object.FindAnyObjectByType<UnitySlotTower.Slot.SlotCabinet>();
            var intro = Object.FindAnyObjectByType<BattleIntroView>();
            Assert.That((_battle.Field.Tick, cabinet.InputLocked, intro.IsShowing), Is.EqualTo((0L, true, true)),
                "開始の演出中に戦闘の時間が進むか、レバーを受け付けるか、演出が出ない");
            yield return RouteTestUtil.WaitForIntro(_battle);
            yield return null;
            Assert.That((_battle.Run.Phase, _battle.IsInputBlocked, cabinet.InputLocked, intro.IsShowing),
                Is.EqualTo((WavePhase.Fighting, false, false, false)), "開始の演出の後に戦闘が始まらないか、演出が消えない");
        }

        [UnityTest]
        [Description("AC-015, D-96: 次の戦闘マスは 1倍で始まり、戦闘の外（ルート・開始の演出）でも 1倍")]
        public IEnumerator Next_Node_Starts_At_Normal_Speed()
        {
            yield return RouteTestUtil.WaitForIntro(_battle);
            _battle.CycleBattleSpeed();
            _battle.CycleBattleSpeed();
            yield return null;
            Assert.That((_battle.BattleSpeed, Time.timeScale), Is.EqualTo((3, 3f)), "3倍にならない");

            // 敵が歩き切るまで速める。マスを終えると BattleController が 1 に戻す。
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 30f;
            while (_battle.Floor == 1 && _battle.Run.Phase != WavePhase.GameOver &&
                   Time.realtimeSinceStartup < limit)
            {
                RouteTestUtil.Step();
                yield return null;
            }

            var outside = Time.timeScale;
            yield return RouteTestUtil.WaitForIntro(_battle);
            yield return null;

            Assert.That((outside, _battle.Run.Phase, _battle.BattleSpeed, Time.timeScale),
                Is.EqualTo((1f, WavePhase.Fighting, 1, 1f)));
        }

        private int TileRoots()
        {
            var count = 0;
            foreach (Transform child in _fieldView.transform)
            {
                if (child.name == "Tiles")
                {
                    count++;
                }
            }

            return count;
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
