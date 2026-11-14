using System;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Tests.EditMode.Run
{
    [Description("plan 16.7 / FR-010, AC-012: ルートの現在地（RunState.Enter）と回復マス")]
    public sealed class RunRouteTests
    {
        private static (RunState run, RouteMap map) StartRun(ulong seed = 3)
        {
            var run = RunFixtures.CreateRun();
            var map = RouteGenerator.Generate(RunFixtures.LoadRules(), new SeededRandom(seed));
            run.BeginRoute(map);
            return (run, map);
        }

        [Test]
        public void New_Route_Starts_Outside_Any_Node()
        {
            var (run, _) = StartRun();

            Assert.That((run.Current, run.Floor), Is.EqualTo(((RouteNode)null, 0)));
        }

        [Test]
        public void First_Entry_Is_The_First_Battle()
        {
            var (run, map) = StartRun();

            Assert.That((run.Enter(map.Start), run.Current, run.Floor), Is.EqualTo((true, map.Start, 1)));
        }

        [Test]
        public void Entering_Another_Node_From_Outside_Is_Refused()
        {
            var (run, map) = StartRun();

            Assert.That((run.Enter(map.NodesOn(2)[0]), run.Current), Is.EqualTo((false, (RouteNode)null)));
        }

        [Test]
        [Description("plan 16.7: 選べるのは今のマスにつながった上の階のマス")]
        public void Enter_Moves_To_A_Linked_Node()
        {
            var (run, map) = StartRun();
            run.Enter(map.Start);
            var next = map.Start.Next[0];

            Assert.That((run.Enter(next), run.Current, run.Floor), Is.EqualTo((true, next, 2)));
        }

        [Test]
        public void Enter_Refuses_An_Unlinked_Node_And_Stays()
        {
            var (run, map) = StartRun();
            run.Enter(map.Start);
            var second = map.Start.Next[0];
            run.Enter(second);
            var unlinked = map.NodesOn(3).FirstOrDefault(n => !second.Next.Contains(n)) ?? map.Boss;

            Assert.That((run.CanEnter(unlinked), run.Enter(unlinked), run.Current), Is.EqualTo((false, false, second)));
        }

        [Test]
        public void Enter_Without_A_Route_Is_Refused()
        {
            var run = RunFixtures.CreateRun();
            var map = RouteGenerator.Generate(RunFixtures.LoadRules(), new SeededRandom(3));

            Assert.That(run.Enter(map.Start), Is.False);
        }

        [Test]
        [Description("AC-012: 回復マスでライフが回復する")]
        public void Heal_Restores_Life()
        {
            var run = RunFixtures.CreateRun(life: 10);
            run.LoseLife(5);

            Assert.That((run.Heal(3), run.Life), Is.EqualTo((3, 8)));
        }

        [Test]
        [Description("AC-012: 回復は最大値を超えない")]
        public void Heal_Stops_At_Max_Life()
        {
            var run = RunFixtures.CreateRun(life: 10);
            run.LoseLife(1);

            Assert.That((run.Heal(3), run.Life), Is.EqualTo((1, 10)));
        }

        [Test]
        public void Negative_Heal_Is_Rejected()
        {
            var run = RunFixtures.CreateRun();

            Assert.Throws<ArgumentOutOfRangeException>(() => run.Heal(-1));
        }
    }
}
