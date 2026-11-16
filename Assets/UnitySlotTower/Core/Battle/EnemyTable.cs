using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>敵1種のステータス（plan 5章「敵」）。</summary>
    public sealed class EnemyDefinition
    {
        internal EnemyDefinition(string id, int maxHp, int atk, int def, double attackInterval, double moveSpeed,
            double range, bool isAir, int blockCost, int lifeDamage, double coinRate, int coinMin, int coinMax,
            double moneyRate, int moneyMin, int moneyMax,
            int areaAtk, double areaRadius, double areaInterval, double rangedDamageRate = 1)
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
            CoinRate = coinRate;
            CoinMin = coinMin;
            CoinMax = coinMax;
            MoneyRate = moneyRate;
            MoneyMin = moneyMin;
            MoneyMax = moneyMax;
            AreaAtk = areaAtk;
            AreaRadius = areaRadius;
            AreaInterval = areaInterval;
            RangedDamageRate = rangedDamageRate;
        }

        public string Id { get; }
        public int MaxHp { get; }
        public int Atk { get; }
        public int Def { get; }

        /// <summary>遠距離職から受ける、防御計算後のダメージ倍率（FR-075）。</summary>
        public double RangedDamageRate { get; }

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
        /// <summary>撃破時にコインを落とす確率（plan 16.2）。</summary>
        public double CoinRate { get; }
        public int CoinMin { get; }
        public int CoinMax { get; }

        /// <summary>撃破時に所持金を落とす確率。コインと同じ1回の抽選で、コインに外れた分から当たる（和は1以下）。</summary>
        public double MoneyRate { get; }
        public int MoneyMin { get; }
        public int MoneyMax { get; }

        /// <summary>範囲攻撃の攻撃力（最終ボス）。0 なら範囲攻撃をしない。</summary>
        public int AreaAtk { get; }

        /// <summary>範囲攻撃が届く半径（マス）。敵の位置から味方のマス中心までの距離で測る。</summary>
        public double AreaRadius { get; }

        /// <summary>範囲攻撃の間隔（秒）。</summary>
        public double AreaInterval { get; }

        public bool HasAreaAttack => AreaAtk > 0;
    }

    /// <summary>enemies.csv の敵一覧。</summary>
    public sealed class EnemyTable
    {
        public const string FileName = "enemies.csv";

        // 0.7 + 0.3 のような和を浮動小数の誤差でエラーにしないための許容幅。
        private const double RateEpsilon = 1e-9;

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

                var areaAtk = row.GetInt("area_atk");
                var areaInterval = row.GetDouble("area_interval");
                if (areaAtk > 0 && areaInterval <= 0)
                {
                    throw row.Error("area_interval", $"範囲攻撃する敵 '{id}' の間隔が 0 以下");
                }

                var coinRate = row.GetProbability("coin_rate");
                var moneyRate = row.GetProbability("money_rate");
                if (coinRate + moneyRate > 1 + RateEpsilon)
                {
                    throw row.Error("money_rate", $"敵 '{id}' のコインと所持金のドロップ率の和が 1 を超える");
                }

                var rangedDamageRate = table.HasColumn("ranged_damage_rate")
                    ? row.GetDouble("ranged_damage_rate") : 1;
                if (!(rangedDamageRate > 0 && rangedDamageRate <= 1))
                {
                    throw row.Error("ranged_damage_rate", "遠距離ダメージ倍率は 0 より大きく 1 以下");
                }

                enemies._enemies.Add(id, new EnemyDefinition(id, row.GetInt("max_hp"), row.GetInt("atk"),
                    row.GetInt("def"), row.GetDouble("attack_interval"), row.GetDouble("move_speed"),
                    row.GetDouble("range"), row.GetBool("is_air"), row.GetInt("block_cost"), row.GetInt("life_damage"),
                    coinRate, row.GetInt("coin_min"), row.GetInt("coin_max"),
                    moneyRate, row.GetInt("money_min"), row.GetInt("money_max"),
                    areaAtk, row.GetDouble("area_radius"), areaInterval, rangedDamageRate));
            }

            return enemies;
        }
    }
}
