using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Guide;

namespace UnitySlotTower.Tests.EditMode.Guide
{
    public sealed class GuideSequenceTests
    {
        // 最初の戦闘マスで開始の演出が終わった後（味方は召喚できる、レバーも引ける）。
        private static GuideObservation Ready() => new GuideObservation
        {
            BattleNode = 1,
            Deploy = DeployPhase.Idle,
            CanSummon = true,
            CanLever = true,
            SkillSlot = -1,
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
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Spinning));
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
        public void Skill_Is_Shown_When_A_Gauge_Becomes_Usable_And_Done_When_Activated()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None), "満タンになるまでは出さない");

            o.SkillSlot = 1;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Skill));
            Assert.That((guide.TargetSlot, guide.HoldsBattle), Is.EqualTo((1, true)), "その職のゲージを指して戦闘を止める");

            o.SkillActivations = 1;
            o.SkillSlot = -1;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None));
            Assert.That(guide.IsDone(GuideTopic.Skill), Is.True);

            o.SkillSlot = 2;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None), "2回目の満タンでは出さない");
        }

        [Test]
        public void Skill_Is_Withdrawn_When_It_Can_No_Longer_Be_Activated()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            o.SkillSlot = 0;
            guide.Update(o);

            // その職がやられて配置0人になったなど。
            o.SkillSlot = -1;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None));
            Assert.That(guide.IsDone(GuideTopic.Skill), Is.False);

            o.SkillSlot = 0;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Skill));
        }

        [Test]
        public void Skill_Waits_Until_The_Reels_Stop()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            o.SlotSpinning = true;
            o.SpinCount = 2;
            o.SkillSlot = 0;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None), "回っている途中では出さない");

            o.SlotSpinning = false;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Skill));
        }

        [Test]
        [Description("FR-004, D-91")]
        public void Route_Shows_The_Node_Contents_Until_Tapped_Then_Points_At_The_Reels()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            o.ChoosingRoute = true;

            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.RouteNodes), "まずマスの中身の絵");
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.RouteNodes), "押すまで残る");
            o.Tapped = true;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.RoutePick), "押したら行き先のリール");
            o.Tapped = false;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.RoutePick));

            o.ChoosingRoute = false;
            guide.Update(o);
            Assert.That(guide.IsDone(GuideTopic.Route), Is.True);

            o.ChoosingRoute = true;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None), "2回目のルートでは出さない");
        }

        [Test]
        public void Route_Choice_Suspends_Battle_Hints()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            o.UpgradeSlot = 0;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Upgrade));

            o.ChoosingRoute = true;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.RouteNodes), "ルートの説明に切り替える");
            Assert.That(guide.IsDone(GuideTopic.Upgrade), Is.False);
        }

        [Test]
        public void Node_Screen_Shows_No_Hints_But_Completes_The_Route()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            o.ChoosingRoute = true;
            guide.Update(o);

            // 行き先を決めたフレームでショップの画面が開いた（画面中は何も出さない）。
            o.ChoosingRoute = false;
            o.OnScreen = true;
            o.UpgradeSlot = 0;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None));
            Assert.That(guide.HoldsBattle, Is.False);
            Assert.That(guide.IsDone(GuideTopic.Route), Is.True, "行き先は決めたのでルートは済み");
            Assert.That(guide.IsDone(GuideTopic.Upgrade), Is.False, "画面中に強化の題目を始めない");
        }

        [Test]
        [Description("FR-004, D-91")]
        public void Block_Is_Shown_When_A_Ground_Enemy_Is_First_Stopped_And_Ends_On_Tap()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            Assert.That(guide.Current, Is.EqualTo(GuideHint.None));

            o.GroundBlocked = true;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Block));
            Assert.That(guide.HoldsBattle, Is.True, "見せているあいだ戦闘を止める");
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Block), "押すまで残る");

            o.Tapped = true;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None));
            o.Tapped = false;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None), "1回のプレイで一度だけ");
            Assert.That(guide.IsDone(GuideTopic.Block), Is.True);
        }

        [Test]
        public void Block_Waits_Until_The_Reels_Stop()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            o.GroundBlocked = true;
            o.SlotSpinning = true;
            o.SpinCount = 2;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None), "回っている途中では出さない");

            o.SlotSpinning = false;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Block));
        }

        [Test]
        public void Block_Waits_Until_The_First_Unit_Is_Placed()
        {
            var guide = new GuideSequence();
            var o = Ready();
            o.GroundBlocked = true;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Summon), "配置の説明が先");
        }

        [Test]
        public void Upgrade_Targets_The_Affordable_Slot_And_Is_Done_When_Upgraded()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);

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
        [Description("plan 17.2: 撤退の題目は2つ目の戦闘マスで味方が配置されていて操作がないときに出す")]
        public void Retreat_Is_Shown_In_The_Second_Battle_Node()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None), "最初の戦闘マスでは出さない");

            o.BattleNode = 2;
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
        [Description("plan 17.2: 撤退せずにレバーを引いたら撤退しないことを選んだとみなす（▶ の代わり）")]
        public void Retreat_Is_Done_When_The_Lever_Is_Pulled_Instead()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            o.BattleNode = 2;
            guide.Update(o);

            o.SpinCount++;
            o.SlotSpinning = true;
            guide.Update(o);

            Assert.That(guide.IsDone(GuideTopic.Retreat), Is.True);
        }

        [Test]
        [Description("plan 17.2: 撤退せずに次の味方の召喚を始めても撤退の題目は終わる")]
        public void Retreat_Is_Done_When_Another_Summon_Is_Started_Instead()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            o.BattleNode = 2;
            guide.Update(o);

            o.Deploy = DeployPhase.ChoosingCell;
            guide.Update(o);

            Assert.That(guide.IsDone(GuideTopic.Retreat), Is.True);
        }

        [Test]
        [Description("plan 17.2, AC-016: 開始の演出のあいだは説明を出さず、戦闘も止めない")]
        public void Intro_Shows_No_Hint()
        {
            var guide = new GuideSequence();
            var o = Ready();
            o.Intro = true;

            Assert.That((guide.Update(o), guide.HoldsBattle), Is.EqualTo((GuideHint.None, false)));
        }

        [Test]
        [Description("plan 17.2: 召喚の説明は最初の戦闘の開始の演出の後に出す")]
        public void Deploy_Is_Shown_When_The_Intro_Ends()
        {
            var guide = new GuideSequence();
            var o = Ready();
            o.Intro = true;
            guide.Update(o);

            o.Intro = false;

            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Summon));
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

        [Test]
        [Description("FR-083, AC-064: × で操作せずに題目を終え、次の題目へ進む。同じ題目は二度と出ない")]
        public void Dismiss_Ends_The_Topic_Without_The_Operation()
        {
            var guide = new GuideSequence();
            var o = Ready();
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Summon));

            o.Dismissed = true;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Lever), "召喚を閉じるとスロットへ");
            Assert.That(guide.IsDone(GuideTopic.Deploy), Is.True);

            o.Dismissed = false;
            o.Deploy = DeployPhase.ChoosingCell;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Lever), "閉じた召喚の説明（マス選び）は二度と出ない");
        }

        [Test]
        [Description("FR-083, AC-064: 撤退の題目は × で閉じると全額返還も終わる")]
        public void Dismissing_Retreat_Ends_Full_Refund()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            o.BattleNode = 2;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.SelectUnit));
            Assert.That(guide.ShowsRetreat, Is.True);

            o.Dismissed = true;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None));
            Assert.That((guide.ShowsRetreat, guide.IsDone(GuideTopic.Retreat)), Is.EqualTo((false, true)));
        }

        [Test]
        [Description("FR-083, AC-064: ルートは × で凡例を閉じると選び方へ、もう一度 × で題目を終える。凡例と選び方は同時に出ない")]
        public void Dismissing_Route_Legend_Moves_To_The_Pick_Then_Ends()
        {
            var guide = new GuideSequence();
            var o = Ready();
            o.ChoosingRoute = true;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.RouteNodes));

            o.Dismissed = true;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.RoutePick), "凡例を閉じると選び方");
            Assert.That(guide.IsDone(GuideTopic.Route), Is.False);
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.None), "選び方も閉じるとルートの題目は終わり");
            Assert.That(guide.IsDone(GuideTopic.Route), Is.True);
        }

        [Test]
        [Description("FR-083: 示した以外の操作でも進む（強化はどの種類でも、スキルはどの職でも）")]
        public void Upgrade_And_Skill_End_With_Any_Target()
        {
            var o = Ready();
            var guide = AfterSlot(ref o);
            o.UpgradeSlot = 0;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Upgrade));
            o.UpgradeLevels = 1; // 示した 0 番ではない種類を強化した
            o.UpgradeSlot = 0;
            guide.Update(o);
            Assert.That(guide.IsDone(GuideTopic.Upgrade), Is.True);

            o.UpgradeSlot = -1;
            o.SkillSlot = 2;
            Assert.That(guide.Update(o), Is.EqualTo(GuideHint.Skill));
            o.SkillActivations = 1; // 示した 2 番ではない職のスキル
            guide.Update(o);
            Assert.That(guide.IsDone(GuideTopic.Skill), Is.True);
        }
    }
}
