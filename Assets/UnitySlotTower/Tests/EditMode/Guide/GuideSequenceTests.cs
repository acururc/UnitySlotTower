using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Guide;

namespace UnitySlotTower.Tests.EditMode.Guide
{
    public sealed class GuideSequenceTests
    {
        // W1 の準備中、カスタムを閉じた直後（味方は召喚できる、レバーも引ける）。
        private static GuideObservation Ready() => new GuideObservation
        {
            Preparing = true,
            Wave = 1,
            Deploy = DeployPhase.Idle,
            CanSummon = true,
            CanLever = true,
            UpgradeSlot = -1,
        };

        private static GuideSequence AfterDeploy(ref GuideObservation o)
        {
            var guide = new GuideSequence();
            guide.Update(o);
            o.FieldUnits = 1;
            guide.Update(o);
            return guide;
        }

        private static GuideSequence AfterSlot(ref GuideObservation o)
        {
            var guide = AfterDeploy(ref o);
            o.SlotSpinning = true;
            o.SpinCount = 1;
            guide.Update(o);
            o.SlotSpinning = false;
            guide.Update(o);
            return guide;
        }

        [Test]
        public void Reel_Custom_Is_Shown_While_Customizing_And_Done_When_Closed()
        {
            var guide = new GuideSequence();
            var o = Ready();
            o.Customizing = true;

            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.ReelCustom));

            o.Customizing = false;
            guide.Update(o);
            Assert.That(guide.IsDone(GuideTopic.ReelCustom), Is.True);

            o.Customizing = true;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None), "2回目のカスタムでは出さない");
        }

        [Test]
        public void Deploy_Follows_Summon_Place_Facing_Until_A_Unit_Is_Placed()
        {
            var guide = new GuideSequence();
            var o = Ready();

            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Summon));
            o.Deploy = DeployPhase.ChoosingCell;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Place));
            o.Deploy = DeployPhase.ChoosingFacing;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Facing));
            o.Deploy = DeployPhase.Idle;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Summon), "置かずに戻ったら召喚から");

            o.FieldUnits = 1;
            guide.Update(o);
            Assert.That(guide.IsDone(GuideTopic.Deploy), Is.True);
        }

        [Test]
        public void Slot_Comes_After_First_Placement_And_Ends_When_The_Reels_Stop()
        {
            var o = Ready();
            var guide = AfterDeploy(ref o);

            Assert.That(guide.Current, Is.EqualTo(GuideHint.Lever));
            o.SlotSpinning = true;
            o.SpinCount = 1;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Stop));
            o.SlotSpinning = false;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None));
            Assert.That(guide.IsDone(GuideTopic.Slot), Is.True);
        }

        [Test]
        public void Slot_Waits_While_Placing_Another_Unit()
        {
            var o = Ready();
            var guide = AfterDeploy(ref o);
            Assert.That(guide.Current, Is.EqualTo(GuideHint.Lever));

            // レバーを引く前に次の味方を選んでも、スロットの説明は残す。
            o.Deploy = DeployPhase.ChoosingCell;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Lever));
        }

        [Test]
        public void Bonus_Is_Shown_When_A_Bonus_Starts_And_Done_At_The_Next_Lever()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            o.Preparing = false;

            o.BonusActive = true;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Bonus));
            Assert.That(guide.HoldsBattle, Is.True);

            o.SlotSpinning = true;
            o.SpinCount = 2;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None));
            Assert.That(guide.IsDone(GuideTopic.Bonus), Is.True);
            Assert.That(guide.HoldsBattle, Is.False);
        }

        [Test]
        public void Upgrade_Targets_The_Affordable_Slot_And_Is_Done_When_Upgraded()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            o.Preparing = false;

            o.UpgradeSlot = 2;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Upgrade));
            Assert.That(guide.TargetSlot, Is.EqualTo(2));

            o.UpgradeLevels = 1;
            o.UpgradeSlot = -1;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None));
            Assert.That(guide.IsDone(GuideTopic.Upgrade), Is.True);
        }

        [Test]
        public void Upgrade_Is_Withdrawn_When_It_Is_No_Longer_Affordable()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            o.UpgradeSlot = 0;
            guide.Update(o);

            o.UpgradeSlot = -1;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None));
            Assert.That(guide.IsDone(GuideTopic.Upgrade), Is.False, "また強化できるようになったら出す");

            o.UpgradeSlot = 0;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Upgrade));
        }

        [Test]
        public void Retreat_Is_Shown_In_The_Second_Wave_Preparation()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None), "W1 では出さない");

            o.Wave = 2;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.SelectUnit));
            o.UnitSelected = true;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Retreat));
            Assert.That(guide.ShowsRetreat, Is.True, "D-56: 撤退の題目を表示中");

            o.FieldUnits = 0;
            o.UnitSelected = false;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None));
            Assert.That(guide.IsDone(GuideTopic.Retreat), Is.True);
            Assert.That(guide.ShowsRetreat, Is.False, "題目の後は通常の返還率");
        }

        [Test]
        public void Retreat_Is_Done_When_The_Wave_Is_Started_Instead()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            o.Wave = 2;
            guide.Update(o);

            o.Preparing = false;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None));
            Assert.That(guide.IsDone(GuideTopic.Retreat), Is.True);
        }

        [Test]
        public void Pause_Hides_The_Hint_And_Resumes_It()
        {
            var guide = new GuideSequence();
            var o = Ready();
            guide.Update(o);

            o.Paused = true;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None));
            o.Paused = false;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Summon));
            Assert.That(guide.IsDone(GuideTopic.Deploy), Is.False);
        }

        [Test]
        public void Customizing_Suspends_Other_Hints()
        {
            var o = Ready();
            var guide = AfterDeploy(ref o);
            Assert.That(guide.Current, Is.EqualTo(GuideHint.Lever));

            o.Customizing = true;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.ReelCustom), "カスタムの説明がまだなら先に出す");
            o.Customizing = false;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Lever));
        }

        [Test]
        public void Deploy_Is_Withdrawn_When_No_Unit_Can_Be_Summoned()
        {
            var guide = new GuideSequence();
            var o = Ready();
            o.CanSummon = false;

            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None));
            Assert.That(guide.HoldsBattle, Is.False, "できない操作を待って戦闘を止めたままにしない");
        }

        [Test]
        public void Finished_Game_Shows_Nothing()
        {
            var guide = new GuideSequence();
            var o = Ready();
            o.Finished = true;

            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None));
        }
    }
}
