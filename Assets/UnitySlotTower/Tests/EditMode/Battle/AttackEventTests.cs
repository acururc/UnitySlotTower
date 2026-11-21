using System.Collections.Generic;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    /// <summary>
    /// 攻撃の通知（FR-082、plan 19.6）。弾の見た目のためだけの通知で、ダメージは今までどおり攻撃の瞬間に入る。
    /// 地図・敵・味方は <see cref="BattleFixtures"/>。
    /// </summary>
    public sealed class AttackEventTests
    {
        private static BattleUnit Place(Battlefield field, string unitId, int x, int y) =>
            field.AddUnit(BattleFixtures.LoadUnits().Get(unitId), new GridPoint(x, y), Facing.Right);

        [Test]
        [Description("AC-063: 味方が攻撃すると、攻撃した味方と相手の敵が通知され、そのときにはダメージが入っている")]
        public void Unit_Attack_Notifies_Attacker_And_Target_After_Damage()
        {
            var field = BattleFixtures.CreateField("0,melee,S1,g,0\n");
            var archer = Place(field, "archer", 1, 1);
            var seen = new List<(BattleUnit, BattleEnemy, int)>();
            field.UnitAttacked += (unit, enemy) => seen.Add((unit, enemy, enemy.Hp));

            field.Advance(45);

            Assert.That(seen, Is.EqualTo(new[] { (archer, field.Enemies[0], 1000 - (350 - 50)) }));
        }

        [Test]
        [Description("AC-063: 敵が攻撃すると、攻撃した敵と相手の味方が通知され、そのときにはダメージが入っている")]
        public void Enemy_Attack_Notifies_Attacker_And_Target_After_Damage()
        {
            var field = BattleFixtures.CreateField("0,ranged,S1,g,0\n");
            var archer = Place(field, "archer", 1, 1);
            var seen = new List<(BattleEnemy, BattleUnit, int)>();
            field.EnemyAttacked += (enemy, unit) => seen.Add((enemy, unit, unit.Hp));

            field.Advance(1);

            Assert.That(seen, Is.EqualTo(new[] { (field.Enemies[0], archer, 700 - (300 - 50)) }));
        }

        [Test]
        public void No_Notification_Without_A_Target()
        {
            var field = BattleFixtures.CreateField("0,melee,S1,g,0\n");
            Place(field, "archer", 1, 1);
            var count = 0;
            field.UnitAttacked += (_, _) => count++;
            field.EnemyAttacked += (_, _) => count++;

            field.Advance(25);

            Assert.That(count, Is.Zero);
        }
    }
}
