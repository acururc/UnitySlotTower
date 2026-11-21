using System;
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
                BattleFixtures.WavesHeader + "e,0,heavy,S1,g,0\n"), "e", BattleFixtures.LoadMap(), enemies);
            return new Battlefield(wave, BattleFixtures.LoadRanges(), 10, new SeededRandom(1));
        }

        private static CsvTable Project(string name) =>
            CsvParser.Parse(name, File.ReadAllText(Path.Combine(TablePaths.Root, name)));

        [TestCase("melee", 4, 1.2, 2, false, 240, 0.9, 1.05)]
        [TestCase("melee", 5, 1.0, 3, false, 240, 0.9, 1.1)]
        [TestCase("heavy", 4, 1.2, 3, false, 240, 0.9, 1.1)]
        [TestCase("mixed", 4, 1.2, 3, false, 240, 0.9, 1.1)]
        [TestCase("air", 4, 2.5, 2, true, 240, 0.9, 1.05)]
        [TestCase("air", 5, 2.5, 3, true, 240, 0.9, 1.1)]
        [Description("AC-056: 固定高台の弓2体・1群れ。段階2と、2回強化した段階3を検証。空中は阻止、地上は到達")]
        public void Two_Archers_Stop_Air_But_Not_Ground_Groups(
            string enemyId, int count, double interval, int level, bool stopsAll,
            int atk, double attackInterval, double atkMult)
        {
            var units = UnitTable.Load(Project(UnitTable.FileName));
            var enemies = EnemyTable.Load(Project(EnemyTable.FileName));
            var ranges = UnitRanges.Load(Project(UnitRanges.FileName), units);
            var upgrades = UnitUpgradeTable.Load(Project(UnitUpgradeTable.FileName), units);
            // 同じ直線経路・配置で地上と空中を比較。経路9マス、高台(2,1),(3,1)、右向き。
            var paths = "map_id,path_id,is_air,step,x,y\n";
            for (var x = 0; x < 9; x++)
                paths += $"m,p,{(enemyId == "air" ? 1 : 0)},{x + 1},{x},0\n";
            var map = BattleMap.Load("m", "S1,G,G,G,G,G,G,G,E\nH,H,H,H,H,H,H,H,H\n",
                CsvParser.Parse(BattleMap.PathsFileName, paths));
            var rows = enemyId == "mixed" ? FormattableString.Invariant(
                $"e,0,melee,S1,p,0,{count - 1},{interval}\ne,{(count - 1) * interval},heavy,S1,p,0,1,0\n") :
                FormattableString.Invariant($"e,0,{enemyId},S1,p,0,{count},{interval}\n");
            var wave = WaveSchedule.Load(CsvParser.Parse(WaveSchedule.FileName,
                BattleFixtures.GroupWavesHeader + rows), "e", map, enemies);
            var field = new Battlefield(wave, ranges, 100, new SeededRandom(1));
            field.AddUnit(units.Get("archer"), new GridPoint(2, 1), Facing.Right, upgrades.Get("archer", level));
            field.AddUnit(units.Get("archer"), new GridPoint(3, 1), Facing.Right, upgrades.Get("archer", level));
            field.Advance(60 * 60);
            Assert.That((field.IsCleared, field.ArrivedCount == 0, units.Get("archer").Atk,
                units.Get("archer").AttackInterval, upgrades.Get("archer", level).AtkMult),
                Is.EqualTo((true, stopsAll, atk, attackInterval, atkMult)));
        }

        [Test]
        public void Project_Heavy_Has_High_Hp_Defense_And_Melee_Grunt_Drops()
        {
            var heavy = EnemyTable.Load(Project(EnemyTable.FileName)).Get("heavy");
            Assert.That((heavy.MaxHp, heavy.Def, heavy.Range, heavy.IsAir, heavy.RangedDamageRate,
                    heavy.CoinRate, heavy.CoinMin, heavy.CoinMax, heavy.MoneyRate, heavy.MoneyMin, heavy.MoneyMax),
                Is.EqualTo((2200, 180, 0.0, false, 0.4, 0.15, 1, 1, 1.0, 30, 30)));
        }
    }
}
