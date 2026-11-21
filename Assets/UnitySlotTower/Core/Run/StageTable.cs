using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Core.Run
{
    /// <summary>ステージのウェーブの進み方（FR-102、plan 2.1）。</summary>
    public enum WaveMode
    {
        /// <summary>前のウェーブが全滅してから次が来る（ステージ1のチュートリアル）。</summary>
        Clear,

        /// <summary>決めた時刻か、全滅してからの間隔の早いほうで次が来る（E-24）。</summary>
        Timed,
    }

    /// <summary>stages.csv の1行。</summary>
    public sealed class StageDefinition
    {
        internal StageDefinition(int number, string mapId, WaveMode mode, string bossId)
        {
            Number = number;
            MapId = mapId;
            Mode = mode;
            BossId = bossId;
        }

        /// <summary>1から始まるステージ番号。</summary>
        public int Number { get; }

        public string MapId { get; }
        public WaveMode Mode { get; }

        /// <summary>最後のウェーブに出るボスの敵 id（FR-104）。ボスのいないステージは null。</summary>
        public string BossId { get; }
    }

    /// <summary>ステージの表（FR-101〜FR-104、plan 2.1）。ステージ番号で引く。</summary>
    public sealed class StageTable
    {
        public const string FileName = "stages.csv";

        /// <summary>wave_mode 列の値（<see cref="WaveMode"/> の順）。</summary>
        public static readonly string[] ModeIds = { "clear", "timed" };

        /// <summary>boss 列でボスがいないことを表す値。</summary>
        public const string NoBoss = "none";

        private readonly List<StageDefinition> _stages = new List<StageDefinition>();

        private StageTable()
        {
        }

        /// <summary>行の順。</summary>
        public IReadOnlyList<StageDefinition> Stages => _stages;

        public int Count => _stages.Count;

        public StageDefinition Get(int number)
        {
            foreach (var stage in _stages)
            {
                if (stage.Number == number)
                {
                    return stage;
                }
            }

            throw new KeyNotFoundException($"ステージ {number} がない");
        }

        /// <summary>そのマップを使うステージ。なければ null。</summary>
        public StageDefinition FindByMap(string mapId)
        {
            foreach (var stage in _stages)
            {
                if (stage.MapId == mapId)
                {
                    return stage;
                }
            }

            return null;
        }

        /// <summary>不正な行（番号の重複・1未満・未知の進み方）はファイル名・行・列つきの <see cref="CsvFormatException"/>。</summary>
        public static StageTable Load(CsvTable table)
        {
            var result = new StageTable();
            foreach (var row in table.Rows)
            {
                var number = row.GetInt("stage");
                if (number < 1)
                {
                    throw row.Error("stage", $"ステージは1以上: {number}");
                }

                foreach (var stage in result._stages)
                {
                    if (stage.Number == number)
                    {
                        throw row.Error("stage", $"ステージ {number} が重複している");
                    }
                }

                var modeIndex = Array.IndexOf(ModeIds, row.Get("wave_mode"));
                if (modeIndex < 0)
                {
                    throw row.Error("wave_mode", $"進み方は {string.Join("/", ModeIds)} のどれか: '{row.Get("wave_mode")}'");
                }

                var boss = row.Get("boss");
                result._stages.Add(new StageDefinition(number, row.Get("map_id"), (WaveMode)modeIndex,
                    boss == NoBoss ? null : boss));
            }

            return result;
        }
    }
}
