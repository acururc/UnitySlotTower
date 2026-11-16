using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Run
{
    /// <summary>ルートのマス1つ。</summary>
    public sealed class RouteNode
    {
        private readonly List<RouteNode> _next = new List<RouteNode>();

        internal RouteNode(int floor, int column)
        {
            Floor = floor;
            Column = column;
        }

        /// <summary>1 から。</summary>
        public int Floor { get; }

        /// <summary>0 から。</summary>
        public int Column { get; }

        /// <summary><see cref="RouteNodeKinds"/> の id。</summary>
        public string Kind { get; internal set; }

        /// <summary>つながっている1つ上の階のマス（列の順）。</summary>
        public IReadOnlyList<RouteNode> Next => _next;

        /// <summary>D-75: 行き先を選ぶリールの番号（0=左・1=中・2=右）。列の差の符号で決める。</summary>
        public int ReelOf(RouteNode next)
        {
            if (next == null)
            {
                throw new ArgumentNullException(nameof(next));
            }

            return Math.Sign(next.Column - Column) + 1;
        }

        /// <summary>D-75: そのリールで選べる行き先。なければ null。</summary>
        public RouteNode NextOnReel(int reel)
        {
            foreach (var next in _next)
            {
                if (ReelOf(next) == reel)
                {
                    return next;
                }
            }

            return null;
        }

        internal void Link(RouteNode next)
        {
            if (!_next.Contains(next))
            {
                _next.Add(next);
                _next.Sort((a, b) => a.Column.CompareTo(b.Column));
            }
        }
    }

    /// <summary>1回のゲームの分岐ルート（FR-010、plan 16.7）。下の階から上へ進み、最上階のボスで終わる。</summary>
    public sealed class RouteMap
    {
        private readonly List<RouteNode>[] _floors;
        private readonly List<RouteNode> _all = new List<RouteNode>();

        internal RouteMap(int floors, int columns)
        {
            Floors = floors;
            Columns = columns;
            _floors = new List<RouteNode>[floors];
            for (var i = 0; i < floors; i++)
            {
                _floors[i] = new List<RouteNode>();
            }
        }

        public int Floors { get; }
        public int Columns { get; }

        /// <summary>1階の最初の戦闘。</summary>
        public RouteNode Start => _floors[0][0];

        /// <summary>最上階のボス。</summary>
        public RouteNode Boss => _floors[Floors - 1][0];

        /// <summary>下の階から、各階は列の順。</summary>
        public IReadOnlyList<RouteNode> All => _all;

        /// <summary>その階のマス（列の順）。</summary>
        public IReadOnlyList<RouteNode> NodesOn(int floor) => _floors[floor - 1];

        /// <summary>from からつながった1つ上の階のマスか。</summary>
        public bool CanMove(RouteNode from, RouteNode to)
        {
            if (from == null || to == null || to.Floor != from.Floor + 1)
            {
                return false;
            }

            foreach (var next in from.Next)
            {
                if (next == to)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>その階・列のマス。なければ null。</summary>
        internal RouteNode Find(int floor, int column)
        {
            foreach (var node in _floors[floor - 1])
            {
                if (node.Column == column)
                {
                    return node;
                }
            }

            return null;
        }

        /// <summary>その階・列のマス。なければ作る（生成用）。</summary>
        internal RouteNode GetOrAdd(int floor, int column)
        {
            var found = Find(floor, column);
            if (found != null)
            {
                return found;
            }

            var nodes = _floors[floor - 1];
            var added = new RouteNode(floor, column);
            nodes.Add(added);
            nodes.Sort((a, b) => a.Column.CompareTo(b.Column));
            return added;
        }

        /// <summary>生成の終わりに <see cref="All"/> を並べ直す。</summary>
        internal void Seal()
        {
            _all.Clear();
            foreach (var nodes in _floors)
            {
                _all.AddRange(nodes);
            }
        }
    }

    /// <summary>
    /// ルートの生成（plan 16.7、17.7、D-88）。3列の格子: 1階は中央の最初の戦闘、2階からボスの直前の階までは各階3マス、
    /// ボスの直前の階は全部回復、最上階は中央のボス。各マスは真上と斜め上（列の差 −1〜+1）のすべてにつながる。
    /// 間の階のマスの種類は重みで選ぶ。同じ乱数の列で同じルートになる（AC-010）。
    /// ショップ・回復（戦闘でないマス）はつながった2マスで続かない（回復の階の直前の階は戦闘だけ）。
    /// </summary>
    public static class RouteGenerator
    {
        public static RouteMap Generate(RouteRules rules, IRandom random)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var floors = rules.Floors;
            var columns = RouteRules.GridColumns;
            var center = columns / 2;
            var restFloor = floors - 1;
            var map = new RouteMap(floors, columns);
            var start = map.GetOrAdd(1, center);
            for (var floor = 2; floor < floors; floor++)
            {
                for (var column = 0; column < columns; column++)
                {
                    map.GetOrAdd(floor, column);
                }
            }

            var boss = map.GetOrAdd(floors, center);
            for (var floor = 1; floor < restFloor; floor++)
            {
                foreach (var node in map.NodesOn(floor))
                {
                    foreach (var next in map.NodesOn(floor + 1))
                    {
                        if (Math.Abs(next.Column - node.Column) <= 1)
                        {
                            node.Link(next);
                        }
                    }
                }
            }

            foreach (var rest in map.NodesOn(restFloor))
            {
                rest.Link(boss);
            }

            start.Kind = RouteNodeKinds.First;
            boss.Kind = RouteNodeKinds.Boss;
            for (var floor = 2; floor < restFloor; floor++)
            {
                foreach (var node in map.NodesOn(floor))
                {
                    var allowNonBattle = floor < restFloor - 1 && !HasNonBattleParent(map, node);
                    node.Kind = PickKind(rules, floor, allowNonBattle, random);
                }
            }

            foreach (var rest in map.NodesOn(restFloor))
            {
                rest.Kind = RouteNodeKinds.Rest;
            }

            map.Seal();
            return map;
        }

        private static bool HasNonBattleParent(RouteMap map, RouteNode node)
        {
            foreach (var parent in map.NodesOn(node.Floor - 1))
            {
                if (map.CanMove(parent, node) && !RouteNodeKinds.Get(parent.Kind).IsBattle)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>重みで種類を選ぶ。選べる種類の重みがすべて0なら通常の戦闘。</summary>
        private static string PickKind(RouteRules rules, int floor, bool allowNonBattle, IRandom random)
        {
            var total = 0;
            foreach (var kind in RouteNodeKinds.All)
            {
                total += Allowed(kind, rules, floor, allowNonBattle) ? rules.WeightOf(kind.Id) : 0;
            }

            if (total == 0)
            {
                return RouteNodeKinds.Battle;
            }

            var roll = random.NextInt(0, total);
            foreach (var kind in RouteNodeKinds.All)
            {
                if (!Allowed(kind, rules, floor, allowNonBattle))
                {
                    continue;
                }

                roll -= rules.WeightOf(kind.Id);
                if (roll < 0)
                {
                    return kind.Id;
                }
            }

            return RouteNodeKinds.Battle;
        }

        private static bool Allowed(RouteNodeKind kind, RouteRules rules, int floor, bool allowNonBattle) =>
            kind.IsWeighted &&
            (kind.IsBattle || allowNonBattle) &&
            (kind.Id != RouteNodeKinds.Elite || floor >= rules.EliteMinFloor);
    }
}
