using NUnit.Framework;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    [Description("FR-042, AC-042: スロットの効果（攻撃力アップ・攻撃速度アップ・全体HP回復）が戦闘に効く")]
    public sealed class SlotEffectsTests
    {
        private static Battlefield Create(string waveRows) => BattleFixtures.CreateField(waveRows);

        private static BattleUnit Place(Battlefield field, string unitId, int x, int y,
            Facing facing = Facing.Right) =>
            field.AddUnit(BattleFixtures.LoadUnits().Get(unitId), new GridPoint(x, y), facing);

        [Test]
        [Description("AC-042: 攻撃力アップ中は攻撃力×倍率（弓 350×1.5=525）でダメージを計算する")]
        public void Attack_Up_Multiplies_The_Damage()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            Place(field, "archer", 1, 1);
            field.Effects.GrantAttack(1.5, 20);

            field.Advance(45);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - (525 - 50)));
        }

        [Test]
        [Description("plan 5章: 攻撃力アップは回復にも掛かる（ヒーラー 200×1.5=300）")]
        public void Attack_Up_Multiplies_The_Heal()
        {
            // 89 ステップ目に brute がタンクを打って 1100/1800（UnitActionTests の回復のテストと同じ流れ）。
            var field = Create("1,0,ranged,S1,g,0\n1,0,brute,S1,g,0\n");
            Place(field, "archer", 1, 1);
            var tank = Place(field, "tank", 2, 0);
            field.Advance(90);
            Place(field, "healer", 2, 1, Facing.Left);
            field.Effects.GrantAttack(1.5, 20);

            field.Advance(1);

            Assert.That(tank.Hp, Is.EqualTo(1100 + 300));
        }

        [Test]
        [Description("AC-042: 攻撃速度アップ中は攻撃間隔を倍率で割る（弓 1秒→約0.67秒。75ステップで通常1回、効果中は2回）")]
        public void Speed_Up_Shortens_The_Attack_Interval()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            Place(field, "archer", 1, 1);
            field.Effects.GrantSpeed(1.5, 20);

            field.Advance(75);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - 2 * (350 - 50)));
        }

        [Test]
        [Description("AC-042: 効果は定義の秒数（1秒＝60ステップ）だけ続き、切れると倍率が1に戻る")]
        public void Effects_End_After_Their_Seconds()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            field.Effects.GrantAttack(1.5, 1);
            field.Effects.GrantSpeed(2, 1);
            field.Advance(59);
            var during = (field.Effects.AttackRate, field.Effects.SpeedRate);

            field.Advance(1);

            Assert.That((during, (field.Effects.AttackRate, field.Effects.SpeedRate)),
                Is.EqualTo(((1.5, 2.0), (1.0, 1.0))));
        }

        [Test]
        [Description("AC-042, D-45: 効果中に再び揃えると、残り時間に持続時間を足す（残り10秒＋20秒＝30秒）")]
        public void Granting_During_The_Effect_Extends_It()
        {
            var field = Create("1,0,melee,S1,g,0\n");
            field.Effects.GrantAttack(1.5, 20);
            field.Effects.GrantSpeed(1.5, 20);
            field.Advance(600);

            field.Effects.GrantAttack(1.5, 20);
            field.Effects.GrantSpeed(1.5, 20);

            Assert.That((field.Effects.AttackSeconds, field.Effects.SpeedSeconds), Is.EqualTo((30.0, 30.0)));
        }

        [Test]
        [Description("plan 5章: 残り時間は戦闘のステップで数える（ステップを進めなければ減らない）")]
        public void Effects_Do_Not_Wear_Off_Without_Steps()
        {
            var field = Create("1,0,melee,S1,g,0\n");

            field.Effects.GrantAttack(1.5, 20);

            Assert.That(field.Effects.AttackSeconds, Is.EqualTo(20.0));
        }

        [Test]
        [Description("AC-042, D-45: 全体HP回復は配置中の味方全員に最大HP×割合を足す（最大HPまで）")]
        public void Heal_All_Restores_A_Rate_Of_Max_Hp_To_Every_Unit()
        {
            // 0 ステップ目に ranged が弓を撃ち 450/700、89 ステップ目に brute がタンクを打って 1100/1800。
            var field = Create("1,0,ranged,S1,g,0\n1,0,brute,S1,g,0\n");
            var archer = Place(field, "archer", 1, 1);
            var tank = Place(field, "tank", 2, 0);
            field.Advance(90);
            var before = (archer.Hp, tank.Hp);

            field.HealAll(0.25);

            Assert.That((before, (archer.Hp, tank.Hp)), Is.EqualTo(((450, 1100), (625, 1550))));
        }

        [Test]
        public void Heal_All_Does_Not_Exceed_Max_Hp()
        {
            var field = Create("1,0,ranged,S1,g,0\n1,0,brute,S1,g,0\n");
            var archer = Place(field, "archer", 1, 1);
            var tank = Place(field, "tank", 2, 0);
            field.Advance(90);

            field.HealAll(0.5);

            Assert.That((archer.Hp, tank.Hp), Is.EqualTo((700, 1800)));
        }
    }
}
