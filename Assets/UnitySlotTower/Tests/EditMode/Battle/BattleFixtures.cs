using System.Text.RegularExpressions;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;
using UnitySlotTower.Core.Simulation;

namespace UnitySlotTower.Tests.EditMode.Battle
{
    /// <summary>敵とウェーブのテスト用の小さなデータ。</summary>
    internal static class BattleFixtures
    {
        // 上の行が地上経路 g（S1→E）。空中経路 a は S2 から高台・通行不可の上を通って E へ。
        //   S1 G G E
        //   S2 H X H
        public const string Map = "S1,G,G,E\nS2,H,X,H\n";

        public const string Paths =
            "map_id,path_id,is_air,step,x,y\n" +
            "m,g,0,1,0,0\nm,g,0,2,1,0\nm,g,0,3,2,0\nm,g,0,4,3,0\n" +
            "m,a,1,1,0,1\nm,a,1,2,1,1\nm,a,1,3,2,1\nm,a,1,4,2,0\nm,a,1,5,3,0\n";

        // melee: 射程なし・1マス/秒、ranged: 射程1.5・0.5マス/秒、air: 空中・射程1・2マス/秒、heavy: 射程なし・1マス/秒・ブロック消費2、
        // brute: 射程なし・1マス/秒・HP100000・攻撃1000（味方を倒す役）、
        // ドロップの列は コイン（率・最小・最大）→ 所持金（率・最小・最大）。
        // fodder: HP1・攻撃0・コイン40%で1枚、rich: HP1・攻撃0・必ずコイン2〜3枚、
        // purse: HP1・攻撃0・コイン25%で3枚／所持金40%で2、banker: HP1・攻撃0・必ず所持金2〜3（ドロップを見る役）、
        // quaker: 動かず・通常攻撃0・2秒ごとに半径1.5マスへ範囲攻撃500（範囲攻撃を見る役）、
        // grunt: HP1・攻撃0・コイン15%で1枚／所持金100%で30（002 の雑魚と同じドロップ、AC-104）、
        // dummy: HP100・攻撃0・防御0・ドロップなし（002 の小さい桁のダメージを見る的、FR-109）
        public const string Enemies =
            "id,max_hp,atk,def,attack_interval,move_speed,range,is_air,block_cost,life_damage," +
            "coin_rate,coin_min,coin_max,money_rate,money_min,money_max,area_atk,area_radius,area_interval\n" +
            "melee,1000,200,50,1.5,1,0,0,1,1,0.4,1,1,0,0,0,0,0,0\n" +
            "ranged,700,300,30,2,0.5,1.5,0,1,1,0.5,1,2,0,0,0,0,0,0\n" +
            "air,600,150,20,1,2,1,1,1,1,0.5,1,2,0.3,4,5,0,0,0\n" +
            "heavy,2000,300,50,2,1,0,0,2,2,0.5,1,2,0,0,0,0,0,0\n" +
            "brute,100000,1000,0,1,1,0,0,1,1,0.5,1,2,0,0,0,0,0,0\n" +
            "fodder,1,0,0,1,1,0,0,1,1,0.4,1,1,0,0,0,0,0,0\n" +
            "rich,1,0,0,1,1,0,0,1,1,1,2,3,0,0,0,0,0,0\n" +
            "purse,1,0,0,1,1,0,0,1,1,0.25,3,3,0.4,2,2,0,0,0\n" +
            "banker,1,0,0,1,1,0,0,1,1,0,0,0,1,2,3,0,0,0\n" +
            "quaker,100000,0,0,1,0,0,0,2,10,0,0,0,0,0,0,500,1.5,2\n" +
            "grunt,1,0,0,1,1,0,0,1,1,0.15,1,1,1,30,30,0,0,0\n" +
            "dummy,100,0,0,1,1,0,0,1,1,0,0,0,0,0,0,0,0,0\n";

        // tank: HP1800・防御300・ブロック3、sword: HP1200・防御50・ブロック1、archer: HP700・防御50、
        // buffer: 倍率1.5、weak_buffer: 倍率1.2（どちらも攻撃力100を持つが攻撃しない）
        public const string Units =
            "id,max_hp,atk,def,attack_interval,block,tile,cost,cooldown,role,buff_rate\n" +
            "tank,1800,220,300,1.2,3,ground,15,20,melee,1\n" +
            "sword,1200,400,50,1,1,ground,12,12,melee,1\n" +
            "archer,700,350,50,1,0,high,14,15,ranged,1\n" +
            "buffer,800,100,50,1,0,high,18,25,buff,1.5\n" +
            "weak_buffer,800,100,50,1,0,high,18,25,buff,1.2\n";

        // 右向き基準。tank・sword: 自マス＋前1、archer・buffer・weak_buffer: 自マスの列から前方 2×3。
        public const string Ranges =
            "unit_id,dx,dy\n" +
            "tank,0,0\ntank,1,0\n" +
            "sword,0,0\nsword,1,0\n" +
            "archer,0,-1\narcher,0,0\narcher,0,1\narcher,1,-1\narcher,1,0\narcher,1,1\n" +
            "buffer,0,-1\nbuffer,0,0\nbuffer,0,1\nbuffer,1,-1\nbuffer,1,0\nbuffer,1,1\n" +
            "weak_buffer,0,-1\nweak_buffer,0,0\nweak_buffer,0,1\nweak_buffer,1,-1\nweak_buffer,1,0\nweak_buffer,1,1\n";

