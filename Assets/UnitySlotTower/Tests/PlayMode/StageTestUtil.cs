using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode
{
    /// <summary>
    /// ステージを進めるテスト用の手順（spec 002 FR-101）。どのステージも近接の敵2体だけにする差し込み表と、
    /// 開始の演出・勝利の帯・ステージの終わりを待つ部品。
    /// </summary>
    internal static class StageTestUtil
    {
        /// <summary>
        /// どのステージも近接の敵1体だけ（ステージを早く終わらせる仕掛け: AC-101）。味方を置かなければ1体ずつゴールに着き、
        /// 8ステージでライフ 10 のうち 8 を失ってクリアする。
        /// </summary>
        public const string ShortWaves =
            "stage,time,enemy_id,spawn,path_id,is_boss\n" +
            "1,0,melee,S1,first_g1,0\n2,0,melee,S1,battle_a_g1,0\n3,0,melee,S1,battle_b_g1,0\n" +
            "4,0,melee,S1,elite_a_g1,0\n5,0,melee,S1,battle_c_g1,0\n6,0,melee,S1,battle_d_g1,0\n" +
            "7,0,melee,S1,battle_e_g1,0\n8,0,melee,S1,boss_a_g1,0\n";

        /// <summary>
        /// <see cref="SceneManager.sceneLoaded"/> に足すと、読み込んだ Game シーンの出現を <see cref="ShortWaves"/> に差し替える
        /// （RunController の Start より前に効く）。
        /// </summary>
        public static void UseShortWaves(Scene scene, LoadSceneMode mode)
        {
            var battle = Object.FindAnyObjectByType<BattleController>();
            if (battle == null)
            {
                return;
            }

            typeof(BattleController)
                .GetField("wavesTable", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(battle, new TextAsset(ShortWaves));
        }

        /// <summary>
        /// ステージ1の見せ場（spec 015 FR-001、ウェーブ1で止まってレバーを待つ）を飛ばす。早送りで終わらせるテスト用。
        /// 筐体の決め打ちとレバーの点滅も消す（次のステージの開始でも消える）。
        /// </summary>
        public static void SkipShowcase(BattleController battle)
        {
            if (battle.Field == null || !battle.Field.IsShowcaseHolding)
            {
                return;
            }

            battle.Field.ReleaseShowcase();
            var cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            if (cabinet != null)
            {
                cabinet.ForcedHits = null;
                cabinet.LeverHighlighted = false;
            }
        }

        public static IEnumerator WaitForVictory(BattleController battle)
        {
            var limit = Time.realtimeSinceStartup + BattlePresentationTiming.VictoryBand + 3f;
            while (battle.IsShowingVictory && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That(battle.IsShowingVictory, Is.False, "勝利の帯が終わらない");
        }

        /// <summary>
        /// ステージの開始の演出（FR-019、既定 2 秒の実時間）が終わるまで待つ。演出中は配置などの操作を受け付けないので、
        /// ステージに入った直後に操作するテストはこれを挟む。
        /// </summary>
        public static IEnumerator WaitForIntro(BattleController battle)
        {
            var limit = Time.realtimeSinceStartup + 10f;
            while (battle.IsInIntro && Time.realtimeSinceStartup < limit)
            {
                yield return null;
            }

            Assert.That(battle.IsInIntro, Is.False, "開始の演出が終わらない");
        }

        /// <summary>
        /// 今のステージを早送りで終わらせ、次のステージに入るか、ゲームが終わる（クリア・ゲームオーバー）まで待つ。
        /// 時間の倍率は戻す。
        /// </summary>
        public static IEnumerator FinishStage(BattleController battle, float seconds = 30f)
        {
            var run = battle.Run;
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + seconds;
            while (battle.Run == run && !battle.IsFinished && Time.realtimeSinceStartup < limit)
            {
                SkipShowcase(battle);
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(battle.Run != run || battle.IsFinished, Is.True,
                $"ステージ {battle.StageNumber} が {seconds} 秒で終わらない（{run.Phase}、残り {run.RemainingEnemies}）");
        }

        /// <summary>今のステージが <see cref="WavePhase.Completed"/> になって勝利の帯が出るまで早送りする（帯は待たない）。</summary>
        public static IEnumerator ClearStage(BattleController battle, float seconds = 30f)
        {
            var run = battle.Run;
            Time.timeScale = 20f;
            var limit = Time.realtimeSinceStartup + seconds;
            while (run.Phase != WavePhase.Completed && run.Phase != WavePhase.GameOver &&
                   Time.realtimeSinceStartup < limit)
            {
                SkipShowcase(battle);
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.That(run.Phase, Is.EqualTo(WavePhase.Completed), $"ステージ {battle.StageNumber} を {seconds} 秒でクリアできない");
        }
    }
}
