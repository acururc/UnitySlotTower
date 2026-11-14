using System.Collections.Generic;

namespace UnitySlotTower.Core.Run
{
    /// <summary>ルートのマスの種類1つ（plan 16.7）。</summary>
    public sealed class RouteNodeKind
    {
        internal RouteNodeKind(string id, EncounterKind? encounter, bool isWeighted)
        {
            Id = id;
            Encounter = encounter;
            IsWeighted = isWeighted;
        }

        /// <summary>マスの種類の id（route_rules.csv の <c>weight_&lt;id&gt;</c>、Runtime の画面の登録表のキー）。</summary>
        public string Id { get; }

        /// <summary>戦闘マスなら出現表の種類、それ以外（ショップ・回復）は null。</summary>
        public EncounterKind? Encounter { get; }

        /// <summary>戦闘マス（最初の戦闘・通常・強敵・ボス）。</summary>
        public bool IsBattle => Encounter.HasValue;

        /// <summary>間の階で重みで選ばれる種類（最初の戦闘・ボスは決まった階にだけ置く）。</summary>
        public bool IsWeighted { get; }

        /// <summary>route_rules.csv の重みのキー。</summary>
        public string WeightKey => RouteNodeKinds.WeightPrefix + Id;
    }

    /// <summary>
    /// マスの種類の登録表（D-69、plan 16.7）。種類を増やすときはここに1行足し、route_rules.csv に重みを、
    /// Runtime の画面の登録表に画面を足す。検証器は route_rules.csv の重みのキーとこの表の食い違いをエラーにする。
    /// </summary>
    public static class RouteNodeKinds
    {
        public const string WeightPrefix = "weight_";

        public const string First = "first";
        public const string Battle = "battle";
        public const string Elite = "elite";
        public const string Shop = "shop";
        public const string Rest = "rest";
        public const string Boss = "boss";

        private static readonly RouteNodeKind[] Kinds =
        {
            new RouteNodeKind(First, EncounterKind.First, false),
            new RouteNodeKind(Battle, EncounterKind.Battle, true),
            new RouteNodeKind(Elite, EncounterKind.Elite, true),
            new RouteNodeKind(Shop, null, true),
            new RouteNodeKind(Rest, null, true),
            new RouteNodeKind(Boss, EncounterKind.Boss, false),
        };

        /// <summary>登録順。</summary>
        public static IReadOnlyList<RouteNodeKind> All => Kinds;

        public static bool Contains(string id) => Find(id) != null;

        public static RouteNodeKind Get(string id) =>
            Find(id) ?? throw new KeyNotFoundException($"マスの種類 '{id}' は登録されていない");

        private static RouteNodeKind Find(string id)
        {
            foreach (var kind in Kinds)
            {
                if (kind.Id == id)
                {
                    return kind;
                }
            }

            return null;
        }
    }
}
