using System;
using System.Collections.Generic;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Core.Slot;

namespace UnitySlotTower.Core.Battle
{
    /// <summary>
    /// 1色のスキル（FR-029、plan 16.4）。発動するとその色に割り当てた職の配置中の全員に効く。
    /// 効果時間のあいだの効果（ブロック数・被ダメージ・攻撃間隔・バッファーの倍率）と、発動の瞬間の範囲攻撃（<see cref="DamageRate"/>）を持つ。
    /// </summary>
    public sealed class SkillDefinition
    {
        internal SkillDefinition(SlotSymbol color, string unitId, double duration, int blockAdd, double damageTakenRate,
            double damageRate, double intervalRate, double buffAdd)
        {
            Color = color;
            UnitId = unitId;
            Duration = duration;
            BlockAdd = blockAdd;
            DamageTakenRate = damageTakenRate;
            DamageRate = damageRate;
            IntervalRate = intervalRate;
            BuffAdd = buffAdd;
        }

        /// <summary>ゲージの色（色7のどれか）。</summary>
        public SlotSymbol Color { get; }

        /// <summary>この色のスキルを使う職。</summary>
        public string UnitId { get; }

        /// <summary>効果時間（秒）。0 なら効果時間のある効果はない。</summary>
        public double Duration { get; }

        /// <summary>効果時間を戦闘ステップの数にしたもの（四捨五入）。</summary>
        public int DurationSteps => (int)Math.Round(Duration * SimulationClock.StepsPerSecond, MidpointRounding.AwayFromZero);

        /// <summary>効果時間中にブロック数に足す数。</summary>
        public int BlockAdd { get; }

        /// <summary>効果時間中に受けるダメージに掛ける倍率（0.5 なら半分、四捨五入）。</summary>
        public double DamageTakenRate { get; }

        /// <summary>発動の瞬間に範囲内の敵全員へ与える攻撃の攻撃力の倍率。0 なら範囲攻撃はしない。</summary>
        public double DamageRate { get; }

        /// <summary>効果時間中の攻撃間隔に掛ける倍率（0.4 なら 1/2.5）。</summary>
        public double IntervalRate { get; }

        /// <summary>効果時間中にバッファーの倍率に足す値。</summary>
        public double BuffAdd { get; }
    }

    /// <summary>skills.csv（plan 16.4）。色7の4色それぞれにちょうど1行。</summary>
    public sealed class SkillTable
    {
        public const string FileName = "skills.csv";

        /// <summary>色7の id（color 列に書ける値）。</summary>
        public static readonly string[] ColorIds =
        {
            SlotSymbols.Ids[(int)SlotSymbol.Red7], SlotSymbols.Ids[(int)SlotSymbol.Blue7],
            SlotSymbols.Ids[(int)SlotSymbol.Green7], SlotSymbols.Ids[(int)SlotSymbol.Yellow7],
        };

        private readonly SkillDefinition[] _skills = new SkillDefinition[ColorIds.Length];

        private SkillTable()
        {
        }

        /// <summary>その色のスキル。</summary>
        public SkillDefinition For(SlotSymbol color)
        {
            if (!SkillGauges.IsColor(color))
            {
                throw new ArgumentOutOfRangeException(nameof(color), color, "色7の図柄ではない");
            }

            return _skills[(int)color];
        }

        /// <summary>その職のスキルの色。スキルを持たない職なら false。</summary>
        public bool TryGetColor(string unitId, out SlotSymbol color)
        {
            foreach (var skill in _skills)
            {
                if (skill.UnitId == unitId)
                {
                    color = skill.Color;
                    return true;
                }
            }

            color = default;
            return false;
        }

        public static SkillTable Load(CsvTable table)
        {
            var skills = new SkillTable();
            foreach (var row in table.Rows)
            {
                var color = SlotSymbols.Parse(row, "color");
                if (!SkillGauges.IsColor(color))
                {
                    throw row.Error("color", $"スキルの色は色7のどれか: '{row.Get("color")}'");
                }

                if (skills._skills[(int)color] != null)
                {
                    throw row.Error("color", $"色 '{row.Get("color")}' が重複している");
                }

                skills._skills[(int)color] = new SkillDefinition(color, row.Get("unit_id"), row.GetDouble("duration"),
                    row.GetInt("block_add"), row.GetDouble("damage_taken_rate"), row.GetDouble("damage_rate"),
                    row.GetDouble("interval_rate"), row.GetDouble("buff_add"));
            }

            for (var i = 0; i < skills._skills.Length; i++)
            {
                if (skills._skills[i] == null)
                {
                    throw new CsvFormatException(new CsvError(table.FileName, 0, 0, $"色 '{ColorIds[i]}' のスキルがない"));
                }
            }

            return skills;
        }
    }
}
