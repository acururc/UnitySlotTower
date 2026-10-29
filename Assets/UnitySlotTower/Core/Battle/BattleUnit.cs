using System;
using System.Collections.Generic;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>フィールドに配置された味方1体。強化は M3。</summary>
    public sealed class BattleUnit
    {
        internal BattleUnit(UnitDefinition definition, GridPoint cell, Facing facing, IReadOnlyList<GridPoint> rangeCells)
        {
            Definition = definition;
            Cell = cell;
            Facing = facing;
            RangeCells = rangeCells;
            Hp = definition.MaxHp;
        }

        public UnitDefinition Definition { get; }
        public GridPoint Cell { get; }
        public Facing Facing { get; }

        /// <summary>向きに合わせて回した攻撃範囲のマス（マップ外も含む）。</summary>
        public IReadOnlyList<GridPoint> RangeCells { get; }

        public int Hp { get; private set; }
        public bool IsAlive => Hp > 0;

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

        internal void TakeDamage(int damage)
        {
            Hp = Hp > damage ? Hp - damage : 0;
        }

        /// <summary>最大 HP を超えては回復しない。</summary>
        internal void Heal(int amount)
        {
            Hp = Math.Min(Hp + amount, Definition.MaxHp);
        }
    }
}
