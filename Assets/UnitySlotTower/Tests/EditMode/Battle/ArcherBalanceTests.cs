using System.IO;
using NUnit.Framework;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    public sealed class ArcherBalanceTests
    {
        [TestCase("archer", 1, 1880)]
        [TestCase("sword", 0, 1650)]
        [Description("AC-056: 通常攻撃が敵の遠距離耐性を使い、近距離は使わない")]
        public void Battlefield_Normal_Attack_Respects_Ranged_Resistance(string unitId, int y, int hp)
        {
            var field = ResistantHeavyField();
            field.AddUnit(BattleFixtures.LoadUnits().Get(unitId), new GridPoint(1, y), Facing.Right);
            field.Advance(45);
            Assert.That(field.Enemies[0].Hp, Is.EqualTo(hp));
        }

        [TestCase("archer", 1, 400)]
        [TestCase("sword", 0, 1150)]
        [Description("AC-056: 即時スキル攻撃にも同じ耐性を適用")]
        public void Battlefield_Skill_Attack_Respects_Ranged_Resistance(string unitId, int y, int damage)
        {
            var field = ResistantHeavyField();
            field.AddUnit(BattleFixtures.LoadUnits().Get(unitId), new GridPoint(1, y), Facing.Right);
            field.Advance(45);
            var before = field.Enemies[0].Hp;
            var skill = BattleFixtures.LoadSkills(BattleFixtures.Skills.Replace(
                "red7,sword,", $"red7,{unitId},"));
            field.ActivateSkill(skill.For(SlotSymbol.Red7));
            Assert.That(before - field.Enemies[0].Hp, Is.EqualTo(damage));
        }

        private static Battlefield ResistantHeavyField()
        {
            var lines = BattleFixtures.Enemies.TrimEnd().Split('\n');
            lines[0] += ",ranged_damage_rate";
            for (var i = 1; i < lines.Length; i++) lines[i] += ",0.4";
            var enemies = EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, string.Join("\n", lines)));
            var wave = WaveSchedule.Load(CsvParser.Parse(WaveSchedule.FileName,
                BattleFixtures.WavesHeader + "1,0,heavy,S1,g,0\n"), 1, BattleFixtures.LoadMap(), enemies);
            return new Battlefield(wave, BattleFixtures.LoadRanges(), 10, new SeededRandom(1));
        }

        private static CsvTable Project(string name) =>
            CsvParser.Parse(name, File.ReadAllText(Path.Combine(TablePaths.Root, name)));

        [Test]
        public void Project_Heavy_Has_High_Hp_Ranged_Resistance_And_Grunt_Drops()
        {
            var heavy = EnemyTable.Load(Project(EnemyTable.FileName)).Get("heavy");
            Assert.That((heavy.MaxHp, heavy.Def, heavy.Range, heavy.IsAir, heavy.RangedDamageRate,
                    heavy.CoinRate, heavy.CoinMin, heavy.CoinMax, heavy.MoneyRate, heavy.MoneyMin, heavy.MoneyMax),
                Is.EqualTo((25, 0, 0.0, false, 0.5, 0.15, 1, 1, 1.0, 30, 30)));
        }
    }
}
