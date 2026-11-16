using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>1体の出現: いつ、どの敵が、どの経路から出るか（waves.csv の群れの行を展開したもの）。</summary>
    public sealed class WaveSpawn
    {
        internal WaveSpawn(double time, EnemyDefinition enemy, MapPath path, bool isBoss)
        {
            Time = time;
            Enemy = enemy;
            Path = path;
            IsBoss = isBoss;
        }

        /// <summary>戦闘開始（開始の演出の後）からの秒数。</summary>
        public double Time { get; }

        public EnemyDefinition Enemy { get; }
        public MapPath Path { get; }
        public bool IsBoss { get; }
    }

    /// <summary>
    /// 1つの戦闘マス（encounters.csv の id）の出現予定（FR-010、plan 17.2）。戦闘マスは1ウェーブで終わる（D-78）。
    /// </summary>
    public sealed class WaveSchedule
    {
        public const string FileName = "waves.csv";

        /// <summary>群れ（FR-034、plan 17.3）: 1行で count 体を interval 秒ごとに出す。列がなければ1体。</summary>
        public const string CountColumn = "count";

        public const string IntervalColumn = "interval";

        private readonly List<WaveSpawn> _spawns = new List<WaveSpawn>();

        private WaveSchedule(string encounterId)
        {
            EncounterId = encounterId;
        }

        /// <summary>encounters.csv の id。</summary>
        public string EncounterId { get; }

        /// <summary>群れを1体ずつに展開した出現。時刻の早い順（同時刻は CSV の行順・群れの中の順）。</summary>
        public IReadOnlyList<WaveSpawn> Spawns => _spawns;

        /// <summary>
        /// 不正な行（未知の敵・マップにない経路・出現口の食い違い・空中フラグの食い違い・群れの数が0以下・間隔が負）は
        /// ファイル名・行・列つきの <see cref="CsvFormatException"/> にする。
        /// </summary>
        public static WaveSchedule Load(CsvTable table, string encounterId, BattleMap map, EnemyTable enemies)
        {
            var schedule = new WaveSchedule(encounterId);
            var hasCount = table.HasColumn(CountColumn);
            var hasInterval = table.HasColumn(IntervalColumn);
            foreach (var row in table.Rows)
            {
                if (row.Get("encounter_id") != encounterId)
                {
                    continue;
                }

                var enemyId = row.Get("enemy_id");
                if (!enemies.Contains(enemyId))
                {
                    throw row.Error("enemy_id", $"敵 '{enemyId}' がない");
                }

                var enemy = enemies.Get(enemyId);
                var pathId = row.Get("path_id");
                var path = map.FindPath(pathId);
                if (path == null)
                {
                    throw row.Error("path_id", $"マップ '{map.Id}' に経路 '{pathId}' がない");
                }

                if (path.IsAir != enemy.IsAir)
                {
                    throw row.Error("path_id",
                        $"{(enemy.IsAir ? "空中" : "地上")}の敵 '{enemyId}' が{(path.IsAir ? "空中" : "地上")}経路 '{pathId}' を進む");
                }

                var spawn = $"S{map.SpawnNumberAt(path.Cells[0])}";
                if (row.Get("spawn") != spawn)
                {
                    throw row.Error("spawn", $"経路 '{pathId}' の出現口は {spawn}");
                }

                var time = row.GetDouble("time");
                var isBoss = row.GetBool("is_boss");
                var count = hasCount ? row.GetInt(CountColumn) : 1;
                if (count < 1)
                {
                    throw row.Error(CountColumn, $"群れの数は1以上: {count}");
                }

                var interval = hasInterval ? row.GetDouble(IntervalColumn) : 0;
                if (interval < 0)
                {
                    throw row.Error(IntervalColumn, $"群れの間隔は0以上: {interval}");
                }

                for (var i = 0; i < count; i++)
                {
                    schedule._spawns.Add(new WaveSpawn(time + i * interval, enemy, path, isBoss));
                }
            }

            // List.Sort は安定でないため、挿入ソートで同時刻の行順を保つ。
            for (var i = 1; i < schedule._spawns.Count; i++)
            {
                var spawn = schedule._spawns[i];
                var j = i - 1;
                while (j >= 0 && schedule._spawns[j].Time > spawn.Time)
                {
                    schedule._spawns[j + 1] = schedule._spawns[j];
                    j--;
                }

                schedule._spawns[j + 1] = spawn;
            }

            return schedule;
        }
    }
}
