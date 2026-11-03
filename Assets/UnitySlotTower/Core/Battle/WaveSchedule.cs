using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>waves.csv の 1 行: いつ、どの敵が、どの経路から出るか。</summary>
    public sealed class WaveSpawn
    {
        internal WaveSpawn(double time, EnemyDefinition enemy, MapPath path, bool isBoss)
        {
            Time = time;
            Enemy = enemy;
            Path = path;
            IsBoss = isBoss;
        }

        /// <summary>そのウェーブの戦闘開始からの秒数。</summary>
        public double Time { get; }

        public EnemyDefinition Enemy { get; }
        public MapPath Path { get; }
        public bool IsBoss { get; }
    }

    /// <summary>
    /// 1 ウェーブ分の出現予定（FR-010、plan 6・7章）。ゲームは <see cref="StageCount"/> ステージ × <see cref="WavesPerStage"/> ウェーブで、
    /// ステージ n はマップ s{n} で戦う。
    /// </summary>
    public sealed class WaveSchedule
    {
        public const string FileName = "waves.csv";
        public const int StageCount = 3;
        public const int WavesPerStage = 2;

        private readonly List<WaveSpawn> _spawns = new List<WaveSpawn>();

        private WaveSchedule(int stage, int wave)
        {
            Stage = stage;
            Wave = wave;
        }

        public int Stage { get; }

        /// <summary>ステージ内のウェーブ番号（1 から）。</summary>
        public int Wave { get; }

        /// <summary>ステージ n のマップの id。</summary>
        public static string MapId(int stage) => $"s{stage}";

        /// <summary>出現時刻の早い順（同時刻は CSV の行順）。</summary>
        public IReadOnlyList<WaveSpawn> Spawns => _spawns;

        /// <summary>
        /// 不正な行（未知の敵・マップにない経路・出現口の食い違い・空中フラグの食い違い）は
        /// ファイル名・行・列つきの <see cref="CsvFormatException"/> にする。
        /// </summary>
        public static WaveSchedule Load(CsvTable table, int stage, int wave, BattleMap map, EnemyTable enemies)
        {
            var schedule = new WaveSchedule(stage, wave);
            foreach (var row in table.Rows)
            {
                if (row.GetInt("stage") != stage || row.GetInt("wave") != wave)
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

                schedule._spawns.Add(new WaveSpawn(row.GetDouble("time"), enemy, path, row.GetBool("is_boss")));
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
