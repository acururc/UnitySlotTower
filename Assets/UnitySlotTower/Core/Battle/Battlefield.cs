using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// 1 ウェーブの戦闘（FR-027, FR-030〜032）。<see cref="WaveSchedule"/> どおりに敵を出し、経路を進め、
    /// 地上の敵を味方にブロックさせ、敵に味方を攻撃させる。1/60 秒の固定ステップで進める。
    /// 味方の攻撃と HP0 での消滅・ブロック解除（T206）、ライフとドロップ（T207）はまだない。
    /// </summary>
    public sealed class Battlefield
    {
        private readonly WaveSchedule _wave;
        private readonly List<BattleEnemy> _enemies = new List<BattleEnemy>();
        private readonly List<BattleUnit> _units = new List<BattleUnit>();

        // 射程ちょうどの距離を浮動小数の誤差で外さないための許容幅。
        private const double RangeEpsilon = 1e-9;

        private int _nextSpawn;

        public Battlefield(WaveSchedule wave)
        {
            _wave = wave;
        }

        /// <summary>戦闘開始から進めたステップ数。</summary>
        public long Tick { get; private set; }

        /// <summary>フィールド上の敵（出現順）。ゴールに着いた敵は消える。</summary>
        public IReadOnlyList<BattleEnemy> Enemies => _enemies;

        /// <summary>配置順。</summary>
        public IReadOnlyList<BattleUnit> Units => _units;

        /// <summary>ゴールに着いた敵の数（ライフ減少は T207）。</summary>
        public int ArrivedCount { get; private set; }

        /// <summary>配置できるマスかは呼び出し側（<see cref="UnitPlacement"/>、T208）で判定する。</summary>
        public BattleUnit AddUnit(UnitDefinition definition, GridPoint cell, Facing facing)
        {
            var unit = new BattleUnit(definition, cell, facing);
            _units.Add(unit);
            return unit;
        }

        /// <summary>1 ステップごとに 出現 → 移動（ゴール到着で退場） → 攻撃 の順で処理する。</summary>
        public void Advance(int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                Spawn();
                Move();
                Attack();
                Tick++;
            }
        }

        private void Spawn()
        {
            var spawns = _wave.Spawns;
            while (_nextSpawn < spawns.Count &&
                   Math.Round(spawns[_nextSpawn].Time * SimulationClock.StepsPerSecond) <= Tick)
            {
                var spawn = spawns[_nextSpawn++];
                _enemies.Add(new BattleEnemy(spawn.Enemy, spawn.Path));
            }
        }

        /// <summary>出現順に進めるので、同じステップで同じ味方に着いた敵は先に出た方がブロックされる。</summary>
        private void Move()
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.BlockedBy != null)
                {
                    continue;
                }

                var distance = enemy.Definition.MoveSpeed * SimulationClock.StepSeconds;
                var stopAt = 0.0;
                var blocker = enemy.Definition.IsAir ? null : FindBlocker(enemy, distance, out stopAt);
                if (blocker == null)
                {
                    enemy.Move(distance);
                    continue;
                }

                enemy.Move(stopAt - enemy.Distance);
                enemy.BlockedBy = blocker;
                blocker.BlockUsed += enemy.Definition.BlockCost;
            }

            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                if (_enemies[i].ReachedGoal)
                {
                    _enemies.RemoveAt(i);
                    ArrivedCount++;
                }
            }
        }

        /// <summary>
        /// このステップで入る（または既に居る）経路上のマスに、ブロックの空きが消費以上ある味方を探す（FR-027）。
        /// 見つかれば、そのマスの手前の境界（マス中心の 0.5 手前）を止まる位置にする。既にマスの中なら今の位置。
        /// </summary>
        private BattleUnit FindBlocker(BattleEnemy enemy, double distance, out double stopAt)
        {
            var cells = enemy.Path.Cells;
            var reach = enemy.Distance + distance;
            for (var k = 1; k < cells.Count; k++)
            {
                var entry = k - 0.5;
                if (entry > reach + RangeEpsilon)
                {
                    break;
                }

                if (k + 0.5 <= enemy.Distance)
                {
                    continue;
                }

                foreach (var unit in _units)
                {
                    if (unit.IsAlive && unit.Definition.Block > 0 && unit.Cell.Equals(cells[k]) &&
                        unit.Definition.Block - unit.BlockUsed >= enemy.Definition.BlockCost)
                    {
                        stopAt = Math.Max(entry, enemy.Distance);
                        return unit;
                    }
                }
            }

            stopAt = 0;
            return null;
        }

        /// <summary>
        /// 攻撃間隔ごとに攻撃する。射程のある敵（遠距離・空中）は射程内で最も近い味方（同じ距離なら先に配置した味方）を、
        /// 射程0の敵（近距離）はブロックした味方だけを攻撃する。
        /// </summary>
        private void Attack()
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.AttackCooldownSteps > 0)
                {
                    enemy.AttackCooldownSteps--;
                }

                if (enemy.AttackCooldownSteps > 0)
                {
                    continue;
                }

                var target = enemy.Definition.Range > 0 ? NearestUnitInRange(enemy) : enemy.BlockedBy;
                if (target == null)
                {
                    continue;
                }

                target.TakeDamage(Damage.Calculate(enemy.Definition.Atk, target.Definition.Def));
                enemy.AttackCooldownSteps =
                    (int)Math.Round(enemy.Definition.AttackInterval * SimulationClock.StepsPerSecond);
            }
        }

        private BattleUnit NearestUnitInRange(BattleEnemy enemy)
        {
            BattleUnit target = null;
            var nearest = double.MaxValue;
            foreach (var unit in _units)
            {
                var distance = enemy.DistanceTo(unit.Cell);
                if (distance <= enemy.Definition.Range + RangeEpsilon && distance < nearest)
                {
                    target = unit;
                    nearest = distance;
                }
            }

            return target;
        }
    }
}
