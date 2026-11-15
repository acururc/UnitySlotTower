using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Tests.EditMode.Run
{
    [Description("AC-010 / FR-010, plan 16.7: ルートの生成（多数シードで決まりを満たし、全マスからボスへ行ける）")]
    public sealed class RouteGeneratorTests
    {
        private const int SeedCount = 200;

        private static readonly (string, string)[][] Shapes =
        {
            new (string, string)[0],
            new[] { ("floors", "3") },
            new[] { ("columns", "1"), ("paths", "1") },
            new[] { ("floors", "15"), ("columns", "7"), ("paths", "6") },
        };

        /// <summary>各形 × シード 1〜<see cref="SeedCount"/> のルート。</summary>
        private static IEnumerable<(RouteRules rules, RouteMap map)> Routes()
        {
            foreach (var shape in Shapes)
            {
                var rules = RunFixtures.LoadRulesWith(shape);
                for (ulong seed = 1; seed <= SeedCount; seed++)
                {
                    yield return (rules, RouteGenerator.Generate(rules, new SeededRandom(seed)));
                }
            }
        }

        private static RouteMap Generate(ulong seed, params (string, string)[] changes) =>
            RouteGenerator.Generate(RunFixtures.LoadRulesWith(changes), new SeededRandom(seed));

        private static string Signature(RouteMap map)
        {
            var text = new StringBuilder();
            foreach (var node in map.All)
            {
                text.Append(node.Floor).Append(':').Append(node.Column).Append(':').Append(node.Kind).Append(':')
                    .Append(node.DisplayOffsetX.ToString("R")).Append('/')
                    .Append(node.DisplayOffsetY.ToString("R")).Append('>')
                    .Append(string.Join(",", node.Next.Select(n => n.Column))).Append(' ');
            }

            return text.ToString();
        }

        private static HashSet<RouteNode> ReachableFrom(RouteNode start)
        {
            var seen = new HashSet<RouteNode> { start };
            var queue = new Queue<RouteNode>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                foreach (var next in queue.Dequeue().Next)
                {
                    if (seen.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return seen;
        }

        private static bool IsNonBattle(RouteNode node) => !RouteNodeKinds.Get(node.Kind).IsBattle;

        [Test]
        [Description("AC-010: 同じシードで同じルート")]
        public void Same_Seed_Gives_The_Same_Route()
        {
            Assert.That(Signature(Generate(42)), Is.EqualTo(Signature(Generate(42))));
        }

        [Test]
        public void Different_Seeds_Give_Different_Routes()
        {
            var signatures = new HashSet<string>();
            for (ulong seed = 1; seed <= 20; seed++)
            {
                signatures.Add(Signature(Generate(seed)));
            }

            Assert.That(signatures.Count, Is.GreaterThan(1));
        }

        [Test]
        [Description("AC-010: 最初は戦闘（1階は最初の戦闘の1マス）")]
        public void First_Floor_Is_A_Single_First_Battle()
        {
            foreach (var (_, map) in Routes())
            {
                Assert.That(map.NodesOn(1).Select(n => n.Kind), Is.EqualTo(new[] { "first" }));
            }
        }

        [Test]
        [Description("AC-010: 最後はボス（最上階はボスの1マス）")]
        public void Top_Floor_Is_A_Single_Boss()
        {
            foreach (var (rules, map) in Routes())
            {
                Assert.That(map.Floors, Is.EqualTo(rules.Floors));
                Assert.That(map.NodesOn(rules.Floors).Select(n => n.Kind), Is.EqualTo(new[] { "boss" }));
            }
        }

        [Test]
        public void Floor_Before_The_Boss_Is_All_Rest()
        {
            foreach (var (rules, map) in Routes())
            {
                Assert.That(map.NodesOn(rules.Floors - 1).Select(n => n.Kind), Is.All.EqualTo("rest"));
            }
        }

        [Test]
        public void Middle_Floors_Use_Only_Weighted_Kinds()
        {
            var weighted = RouteNodeKinds.All.Where(k => k.IsWeighted).Select(k => k.Id).ToArray();
            foreach (var (rules, map) in Routes())
            {
                foreach (var node in map.All.Where(n => n.Floor > 1 && n.Floor < rules.Floors - 1))
                {
                    Assert.That(weighted, Does.Contain(node.Kind));
                }
            }
        }

        [Test]
        [Description("AC-010: どのマスからもボスまで行ける")]
        public void Every_Node_Reaches_The_Boss()
        {
            foreach (var (_, map) in Routes())
            {
                foreach (var node in map.All)
                {
                    Assert.That(ReachableFrom(node), Does.Contain(map.Boss), $"{node.Floor}階 {node.Column}列");
                }
            }
        }

        [Test]
        public void Every_Node_Is_Reachable_From_The_Start()
        {
            foreach (var (_, map) in Routes())
            {
                Assert.That(ReachableFrom(map.Start).Count, Is.EqualTo(map.All.Count));
            }
        }

        [Test]
        [Description("AC-010: 列数（すべてのマスが列の範囲内）")]
        public void Nodes_Stay_Within_The_Columns()
        {
            foreach (var (rules, map) in Routes())
            {
                Assert.That(map.Columns, Is.EqualTo(rules.Columns));
                Assert.That(map.All.Select(n => n.Column), Is.All.InRange(0, rules.Columns - 1));
            }
        }

        [Test]
        [Description("AC-010, D-75: つながりは1つ上の階、ボスへ以外は隣の列まで（1階からも）")]
        public void Links_Go_Up_One_Floor_And_At_Most_One_Column()
        {
            foreach (var (rules, map) in Routes())
            {
                foreach (var node in map.All)
                {
                    foreach (var next in node.Next)
                    {
                        Assert.That(next.Floor, Is.EqualTo(node.Floor + 1));
                        if (next.Floor < rules.Floors)
                        {
                            Assert.That(Math.Abs(next.Column - node.Column), Is.LessThanOrEqualTo(1));
                        }
                    }
                }
            }
        }

        [Test]
        [Description("plan 16.7: 道は交差しない")]
        public void Links_Do_Not_Cross()
        {
            foreach (var (_, map) in Routes())
            {
                var links = map.All.SelectMany(a => a.Next.Select(b => (a, b))).ToList();
                foreach (var (a1, b1) in links)
                {
                    foreach (var (a2, b2) in links)
                    {
                        if (a1.Floor == a2.Floor && a1.Column < a2.Column)
                        {
                            Assert.That(b1.Column, Is.LessThanOrEqualTo(b2.Column),
                                $"{a1.Floor}階 {a1.Column}→{b1.Column} と {a2.Column}→{b2.Column}");
                        }
                    }
                }
            }
        }

        [Test]
        [Description("plan 16.7: 強敵は elite_min_floor 未満に出ない")]
        public void No_Elite_Below_The_Minimum_Floor()
        {
            foreach (var (rules, map) in Routes())
            {
                Assert.That(map.All.Where(n => n.Kind == "elite").Select(n => n.Floor),
                    Is.All.GreaterThanOrEqualTo(rules.EliteMinFloor));
            }
        }

        [Test]
        [Description("plan 16.7: 同じ道でショップ・回復が続かない")]
        public void Non_Battle_Nodes_Are_Not_Consecutive()
        {
            foreach (var (_, map) in Routes())
            {
                foreach (var node in map.All.Where(IsNonBattle))
                {
                    Assert.That(node.Next.Where(IsNonBattle), Is.Empty, $"{node.Floor}階 {node.Column}列 {node.Kind}");
                }
            }
        }

        [Test]
        [Description("D-75: 行き先はリール（左・中・右）に1つずつ。どのマスでも同じリールに2つ載らない")]
        public void Each_Reel_Has_At_Most_One_Destination()
        {
            foreach (var (_, map) in Routes())
            {
                foreach (var node in map.All)
                {
                    var reels = node.Next.Select(node.ReelOf).ToList();
                    Assert.That(reels, Is.Unique.And.All.InRange(0, 2), $"{node.Floor}階 {node.Column}列");
                }
            }
        }

        [Test]
        [Description("D-75: 左のリールは左上、中は真上、右は右上の行き先")]
        public void Reel_Follows_The_Direction_Of_The_Destination()
        {
            foreach (var (_, map) in Routes())
            {
                foreach (var node in map.All.Where(n => n.Floor < map.Floors - 1))
                {
                    Assert.That(node.NextOnReel(0)?.Column, Is.Null.Or.EqualTo(node.Column - 1));
                    Assert.That(node.NextOnReel(1)?.Column, Is.Null.Or.EqualTo(node.Column));
                    Assert.That(node.NextOnReel(2)?.Column, Is.Null.Or.EqualTo(node.Column + 1));
                }
            }
        }

        [Test]
        [Description("D-75: どの行き先もどれかのリールで選べる")]
        public void Every_Destination_Is_On_Its_Reel()
        {
            foreach (var (_, map) in Routes())
            {
                foreach (var node in map.All)
                {
                    foreach (var next in node.Next)
                    {
                        Assert.That(node.NextOnReel(node.ReelOf(next)), Is.SameAs(next));
                    }
                }
            }
        }

        [Test]
        [Description("D-75: ボスは1つだけなので、回復の階のどの列からも列の向きのリールに載る（4列: 0列→右、2列→中、3列→左）")]
        public void Boss_Is_On_The_Reel_Toward_Its_Column()
        {
            var map = Generate(7, ("columns", "4"));
            var reels = map.NodesOn(map.Floors - 1).ToDictionary(n => n.Column, n => n.ReelOf(map.Boss));

            foreach (var (column, reel) in reels)
            {
                Assert.That(reel, Is.EqualTo(column < 2 ? 2 : column == 2 ? 1 : 0), $"{column}列");
            }
        }

        [Test]
        public void Reel_Without_A_Destination_Gives_Nothing()
        {
            var map = Generate(7);

            Assert.That(map.Boss.NextOnReel(1), Is.Null);
        }

        [Test]
        public void Second_Floor_Offers_A_Choice_When_There_Are_Several_Paths()
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                Assert.That(Generate(seed).NodesOn(2).Count, Is.GreaterThanOrEqualTo(2));
            }
        }

        [Test]
        [Description("T358: 既定の形では道が横に広がる（1階とボスを除く使う列の数が、平均5以上・どのシードも4以上）")]
        public void Paths_Spread_Sideways()
        {
            var rules = RunFixtures.LoadRules();
            var total = 0;
            var min = int.MaxValue;
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var used = Generate(seed).All.Where(n => n.Floor > 1 && n.Floor < rules.Floors)
                    .Select(n => n.Column).Distinct().Count();
                total += used;
                min = Math.Min(min, used);
            }

            var average = total / (double)SeedCount;
            Assert.That(average >= 5 && min >= 4, Is.True, $"平均 {average:F2}、最小 {min}");
        }

        [Test]
        [Description("T358: 間の階の道は斜めに進みやすい（ボスへ以外の2階より上のつながりで、まっすぐは3割未満）")]
        public void Middle_Links_Are_Mostly_Diagonal()
        {
            var straight = 0;
            var links = 0;
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var map = Generate(seed);
                foreach (var node in map.All.Where(n => n.Floor >= 2))
                {
                    foreach (var next in node.Next.Where(n => n.Floor < map.Floors))
                    {
                        links++;
                        straight += next.Column == node.Column ? 1 : 0;
                    }
                }
            }

            var ratio = straight / (double)links;
            Assert.That(ratio, Is.LessThan(0.3), $"まっすぐ {straight}/{links}");
        }

        [Test]
        [Description("T358: 表示のずれはマスの区画の中（縦横とも半マス未満）")]
        public void Display_Offsets_Stay_Inside_The_Cell()
        {
            foreach (var (_, map) in Routes())
            {
                foreach (var node in map.All)
                {
                    Assert.That((Math.Abs(node.DisplayOffsetX) < 0.5, Math.Abs(node.DisplayOffsetY) < 0.5),
                        Is.EqualTo((true, true)), $"{node.Floor}-{node.Column}");
                }
            }
        }

        [Test]
        [Description("T358: 1階とボスは区画の中心、間の階のマスは格子からずれる（横に0.1マス以上ずれるマスが3割以上）")]
        public void Middle_Nodes_Are_Offset_From_The_Grid()
        {
            var shifted = 0;
            var middle = 0;
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var map = Generate(seed);
                Assert.That((map.Start.DisplayOffsetX, map.Start.DisplayOffsetY, map.Boss.DisplayOffsetX,
                    map.Boss.DisplayOffsetY), Is.EqualTo((0.0, 0.0, 0.0, 0.0)));
                foreach (var node in map.All.Where(n => n.Floor > 1 && n.Floor < map.Floors))
                {
                    middle++;
                    shifted += Math.Abs(node.DisplayOffsetX) >= 0.1 ? 1 : 0;
                }
            }

            Assert.That(shifted / (double)middle, Is.GreaterThanOrEqualTo(0.3), $"{shifted}/{middle}");
        }

        [Test]
        [Description("T358, D-75: ずらしても向きが見分けられる（ボスへ以外のつながりの表示上の横の差は、まっすぐなら半列未満、斜めなら半列より大きい）")]
        public void Displayed_Direction_Matches_The_Reel()
        {
            foreach (var (_, map) in Routes())
            {
                foreach (var node in map.All)
                {
                    foreach (var next in node.Next.Where(n => n.Floor < map.Floors))
                    {
                        var dx = next.Column + next.DisplayOffsetX - (node.Column + node.DisplayOffsetX);
                        var ok = next.Column == node.Column ? Math.Abs(dx) < 0.5
                            : Math.Abs(dx) > 0.5 && Math.Sign(dx) == Math.Sign(next.Column - node.Column);
                        Assert.That(ok, Is.True, $"{node.Floor}-{node.Column} → {next.Column}: {dx:F2}");
                    }
                }
            }
        }

        [Test]
        public void Kind_With_Zero_Weight_Is_Never_Chosen()
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                Assert.That(Generate(seed, ("weight_shop", "0")).All.Select(n => n.Kind), Has.None.EqualTo("shop"));
            }
        }

        [Test]
        [Description("plan 16.7: 種類は重みで選ぶ（強敵 1 : 通常 3 → 強敵 25% ± 3%）")]
        public void Kinds_Are_Chosen_By_Weight()
        {
            var elites = 0;
            var total = 0;
            for (ulong seed = 1; seed <= 500; seed++)
            {
                var map = Generate(seed, ("weight_battle", "3"), ("weight_elite", "1"),
                    ("weight_shop", "0"), ("weight_rest", "0"), ("elite_min_floor", "1"));
                var middle = map.All.Where(n => n.Floor > 1 && n.Floor < map.Floors - 1).ToList();
                elites += middle.Count(n => n.Kind == "elite");
                total += middle.Count;
            }

            Assert.That((double)elites / total, Is.EqualTo(0.25).Within(0.03));
        }

        [Test]
        public void CanMove_To_A_Linked_Node_Of_The_Next_Floor()
        {
            var map = Generate(5);

            Assert.That(map.Start.Next.All(next => map.CanMove(map.Start, next)), Is.True);
        }

        [Test]
        public void CanMove_Rejects_An_Unlinked_Node_Of_The_Next_Floor()
        {
            for (ulong seed = 1; seed <= SeedCount; seed++)
            {
                var map = Generate(seed);
                foreach (var node in map.All)
                {
                    var unlinked = map.All.FirstOrDefault(n => n.Floor == node.Floor + 1 && !node.Next.Contains(n));
                    if (unlinked != null)
                    {
                        Assert.That(map.CanMove(node, unlinked), Is.False);
                        return;
                    }
                }
            }

            Assert.Fail("つながっていない上の階のマスが見つからなかった");
        }

        [Test]
        public void CanMove_Rejects_Going_Down_Or_Sideways()
        {
            var map = Generate(5);
            var second = map.NodesOn(2);

            Assert.That((map.CanMove(second[0], map.Start), map.CanMove(second[0], second[1])),
                Is.EqualTo((false, false)));
        }
    }
}
