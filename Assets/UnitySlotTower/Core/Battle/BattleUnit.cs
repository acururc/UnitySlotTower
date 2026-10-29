namespace UnitySlotTower.Core.Battle
{
    /// <summary>フィールドに配置された味方1体。HP0 での消滅・攻撃は T206、強化は M3。</summary>
    public sealed class BattleUnit
    {
        internal BattleUnit(UnitDefinition definition, GridPoint cell, Facing facing)
        {
            Definition = definition;
            Cell = cell;
            Facing = facing;
            Hp = definition.MaxHp;
        }

        public UnitDefinition Definition { get; }
        public GridPoint Cell { get; }
        public Facing Facing { get; }
        public int Hp { get; private set; }
        public bool IsAlive => Hp > 0;

        internal void TakeDamage(int damage)
        {
            Hp = Hp > damage ? Hp - damage : 0;
        }
    }
}
