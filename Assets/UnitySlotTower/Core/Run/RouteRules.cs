using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Run
{
    /// <summary>route_rules.csv の値（plan 16.7）: ルートの大きさ・マスの種類の重み・回復量・ショップの品数・両替レート。</summary>
    public sealed class RouteRules
    {
        public const string FileName = "route_rules.csv";

        /// <summary>最も少ない階の数（最初の戦闘・回復・ボス）。</summary>
        public const int MinFloors = 3;

        private readonly Dictionary<string, int> _weights = new Dictionary<string, int>();

        private RouteRules()
        {
        }

        /// <summary>階の数（1階 = 最初の戦闘、最上階 = ボス、その直前 = 回復）。3以上。</summary>
        public int Floors { get; private set; }

        /// <summary>列の数。</summary>
        public int Columns { get; private set; }

        /// <summary>1階から上へ引く道の本数。</summary>
        public int Paths { get; private set; }

        /// <summary>強敵が出る最低の階。</summary>
        public int EliteMinFloor { get; private set; }

        /// <summary>回復マスで回復するライフ。</summary>
        public int RestHeal { get; private set; }

        /// <summary>ショップに並ぶ品の数。</summary>
        public int ShopSlots { get; private set; }

        /// <summary>コイン1枚あたりの所持金（両替所）。</summary>
        public int ExchangeMoneyPerCoin { get; private set; }

        /// <summary>重みで選ばれる種類（<see cref="RouteNodeKind.IsWeighted"/>）の重み。</summary>
        public int WeightOf(string kindId) =>
            _weights.TryGetValue(kindId, out var weight)
                ? weight
                : throw new KeyNotFoundException($"マスの種類 '{kindId}' は重みで選ばれない");

        /// <summary>
        /// 登録表の重みで選ばれる種類ごとに <c>weight_&lt;id&gt;</c> が必要。登録にない <c>weight_</c> のキー・
        /// 3階未満・列や道が0以下・負の値は <see cref="CsvFormatException"/>。
        /// </summary>
        public static RouteRules Load(CsvTable table)
        {
            var values = new KeyValueTable(table);
            var rules = new RouteRules
            {
                Floors = AtLeast(values, "floors", MinFloors),
                Columns = AtLeast(values, "columns", 1),
                Paths = AtLeast(values, "paths", 1),
                EliteMinFloor = AtLeast(values, "elite_min_floor", 1),
                RestHeal = AtLeast(values, "rest_heal", 0),
                ShopSlots = AtLeast(values, "shop_slots", 0),
                ExchangeMoneyPerCoin = AtLeast(values, "exchange_money_per_coin", 1),
            };

            foreach (var kind in RouteNodeKinds.All)
            {
                if (kind.IsWeighted)
                {
                    rules._weights.Add(kind.Id, AtLeast(values, kind.WeightKey, 0));
                }
            }

            foreach (var row in table.Rows)
            {
                var key = row.Get(KeyValueTable.KeyColumn);
                if (key.StartsWith(RouteNodeKinds.WeightPrefix, System.StringComparison.Ordinal) &&
                    !rules._weights.ContainsKey(key.Substring(RouteNodeKinds.WeightPrefix.Length)))
                {
                    throw row.Error(KeyValueTable.KeyColumn, $"'{key}' の種類は重みで選ばれる種類として登録されていない");
                }
            }

            return rules;
        }

        private static int AtLeast(KeyValueTable values, string key, int min)
        {
            var row = values.Row(key);
            var value = row.GetInt(KeyValueTable.ValueColumn);
            return value >= min ? value : throw row.Error(KeyValueTable.ValueColumn, $"{key} は {min} 以上: {value}");
        }
    }
}
