using System;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Tests.EditMode.Slot;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    /// <summary>spec 011 AC-003: 効果の強さは賭け枚数倍、赤7・毒は最大 HP の割合（ボスは半分、最低1）。</summary>
    public sealed class SlotPowerTests
    {
        private static Battlefield Create(string waveRows, SlotRules rules = null) =>
            new Battlefield(BattleFixtures.LoadWave(waveRows), BattleFixtures.LoadRanges(), 10, new SeededRandom(1),
                new SlotEffects(rules ?? SlotTestRules.Create()));

        [TestCase(1, 1000 - 200)]
        [TestCase(2, 1000 - 400)]
        [TestCase(3, 1000 - 600)]
        [Description("FR-020/021: 赤7は最大HPの20%×枚数")]
        public void Red_Scales_With_Power(int power, int expected)
        {
            var field = Create("0,melee,S1,g,0\n");
            field.Advance(1);

            field.ApplySlot(SlotSymbol.Red7, power);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(expected));
        }

        [Test]
        [Description("FR-021: ボスへの赤7は boss_damage_rate（0.5）倍")]
        public void Red_Is_Halved_For_Bosses()
        {
            var field = Create("0,melee,S1,g,1\n0,melee,S1,g,0\n");
            field.Advance(1);

            field.ApplySlot(SlotSymbol.Red7, 3);

            Assert.That((field.Enemies[0].IsBoss, field.Enemies[0].Hp, field.Enemies[1].Hp), Is.EqualTo((true, 1000 - 300, 1000 - 600)));
        }

        [Test]
        [Description("FR-021: 割合のダメージは最低1")]
        public void Hp_Rate_Damage_Is_At_Least_One()
        {
            var field = Create("0,melee,S1,g,0\n", SlotTestRules.Create(redHpRate: 0.0001));
            field.Advance(1);

            field.ApplySlot(SlotSymbol.Red7);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(999));
        }

        [Test]
        [Description("FR-020/021: 毒は毎秒最大HPの4%×枚数、ボスは半分")]
        public void Poison_Scales_With_Power_And_Is_Halved_For_Bosses()
        {
            var field = Create("0,quaker,S1,g,0\n0,quaker,S1,g,1\n");
            field.Advance(1);

            field.ApplySlot(SlotSymbol.Yellow7, 2);
            field.Advance(60);

            Assert.That((field.Enemies[0].Hp, field.Enemies[1].Hp), Is.EqualTo((100000 - 8000, 100000 - 4000)));
        }

        [TestCase(1, 180)]
        [TestCase(3, 540)]
        [Description("FR-020: 青7の停止は3秒×枚数（表示の分母も同じ）")]
        public void Stop_Duration_Scales_With_Power(int power, int steps)
        {
            var field = Create("0,melee,S1,g,0\n");

            field.ApplySlot(SlotSymbol.Blue7, power);

            Assert.That((field.Effects.StopStepsLeft, field.Effects.StopSteps), Is.EqualTo((steps, steps)));
        }

        [TestCase(1, 2.0)]
        [TestCase(2, 3.0)]
        [TestCase(3, 4.0)]
        [Description("FR-020 / D6: バーの攻撃力の倍率は 1＋枚数、時間は10秒のまま")]
        public void Bar_Rate_Is_One_Plus_Power(int power, double rate)
        {
            var field = Create("0,quaker,S1,g,0\n");

            field.ApplySlot(SlotSymbol.Bar, power);

            Assert.That((field.Effects.AttackRate, field.Effects.AttackStepsLeft), Is.EqualTo((rate, 600)));
        }

        [Test]
        [Description("FR-020: 緑7の回復量は枚数倍（最大値で止まる）")]
        public void Green_Heal_Scales_With_Power()
        {
            var field = Create("0,brute,S1,g,0\n", SlotTestRules.Create(greenHealRate: 0.01));
            var sword = field.AddUnit(BattleFixtures.LoadUnits().Get("sword"), new GridPoint(1, 0), Facing.Right);
            for (var i = 0; i < 120 && sword.Hp == sword.MaxHp; i++)
            {
                field.Advance(1);
            }

            Assume.That(sword.MaxHp - sword.Hp, Is.GreaterThanOrEqualTo(36));
            var hp = sword.Hp;

            field.ApplySlot(SlotSymbol.Green7, 3);

            Assert.That(sword.Hp, Is.EqualTo(hp + 36));
        }

        [Test]
        [Description("FR-020: コインは倍率を +0.1×枚数 上げ、上限1.5で止まって抽選から外れる")]
        public void Coin_Steps_By_Power_Up_To_The_Cap()
        {
            var field = Create("0,quaker,S1,g,0\n");

            field.ApplySlot(SlotSymbol.Coin, 3);
            var first = (field.Effects.MoneyRate, field.Effects.CanRoll(SlotSymbol.Coin));
            field.ApplySlot(SlotSymbol.Coin, 3);

            Assert.That((first, field.Effects.MoneyRate, field.Effects.CanRoll(SlotSymbol.Coin)),
                Is.EqualTo(((1.3, true), 1.5, false)));
        }

        [Test]
        [Description("spec 009 + 011: 待っているコインは枚数ぶん数え、吸い込みで枚数段上がる")]
        public void Pending_Coins_Count_Their_Power()
        {
            var field = Create("0,quaker,S1,g,0\n", SlotTestRules.Create(coinDelaySeconds: 1));

            field.ApplySlot(SlotSymbol.Coin, 2);
            var waiting = (field.Effects.PendingCoins, field.Effects.MoneyRate);
            field.Advance(60);

            Assert.That((waiting, field.Effects.PendingCoins, field.Effects.MoneyRate), Is.EqualTo(((2, 1.0), 0, 1.2)));
        }

        [Test]
        [Description("spec 003 + 011: 待っている赤7は揃えたときの枚数で当たる")]
        public void Delayed_Red_Keeps_Its_Power()
        {
            var field = Create("0,melee,S1,g,0\n", SlotTestRules.Create(redDelaySeconds: 1));
            field.Advance(1);

            field.ApplySlot(SlotSymbol.Red7, 2);
            field.Advance(60);

            Assert.That(field.Enemies[0].Hp, Is.EqualTo(1000 - 400));
        }

        [TestCase(0)]
        [TestCase(4)]
        public void Power_Out_Of_Range_Throws(int power)
        {
            var field = Create("0,melee,S1,g,0\n");

            Assert.Throws<ArgumentOutOfRangeException>(() => field.ApplySlot(SlotSymbol.Red7, power));
        }
    }
}
