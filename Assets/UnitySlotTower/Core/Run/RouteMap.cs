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

        /// <summary>T358: ルート画面での表示位置のずれ（列の幅を1とする横方向。マスの区画の中に収まる）。1階とボスは0。</summary>
        public double DisplayOffsetX { get; internal set; }

        /// <summary>T358: ルート画面での表示位置のずれ（階の高さを1とする縦方向、上が正）。1階とボスは0。</summary>
        public double DisplayOffsetY { get; internal set; }

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
    /// ルートの生成（plan 16.7）。1階は最初の戦闘の1マス、最上階はボスの1マス、その直前の階は全部回復。
    /// 間の階は道を1階から列を ±1 ずつ動かしながら上へ引き（交差しない）、通ったマスの種類を重みで選ぶ。
    /// 同じ乱数の列で同じルートになる（AC-010）。
    /// ショップ・回復（戦闘でないマス）はつながった2マスで続かない（回復の階の直前の階は戦闘だけ）。
    /// 間の階のマスには表示位置のずれ（区画の中）を付ける（T358）。
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
            var columns = rules.Columns;
            var restFloor = floors - 1;
            var map = new RouteMap(floors, columns);
            var start = map.GetOrAdd(1, columns / 2);

            // D-75: 2階は1階の列 ±1 から始め、行き先を左・中・右のリールに1つずつ載せる。
            var minColumn = Math.Max(0, start.Column - 1);
            var maxColumn = Math.Min(columns - 1, start.Column + 1);
            var firstColumn = -1;
            for (var path = 0; path < rules.Paths; path++)
            {
                var column = random.NextInt(minColumn, maxColumn + 1);
                // 2本目は1本目と別の列から始め、2階で選べるようにする。
                while (path == 1 && maxColumn > minColumn && column == firstColumn)
                {
                    column = random.NextInt(minColumn, maxColumn + 1);
                }

                if (path == 0)
                {
                    firstColumn = column;
                }

                var node = map.GetOrAdd(2, column);
                start.Link(node);
                for (var floor = 2; floor < restFloor; floor++)
                {
                    column = NextColumn(map, floor, column, columns, random);
                    var next = map.GetOrAdd(floor + 1, column);
                    node.Link(next);
                    node = next;
                }
            }

            var boss = map.GetOrAdd(floors, columns / 2);
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

            // T358: 格子に並んで一直線に見えないよう、間の階のマスの表示位置をずらす（構造を決めた後に引くので道は変わらない）。
            for (var floor = 2; floor < floors; floor++)
            {
                foreach (var node in map.NodesOn(floor))
                {
                    node.DisplayOffsetX = (random.NextDouble() * 2 - 1) * MaxDisplayOffsetX;
                    node.DisplayOffsetY = (random.NextDouble() * 2 - 1) * MaxDisplayOffsetY;
                }
            }

            map.Seal();
            return map;
        }

        /// <summary>表示のずれの最大（横は列の幅、縦は階の高さに対する割合。T358）。横は隣どうしの差が半列未満になる大きさにして、まっすぐと斜めを見分けられるようにする。</summary>
        public const double MaxDisplayOffsetX = 0.2;
        public const double MaxDisplayOffsetY = 0.2;

        /// <summary>
        /// 道の次の列。左・まっすぐ・右から、範囲内で既存の斜めのつながりと交差しないものを選ぶ。
        /// T358: 横に広がるよう、斜めは重み2・まっすぐは重み1。
        /// </summary>
        private static int NextColumn(RouteMap map, int floor, int column, int columns, IRandom random)
        {
            var candidates = new int[3];
            var weights = new int[3];
            var count = 0;
            var total = 0;
            for (var delta = -1; delta <= 1; delta++)
            {
                var target = column + delta;
                if (target < 0 || target >= columns)
                {
                    continue;
                }

                // (floor, column) → (floor+1, column+d) は (floor, column+d) → (floor+1, column) と交差する。
                var neighbor = delta == 0 ? null : map.Find(floor, target);
                var below = map.Find(floor + 1, column);
                if (neighbor != null && below != null && map.CanMove(neighbor, below))
                {
                    continue;
                }

                weights[count] = delta == 0 ? 1 : 2;
                total += weights[count];
                candidates[count++] = target;
            }

            var roll = random.NextInt(0, total);
            for (var i = 0; i < count - 1; i++)
            {
                roll -= weights[i];
                if (roll < 0)
                {
                    return candidates[i];
                }
            }

            return candidates[count - 1];
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
