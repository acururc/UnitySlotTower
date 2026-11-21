using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>1体の出現: どのウェーブの何秒目に、どの敵が、どの経路から出るか（waves.csv の群れの行を展開したもの）。</summary>
    public sealed class WaveSpawn
    {
        internal WaveSpawn(int wave, double time, EnemyDefinition enemy, MapPath path, bool isBoss)
        {
            Wave = wave;
            Time = time;
            Enemy = enemy;
            Path = path;
            IsBoss = isBoss;
        }

        /// <summary>ウェーブの番号（1から）。</summary>
        public int Wave { get; }

        /// <summary>そのウェーブの始まりからの秒数。</summary>
        public double Time { get; }

        public EnemyDefinition Enemy { get; }
        public MapPath Path { get; }
        public bool IsBoss { get; }
    }

    /// <summary>1つのウェーブ（plan 2.1）。</summary>
    public sealed class WaveDefinition
    {
        internal readonly List<WaveSpawn> SpawnList = new List<WaveSpawn>();

        internal WaveDefinition(int number)
        {
            Number = number;
        }

        public int Number { get; }

        /// <summary>前のウェーブの始まりから、決めた時刻で次が来るまでの秒数（stage_waves.csv、timed のときだけ使う）。</summary>
        public double StartAfter { get; internal set; }

        /// <summary>時刻の早い順（同時刻は CSV の行順・群れの中の順）。</summary>
        public IReadOnlyList<WaveSpawn> Spawns => SpawnList;
    }

    /// <summary>
    /// 1つのステージの出現予定（FR-102、plan 2.1）。ウェーブごとに分かれ、時刻はウェーブの始まりからの秒数。
    /// いつ次のウェーブを始めるかは <see cref="WaveProgress"/> が決める。
    /// </summary>
    public sealed class WaveSchedule
    {
        public const string FileName = "waves.csv";
        public const string StageWavesFileName = "stage_waves.csv";

        /// <summary>ウェーブの番号の列。なければ全行がウェーブ1。</summary>
        public const string WaveColumn = "wave";

        /// <summary>群れ（FR-034、plan 17.3）: 1行で count 体を interval 秒ごとに出す。列がなければ1体。</summary>
        public const string CountColumn = "count";

        public const string IntervalColumn = "interval";

        private readonly List<WaveDefinition> _waves = new List<WaveDefinition>();
        private readonly List<WaveSpawn> _spawns = new List<WaveSpawn>();

        private WaveSchedule(int stage)
        {
            Stage = stage;
        }

        /// <summary>stages.csv のステージ番号。</summary>
        public int Stage { get; }

        /// <summary>ウェーブの番号の順。</summary>
        public IReadOnlyList<WaveDefinition> Waves => _waves;

        /// <summary>全ウェーブの出現をウェーブの順に並べたもの。</summary>
        public IReadOnlyList<WaveSpawn> Spawns => _spawns;

        /// <summary>
        /// waves.csv のうち <paramref name="stage"/> の行を読む。不正な行（未知の敵・マップにない経路・出現口の食い違い・
        /// 空中フラグの食い違い・群れの数が0以下・間隔が負・ウェーブが1未満）はファイル名・行・列つきの <see cref="CsvFormatException"/>。
        /// <paramref name="stageWaves"/> があれば各ウェーブの <see cref="WaveDefinition.StartAfter"/> をそこから読み、
        /// stage_waves.csv にないウェーブの行も同じ例外にする（出現のないウェーブの stage_waves.csv の行は読み飛ばす）。なければ StartAfter は全て0。
        /// </summary>
        public static WaveSchedule Load(CsvTable table, int stage, BattleMap map, EnemyTable enemies,
            CsvTable stageWaves = null)
        {
            var schedule = new WaveSchedule(stage);
            var hasWave = table.HasColumn(WaveColumn);
            var hasCount = table.HasColumn(CountColumn);
            var hasInterval = table.HasColumn(IntervalColumn);
            var firstRows = new Dictionary<int, CsvRow>();
            foreach (var row in table.Rows)
            {
                if (row.GetInt("stage") != stage)
                {
                    continue;
                }

                var waveNumber = hasWave ? row.GetInt(WaveColumn) : 1;
                if (waveNumber < 1)
                {
                    throw row.Error(WaveColumn, $"ウェーブは1以上: {waveNumber}");
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

                var wave = schedule.FindWave(waveNumber);
                if (wave == null)
                {
                    wave = new WaveDefinition(waveNumber);
                    schedule.InsertWave(wave);
                    firstRows.Add(waveNumber, row);
                }

                for (var i = 0; i < count; i++)
                {
                    wave.SpawnList.Add(new WaveSpawn(waveNumber, time + i * interval, enemy, path, isBoss));
                }
            }

            if (stageWaves != null)
            {
                ApplyStartTimes(schedule, stageWaves, firstRows);
            }

            foreach (var wave in schedule._waves)
            {
                SortByTime(wave.SpawnList);
                schedule._spawns.AddRange(wave.SpawnList);
            }

            return schedule;
        }

        private static void ApplyStartTimes(WaveSchedule schedule, CsvTable stageWaves, Dictionary<int, CsvRow> firstRows)
        {
            var listed = new HashSet<int>();
            foreach (var row in stageWaves.Rows)
            {
                if (row.GetInt("stage") != schedule.Stage)
                {
                    continue;
                }

                var number = row.GetInt(WaveColumn);
                var wave = schedule.FindWave(number);
                if (wave == null)
                {
                    continue; // 出現のないウェーブはデータ検証（GameDataValidator）でエラーにする。
                }

                wave.StartAfter = row.GetDouble("start_after");
                listed.Add(number);
            }

            foreach (var wave in schedule._waves)
            {
                if (!listed.Contains(wave.Number))
                {
                    throw firstRows[wave.Number].Error(WaveColumn,
                        $"ステージ {schedule.Stage} のウェーブ {wave.Number} が {StageWavesFileName} にない");
                }
            }
        }

        private WaveDefinition FindWave(int number)
        {
            foreach (var wave in _waves)
            {
                if (wave.Number == number)
                {
                    return wave;
                }
            }

            return null;
        }

        private void InsertWave(WaveDefinition wave)
        {
            var index = _waves.Count;
            while (index > 0 && _waves[index - 1].Number > wave.Number)
            {
                index--;
            }

            _waves.Insert(index, wave);
        }

        // List.Sort は安定でないため、挿入ソートで同時刻の行順を保つ。
        private static void SortByTime(List<WaveSpawn> spawns)
        {
            for (var i = 1; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                var j = i - 1;
                while (j >= 0 && spawns[j].Time > spawn.Time)
                {
                    spawns[j + 1] = spawns[j];
                    j--;
                }

                spawns[j + 1] = spawn;
            }
        }
    }
}
