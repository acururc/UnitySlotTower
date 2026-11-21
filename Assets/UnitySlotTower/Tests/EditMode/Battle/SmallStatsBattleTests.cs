using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Slot;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("FR-109: 002 の小さい桁。攻撃0の職は攻撃せず、攻撃1でもバッファーとバーが効く")]
    public sealed class SmallStatsBattleTests
    {
        // plan 4章の仮値と同じ桁。id は fixture の範囲（unit_ranges）を使うため tank・sword・buffer にする。
        private const string Units =
            "id,max_hp,atk,def,attack_interval,block,tile,cost,cooldown,role,buff_rate\n" +
            "tank,30,0,0,1.2,3,ground,15,20,melee,1\n" +
            "sword,10,1,0,0.5,2,ground,12,12,melee,1\n" +
            "buffer,10,0,0,2.5,0,high,18,25,buff,2\n";

        private static readonly UnitTable SmallUnits = UnitTable.Load(CsvParser.Parse(UnitTable.FileName, Units));

        private static Battlefield Create(bool bar)
        {
            var field = new Battlefield(BattleFixtures.LoadWave("0,dummy,S1,g,0\n"), BattleFixtures.LoadRanges(), 10,
                new SeededRandom(1), new SlotEffects(SlotTestRules.Create()));
            if (bar)
            {
                field.ApplySlot(SlotSymbol.Bar);
            }

            return field;
        }

        private static int CountAttacks(Battlefield field, int steps)
        {
            var attacks = 0;
            field.UnitAttacked += (_, _) => attacks++;
            field.Advance(steps);
            return attacks;
        }

        [TestCase(false)]
        [TestCase(true)]
        [Description("攻撃0のタンクはブロックはするが攻撃しない（バーの効果中も 0×2=0）")]
        public void Zero_Attack_Unit_Blocks_But_Never_Attacks(bool bar)
        {
            var field = Create(bar);
            var tank = field.AddUnit(SmallUnits.Get("tank"), new GridPoint(1, 0), Facing.Right);

            var attacks = CountAttacks(field, 300);

            Assert.That((attacks, field.Enemies[0].Hp, field.Enemies[0].BlockedBy == tank),
                Is.EqualTo((0, 100, true)));
        }

        [TestCase(false, false, 1)]
        [TestCase(true, false, 2)]
        [TestCase(false, true, 2)]
        [TestCase(true, true, 4)]
        [Description("攻撃1の剣士の1発: バッファー（倍率2）で2、バーで2、両方で4（防御0の的）")]
        public void Attack_One_Is_Raised_By_Buffer_And_Bar(bool buffer, bool bar, int perHit)
        {
            var field = Create(bar);
            field.AddUnit(SmallUnits.Get("sword"), new GridPoint(1, 0), Facing.Right);
            if (buffer)
            {
                field.AddUnit(SmallUnits.Get("buffer"), new GridPoint(1, 1), Facing.Right);
            }

            var attacks = CountAttacks(field, 300);

            Assert.That(attacks, Is.GreaterThan(0));
            Assert.That(field.Enemies[0].Hp, Is.EqualTo(100 - perHit * attacks));
        }
    }
}
