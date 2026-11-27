using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// 1 ステージの戦闘（FR-013, FR-024, FR-027, FR-028, FR-030〜033）。<see cref="WaveSchedule"/> どおりに敵を出し、経路を進め、
    /// 地上の敵を味方にブロックさせ、敵と味方を攻撃させ（回復・強化を含む）、HP0 になった側を消す。ゴールに着いた敵はライフを減らし、
    /// 倒された敵はコインを落とす。1/60 秒の固定ステップで進め、ライフ0（ゲームオーバー）で止まる。
    /// </summary>
    public sealed class Battlefield
    {
        private readonly UnitRanges _ranges;
        private readonly IRandom _random;
        private readonly List<BattleEnemy> _enemies = new List<BattleEnemy>();
        private readonly List<BattleUnit> _units = new List<BattleUnit>();

        // 射程ちょうどの距離を浮動小数の誤差で外さないための許容幅。
        private const double RangeEpsilon = 1e-9;

        // Spawn で使い回す（ステップごとに確保しない）。
        private readonly List<WaveSpawn> _due = new List<WaveSpawn>();

        // 待っている赤7ごとの、ダメージが入るまでの残りステップ（spec 003 FR-005）。
        private readonly List<int> _redStrikes = new List<int>();
        private readonly List<int> _redPowers = new List<int>();

        // 待っている緑7ごとの、回復するまでの残りステップ（spec 005 FR-002）。
        private readonly List<int> _greenHeals = new List<int>();
        private readonly List<int> _greenPowers = new List<int>();

        /// <summary>全ウェーブを決めた時刻に出す（時刻が絶対の1ウェーブの表として扱う）。</summary>
        public Battlefield(WaveSchedule wave, UnitRanges ranges, int life, IRandom random, SlotEffects effects = null)
            : this(new WaveProgress(wave, WaveMode.Timed, 0), ranges, life, random, effects)
        {
        }

        /// <param name="waves">このステージのウェーブの進行（FR-102）。</param>
        /// <param name="life">ウェーブ開始時のライフ（満タン、FR-013）。</param>
        /// <param name="random">撃破ドロップの抽選に使う（FR-033）。</param>
        /// <param name="effects">この戦闘の出目の効果（FR-112）。null なら効果なし（<see cref="ApplySlot"/> は使えない）。</param>
        public Battlefield(WaveProgress waves, UnitRanges ranges, int life, IRandom random, SlotEffects effects = null)
        {
            Waves = waves ?? throw new ArgumentNullException(nameof(waves));
            _ranges = ranges;
            _random = random;
            Life = life;
            Effects = effects;
        }

        /// <summary>この戦闘の出目の効果（FR-112）。null なら効果なし。</summary>
        public SlotEffects Effects { get; }

        /// <summary>出現する敵の HP の倍率（T617、stages.csv の enemy_hp_rate）。</summary>
        public double EnemyHpRate { get; set; } = 1;

        /// <summary>このステージのウェーブの進行（FR-102）。</summary>
        public WaveProgress Waves { get; }

        /// <summary>
        /// 攻撃に使う攻撃力。基礎×強化（<see cref="BattleUnit.Atk"/>、四捨五入済み）× バッファー × バーを最後に四捨五入する
        /// （plan 16.4 の掛け順）。
        /// </summary>
        private int EffectiveAtk(BattleUnit unit)
        {
            var rate = BuffRateOf(unit);
            if (Effects != null)
            {
                rate *= Effects.AttackRate;
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

        /// <summary>
        /// 揃った出目の効果をすぐ出す（FR-112）。強さは賭け枚数 <paramref name="power"/>（1〜3）倍（spec 011 FR-020）。
        /// 赤7は空中を含む全ての敵に防御を無視した最大 HP の割合のダメージ（spec 011 FR-021、倒した敵はドロップする）。
        /// ただし赤7のダメージは <see cref="SlotEffects.RedDelaySteps"/> だけ戦闘を進めてから入る（spec 003 FR-005、息吹が爆発する瞬間）。
        /// 待っている間に出た敵にも当たり、ステージが先に終わって進めなくなれば入らない。
        /// 緑7は配置中の味方の HP を最大 HP × 率だけ回復（最大値で止め、ライフは回復しない）。回復は <see cref="SlotEffects.GreenDelaySteps"/>
        /// だけ戦闘を進めてから、その時点の味方に入る（spec 005 FR-002、光の柱が立つ瞬間）。青7・黄7・バーは時間の効果を始め、コインは倍率を上げる。
        /// </summary>
        public void ApplySlot(SlotSymbol symbol, int power = 1)
        {
            if (Effects == null)
            {
                throw new InvalidOperationException("この戦闘には出目の効果がない。");
            }

            if (power < SlotMachine.MinBet || power > SlotMachine.MaxBet)
            {
                throw new ArgumentOutOfRangeException(nameof(power), power, "1〜3");
            }

            switch (symbol)
            {
                case SlotSymbol.Red7:
                    if (Effects.RedDelaySteps > 0)
                    {
                        _redStrikes.Add(Effects.RedDelaySteps);
                        _redPowers.Add(power);
                    }
                    else
                    {
                        StrikeRed(power);
                        RemoveDefeatedEnemies();
                    }

                    break;
                case SlotSymbol.Blue7:
                    if (Effects.QueueStop(power))
                    {
                        BlueFroze?.Invoke();
                    }

                    break;
                case SlotSymbol.Green7:
                    if (Effects.GreenDelaySteps > 0)
                    {
                        _greenHeals.Add(Effects.GreenDelaySteps);
                        _greenPowers.Add(power);
                    }
                    else
                    {
                        HealGreen(power);
                    }

                    break;
                case SlotSymbol.Yellow7:
                    if (Effects.QueuePoison(power))
                    {
                        YellowBurst?.Invoke();
                    }

                    break;
                case SlotSymbol.Bar:
                    if (Effects.QueueAttack(power))
                    {
                        BarBurst?.Invoke();
                    }

                    break;
                case SlotSymbol.Coin:
                    if (Effects.QueueCoin(power))
                    {
                        CoinGained?.Invoke();
                    }

                    break;
            }
        }

        /// <summary>待っている赤7の数（spec 003 FR-005）。</summary>
        public int PendingRedStrikes => _redStrikes.Count;

        /// <summary>赤7のダメージが入った（息吹の爆発、spec 003 FR-001）。ダメージを入れた後、倒した敵を消す前に呼ぶ。</summary>
        public event Action RedStruck;

        /// <summary>青7の停止が始まった（盤面が凍りついた、spec 004 FR-003）。待ちが 0 なら <see cref="ApplySlot"/> の中から呼ぶ。</summary>
        public event Action BlueFroze;

        /// <summary>黄7の毒が始まった（毒の沼が弾けた、spec 006 FR-003）。待ちが 0 なら <see cref="ApplySlot"/> の中から呼ぶ。</summary>
        public event Action YellowBurst;

        /// <summary>バーの攻撃力アップが始まった（闘気が爆発した、spec 007 FR-003）。待ちが 0 なら <see cref="ApplySlot"/> の中から呼ぶ。</summary>
        public event Action BarBurst;

        /// <summary>コインの所持金の倍率が1段上がった（金貨が所持金の表示に吸い込まれた、spec 009 FR-003）。待ちが 0 なら <see cref="ApplySlot"/> の中から呼ぶ。</summary>
        public event Action CoinGained;

        /// <summary>敵を倒した（spec 009 FR-005、倒した敵からお札とコインが飛ぶ）。その敵が落とした所持金（倍率込み）とスロットコインを渡す。ドロップを入れた後、リストから消す前に呼ぶ。全体ダメージ・毒で倒した敵も含む。</summary>
        public event Action<BattleEnemy, int, int> EnemyDefeated;

        /// <summary>毒のダメージが入った（spec 006 FR-004、オーラの点滅）。ダメージを入れた後、倒した敵を消す前に呼ぶ。</summary>
        public event Action PoisonTicked;

        /// <summary>待っている緑7の数（spec 005 FR-002）。</summary>
        public int PendingGreenHeals => _greenHeals.Count;

        /// <summary>緑7の回復が入った（光の柱が立った、spec 005 FR-001）。回復した後に呼ぶ。待ちが 0 なら <see cref="ApplySlot"/> の中から呼ぶ。</summary>
        public event Action GreenHealed;

        private void HealGreen(int power)
        {
            foreach (var unit in _units)
            {
                if (unit.IsAlive)
                {
                    unit.Heal((int)Math.Round(unit.MaxHp * Effects.GreenHealRate * power, MidpointRounding.AwayFromZero));
                }
            }

            GreenHealed?.Invoke();
        }

        // 待っている緑7の残りを1ステップ減らし、0 になったものを回復する。
        private void StepGreenHeals()
        {
            for (var i = _greenHeals.Count - 1; i >= 0; i--)
            {
                if (--_greenHeals[i] <= 0)
                {
                    var power = _greenPowers[i];
                    _greenHeals.RemoveAt(i);
                    _greenPowers.RemoveAt(i);
                    HealGreen(power);
                }
            }
        }

        private void StrikeRed(int power)
        {
            DamageAllEnemies(Effects.RedHpRate(power));
            RedStruck?.Invoke();
        }

        // 待っている赤7の残りを1ステップ減らし、0 になったものを当てる。
        private void StepRedStrikes()
        {
            for (var i = _redStrikes.Count - 1; i >= 0; i--)
            {
                if (--_redStrikes[i] <= 0)
                {
                    var power = _redPowers[i];
                    _redStrikes.RemoveAt(i);
                    _redPowers.RemoveAt(i);
                    StrikeRed(power);
                }
            }
        }

        /// <summary>
        /// 生きている敵全員（空中を含む）に防御を無視した最大 HP の割合のダメージ（ボスは弱める、最低1、spec 011 FR-021）。
        /// 撃破とドロップは <see cref="RemoveDefeatedEnemies"/>。
        /// </summary>
        private void DamageAllEnemies(double hpRate)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.IsAlive)
                {
                    enemy.TakeDamage(Effects.HpRateDamage(enemy, hpRate));
                }
            }
        }

        /// <summary>
        /// 残りの敵の数（FR-009・FR-102）: ステージ全体のまだ出ていない出現予定（始まっていないウェーブを含む）とフィールドにいる敵の合計。
        /// 撃破でもゴールでも1減る。
        /// </summary>
        public int RemainingEnemies => Waves.Unspawned + _enemies.Count;

        /// <summary>最後のウェーブまで出し終わり、フィールドに敵が残っていない（ゲームオーバーでない）。</summary>
        public bool IsCleared => !IsGameOver && Waves.IsFinished && _enemies.Count == 0;

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

        /// <summary>HP0 で味方が消えたとき（FR-024）。撤退では呼ばれない。</summary>
        public event Action<BattleUnit> UnitFell;

        /// <summary>表示用の通知。複数ステップを一度に進めても出現・到達を取りこぼさない。</summary>
        public event Action<MapPath> EnemySpawned;
        public event Action<GridPoint> BaseReached;

        /// <summary>通常攻撃の通知（弾の見た目用、FR-082）。ダメージを入れた後に呼ぶ。範囲攻撃では呼ばれない。</summary>
        public event Action<BattleUnit, BattleEnemy> UnitAttacked;
        public event Action<BattleEnemy, BattleUnit> EnemyAttacked;

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
        /// → 毒 → 待っている赤7 → HP0 の敵の消滅（ドロップ） の順で処理し、出目の効果の残り時間を1ステップ減らす。
        /// 停止中（青7）は移動と敵の攻撃を飛ばす。ゲームオーバーになったら残りのステップは進めない。
        /// 見せ場のウェーブ（<see cref="WaveDefinition.SlotShowcase"/>）を出し終わったステップの後は、<see cref="ReleaseShowcase"/> まで進めない（spec 011 FR-031）。
        /// </summary>
        public void Advance(int steps)
        {
            for (var i = 0; i < steps && !IsGameOver && !IsShowcaseHolding; i++)
            {
                Spawn();
                // 停止中の敵は進まず攻撃もしない（範囲攻撃と間隔の消化も止まる、E-27）。停止中に出た敵も止まる。
                if (Effects == null || !Effects.IsStopped)
                {
                    Move();
                    Attack();
                }

                RemoveFallenUnits();
                Act();
                // 毒は停止中も減る（E-27）。出目の効果の残り時間はここで1ステップ減る。
                if (Effects != null && Effects.Step())
                {
                    DamageAllEnemies(Effects.PoisonHpRate);
                    PoisonTicked?.Invoke();
                }

                // 黄7の沼が弾ける（spec 006 FR-003）: 毒は次のステップから丸々 PoisonSteps 続く。
                if (Effects != null && Effects.StepPoisonDelay())
                {
                    YellowBurst?.Invoke();
                }

                // バーの闘気が爆発する（spec 007 FR-003）: 攻撃力アップは次のステップから丸々 AttackSteps 続く。
                if (Effects != null && Effects.StepAttackDelay())
                {
                    BarBurst?.Invoke();
                }

                // コインの金貨が吸い込まれる（spec 009 FR-003）: 倍率は次に倒した敵から効く。
                if (Effects != null)
                {
                    for (var gained = Effects.StepCoinDelay(); gained > 0; gained--)
                    {
                        CoinGained?.Invoke();
                    }
                }

                // 青7の凍結（spec 004 FR-003）: 停止は次のステップから丸々 StopSteps 続く。
                if (Effects != null && Effects.StepStopDelay())
                {
                    BlueFroze?.Invoke();
                }

                StepRedStrikes();
                StepGreenHeals();
                RemoveDefeatedEnemies();
                Tick++;
                CheckShowcase();
                CheckShowcaseEnd();
            }
        }

        private bool _showcaseDisabled;
        private int _hasShowcase = -1;

        /// <summary>このステージに見せ場のウェーブがある（無効にされていない）。</summary>
        public bool HasShowcase
        {
            get
            {
                if (_hasShowcase < 0)
                {
                    _hasShowcase = 0;
                    foreach (var wave in Waves.Schedule.Waves)
                    {
                        if (wave.SlotShowcase) _hasShowcase = 1;
                    }
                }

                return _hasShowcase == 1 && !_showcaseDisabled;
            }
        }

        /// <summary>見せ場が終わった（止まりが解け、見せ場の群れが全滅した、spec 015 FR-006）。</summary>
        public bool ShowcaseFinished { get; private set; }

        /// <summary>見せ場がまだ終わっていない（この間はキャラを置けない、spec 015 FR-006）。</summary>
        public bool IsShowcasePending => HasShowcase && !ShowcaseFinished;

        /// <summary>見せ場の群れが全滅し、青7と黄7の効果を消した（spec 015 FR-007）。<see cref="Advance"/> の中から呼ぶ。</summary>
        public event Action ShowcaseEnded;

        /// <summary>この戦闘の見せ場を起こさない（止めない・配置も止めない・見せ場の群れは普通の敵として扱う）。テスト用（BattleController.SkipShowcase）。</summary>
        public void DisableShowcase()
        {
            _showcaseDisabled = true;
            ShowcaseStarted = true;
            ShowcaseFinished = true;
            IsShowcaseHolding = false;
        }

        private void CheckShowcaseEnd()
        {
            if (ShowcaseFinished || !ShowcaseStarted || IsShowcaseHolding)
            {
                return;
            }

            foreach (var enemy in _enemies)
            {
                if (enemy.FromShowcase)
                {
                    return;
                }
            }

            ShowcaseFinished = true;
            Effects?.ClearStopAndPoison();
            ShowcaseEnded?.Invoke();
        }

        /// <summary>見せ場のウェーブを出し終わり、レバーを待って戦闘を止めている（spec 011 FR-031）。</summary>
        public bool IsShowcaseHolding { get; private set; }

        /// <summary>この戦闘の見せ場がもう始まった（1戦闘に1回、FR-033）。</summary>
        public bool ShowcaseStarted { get; private set; }

        /// <summary>見せ場で戦闘を止めた。<see cref="Advance"/> の中から呼ぶ。</summary>
        public event Action ShowcaseHeld;

        /// <summary>見せ場の止まりを解いて戦闘を再開する（レバーを引いたとき）。止まっていなければ何もしない。</summary>
        public void ReleaseShowcase() => IsShowcaseHolding = false;

        private void CheckShowcase()
        {
            if (ShowcaseStarted || IsGameOver)
            {
                return;
            }

            var waves = Waves.Schedule.Waves;
            for (var w = 0; w < waves.Count; w++)
            {
                if (waves[w].SlotShowcase && Waves.IsWaveEmitted(w))
                {
                    ShowcaseStarted = true;
                    IsShowcaseHolding = true;
                    ShowcaseHeld?.Invoke();
                    return;
                }
            }
        }

        private void Spawn()
        {
            _due.Clear();
            Waves.Step(_enemies.Count, _due);
            foreach (var spawn in _due)
            {
                var showcase = Waves.Schedule.FindWave(spawn.Wave)?.SlotShowcase == true;
                _enemies.Add(new BattleEnemy(spawn.Enemy, spawn.Path, EnemyHpRate)
                {
                    AreaCooldownSteps = StepsOf(spawn.Enemy.AreaInterval),
                    IsBoss = spawn.IsBoss,
                    FromShowcase = showcase && !_showcaseDisabled,

                });
                EnemySpawned?.Invoke(spawn.Path);
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
                    var cells = _enemies[i].Path.Cells;
                    BaseReached?.Invoke(cells[cells.Count - 1]);
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
                EnemyAttacked?.Invoke(enemy, target);
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
        /// 攻撃力0の職（タンク）も攻撃しない（FR-109）。ブロックはする。
        /// </summary>
        private void Act()
        {
            foreach (var unit in _units)
            {
                if (unit.Definition.Role == UnitRole.Buff)
                {
                    continue;
                }

                if (EffectiveAtk(unit) <= 0)
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

                target.TakeDamage(Damage.Calculate(EffectiveAtk(unit), target.Definition.Def,
                    unit.Definition.Role, target.Definition.RangedDamageRate));

                unit.AttackCooldownSteps = (int)Math.Round(
                    unit.Definition.AttackInterval * SimulationClock.StepsPerSecond);
                UnitAttacked?.Invoke(unit, target);
            }
        }

        /// <summary>
        /// 近距離職はブロック中の敵、遠距離職は範囲内の空中の敵があればその中から（FR-060）、なければ範囲内の敵から、
        /// ゴールに最も近い敵（同じなら先に出た敵）を選ぶ。空中の敵は遠距離職（弓）だけが狙える（FR-031）。
        /// </summary>
        private BattleEnemy FindTarget(BattleUnit unit)
        {
            BattleEnemy target = null;
            var targetFirst = false;
            var ranged = unit.Definition.Role == UnitRole.Ranged;
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

                // 近距離職はブロック中の敵が先、遠距離職は空中の敵が先（FR-060）。
                var first = ranged ? enemy.Definition.IsAir : enemy.BlockedBy == unit;

                if (target == null || first && !targetFirst ||
                    first == targetFirst && enemy.RemainingDistance < target.RemainingDistance)
                {
                    target = enemy;
                    targetFirst = first;
                }
            }

            return target;
        }

        /// <summary>ブロック中の敵か、範囲内の敵（空中の敵は遠距離職だけ、FR-031）なら狙える。</summary>
        private static bool CanTarget(BattleUnit unit, BattleEnemy enemy) =>
            enemy.BlockedBy == unit ||
            unit.InRange(enemy.Cell) && (!enemy.Definition.IsAir || unit.Definition.Role == UnitRole.Ranged);

        /// <summary>
        /// HP0 の敵を消し、使っていたブロックの空きを戻す。コインと所持金をそれぞれ別に抽選し、当たれば最小〜最大の量（両端を含む一様）を落とす（FR-107）。
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
                var coins = 0;
                var gainedMoney = 0;
                // コインと所持金は別々の抽選（FR-107、002 plan 2.4）。見せ場の群れは落とさない（spec 015 FR-005）。
                if (!enemy.FromShowcase && _random.NextDouble() < definition.CoinRate)
                {
                    coins = _random.NextInt(definition.CoinMin, definition.CoinMax + 1);
                    DroppedCoins += coins;
                }

                if (!enemy.FromShowcase && _random.NextDouble() < definition.MoneyRate)
                {
                    // 所持金だけにコインの倍率を掛ける（四捨五入、plan 16.2）。
                    var money = _random.NextInt(definition.MoneyMin, definition.MoneyMax + 1);
                    gainedMoney = Effects == null ? money : Effects.ApplyMoneyRate(money);
                    DroppedMoney += gainedMoney;
                }

                EnemyDefeated?.Invoke(enemy, gainedMoney, coins);
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
