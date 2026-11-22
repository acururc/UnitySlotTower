using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Flow;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("spec 002 FR-101・FR-105・FR-106 (AC-101, AC-103): RunController がステージ1〜8を順に戦わせ、状態を持ち越し、最後にクリアのリザルトを出す。カメラはマップに合う")]
    public sealed class RunControllerTests
    {
        // stages.csv の順のマップ。
        private static readonly string[] MapIds = { "first", "battle_a", "battle_b", "elite_a", "battle_c", "battle_d", "battle_e", "boss_a" };

        private bool _runInBackground;
        private RunController _run;
        private BattleController _battle;
        private BattleFieldView _fieldView;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            SceneManager.sceneLoaded += StageTestUtil.UseShortWaves;
            try
            {
                yield return SceneManager.LoadSceneAsync("Game");
            }
            finally
            {
                SceneManager.sceneLoaded -= StageTestUtil.UseShortWaves;
            }

            _run = Object.FindAnyObjectByType<RunController>();
            _battle = Object.FindAnyObjectByType<BattleController>();
            _fieldView = Object.FindAnyObjectByType<BattleFieldView>();
            for (var i = 0; i < 60 && (_battle == null || _battle.Run == null); i++)
            {
                yield return null;
            }

            Assert.That((_run != null, _battle != null && _battle.Run != null), Is.EqualTo((true, true)),
                "Game シーンに RunController がないか、ステージ1が始まらない");
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Application.runInBackground = _runInBackground;
        }

        [Test]
        public void Run_Starts_On_Stage_One()
        {
            var state = _run.State;
            Assert.That((state.StageIndex, _battle.StageNumber, _battle.StageCount, _battle.Run.Stage.Number, _fieldView.Map.Id),
                Is.EqualTo((1, 1, 8, 1, "first")));
            Assert.That(CornersVisible(_fieldView.Map), Is.True, "最初のマップの四隅がカメラに収まらない");
        }

        [UnityTest]
        [Description("AC-101: タイトルの後のステージ1〜8が順に来て、各ステージの開始時に盤上の味方は0、途中にルート・ショップ・回復が出ず、最後にクリアのリザルト")]
        public IEnumerator Stages_Run_In_Order_And_End_In_The_Clear_Result()
        {
            var begun = new List<int> { _battle.StageNumber };
            var maps = new List<string> { _fieldView.Map.Id };
            var unitsAtStart = new List<int> { _battle.Field.Units.Count };
            void OnBegan(int stage)
            {
                begun.Add(stage);
                maps.Add(_battle.Run.Map.Id);
                unitsAtStart.Add(_battle.Field.Units.Count);
            }

            _run.StageBegan += OnBegan;
            yield return StageTestUtil.WaitForIntro(_battle);
            // ステージ1で無料の弓を置いても、次のステージには持ち越さない（FR-106）。
            Assert.That(Place("archer"), Is.True, "ステージ1の最初の弓を無料で置けない");
            for (var i = 0; i < 8 && !_battle.IsFinished; i++)
            {
                yield return StageTestUtil.FinishStage(_battle);
            }

            _run.StageBegan -= OnBegan;
            var result = Object.FindAnyObjectByType<ResultScreen>();
            yield return null;
            Assert.That(begun, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }), "ステージが1から8の順に来ない");
            Assert.That(maps, Is.EqualTo(MapIds), "ステージのマップが stages.csv の順でない");
            Assert.That(unitsAtStart, Is.All.EqualTo(0), "ステージの開始時に味方が残っている");
            Assert.That((_battle.Run.IsRunCleared, _battle.IsFinished, result != null && result.IsOpen, result != null && result.IsClear),
                Is.EqualTo((true, true, true, true)), "ステージ8のクリアでクリアのリザルトにならない");
            Assert.That(OldScreens(), Is.Empty, "ルート・ショップ・回復の画面が残っている");
        }

        [UnityTest]
        public IEnumerator Placement_Is_Not_Carried_And_State_Carries_To_The_Next_Stage()
        {
            var state = _run.State;
            var first = _battle.Run;
            yield return StageTestUtil.WaitForIntro(_battle);
            // spec 002 FR-108: 所持金0でもステージ1の最初の1体は無料で置ける。
            Assert.That((state.Money.Coins, Place("archer")), Is.EqualTo((0, true)), "ステージ1の最初の弓を無料で置けない");

            // このステージが終わる。配置中の弓は持ち越さない（spec 002 FR-106）。
            var completed = false;
            void OnCompleted() => completed = true;
            _battle.NodeCompleted += OnCompleted;
            var lifeAtEnd = 0;
            var moneyAtEnd = 0;
            var coinsAtEnd = 0;
            first.Ended += Snapshot;
            yield return StageTestUtil.FinishStage(_battle);
            _battle.NodeCompleted -= OnCompleted;
            first.Ended -= Snapshot;

            Assert.That((completed, first.Phase, _battle.Run != first), Is.EqualTo((true, WavePhase.Completed, true)),
                "ステージ1が終わってステージ2に入らない");
            var next = _battle.Run;
            Assert.That((state.StageIndex, _battle.StageNumber, next.Stage.Number, _fieldView.Map.Id, next.Map == _fieldView.Map,
                    next.Phase),
                Is.EqualTo((2, 2, 2, "battle_a", true, WavePhase.Intro)));
            Assert.That((state.Life, state.Money.Coins, state.Coins.Coins, _battle.Field.Units.Count),
                Is.EqualTo((lifeAtEnd, moneyAtEnd, coinsAtEnd, 0)),
                "ステージをまたいでライフ・所持金・コインが引き継がれない、または味方が残った");
            Assert.That(next.Summoner.FreeSummons, Is.Zero, "ステージ2で無料の1体が残った");
            Assert.That(CornersVisible(_fieldView.Map), Is.True, $"{_fieldView.Map.Id} の四隅がカメラに収まらない");

            // Ended は RunController が次のステージへ進む前に呼ばれるので、そこで終了時の値を取る。
            void Snapshot()
            {
                lifeAtEnd = state.Life;
                moneyAtEnd = state.Money.Coins;
                coinsAtEnd = state.Coins.Coins;
            }
        }

        // 削除したルート・ショップ・回復の画面とルート選びのリールの部品（spec 002 FR-117）がシーンに残っていれば、その名前。
        private static List<string> OldScreens()
        {
            var names = new List<string>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name is "Route Screen" or "Shop Screen" or "Rest Screen" or "Route Pick" or "Route Dim")
                {
                    names.Add(t.name);
                }
            }

            return names;
        }

        [UnityTest]
        public IEnumerator Camera_Fits_Every_Map([ValueSource(nameof(MapIds))] string mapId)
        {
            _fieldView.Show(_fieldView.LoadMap(mapId));
            yield return null;
            Assert.That(CornersVisible(_fieldView.Map), Is.True, $"{mapId} の四隅がカメラに収まらない");
        }

        // 地面の四隅は視野の中。空中の高さの四隅は上下だけ視野の中（D-87、T368: 左右の余白をなくし、空中の敵は手前の角でわずかに切れてよい）。
        private bool CornersVisible(BattleMap map)
        {
            const float epsilon = 1e-4f;
            var camera = GameObject.Find("Battle Camera").GetComponent<Camera>();
            foreach (var x in new[] { -0.5, map.Width - 0.5 })
            {
                foreach (var y in new[] { -0.5, map.Height - 0.5 })
                {
                    foreach (var air in new[] { false, true })
                    {
                        var p = camera.WorldToViewportPoint(_fieldView.PositionToWorld(x, y, air));
                        var outsideX = !air && (p.x < -epsilon || p.x > 1f + epsilon);
                        if (p.z <= 0f || outsideX || p.y < -epsilon || p.y > 1f + epsilon)
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
    }
}
