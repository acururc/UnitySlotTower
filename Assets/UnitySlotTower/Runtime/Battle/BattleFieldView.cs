using System;
using UnityEngine;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 上部の戦闘フィールドの仮表示（T201、plan 2・7章）。マップ CSV のマスを 1 単位の箱で並べ、経路を線で示す。
    /// グリッドの中心をこのオブジェクトの原点に置き、CSV の上の行ほど奥（+z）にする。
    /// </summary>
    public sealed class BattleFieldView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Header("Tables")]
        [SerializeField] private TextAsset mapTable;
        [SerializeField] private TextAsset pathsTable;
        [Tooltip("maps/{id}.csv の id。M3 でウェーブ進行から渡す。")]
        [SerializeField] private string mapId = "w1";

        [Header("View")]
        [SerializeField] private float groundHeight = 0.1f;
        [SerializeField] private float highGroundHeight = 0.5f;
        [SerializeField] private float airPathHeight = 1.2f;
        [SerializeField] private Color groundColor = new Color(0.55f, 0.58f, 0.52f);
        [SerializeField] private Color highGroundColor = new Color(0.45f, 0.5f, 0.68f);
        [SerializeField] private Color blockedColor = new Color(0.18f, 0.18f, 0.2f);
        [SerializeField] private Color spawnColor = new Color(0.85f, 0.25f, 0.2f);
        [SerializeField] private Color goalColor = new Color(0.2f, 0.55f, 0.95f);
        [SerializeField] private Color groundPathColor = new Color(1f, 0.82f, 0.2f);
        [SerializeField] private Color airPathColor = new Color(0.4f, 0.95f, 1f);

        private MaterialPropertyBlock _block;

        public BattleMap Map { get; private set; }

        /// <summary>マスの中心（地面の高さ 0）のワールド座標。</summary>
        public Vector3 CellToWorld(GridPoint point) => transform.TransformPoint(CellToLocal(point));

        /// <summary>マスの箱の上面の中心（カーソルや味方を置く高さ）のワールド座標。</summary>
        public Vector3 CellTopToWorld(GridPoint point) =>
            transform.TransformPoint(CellToLocal(point) + Vector3.up * (TileHeight(Map.TileAt(point)) - groundHeight));

        /// <summary>ワールド座標の真下（フィールドの水平面上）のマス。マップ外なら false。</summary>
        public bool TryGetCell(Vector3 world, out GridPoint cell)
        {
            var local = transform.InverseTransformPoint(world);
            cell = new GridPoint(Mathf.RoundToInt(local.x + (Map.Width - 1) * 0.5f),
                Mathf.RoundToInt((Map.Height - 1) * 0.5f - local.z));
            return Map.Contains(cell);
        }

        private void Awake()
        {
            try
            {
                if (mapTable == null || pathsTable == null)
                {
                    throw new InvalidOperationException("マップか経路の表が設定されていない");
                }

                Map = BattleMap.Load(mapId, mapTable.text, CsvParser.Parse(BattleMap.PathsFileName, pathsTable.text));
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                enabled = false;
                return;
            }

            _block = new MaterialPropertyBlock();
            BuildTiles();
            BuildPaths();
        }

        private Vector3 CellToLocal(GridPoint point) =>
            new Vector3(point.X - (Map.Width - 1) * 0.5f, 0f, (Map.Height - 1) * 0.5f - point.Y);

        private void BuildTiles()
        {
            var root = new GameObject("Tiles").transform;
            root.SetParent(transform, false);
            for (var y = 0; y < Map.Height; y++)
            {
                for (var x = 0; x < Map.Width; x++)
                {
                    var point = new GridPoint(x, y);
                    var tile = Map.TileAt(point);
                    var height = TileHeight(tile);
                    // 隣のマスと見分けられるよう少し隙間をあける。
                    var cube = CreateCube($"Tile {x},{y} {tile}", root, TileColor(tile), true);
                    cube.localScale = new Vector3(0.94f, height, 0.94f);
                    cube.localPosition = CellToLocal(point) + new Vector3(0f, height * 0.5f - groundHeight, 0f);
                }
            }
        }

        private void BuildPaths()
        {
            var root = new GameObject("Paths").transform;
            root.SetParent(transform, false);
            foreach (var path in Map.Paths)
            {
                var color = path.IsAir ? airPathColor : groundPathColor;
                var lift = Vector3.up * (path.IsAir ? airPathHeight : 0.02f);
                for (var i = 1; i < path.Cells.Count; i++)
                {
                    var from = CellToLocal(path.Cells[i - 1]) + lift;
                    var to = CellToLocal(path.Cells[i]) + lift;
                    var segment = CreateCube($"Path {path.Id} {i}", root, color, false);
                    segment.localPosition = (from + to) * 0.5f;
                    segment.localRotation = Quaternion.LookRotation(to - from);
                    segment.localScale = new Vector3(0.12f, 0.02f, 1.12f);
                }
            }
        }

        private float TileHeight(MapTile tile) => tile == MapTile.HighGround ? highGroundHeight : groundHeight;

        private Color TileColor(MapTile tile)
        {
            switch (tile)
            {
                case MapTile.HighGround:
                    return highGroundColor;
                case MapTile.Blocked:
                    return blockedColor;
                case MapTile.Spawn:
                    return spawnColor;
                case MapTile.Goal:
                    return goalColor;
                default:
                    return groundColor;
            }
        }

        private Transform CreateCube(string name, Transform parent, Color color, bool keepCollider)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            if (!keepCollider)
            {
                Destroy(cube.GetComponent<Collider>());
            }

            cube.transform.SetParent(parent, false);
            _block.SetColor(BaseColor, color);
            cube.GetComponent<Renderer>().SetPropertyBlock(_block);
            return cube.transform;
        }
    }
}
