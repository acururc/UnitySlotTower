using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Flow;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("T350a (FR-010, FR-017, AC-010, AC-014): RunController がルートを作って戦闘マスを続け、状態を引き継ぎ、カメラをマップに合わせる")]
    public sealed class RunControllerTests
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

        private static readonly string[] MapIds = { "first", "battle_a", "battle_b", "battle_c", "elite_a", "boss_a" };

        private bool _runInBackground;
        private RunController _run;
        private BattleController _battle;
        private BattleFieldView _fieldView;

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
            _fieldView = Object.FindAnyObjectByType<BattleFieldView>();
            for (var i = 0; i < 60 && (_battle == null || _battle.Run == null); i++)
            {
                yield return null;
            }

            Assert.That((_run != null, _battle != null && _battle.Run != null), Is.EqualTo((true, true)),
                "Game シーンに RunController がないか、最初の戦闘マスに入らない");
        }

        [TearDown]
        public void TearDown()
        {
            RunController.SeedOverride = null;
            Time.timeScale = 1f;
            Application.runInBackground = _runInBackground;
        }

        [Test]
        public void Run_Starts_On_The_First_Battle_Of_A_Generated_Route()
        {
            var state = _run.State;
            Assert.That((state.Route != null, state.Current, _run.Visited.Count, _battle.Floor, _battle.Run.Encounter.Kind,
                    _fieldView.Map.Id),
                Is.EqualTo((true, state.Route.Start, 1, 1, EncounterKind.First, "first")));
            Assert.That(CornersVisible(_fieldView.Map), Is.True, "最初のマップの四隅がカメラに収まらない");
        }

        [UnityTest]
        public IEnumerator Placement_Survives_Wave_2_And_State_Carries_To_The_Next_Battle()
        {
            var state = _run.State;
            var first = _battle.Run;
            Assert.That(Place("archer"), Is.True, "弓を置けない");

            // W1 → 準備（配置と HP を保つ: AC-010）。
            Time.timeScale = 20f;
            _battle.StartWave();
            yield return WaitUntil(() => first.Wave == 2 || first.Phase == WavePhase.GameOver, 30f);
            Time.timeScale = 1f;
            Assert.That((first.Wave, first.Phase, _battle.Field.Units.Count, _battle.Field.Units[0].Definition.Id),
                Is.EqualTo((2, WavePhase.Preparing, 1, "archer")), "W1 の後に配置が残らない");

            // W2 → このマスの終了。配置中の弓はストックに戻る。
            var completed = false;
            void OnCompleted() => completed = true;
            _battle.NodeCompleted += OnCompleted;
            var lifeAtEnd = 0;
            var moneyAtEnd = 0;
            var coinsAtEnd = 0;
            var stockAtEnd = 0;
            first.Ended += Snapshot;
            Time.timeScale = 20f;
            _battle.StartWave();
            var limit = Time.realtimeSinceStartup + 30f;
            while (_battle.Run == first && first.Phase != WavePhase.GameOver && Time.realtimeSinceStartup < limit)
            {
                RouteTestUtil.Step();
                yield return null;
            }
            Time.timeScale = 1f;
            _battle.NodeCompleted -= OnCompleted;
            first.Ended -= Snapshot;

            Assert.That((completed, first.Phase, _battle.Run != first), Is.EqualTo((true, WavePhase.Completed, true)),
                "最初の戦闘マスが終わって次の戦闘マスに入らない");
            var between = _run.Visited.Skip(1).Take(_run.Visited.Count - 2).ToList();
            var heal = between.Count(n => n.Kind == RouteNodeKinds.Rest) * _run.Rules.RestHeal;
            Assert.That(between.All(n => !RouteNodeKinds.Get(n.Kind).IsBattle), Is.True, "途中で戦闘マスを飛ばした");

            var next = _battle.Run;
            Assert.That((next.Encounter.Kind != EncounterKind.First, _battle.Floor, state.Current, _fieldView.Map.Id,
                    next.Map == _fieldView.Map, next.Wave, next.Phase),
                Is.EqualTo((true, state.Current.Floor, _run.Visited[_run.Visited.Count - 1], next.Encounter.MapId, true, 1,
                    WavePhase.Preparing)));
            Assert.That((state.Life, state.Money.Coins, state.Coins.Coins, state.Stock.Count("archer"), _battle.Field.Units.Count),
                Is.EqualTo((Mathf.Min(state.MaxLife, lifeAtEnd + heal), moneyAtEnd, coinsAtEnd, stockAtEnd, 0)),
                "戦闘マスをまたいでライフ・所持金・コイン・ストックが引き継がれない");
            Assert.That(stockAtEnd, Is.EqualTo(1), "終了時に配置中の弓がストックに戻らない");
            Assert.That(CornersVisible(_fieldView.Map), Is.True, $"{_fieldView.Map.Id} の四隅がカメラに収まらない");

            // Ended は報酬とストックへの戻しの後、RunController が次のマスへ進む前に呼ばれるので、そこで終了時の値を取る。
            void Snapshot()
            {
                lifeAtEnd = state.Life;
                moneyAtEnd = state.Money.Coins;
                coinsAtEnd = state.Coins.Coins;
                stockAtEnd = state.Stock.Count("archer");
            }
        }

        [UnityTest]
        public IEnumerator Camera_Fits_Every_Map([ValueSource(nameof(MapIds))] string mapId)
        {
            _fieldView.Show(_fieldView.LoadMap(mapId));
            yield return null;
            Assert.That(CornersVisible(_fieldView.Map), Is.True, $"{mapId} の四隅がカメラに収まらない");
        }

        private bool CornersVisible(BattleMap map)
        {
            var camera = GameObject.Find("Battle Camera").GetComponent<Camera>();
            foreach (var x in new[] { -0.5, map.Width - 0.5 })
            {
                foreach (var y in new[] { -0.5, map.Height - 0.5 })
                {
                    foreach (var air in new[] { false, true })
                    {
                        var p = camera.WorldToViewportPoint(_fieldView.PositionToWorld(x, y, air));
                        if (p.z <= 0f || p.x < 0f || p.x > 1f || p.y < 0f || p.y > 1f)
                        {
                            Debug.Log($"{map.Id} ({x},{y},{air}) → {p}");
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private bool Place(string unitId)
        {
            var deployment = _battle.Deployment;
            if (deployment.Select(unitId) != SummonResult.Summoned)
            {
                return false;
            }

            var map = _battle.Run.Map;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var cell = new GridPoint(x, y);
                    if (deployment.CanPlace(cell))
                    {
                        deployment.SetCursor(cell);
                        deployment.ChooseCell();
                        deployment.SetFacing(Facing.Right);
                        return deployment.Confirm() == SummonResult.Summoned;
                    }
                }
            }

            deployment.Cancel();
            return false;
        }

        private static IEnumerator WaitUntil(System.Func<bool> done, float seconds)
        {
            var limit = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < limit)
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
