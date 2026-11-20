using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Core.Run
{
    /// <summary>戦闘マスの種類（plan 16.6）。最初の戦闘・通常・強敵・ボス。</summary>
    public enum EncounterKind
    {
        First,
        Battle,
        Elite,
        Boss,
    }

    /// <summary>encounters.csv の1行: 戦闘マスのマップ・出現する階・回数固定・報酬。</summary>
    public sealed class Encounter
    {
        internal Encounter(string id, EncounterKind kind, string mapId, int minFloor, int maxFloor, int fixedSpins,
            int rewardMoney, int rewardCoins, int weight)
        {
            Id = id;
            Kind = kind;
            MapId = mapId;
            MinFloor = minFloor;
            MaxFloor = maxFloor;
            FixedSpins = fixedSpins;
            RewardMoney = rewardMoney;
            RewardCoins = rewardCoins;
            Weight = weight;
        }

        /// <summary>waves.csv の encounter_id。</summary>
        public string Id { get; }

        public EncounterKind Kind { get; }

        /// <summary>maps/&lt;id&gt;.csv の id。</summary>
        public string MapId { get; }

        public int MinFloor { get; }
        public int MaxFloor { get; }

        /// <summary>コインなしで回せる回数（D-68）。0 なら通常どおりコインを賭ける。</summary>
        public int FixedSpins { get; }

        /// <summary>クリア時に足す所持金。</summary>
        public int RewardMoney { get; }

        /// <summary>クリア時に足すコイン。</summary>
        public int RewardCoins { get; }

        public int Weight { get; }

        public bool IsOnFloor(int floor) => MinFloor <= floor && floor <= MaxFloor;
    }

    /// <summary>
    /// 戦闘マスの出現表（FR-018、plan 16.6）。戦闘マスに入ったとき、種類と階が合う行から重みに応じて1つ選ぶ。
    /// </summary>
    public sealed class EncounterTable
    {
        public const string FileName = "encounters.csv";

        /// <summary>kind 列の値（<see cref="EncounterKind"/> の順）。</summary>
        public static readonly string[] KindIds = { "first", "battle", "elite", "boss" };

        private readonly List<Encounter> _encounters = new List<Encounter>();
        private readonly Dictionary<string, Encounter> _byId = new Dictionary<string, Encounter>();
        private readonly List<Encounter> _matches = new List<Encounter>();

        private EncounterTable()
        {
        }

        /// <summary>行の順。</summary>
        public IReadOnlyList<Encounter> All => _encounters;

        public bool Contains(string id) => _byId.ContainsKey(id);

        public Encounter Get(string id) =>
            _byId.TryGetValue(id, out var encounter)
                ? encounter
                : throw new KeyNotFoundException($"戦闘マス '{id}' がない");

        /// <summary>
        /// 種類と階が合う行から重みに応じて1つ選ぶ。合う行がなければ <see cref="InvalidOperationException"/>。
        /// D-89: <paramref name="avoidMapId"/> のマップの行は、ほかのマップの行もあるときだけ除く。
        /// </summary>
        public Encounter Pick(EncounterKind kind, int floor, IRandom random, string avoidMapId = null)
        {
            _matches.Clear();
            var avoided = 0;
            foreach (var encounter in _encounters)
            {
                if (encounter.Kind == kind && encounter.IsOnFloor(floor))
                {
                    _matches.Add(encounter);
                    avoided += encounter.MapId == avoidMapId ? 1 : 0;
                }
            }

            if (_matches.Count == 0)
            {
                throw new InvalidOperationException($"{KindIds[(int)kind]} の {floor} 階に合う戦闘マスがない");
            }

            if (avoided > 0 && avoided < _matches.Count)
            {
                for (var i = _matches.Count - 1; i >= 0; i--)
                {
                    if (_matches[i].MapId == avoidMapId)
                    {
                        _matches.RemoveAt(i);
                    }
                }
            }

            var totalWeight = 0;
            foreach (var match in _matches)
            {
                totalWeight = checked(totalWeight + match.Weight);
            }

            var ticket = random.NextInt(0, totalWeight);
            foreach (var match in _matches)
            {
                if (ticket < match.Weight)
                {
                    return match;
                }

                ticket -= match.Weight;
            }

            throw new InvalidOperationException("抽選値が重みの範囲外");
        }

        /// <summary>不正な行（id の重複・未知の種類・階の範囲の逆転・負の値）はファイル名・行・列つきの <see cref="CsvFormatException"/>。</summary>
        public static EncounterTable Load(CsvTable table)
        {
            var result = new EncounterTable();
            var hasWeight = table.HasColumn("weight");
            foreach (var row in table.Rows)
            {
                var id = row.Get("id");
                if (result._byId.ContainsKey(id))
                {
                    throw row.Error("id", $"id '{id}' が重複している");
                }

                var kindIndex = Array.IndexOf(KindIds, row.Get("kind"));
                if (kindIndex < 0)
                {
                    throw row.Error("kind", $"種類は {string.Join("/", KindIds)} のどれか: '{row.Get("kind")}'");
                }

                var minFloor = row.GetInt("min_floor");
                if (minFloor < 1)
                {
                    throw row.Error("min_floor", $"階は1以上: {minFloor}");
                }

                var maxFloor = row.GetInt("max_floor");
                if (maxFloor < minFloor)
                {
                    throw row.Error("max_floor", $"max_floor {maxFloor} が min_floor {minFloor} より小さい");
                }

                var weight = hasWeight ? row.GetInt("weight") : 1;
                if (weight <= 0)
                {
                    throw row.Error("weight", $"重みは1以上: {weight}");
                }

                var encounter = new Encounter(id, (EncounterKind)kindIndex, row.Get("map_id"), minFloor, maxFloor,
                    NonNegative(row, "fixed_spins"), NonNegative(row, "reward_money"), NonNegative(row, "reward_coins"), weight);
                result._encounters.Add(encounter);
                result._byId.Add(id, encounter);
            }

            result._matches.Capacity = result._encounters.Count;
            return result;
        }

        private static int NonNegative(CsvRow row, string column)
        {
            var value = row.GetInt(column);
            return value >= 0 ? value : throw row.Error(column, $"負の値: {value}");
        }
    }
}
