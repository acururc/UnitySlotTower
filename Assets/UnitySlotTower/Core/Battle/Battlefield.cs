using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// 1 ウェーブの戦闘（FR-030〜032）。<see cref="WaveSchedule"/> どおりに敵を出し、経路を進め、
    /// 射程のある敵に味方を攻撃させる。1/60 秒の固定ステップで進める。
    /// ブロック（T205）・味方の攻撃（T206）・ライフとドロップ（T207）はまだない。
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

        private void Move()
        {
            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];
                enemy.Move(enemy.Definition.MoveSpeed * SimulationClock.StepSeconds);
                if (enemy.ReachedGoal)
                {
                    _enemies.RemoveAt(i);
                    ArrivedCount++;
                }
            }
        }

        /// <summary>射程のある敵（遠距離・空中）は、射程内で最も近い味方を攻撃間隔ごとに攻撃する。同じ距離なら先に配置した味方。</summary>
        private void Attack()
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.AttackCooldownSteps > 0)
                {
                    enemy.AttackCooldownSteps--;
                }

                if (enemy.Definition.Range <= 0 || enemy.AttackCooldownSteps > 0)
                {
                    continue;
                }

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

                if (target == null)
                {
                    continue;
                }

                target.TakeDamage(Damage.Calculate(enemy.Definition.Atk, target.Definition.Def));
                enemy.AttackCooldownSteps =
                    (int)Math.Round(enemy.Definition.AttackInterval * SimulationClock.StepsPerSecond);
            }
        }
    }
}
