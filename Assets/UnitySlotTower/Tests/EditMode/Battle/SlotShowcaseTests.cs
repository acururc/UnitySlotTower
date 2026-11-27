using System.Text.RegularExpressions;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    /// <summary>spec 011 AC-005 の Core 部分: 見せ場のウェーブを出し終わると戦闘が止まり、解くまで進まない。</summary>
    public sealed class SlotShowcaseTests
    {
        private const int Second = 60;

        // ウェーブ1: melee 1体。ウェーブ2（見せ場）: melee 3体を0.5秒おき。
        private static Battlefield Create(bool showcase = true)
        {
            var stageWaves = "stage,wave,start_after,slot_showcase\n1,1,0,0\n" + $"1,2,0,{(showcase ? 1 : 0)}\n";
            var schedule = WaveSchedule.Load(
                CsvParser.Parse(WaveSchedule.FileName, BattleFixtures.StagedWavesHeader +
                    Regex.Replace("1,0,fodder,S1,g,0,1,0\n2,0,melee,S1,g,0,3,0.5\n", "^(?=.)", "1,", RegexOptions.Multiline)),
                1, BattleFixtures.LoadMap(), BattleFixtures.LoadEnemies(),
                CsvParser.Parse(WaveSchedule.StageWavesFileName, stageWaves));
            return new Battlefield(new WaveProgress(schedule, WaveMode.Clear, 1), BattleFixtures.LoadRanges(), 10,
                new SeededRandom(1));
        }

        [Test]
        [Description("FR-031: 見せ場のウェーブを出し終わると止まり、通知は1回。止まっている間は Advance しても何も動かない")]
        public void Battle_Holds_After_The_Showcase_Wave_Is_Emitted()
        {
            var field = Create();
            var held = 0;
            field.ShowcaseHeld += () => held++;

            for (var i = 0; i < 20 * Second && !field.IsShowcaseHolding; i++)
            {
                field.Advance(1);
            }

            var tick = field.Tick;
            var distance = field.Enemies[0].Distance;
            field.Advance(5 * Second);

            Assert.That((field.IsShowcaseHolding, held, field.Enemies.Count, field.Waves.Unspawned),
                Is.EqualTo((true, 1, 3, 0)));
            Assert.That((field.Tick, field.Enemies[0].Distance), Is.EqualTo((tick, distance)));
        }

        [Test]
        [Description("FR-031/033: 解くと進み、同じ戦闘ではもう止まらない")]
        public void Release_Resumes_And_Does_Not_Hold_Again()
        {
            var field = Create();
            for (var i = 0; i < 20 * Second && !field.IsShowcaseHolding; i++)
            {
                field.Advance(1);
            }

            var tick = field.Tick;
            field.ReleaseShowcase();
            field.Advance(Second);

            Assert.That((field.IsShowcaseHolding, field.ShowcaseStarted, field.Tick), Is.EqualTo((false, true, tick + Second)));
        }

        [Test]
        [Description("見せ場のないステージは止まらない")]
        public void Battle_Without_Showcase_Never_Holds()
        {
            var field = Create(showcase: false);

            field.Advance(20 * Second);

            Assert.That((field.IsShowcaseHolding, field.ShowcaseStarted), Is.EqualTo((false, false)));
        }

        [Test]
        [Description("spec 015 FR-001: 正本のステージ1はウェーブ1が見せ場で、ほかのステージに見せ場はない")]
        public void Project_Stage_One_Wave_One_Is_The_Only_Showcase()
        {
            var stageWaves = CsvParser.Parse(WaveSchedule.StageWavesFileName,
                System.IO.File.ReadAllText(System.IO.Path.Combine(TablePaths.Root, WaveSchedule.StageWavesFileName)));
            var showcases = 0;
            foreach (var row in stageWaves.Rows)
            {
                if (row.GetBool(WaveSchedule.SlotShowcaseColumn))
                {
                    showcases++;
                    Assert.That((row.GetInt("stage"), row.GetInt(WaveSchedule.WaveColumn)), Is.EqualTo((1, 1)));
                }
            }

            Assert.That(showcases, Is.EqualTo(1));
        }
    }
}
