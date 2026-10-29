using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;

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
        // brute: 射程なし・1マス/秒・HP100000・攻撃1000（味方を倒す役）
        public const string Enemies =
            "id,max_hp,atk,def,attack_interval,move_speed,range,is_air,block_cost,life_damage,drop_rate,drop_min,drop_max\n" +
            "melee,1000,200,50,1.5,1,0,0,1,1,0.4,1,1\n" +
            "ranged,700,300,30,2,0.5,1.5,0,1,1,0.5,1,2\n" +
            "air,600,150,20,1,2,1,1,1,1,0.5,1,2\n" +
            "heavy,2000,300,50,2,1,0,0,2,2,0.5,1,2\n" +
            "brute,100000,1000,0,1,1,0,0,1,1,0.5,1,2\n";

        // tank: HP1800・防御300・ブロック3、sword: HP1200・防御50・ブロック1、archer: HP700・防御50
        public const string Units =
            "id,max_hp,atk,def,attack_interval,block,tile,cost,cooldown,role,buff_rate\n" +
            "tank,1800,220,300,1.2,3,ground,15,20,melee,1\n" +
            "sword,1200,400,50,1,1,ground,12,12,melee,1\n" +
            "archer,700,350,50,1,0,high,14,15,ranged,1\n" +
            "healer,800,200,50,1,0,high,18,25,heal,1\n" +
            "buffer,900,0,50,1,0,high,18,25,buff,1.5\n" +
            "weak_buffer,900,0,50,1,0,high,18,25,buff,1.2\n";

        // 右向き基準。tank・sword: 自マス＋前1、archer・healer・buffer・weak_buffer: 自マスの列から前方 2×3。
        public const string Ranges =
            "unit_id,dx,dy\n" +
            "tank,0,0\ntank,1,0\n" +
            "sword,0,0\nsword,1,0\n" +
            "archer,0,-1\narcher,0,0\narcher,0,1\narcher,1,-1\narcher,1,0\narcher,1,1\n" +
            "healer,0,-1\nhealer,0,0\nhealer,0,1\nhealer,1,-1\nhealer,1,0\nhealer,1,1\n" +
            "buffer,0,-1\nbuffer,0,0\nbuffer,0,1\nbuffer,1,-1\nbuffer,1,0\nbuffer,1,1\n" +
            "weak_buffer,0,-1\nweak_buffer,0,0\nweak_buffer,0,1\nweak_buffer,1,-1\nweak_buffer,1,0\nweak_buffer,1,1\n";

        public const string WavesHeader = "wave,time,enemy_id,spawn,path_id,is_boss\n";

        public static BattleMap LoadMap() =>
            BattleMap.Load("m", Map, CsvParser.Parse(BattleMap.PathsFileName, Paths));

        public static EnemyTable LoadEnemies() => EnemyTable.Load(CsvParser.Parse(EnemyTable.FileName, Enemies));

        public static UnitTable LoadUnits() => UnitTable.Load(CsvParser.Parse(UnitTable.FileName, Units));

        public static UnitRanges LoadRanges() =>
            UnitRanges.Load(CsvParser.Parse(UnitRanges.FileName, Ranges), LoadUnits());

        public static WaveSchedule LoadWave(string rows, int wave = 1) =>
            WaveSchedule.Load(CsvParser.Parse(WaveSchedule.FileName, WavesHeader + rows), wave, LoadMap(), LoadEnemies());
    }
}
