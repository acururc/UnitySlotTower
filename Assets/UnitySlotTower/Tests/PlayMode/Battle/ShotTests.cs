using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    [Description("AC-063: 弓が攻撃すると矢が相手まで飛ぶ。弾は使い回す（FR-082, T425）")]
    public sealed class ShotTests
    {
        private bool _runInBackground;

        [SetUp]
        public void SetUp()
        {
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
        }

        [TearDown]
        public void TearDown() => Application.runInBackground = _runInBackground;

        [UnityTest]
        public IEnumerator Archer_Shots_Fly_To_The_Target_And_Are_Reused()
        {
            yield return SceneManager.LoadSceneAsync("Game");
            var battle = Object.FindAnyObjectByType<BattleController>();
            for (var i = 0; i < 300 && battle.Field == null; i++)
            {
                yield return null;
            }

            Assert.That(battle?.Field, Is.Not.Null, "Game シーンの戦闘が始まらない");
            yield return StageTestUtil.WaitForIntro(battle);
            var view = Object.FindAnyObjectByType<BattleActorsView>();
            var map = Object.FindAnyObjectByType<BattleFieldView>().Map;
            var archer = battle.Units.Get("archer");
            var field = battle.Field;

            // 説明が戦闘の時間を止めていても進むよう、テストから進めて最初の敵を出す。
            for (var i = 0; i < 60 * 30 && field.Enemies.Count == 0; i++)
            {
                field.Advance(1);
            }

            Assert.That(field.Enemies.Count, Is.GreaterThan(0), "敵が出ない");
            var path = field.Enemies[0].Path;

            // その敵の道（出口の次のマスから先）が射程に入る向きで、弓を2体置く。
            var placed = 0;
            for (var y = 0; y < map.Height && placed < 2; y++)
            {
                for (var x = 0; x < map.Width && placed < 2; x++)
                {
                    var cell = new GridPoint(x, y);
                    if (!UnitPlacement.CanPlace(map, cell, archer) || Occupied(field, cell))
                    {
                        continue;
                    }

                    foreach (Facing facing in System.Enum.GetValues(typeof(Facing)))
                    {
                        var unit = field.AddUnit(archer, cell, facing);
                        if (CoversPath(path, unit))
                        {
                            placed++;
                            break;
                        }

                        field.RemoveUnit(unit);
                    }
                }
            }

            Assert.That(placed, Is.EqualTo(2), "敵の道を射程に入れて弓を置けるマスが足りない");

            var impact = Object.FindAnyObjectByType<HitImpactEffect>();
            Assert.That(impact, Is.Not.Null, "Game シーンに当たりの衝撃がない（Build Hit Impact Effect (spec 008)）");
            var impacts = view.ImpactCount;
            var played = impact.PlayCount;
            var sawInFlight = false;
            var sawTrail = false;
            for (var i = 0; i < 60 * 40 && view.ArrowsFired < 4 && battle.Field == field && !field.IsGameOver; i++)
            {
                field.Advance(1);
                sawInFlight |= view.ActiveShotCount > 0;
                sawTrail |= view.ActiveShotCount > 0 && EmittingTrails() > 0;
                // 1ステップ分（1/60 秒）の時間を待ち、弾が戦闘と同じ速さで飛ぶようにする。
                yield return new WaitForSeconds(1f / 60f);
            }

            Assert.That(view.ArrowsFired, Is.GreaterThanOrEqualTo(4), "弓が撃っても矢が出ない");
            Assert.That(sawInFlight, Is.True, "矢が飛んでいる間がない");

            var until = Time.realtimeSinceStartup + 2f;
            while (view.ArrowsLanded == 0 && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(view.ArrowsLanded, Is.GreaterThan(0), "矢が相手まで届かない");
            Assert.That(view.CreatedShotCount, Is.LessThan(view.ShotsFired), "弾を使い回さず毎回作っている");

            // spec 008 AC-002: 弾は尾を引き、届くと当たりの衝撃が出る。片付けた弾に尾は残らず、衝撃の粒も消える。
            Assert.That(sawTrail, Is.True, "飛んでいる弾に尾が付かない");
            Assert.That(view.ImpactCount - impacts, Is.GreaterThanOrEqualTo(view.ArrowsLanded), "届いた矢の数だけ当たりの衝撃が出ない");
            Assert.That(impact.PlayCount - played, Is.EqualTo(view.ImpactCount - impacts), "当たりの通知と衝撃の数が合わない");
            until = Time.realtimeSinceStartup + 2f;
            while ((view.ActiveShotCount > 0 || impact.IsPlaying) && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That((view.ActiveShotCount, impact.ActiveParticleCount, EmittingTrails(), VisibleTrailPoints()),
                Is.EqualTo((0, 0, 0, 0)), "弾・衝撃の粒・尾が残る");
        }

        private static int EmittingTrails()
        {
            var count = 0;
            foreach (var trail in Object.FindObjectsByType<TrailRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (trail.gameObject.activeInHierarchy && trail.emitting) count++;
            }

            return count;
        }

        private static int VisibleTrailPoints()
        {
            var count = 0;
            foreach (var trail in Object.FindObjectsByType<TrailRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (trail.gameObject.activeInHierarchy) count += trail.positionCount;
            }

            return count;
        }

        private static bool CoversPath(MapPath path, BattleUnit unit)
        {
            for (var k = 1; k < path.Cells.Count; k++)
            {
                foreach (var range in unit.RangeCells)
                {
                    if (range.Equals(path.Cells[k])) return true;
                }
            }

            return false;
        }

        private static bool Occupied(Battlefield field, GridPoint cell)
        {
            foreach (var unit in field.Units)
            {
                if (unit.Cell.Equals(cell)) return true;
            }

            return false;
        }
    }
}
