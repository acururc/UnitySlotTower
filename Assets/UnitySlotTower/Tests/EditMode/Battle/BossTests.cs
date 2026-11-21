using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Tests.EditMode.Data;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    /// <summary>
    /// 中ボス・最終ボス（FR-030, AC-010）。中ボスは固有行動なしで強いステータスだけ、最終ボスは範囲攻撃を持つ（2026-10-30 ユーザー決定）。
    /// 範囲攻撃は <see cref="BattleFixtures"/> の quaker（(0,0) から動かず、2 秒ごとに半径 1.5 マスへ攻撃力 500）で見る。
    /// 味方の防御は剣士・弓とも 50 なので、1 回の範囲攻撃で 450 減る。
    /// </summary>
    public sealed class BossTests
    {
        private const string Quaker = "0,quaker,S1,g,1\n";

        private static Battlefield FieldWithQuaker() => BattleFixtures.CreateField(Quaker);

        [Test]
        public void Area_Attack_Hits_Every_Unit_Within_Its_Radius()
        {
            var field = FieldWithQuaker();
            var units = BattleFixtures.LoadUnits();
            var sword = field.AddUnit(units.Get("sword"), new GridPoint(1, 0), Facing.Right);
            var near = field.AddUnit(units.Get("archer"), new GridPoint(1, 1), Facing.Right);
            var far = field.AddUnit(units.Get("archer"), new GridPoint(3, 1), Facing.Right);

            field.Advance(120);

            Assert.That((sword.Hp, near.Hp, far.Hp), Is.EqualTo((750, 250, 700)));
        }

        [Test]
        public void First_Area_Attack_Comes_One_Interval_After_Spawning()
        {
            var field = FieldWithQuaker();
            var sword = field.AddUnit(BattleFixtures.LoadUnits().Get("sword"), new GridPoint(1, 0), Facing.Right);

            field.Advance(119);

            Assert.That(sword.Hp, Is.EqualTo(1200));
        }

        [Test]
        public void Area_Attack_Repeats_Every_Interval()
        {
            var field = FieldWithQuaker();
            var sword = field.AddUnit(BattleFixtures.LoadUnits().Get("sword"), new GridPoint(1, 0), Facing.Right);

            field.Advance(240);

            Assert.That(sword.Hp, Is.EqualTo(300));
        }

        [Test]
        [Description("味方の攻撃と同じく、対象がないあいだは間隔を消費せず、入った瞬間に撃つ")]
        public void Area_Attack_Waits_For_A_Unit_In_Its_Radius()
        {
            var field = FieldWithQuaker();
            field.Advance(300);
            var sword = field.AddUnit(BattleFixtures.LoadUnits().Get("sword"), new GridPoint(1, 0), Facing.Right);

            field.Advance(1);

            Assert.That(sword.Hp, Is.EqualTo(750));
        }

        [Test]
        public void Area_Attack_Without_Interval_Is_A_Load_Error()
        {
            var text = BattleFixtures.Enemies.Replace("500,1.5,2\n", "500,1.5,0\n");

            var ex = Assert.Throws<CsvFormatException>(() => EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, text)));

            Assert.That((ex.Error.Line, ex.Error.Column), Is.EqualTo((11, 19)));
        }

        private static EnemyTable ProjectEnemies() =>
            EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, TablePaths.ReadAll()[EnemyTable.FileName]));

        [Test]
        [Description("FR-030 / plan 5章: ボスはブロック消費2以上。中ボスは固有行動なし、最終ボスは範囲攻撃")]
        public void Project_Bosses_Block_Heavily_And_Only_The_Final_Boss_Has_An_Area_Attack()
        {
            var enemies = ProjectEnemies();
            var mid = enemies.Get("mid_boss");
            var final = enemies.Get("final_boss");

            Assert.That((mid.BlockCost >= 2, final.BlockCost >= 2, mid.HasAreaAttack, final.HasAreaAttack),
                Is.EqualTo((true, true, false, true)));
        }

        [Test]
        [Description("plan 16.2・17.1: 中ボスはコインなし・所持金 100% で 120 円、最終ボスは落とさない")]
        public void Project_Boss_Drops_Follow_Plan()
        {
            var enemies = ProjectEnemies();
            var mid = enemies.Get("mid_boss");
            var final = enemies.Get("final_boss");

            Assert.That((mid.CoinRate, mid.MoneyRate, mid.MoneyMin, mid.MoneyMax, final.CoinRate, final.MoneyRate),
                Is.EqualTo((0.0, 1.0, 200, 200, 0.0, 0.0)));
        }

        [Test]
        [Description("AC-010 / plan 4章: 最終ボスがゴールに着けば全ライフを失いゲームオーバー")]
        public void Project_Final_Boss_Takes_All_Life_At_The_Goal()
        {
            var tables = TablePaths.ReadAll();
            var life = GameConfig.Load(CsvParser.Parse(GameConfig.FileName, tables[GameConfig.FileName])).Life;

            Assert.That(ProjectEnemies().Get("final_boss").LifeDamage, Is.GreaterThanOrEqualTo(life));
        }

        [Test]
        [Description("FR-010: ボスは戦闘マスの最後に1体だけ。ボスマスは最終ボスで終わり、最終ボスはボスマスにだけ出る")]
        public void Project_Bosses_Appear_Only_At_The_End_Of_The_Wave()
        {
            // 通常の戦闘マスにボスを出さない決まり（強敵は中ボス）は、出現データを作り直す T347 で確かめる。
            var tables = TablePaths.ReadAll();
            var paths = CsvParser.Parse(BattleMap.PathsFileName, tables[BattleMap.PathsFileName]);
            var waves = CsvParser.Parse(WaveSchedule.FileName, tables[WaveSchedule.FileName]);
            var encounters = EncounterTable.Load(CsvParser.Parse(EncounterTable.FileName, tables[EncounterTable.FileName]));
            var enemies = ProjectEnemies();
            var problems = new List<string>();
            foreach (var encounter in encounters.All)
            {
                var map = BattleMap.Load(encounter.MapId, tables[BattleMap.MapFileName(encounter.MapId)], paths);
                var spawns = WaveSchedule.Load(waves, encounter.Id, map, enemies).Spawns;
                for (var i = 0; i < spawns.Count; i++)
                {
                    var isLast = i == spawns.Count - 1;
                    if (spawns[i].IsBoss && !isLast)
                    {
                        problems.Add($"{encounter.Id} の {i + 1} 体目がボス");
                    }

                    var isFinal = spawns[i].Enemy.Id == "final_boss";
                    if (isFinal != (isLast && encounter.Kind == EncounterKind.Boss))
                    {
                        problems.Add($"{encounter.Id} の {i + 1} 体目: 最終ボスの位置が違う");
                    }
                }
            }

            Assert.That(problems, Is.Empty);
        }
    }
}
