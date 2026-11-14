using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    [Description("FR-010, T312: ステージが終わると戦闘フィールドが次のステージのマップに替わり、操作バーが S-W を表示する")]
    public sealed class StageTransitionTests
    {
        // どのウェーブも近接の敵1体だけ（S1 は2ウェーブ、S2-W1 まで）。味方を置かないので敵はゴールに着き、ライフ1ずつ減ってウェーブが終わる。
        private const string ShortWaves =
            "stage,wave,time,enemy_id,spawn,path_id,is_boss\n" +
            "1,1,0,melee,S1,s1_g1,0\n" +
            "1,2,0,melee,S1,s1_g1,0\n" +
            "2,1,0,melee,S1,s2_g1,0\n";

        private bool _runInBackground;
        private BattleController _battle;
        private BattleFieldView _fieldView;
        private TMP_Text _waveText;

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
            _waveText = (TMP_Text)typeof(OperationBarStatus)
                .GetField("waveText", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(Object.FindAnyObjectByType<OperationBarStatus>());
            for (var i = 0; i < 60 && _battle.Run == null; i++)
            {
                yield return null;
            }

            Assert.That(_battle.Run, Is.Not.Null, "Game シーンの進行が作られない");
            yield return ReelCustomTestUtil.CloseWithDefault(_battle);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Application.runInBackground = _runInBackground;
        }

        [UnityTest]
        public IEnumerator Clearing_Stage_1_Shows_Stage_2_Map_And_Label()
        {
            yield return null;
            Assert.That((_fieldView.Map.Id, _waveText.text, TileRoots()), Is.EqualTo(("s1", "1-1", 1)));

            // 敵が歩き切るまで速める（1体あたり実時間で約1秒）。
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + 30f;
            while (_battle.Run.Stage == 1 && _battle.Run.Phase != WavePhase.GameOver &&
                   Time.realtimeSinceStartup < limit)
            {
                _battle.StartWave();
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That((_battle.Run.Stage, _battle.Run.Wave, _battle.Run.Phase),
                Is.EqualTo((2, 1, WavePhase.Preparing)), "S1 を終えて S2 の準備に入らない");

            // 古いマスの Destroy はフレームの終わりに効く。
            yield return null;
            var map = _fieldView.Map;
            var tiles = _fieldView.transform.Find("Tiles");
            Assert.That((map.Id, map == _battle.Run.Map, _waveText.text, TileRoots(), tiles.childCount),
                Is.EqualTo(("s2", true, "2-1", 1, map.Width * map.Height)));

            // ステージの間で止まらず、すぐ S2 の戦闘を始められる（カスタムは 2026-11-14 に廃止）。
            _battle.StartWave();
            Assert.That(_battle.Run.Phase, Is.EqualTo(WavePhase.Fighting), "S2 の戦闘を始められない");
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
