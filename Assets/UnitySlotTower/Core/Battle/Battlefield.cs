using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// 1 ステージの戦闘（FR-013, FR-024, FR-027, FR-028, FR-030〜033）。<see cref="WaveSchedule"/> どおりに敵を出し、経路を進め、
    /// 地上の敵を味方にブロックさせ、敵と味方を攻撃させ（回復・強化を含む）、HP0 になった側を消す。ゴールに着いた敵はライフを減らし、
    /// 倒された敵はコインを落とす。1/60 秒の固定ステップで進め、ライフ0（ゲームオーバー）で止まる。
    /// </summary>
    public sealed class Battlefield
    {
        private WaveSchedule _wave;
        private readonly UnitRanges _ranges;
        private readonly IRandom _random;
        private readonly List<BattleEnemy> _enemies = new List<BattleEnemy>();
        private readonly List<BattleUnit> _units = new List<BattleUnit>();

        // 射程ちょうどの距離を浮動小数の誤差で外さないための許容幅。
        private const double RangeEpsilon = 1e-9;

        private int _nextSpawn;

        /// <param name="life">ウェーブ開始時のライフ（満タン、FR-013）。</param>
        /// <param name="random">撃破ドロップの抽選に使う（FR-033）。</param>
        /// <param name="boosts">この戦闘マスの出目の効果（バー・コインの倍率）。null なら倍率なし。</param>
        public Battlefield(WaveSchedule wave, UnitRanges ranges, int life, IRandom random, BattleBoosts boosts = null)
        {
            _wave = wave;
            _ranges = ranges;
            _random = random;
            Life = life;
            Boosts = boosts;
        }

        /// <summary>この戦闘マスの出目の効果（plan 16.4）。null なら倍率なし。</summary>
        public BattleBoosts Boosts { get; }

        /// <summary>
        /// 攻撃に使う攻撃力。基礎×強化（<see cref="BattleUnit.Atk"/>、四捨五入済み）× バッファー × バーを最後に四捨五入する
        /// （plan 16.4 の掛け順。<paramref name="skillRate"/> はスキルの範囲攻撃の倍率で、バーの後に掛ける）。
        /// </summary>
        private int EffectiveAtk(BattleUnit unit, double skillRate = 1)
        {
            var rate = BuffRateOf(unit) * skillRate;
            if (Boosts != null)
            {
                rate *= Boosts.AttackRate;
            }

            return (int)Math.Round(unit.Atk * rate, MidpointRounding.AwayFromZero);
        }

        /// <summary>範囲に <paramref name="unit"/> を含む、自分以外の生きているバッファーのうち最大の倍率（重ねがけしない、FR-028）。いなければ1。</summary>
        private double BuffRateOf(BattleUnit unit)
        {
            var rate = 1.0;
            foreach (var buffer in _units)
            {
                if (buffer != unit && buffer.IsAlive && buffer.Definition.Role == UnitRole.Buff &&
                    buffer.BuffRate > rate && buffer.InRange(unit.Cell))
                {
                    rate = buffer.BuffRate;
                }
            }

            return rate;
        }

        /// <summary>残りライフ。</summary>
        public int Life { get; private set; }

        /// <summary>ライフが0になった（FR-013）。</summary>
        public bool IsGameOver => Life <= 0;

        /// <summary>撃破ドロップで落ちたコインの合計（FR-033。財布への加算は進行側）。</summary>
        public int DroppedCoins { get; private set; }

        /// <summary>撃破ドロップで落ちた所持金の合計（plan 16.2）。</summary>
        public int DroppedMoney { get; private set; }

        /// <summary>いまのウェーブの戦闘開始から進めたステップ数。</summary>
        public long Tick { get; private set; }

        /// <summary>フィールド上の敵（出現順）。ゴールに着いた敵は消える。</summary>
        public IReadOnlyList<BattleEnemy> Enemies => _enemies;

        /// <summary>配置順。</summary>
        public IReadOnlyList<BattleUnit> Units => _units;

        /// <summary>ゴールに着いた敵の数。</summary>
        public int ArrivedCount { get; private set; }

        /// <summary>出現予定の敵がすべて出て、フィールドに敵が残っていない（ゲームオーバーでない）。</summary>
        public bool IsCleared => !IsGameOver && _nextSpawn >= _wave.Spawns.Count && _enemies.Count == 0;

        /// <summary>
        /// 同じステージの次のウェーブに入る（FR-010）。味方とその HP・ライフ・撃破ドロップの合計はそのままで、出現予定と経過ステップだけを入れ替える。
        /// </summary>
        public void BeginWave(WaveSchedule wave)
        {
            if (!IsCleared)
            {
                throw new InvalidOperationException("いまのウェーブが終わっていない");
            }

            _wave = wave;
            _nextSpawn = 0;
            Tick = 0;
        }

        /// <summary>配置できるマスかは呼び出し側（<see cref="UnitPlacement"/>、T208）で判定する。</summary>
        /// <param name="upgrade">その種類のいまの強化段階（FR-026）。null なら段階1。</param>
        public BattleUnit AddUnit(UnitDefinition definition, GridPoint cell, Facing facing,
            UnitUpgradeLevel upgrade = null)
        {
            var unit = new BattleUnit(definition, cell, facing, _ranges.CellsFrom(definition.Id, cell, facing),
                upgrade ?? UnitUpgradeLevel.Base);
            _units.Add(unit);
            return unit;
        }

        /// <summary>配置中のその種類の全個体を強化の段階に合わせる（FR-026）。</summary>
        public void ApplyUpgrade(string unitId, UnitUpgradeLevel level)
        {
            foreach (var unit in _units)
            {
                if (unit.Definition.Id == unitId)
                {
                    unit.ApplyUpgrade(level);
                }
            }
        }

        /// <summary>スキル（FR-029、plan 16.4）。配置中のその職の全員に効く。その職が1体もいなければ何もせず false。</summary>
        /// <remarks>
        /// 効果時間は発動時にいた個体だけに付き、戦闘ステップ（<see cref="Advance"/>）でだけ減る。
        /// <see cref="SkillDefinition.DamageRate"/> があれば、その場で範囲内（ブロック中を含む）の敵全員に1回ずつ攻撃し、倒れた敵はすぐ消す。
        /// </remarks>
        public bool ActivateSkill(SkillDefinition skill)
        {
            var activated = false;
            foreach (var unit in _units)
            {
                if (!unit.IsAlive || unit.Definition.Id != skill.UnitId)
                {
                    continue;
                }

                activated = true;
                unit.StartSkill(skill);
                if (skill.DamageRate > 0)
                {
                    SkillAttack(unit, skill.DamageRate);
                }
            }

            if (activated && skill.DamageRate > 0)
            {
                RemoveDefeatedEnemies();
            }

            return activated;
        }

        /// <summary>範囲内（ブロック中を含む）で狙える敵全員に、攻撃力×<paramref name="rate"/> で1回ずつ攻撃する。通常攻撃の間隔は変えない。</summary>
        private void SkillAttack(BattleUnit unit, double rate)
        {
            var atk = EffectiveAtk(unit, rate);
            foreach (var enemy in _enemies)
            {
                if (enemy.IsAlive && CanTarget(unit, enemy))
                {
                    enemy.TakeDamage(Damage.Calculate(atk, enemy.Definition.Def));
                }
            }
        }

        /// <summary>HP0 で味方が消えたとき（FR-024）。撤退では呼ばれない。</summary>
        public event Action<BattleUnit> UnitFell;

        /// <summary>撤退（FR-025）。その味方がブロックしていた敵は再び進む。フィールドに居ない味方なら何もせず false。</summary>
        public bool RemoveUnit(BattleUnit unit)
        {
            if (!_units.Remove(unit))
            {
                return false;
            }

            ReleaseBlockedEnemies(unit);
            return true;
        }

        /// <summary>
        /// 1 ステップごとに 出現 → 移動（ゴール到着で退場・ライフ減少） → 敵の攻撃 → HP0 の味方の消滅 → 味方の行動
        /// → HP0 の敵の消滅（ドロップ） の順で処理し、最後にスロットの効果の残り時間を1ステップ減らす。ゲームオーバーになったら残りのステップは進めない。
        /// </summary>
        public void Advance(int steps)
        {
            for (var i = 0; i < steps && !IsGameOver; i++)
            {
                Spawn();
                Move();
                Attack();
                RemoveFallenUnits();
                Act();
                RemoveDefeatedEnemies();
                TickSkills();
                Tick++;
            }
        }

        private void TickSkills()
        {
            foreach (var unit in _units)
            {
                unit.TickSkill();
            }
        }

        private void Spawn()
        {
            var spawns = _wave.Spawns;
            while (_nextSpawn < spawns.Count &&
                   Math.Round(spawns[_nextSpawn].Time * SimulationClock.StepsPerSecond) <= Tick)
            {
                var spawn = spawns[_nextSpawn++];
                _enemies.Add(new BattleEnemy(spawn.Enemy, spawn.Path)
                {
                    AreaCooldownSteps = StepsOf(spawn.Enemy.AreaInterval),
                });
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
                    Life = Math.Max(Life - _enemies[i].Definition.LifeDamage, 0);
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
                    if (unit.IsAlive && unit.Block > 0 && unit.Cell.Equals(cells[k]) &&
                        unit.Block - unit.BlockUsed >= enemy.Definition.BlockCost)
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
        /// 射程0の敵（近距離）はブロックした味方だけを攻撃する。範囲攻撃を持つ敵（最終ボス）はこれとは別の間隔で範囲攻撃もする。
        /// </summary>
        private void Attack()
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.Definition.HasAreaAttack)
                {
                    AreaAttack(enemy);
                }

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

                target.TakeDamage(Damage.Calculate(enemy.Definition.Atk, target.Def));
                enemy.AttackCooldownSteps =
                    (int)Math.Round(enemy.Definition.AttackInterval * SimulationClock.StepsPerSecond);
            }
        }

        /// <summary>
        /// 範囲攻撃の間隔ごとに、敵の位置から半径内にマス中心がある味方全員を範囲攻撃の攻撃力で攻撃する。
        /// 半径内に味方がいないあいだは間隔を消費しない。
        /// </summary>
        private void AreaAttack(BattleEnemy enemy)
        {
            if (enemy.AreaCooldownSteps > 0)
            {
                enemy.AreaCooldownSteps--;
            }

            if (enemy.AreaCooldownSteps > 0)
            {
                return;
            }

            var definition = enemy.Definition;
            var hit = false;
            foreach (var unit in _units)
            {
                if (unit.IsAlive && enemy.DistanceTo(unit.Cell) <= definition.AreaRadius + RangeEpsilon)
                {
                    unit.TakeDamage(Damage.Calculate(definition.AreaAtk, unit.Def));
                    hit = true;
                }
            }

            if (hit)
            {
                enemy.AreaCooldownSteps = StepsOf(definition.AreaInterval);
            }
        }

        private static int StepsOf(double seconds) => (int)Math.Round(seconds * SimulationClock.StepsPerSecond);

        /// <summary>HP0 の味方を消し（FR-024）、その味方がブロックしていた敵を再び進ませる。</summary>
        private void RemoveFallenUnits()
        {
            for (var i = _units.Count - 1; i >= 0; i--)
            {
                var unit = _units[i];
                if (unit.IsAlive)
                {
                    continue;
                }

                ReleaseBlockedEnemies(unit);
                _units.RemoveAt(i);
                UnitFell?.Invoke(unit);
            }
        }

        private void ReleaseBlockedEnemies(BattleUnit unit)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.BlockedBy == unit)
                {
                    enemy.BlockedBy = null;
                }
            }

            unit.BlockUsed = 0;
        }

        /// <summary>
        /// 攻撃間隔ごとに、範囲内の敵を攻撃する。対象がなければ待たずに次のステップで探す。
        /// バッファーは攻撃せず、<see cref="EffectiveAtk"/> で範囲内の味方の攻撃力を上げる。
        /// </summary>
        private void Act()
        {
            foreach (var unit in _units)
            {
                if (unit.Definition.Role == UnitRole.Buff)
                {
                    continue;
                }

                if (unit.AttackCooldownSteps > 0)
                {
                    unit.AttackCooldownSteps--;
                }

                if (unit.AttackCooldownSteps > 0)
                {
                    continue;
                }

                var target = FindTarget(unit);
                if (target == null)
                {
                    continue;
                }

                target.TakeDamage(Damage.Calculate(EffectiveAtk(unit), target.Definition.Def));

                unit.AttackCooldownSteps = (int)Math.Round(
                    unit.Definition.AttackInterval * unit.IntervalRate * SimulationClock.StepsPerSecond);
            }
        }

        /// <summary>
        /// ブロック中の敵があればその中から、なければ範囲内の敵から、ゴールに最も近い敵（同じなら先に出た敵）を選ぶ。
        /// 空中の敵は遠距離職（弓）だけが狙える（FR-031）。
        /// </summary>
        private BattleEnemy FindTarget(BattleUnit unit)
        {
            BattleEnemy target = null;
            var targetBlocked = false;
            foreach (var enemy in _enemies)
            {
                if (!enemy.IsAlive)
                {
                    continue;
                }

                if (!CanTarget(unit, enemy))
                {
                    continue;
                }

                var blocked = enemy.BlockedBy == unit;

                if (target == null || blocked && !targetBlocked ||
                    blocked == targetBlocked && enemy.RemainingDistance < target.RemainingDistance)
                {
                    target = enemy;
                    targetBlocked = blocked;
                }
            }

            return target;
        }

        /// <summary>ブロック中の敵か、範囲内の敵（空中の敵は遠距離職だけ、FR-031）なら狙える。</summary>
        private static bool CanTarget(BattleUnit unit, BattleEnemy enemy) =>
            enemy.BlockedBy == unit ||
            unit.InRange(enemy.Cell) && (!enemy.Definition.IsAir || unit.Definition.Role == UnitRole.Ranged);

        /// <summary>
        /// HP0 の敵を消し、使っていたブロックの空きを戻す。ドロップ確率に当たれば、最小〜最大の量（両端を含む一様）を落とす（FR-033）。
        /// </summary>
        private void RemoveDefeatedEnemies()
        {
            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];
                if (enemy.IsAlive)
                {
                    continue;
                }

                if (enemy.BlockedBy != null)
                {
                    enemy.BlockedBy.BlockUsed -= enemy.Definition.BlockCost;
                }

                var definition = enemy.Definition;
                // 1回の抽選で [0, コイン率) ならコイン、[コイン率, コイン率 + 所持金率) なら所持金（plan 16.2）。
                var roll = _random.NextDouble();
                if (roll < definition.CoinRate)
                {
                    DroppedCoins += _random.NextInt(definition.CoinMin, definition.CoinMax + 1);
                }
                else if (roll < definition.CoinRate + definition.MoneyRate)
                {
                    // 所持金だけにコインの倍率を掛ける（四捨五入、plan 16.2）。
                    var money = _random.NextInt(definition.MoneyMin, definition.MoneyMax + 1);
                    DroppedMoney += Boosts == null ? money : Boosts.ApplyMoneyRate(money);
                }

                _enemies.RemoveAt(i);
            }
        }

        private BattleUnit NearestUnitInRange(BattleEnemy enemy)
        {
            BattleUnit target = null;
            var nearest = double.MaxValue;
            foreach (var unit in _units)
            {
                if (!unit.IsAlive)
                {
                    continue;
                }

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
