using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>敵1種のステータス（plan 5章「敵」）。</summary>
    public sealed class EnemyDefinition
    {
        internal EnemyDefinition(string id, int maxHp, int atk, int def, double attackInterval, double moveSpeed,
            double range, bool isAir, int blockCost, int lifeDamage, double dropRate, int dropMin, int dropMax)
        {
            Id = id;
            MaxHp = maxHp;
            Atk = atk;
            Def = def;
            AttackInterval = attackInterval;
            MoveSpeed = moveSpeed;
            Range = range;
            IsAir = isAir;
            BlockCost = blockCost;
            LifeDamage = lifeDamage;
            DropRate = dropRate;
            DropMin = dropMin;
            DropMax = dropMax;
        }

        public string Id { get; }
        public int MaxHp { get; }
        public int Atk { get; }
        public int Def { get; }

        /// <summary>攻撃間隔（秒）。</summary>
        public double AttackInterval { get; }

        /// <summary>移動速度（マス/秒）。</summary>
        public double MoveSpeed { get; }

        /// <summary>射程（マス）。0 は射程攻撃をせず、ブロックした味方だけを攻撃する（近距離雑魚）。</summary>
        public double Range { get; }

        /// <summary>空中の敵は空中経路を進み、ブロックされない（FR-031）。</summary>
        public bool IsAir { get; }

        public int BlockCost { get; }
        public int LifeDamage { get; }
        public double DropRate { get; }
        public int DropMin { get; }
        public int DropMax { get; }
    }

    /// <summary>enemies.csv の敵一覧。</summary>
    public sealed class EnemyTable
    {
        public const string FileName = "enemies.csv";

        private readonly Dictionary<string, EnemyDefinition> _enemies = new Dictionary<string, EnemyDefinition>();
        private readonly string _fileName;

        private EnemyTable(string fileName)
        {
            _fileName = fileName;
        }

        public bool Contains(string id) => _enemies.ContainsKey(id);

        public EnemyDefinition Get(string id)
        {
            if (_enemies.TryGetValue(id, out var enemy))
            {
                return enemy;
            }

            throw new CsvFormatException(new CsvError(_fileName, 0, 0, $"敵 '{id}' がない"));
        }

        public static EnemyTable Load(CsvTable table)
        {
            var enemies = new EnemyTable(table.FileName);
            foreach (var row in table.Rows)
            {
                var id = row.Get("id");
                if (enemies._enemies.ContainsKey(id))
                {
                    throw row.Error("id", $"敵 '{id}' が重複している");
                }

                enemies._enemies.Add(id, new EnemyDefinition(id, row.GetInt("max_hp"), row.GetInt("atk"),
                    row.GetInt("def"), row.GetDouble("attack_interval"), row.GetDouble("move_speed"),
                    row.GetDouble("range"), row.GetBool("is_air"), row.GetInt("block_cost"), row.GetInt("life_damage"),
                    row.GetProbability("drop_rate"), row.GetInt("drop_min"), row.GetInt("drop_max")));
            }

            return enemies;
        }
    }
}
