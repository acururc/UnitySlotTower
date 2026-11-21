using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Core.Run
{
    /// <summary>ショップの品の種類: 無料ストック1体・スキルゲージ1段・強化1段。</summary>
    public enum ShopItemKind
    {
        Gauge,
        Upgrade,
    }

    /// <summary>shop_items.csv の1行。</summary>
    public sealed class ShopItem
    {
        internal ShopItem(string id, ShopItemKind kind, string unitId, int price, int weight)
        {
            Id = id;
            Kind = kind;
            UnitId = unitId;
            Price = price;
            Weight = weight;
        }

        public string Id { get; }
        public ShopItemKind Kind { get; }

        /// <summary>対象の職（ゲージはその職のスキルの色）。</summary>
        public string UnitId { get; }

        /// <summary>値段（所持金）。強化の品では使わず、その職の次の強化費用になる。</summary>
        public int Price { get; }

        /// <summary>並ぶ品を選ぶときの重み。0 なら並ばない。</summary>
        public int Weight { get; }
    }

    /// <summary>ショップの品の表（FR-017、plan 16.2）。</summary>
    public sealed class ShopTable
    {
        public const string FileName = "shop_items.csv";

        /// <summary>kind 列の値（<see cref="ShopItemKind"/> の順）。</summary>
        public static readonly string[] KindIds = { "gauge", "upgrade" };

        private readonly List<ShopItem> _items = new List<ShopItem>();

        private ShopTable()
        {
        }

        /// <summary>行の順。</summary>
        public IReadOnlyList<ShopItem> All => _items;

        /// <summary>id の重複・未知の種類・負の値段や重みは <see cref="CsvFormatException"/>。</summary>
        public static ShopTable Load(CsvTable table)
        {
            var result = new ShopTable();
            var ids = new HashSet<string>();
            foreach (var row in table.Rows)
            {
                var id = row.Get("id");
                if (!ids.Add(id))
                {
                    throw row.Error("id", $"id '{id}' が重複している");
                }

                var kindIndex = Array.IndexOf(KindIds, row.Get("kind"));
                if (kindIndex < 0)
                {
                    throw row.Error("kind", $"種類は {string.Join("/", KindIds)} のどれか: '{row.Get("kind")}'");
                }

                result._items.Add(new ShopItem(id, (ShopItemKind)kindIndex, row.Get("unit_id"),
                    NonNegative(row, "price"), NonNegative(row, "weight")));
            }

            return result;
        }

        private static int NonNegative(CsvRow row, string column)
        {
            var value = row.GetInt(column);
            return value >= 0 ? value : throw row.Error(column, $"負の値: {value}");
        }
    }

    public enum ShopResult
    {
        Bought,

        /// <summary>所持金が足りない。</summary>
        NotEnoughMoney,

        /// <summary>この品はもう買った。</summary>
        SoldOut,

        /// <summary>強化が最大段階・ゲージが満タンで買えない。</summary>
        Unavailable,
    }

    /// <summary>
    /// ショップマス1回分（FR-017、plan 16.2）。入ったときに重みで品を重複なしに選び、各品は1回だけ買える。
    /// 拒否したときは何も変えない（AC-013）。
    /// </summary>
    public sealed class Shop
    {
        private readonly RunState _run;
        private readonly SkillTable _skills;
        private readonly List<ShopItem> _offers = new List<ShopItem>();
        private readonly List<bool> _sold = new List<bool>();

        /// <param name="slots">並ぶ品の数（route_rules.csv の <c>shop_slots</c>）。重みが正の品がそれより少なければ全部。</param>
        public Shop(ShopTable table, int slots, RunState run, SkillTable skills, IRandom random)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _skills = skills ?? throw new ArgumentNullException(nameof(skills));
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (slots < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slots), slots, "品の数が負");
            }

            var pool = new List<ShopItem>();
            var total = 0;
            foreach (var item in table.All)
            {
                if (item.Weight > 0)
                {
                    pool.Add(item);
                    total += item.Weight;
                }
            }

            while (_offers.Count < slots && pool.Count > 0)
            {
                var roll = random.NextInt(0, total);
                var picked = 0;
                while (roll >= pool[picked].Weight)
                {
                    roll -= pool[picked].Weight;
                    picked++;
                }

                _offers.Add(pool[picked]);
                _sold.Add(false);
                total -= pool[picked].Weight;
                pool.RemoveAt(picked);
            }
        }

        /// <summary>並んだ品（選んだ順）。</summary>
        public IReadOnlyList<ShopItem> Offers => _offers;

        public bool IsSold(int index) => _sold[index];

        /// <summary>いまの値段。強化の品はその職の次の強化費用（最大段階なら 0）。</summary>
        public int PriceOf(int index)
        {
            var item = _offers[index];
            return item.Kind == ShopItemKind.Upgrade ? _run.Upgrades.NextCost(item.UnitId) ?? 0 : item.Price;
        }

        /// <summary>いま買えるかだけを調べる（何も変えない）。</summary>
        public ShopResult Check(int index)
        {
            if (_sold[index])
            {
                return ShopResult.SoldOut;
            }

            var item = _offers[index];
            switch (item.Kind)
            {
                case ShopItemKind.Upgrade when _run.Upgrades.NextCost(item.UnitId) == null:
                case ShopItemKind.Gauge when _run.Gauges.IsFull(ColorOf(item)):
                    return ShopResult.Unavailable;
            }

            return _run.Money.Coins < PriceOf(index) ? ShopResult.NotEnoughMoney : ShopResult.Bought;
        }

        /// <summary>買えれば所持金を払って品を渡す。拒否なら何も変えない。</summary>
        public ShopResult TryBuy(int index)
        {
            var result = Check(index);
            if (result != ShopResult.Bought)
            {
                return result;
            }

            var item = _offers[index];
            switch (item.Kind)
            {
                case ShopItemKind.Gauge:
                    _run.Money.TrySpend(item.Price);
                    _run.Gauges.Add(ColorOf(item));
                    break;
                case ShopItemKind.Upgrade:
                    _run.Upgrades.TryUpgrade(item.UnitId);
                    break;
            }

            _sold[index] = true;
            return result;
        }

        private SlotSymbol ColorOf(ShopItem item) =>
            _skills.TryGetColor(item.UnitId, out var color)
                ? color
                : throw new InvalidOperationException($"ゲージの品 '{item.Id}' の職 '{item.UnitId}' にスキルがない");
    }
}