        // sword: 3段階（段階2で HP×1.5・攻撃×1.25・防御×2、費用20）、archer・buffer: 2段階、ほかの種類は行がない（強化できない）。
        public const string Upgrades =
            "unit_id,level,hp_mult,atk_mult,def_mult,cost\n" +
            "sword,1,1,1,1,0\nsword,2,1.5,1.25,2,20\nsword,3,2,1.5,3,35\n" +
            "archer,1,1,1,1,0\narcher,2,1.2,2,1,30\n" +
            "buffer,1,1,1,1,0\nbuffer,2,1.5,1.5,1,30\n";

        // plan 16.4 の仮値: 黄=タンク 10秒 ブロック+1・被ダメージ×0.5、赤=剣士 即時に範囲の敵へ攻撃力×3、
        // 青=弓 8秒 攻撃間隔×0.4（÷2.5）、緑=バッファー 10秒 倍率+0.3。
        public const string SkillsHeader =
            "color,unit_id,duration,block_add,damage_taken_rate,damage_rate,interval_rate,buff_add\n";

        public const string Skills = SkillsHeader +
            "red7,sword,0,0,1,3,1,0\n" +
            "blue7,archer,8,0,1,0,0.4,0\n" +
            "green7,buffer,10,0,1,0,1,0.3\n" +
            "yellow7,tank,10,1,0.5,0,1,0\n";

        public static SkillTable LoadSkills(string csv = Skills) =>
            SkillTable.Load(CsvParser.Parse(SkillTable.FileName, csv));

        public const string WavesHeader = "stage,time,enemy_id,spawn,path_id,is_boss\n";

        public static BattleMap LoadMap() =>
            BattleMap.Load("m", Map, CsvParser.Parse(BattleMap.PathsFileName, Paths));

        public static EnemyTable LoadEnemies() => EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, Enemies));

        public static UnitTable LoadUnits() => UnitTable.Load(CsvParser.Parse(UnitTable.FileName, Units));

        public static UnitRanges LoadRanges() =>
            UnitRanges.Load(CsvParser.Parse(UnitRanges.FileName, Ranges), LoadUnits());

        public static UnitUpgradeTable LoadUpgrades(string csv = Upgrades) =>
            UnitUpgradeTable.Load(CsvParser.Parse(UnitUpgradeTable.FileName, csv), LoadUnits());

        /// <summary>ステージ1の1ウェーブの出現。行は stage 列を省いて書く（time,enemy_id,spawn,path_id,is_boss）。</summary>
        public static WaveSchedule LoadWave(string rows) =>
            LoadStageWave(Regex.Replace(rows, "^(?=.)", "1,", RegexOptions.Multiline), 1);

        /// <summary>行は stage 列から書く（wave 列なし＝全行ウェーブ1）。</summary>
        public static WaveSchedule LoadStageWave(string rows, int stage) =>
            WaveSchedule.Load(CsvParser.Parse(WaveSchedule.FileName, WavesHeader + rows), stage, LoadMap(),
                LoadEnemies());

        public const string GroupWavesHeader = "stage,time,enemy_id,spawn,path_id,is_boss,count,interval\n";

        /// <summary>群れの列つき（FR-034）。行は stage 列を省いて書く（time,enemy_id,spawn,path_id,is_boss,count,interval）。</summary>
        public static WaveSchedule LoadGroupWave(string rows) =>
            WaveSchedule.Load(
                CsvParser.Parse(WaveSchedule.FileName,
                    GroupWavesHeader + Regex.Replace(rows, "^(?=.)", "1,", RegexOptions.Multiline)),
                1, LoadMap(), LoadEnemies());

        public const string StagedWavesHeader = "stage,wave,time,enemy_id,spawn,path_id,is_boss,count,interval\n";

        /// <summary>
        /// ウェーブ分けしたステージ1（FR-102）。行は stage 列を省いて書く（wave,time,enemy_id,spawn,path_id,is_boss,count,interval）。
        /// <paramref name="startAfters"/> はウェーブ1からの順の start_after。
        /// </summary>
        public static WaveSchedule LoadStagedWave(string rows, params double[] startAfters)
        {
            var stageWaves = "stage,wave,start_after\n";
            for (var i = 0; i < startAfters.Length; i++)
            {
                stageWaves += $"1,{i + 1},{startAfters[i].ToString(System.Globalization.CultureInfo.InvariantCulture)}\n";
            }

            return WaveSchedule.Load(
                CsvParser.Parse(WaveSchedule.FileName,
                    StagedWavesHeader + Regex.Replace(rows, "^(?=.)", "1,", RegexOptions.Multiline)),
                1, LoadMap(), LoadEnemies(), CsvParser.Parse(WaveSchedule.StageWavesFileName, stageWaves));
        }

        /// <summary>既定はライフ10・シード1の戦闘。</summary>
        public static Battlefield CreateField(string waveRows, int life = 10, IRandom random = null) =>
            new Battlefield(LoadWave(waveRows), LoadRanges(), life, random ?? new SeededRandom(1));
    }
}
