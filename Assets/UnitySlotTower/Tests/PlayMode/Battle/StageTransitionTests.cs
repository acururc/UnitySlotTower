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
    [Description("FR-010, FR-019, T312: 戦闘マスが終わると戦闘フィールドが次の戦闘マスのマップに替わり、開始の演出の後すぐ戦闘になる（AC-016）")]
    public sealed class StageTransitionTests
    {
        // 近接の敵だけ（最初の戦闘は2体、通常の戦闘は1体）。味方を置かないので敵はゴールに着き、ライフ1ずつ減って戦闘が終わる。
        private const string ShortWaves =
            "encounter_id,time,enemy_id,spawn,path_id,is_boss\n" +
            "first,0,melee,S1,first_g1,0\n" +
            "first,3,melee,S1,first_g1,0\n" +
            "battle_a,0,melee,S1,battle_a_g1,0\nbattle_b,0,melee,S1,battle_b_g1,0\nbattle_c,0,melee,S1,battle_c_g1,0\n" +
            "elite_a,0,melee,S1,elite_a_g1,0\n";

        private bool _runInBackground;
        private BattleController _battle;
        private BattleFieldView _fieldView;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runInBackground = Application.runInBackground;
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
