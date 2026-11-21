using System;
using System.Collections.Generic;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>フィールドに配置された味方1体。ステータスは種類の強化段階に合わせる（FR-026）。</summary>
    public sealed class BattleUnit
    {
        internal BattleUnit(UnitDefinition definition, GridPoint cell, Facing facing, IReadOnlyList<GridPoint> rangeCells,
            UnitUpgradeLevel upgrade)
        {
            Definition = definition;
            Cell = cell;
            Facing = facing;
            RangeCells = rangeCells;
            ApplyUpgrade(upgrade);
            Hp = MaxHp;
        }

        public UnitDefinition Definition { get; }

        /// <summary>強化後の最大 HP・攻撃力・防御力（FR-026）。</summary>
        public int MaxHp { get; private set; }

        public int Atk { get; private set; }
        public int Def { get; private set; }

        public GridPoint Cell { get; }
        public Facing Facing { get; }

        /// <summary>向きに合わせて回した攻撃範囲のマス（マップ外も含む）。</summary>
        public IReadOnlyList<GridPoint> RangeCells { get; }

        public int Hp { get; private set; }
        public bool IsAlive => Hp > 0;

        /// <summary>効果時間中のスキル（plan 16.4）。なければ null。</summary>
        public SkillDefinition Skill { get; private set; }

        /// <summary>スキルの効果時間の残りステップ数。0 なら効果なし。</summary>
        public int SkillStepsLeft { get; private set; }

        /// <summary>いまのブロック数（スキルの効果時間中は <see cref="SkillDefinition.BlockAdd"/> を足す）。</summary>
        public int Block => Definition.Block + (Skill?.BlockAdd ?? 0);

        /// <summary>攻撃間隔に掛ける倍率（スキルの効果時間中だけ1以外）。</summary>
        public double IntervalRate => Skill?.IntervalRate ?? 1;

        /// <summary>バッファーとしての倍率（スキルの効果時間中は <see cref="SkillDefinition.BuffAdd"/> を足す）。</summary>
        internal double BuffRate => Definition.BuffRate + (Skill?.BuffAdd ?? 0);

        /// <summary>効果時間を付ける（効果中なら残りを最初からにする）。効果時間0のスキルでは何も付けない。</summary>
        internal void StartSkill(SkillDefinition skill)
        {
            var steps = skill.DurationSteps;
            if (steps <= 0)
            {
                return;
            }

            Skill = skill;
            SkillStepsLeft = steps;
        }

        /// <summary>効果時間を1ステップ減らし、0 になったら効果を外す。</summary>
        internal void TickSkill()
        {
            if (SkillStepsLeft > 0 && --SkillStepsLeft == 0)
            {
                Skill = null;
            }
        }

        /// <summary>ブロック中の敵のブロック消費の合計。<see cref="UnitDefinition.Block"/> を超えない。</summary>
        public int BlockUsed { get; internal set; }

        /// <summary>次に攻撃（回復）できるまでの残りステップ数。</summary>
        internal int AttackCooldownSteps { get; set; }

        internal bool InRange(GridPoint cell)
        {
            for (var i = 0; i < RangeCells.Count; i++)
            {
                if (RangeCells[i].Equals(cell))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>スキルの効果時間中は <see cref="SkillDefinition.DamageTakenRate"/> を掛ける（四捨五入）。</summary>
        internal void TakeDamage(int damage)
        {
            if (Skill != null)
            {
                damage = (int)Math.Round(damage * Skill.DamageTakenRate, MidpointRounding.AwayFromZero);
            }

            Hp = Hp > damage ? Hp - damage : 0;
        }

        /// <summary>基本ステータスに段階の倍率をかけ直す（端数は四捨五入）。最大 HP が増えた分は現在 HP にも足す（plan 5章）。</summary>
        internal void ApplyUpgrade(UnitUpgradeLevel upgrade)
        {
            var maxHp = upgrade.MaxHpOf(Definition);
            Hp += maxHp - MaxHp;
            MaxHp = maxHp;
            Atk = upgrade.AtkOf(Definition);
            Def = upgrade.DefOf(Definition);
        }
    }
}
