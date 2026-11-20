using System;
using System.Collections.Generic;
using UnityEngine;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Data;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 上部の戦闘フィールドの仮表示（T201、plan 2・7章）。マップ CSV のマスを 1 単位の箱で並べ、経路を線で示す。
    /// グリッドの中心をこのオブジェクトの原点に置き、CSV の上の行ほど奥（+z）にする。
    /// マップは id（maps/&lt;id&gt;.csv）で読み（<see cref="LoadMap"/>）、戦闘マスごとに <see cref="Show"/> で表示を差し替える。
    /// </summary>
    public sealed class BattleFieldView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Header("Tables")]
        [Tooltip("戦闘マップ（maps/*.csv）。名前がマップ id。最初のマップを起動時に表示する。")]
        [SerializeField] private TextAsset[] mapTables;
        [SerializeField] private TextAsset pathsTable;

        [Header("View")]
        [Tooltip("マップ全体が収まるよう、マップを表示するたびに置き直すカメラ（D-71）。未設定なら動かさない。")]
        [SerializeField] private Camera battleCamera;
        [Tooltip("マスや道の箱に使う URP のマテリアル（Greybox.mat）。URP の既定マテリアルは Editor 専用で、未設定だとプレイヤーでマゼンタになる。")]
        [SerializeField] private Material primitiveMaterial;
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
        private CsvTable _paths;
        private Transform _tilesRoot;
        private Transform _pathsRoot;
        private Transform _markersRoot;
        private Battlefield _observed;
        private sealed class PathLine
        {
            public Transform Root;
            public Vector3[] Points;
            public Quaternion[] Headings;
            public Transform[] Arrows;
            public Transform[] Shadows;
            public float Offset;
            public float Remaining;
            public double LastSpawn = double.NegativeInfinity;
        }

        private readonly Dictionary<string, PathLine> _lines = new Dictionary<string, PathLine>();
        private double _battleTime;
        internal bool ShowingAllPaths { get; private set; }

        private sealed class Marker
        {
            public Renderer[] Parts;
            public float Until;
            public Color Normal;
        }
        private readonly System.Collections.Generic.Dictionary<GridPoint, Marker> _exits =
            new System.Collections.Generic.Dictionary<GridPoint, Marker>();
        private readonly System.Collections.Generic.Dictionary<GridPoint, Marker> _bases =
            new System.Collections.Generic.Dictionary<GridPoint, Marker>();

        public bool HasExitMarker(GridPoint cell) => _exits.ContainsKey(cell);
        public bool IsExitFlashing(GridPoint cell) => _exits.TryGetValue(cell, out var m) && m.Until > Time.unscaledTime;
        public bool IsBaseFlashing(GridPoint cell) => _bases.TryGetValue(cell, out var m) && m.Until > Time.unscaledTime;

        public void Observe(Battlefield field)
        {
            if (_observed != null)
            {
                _observed.EnemySpawned -= OnSpawn;
                _observed.BaseReached -= OnArrival;
            }
            _observed = field;
            ResetPathCues();
            if (field != null)
            {
                field.EnemySpawned += OnSpawn;
                field.BaseReached += OnArrival;
            }
            foreach (var marker in _exits.Values) marker.Until = 0f;
            foreach (var marker in _bases.Values) marker.Until = 0f;
        }

        private void OnDestroy()
        {
            _lines.Clear();
            Observe(null);
        }

        private void OnSpawn(MapPath path)
        {
            var cell = path.Cells[0];
            if (ShowingAllPaths) HideAllPaths();
            if (_lines.TryGetValue(path.Id, out var line))
            {
                if (_battleTime - line.LastSpawn >= BattlePresentationTiming.PathCueGap)
                    ShowPath(path.Id, BattlePresentationTiming.PathCue);
                line.LastSpawn = _battleTime;
            }
            if (_exits.TryGetValue(cell, out var marker)) marker.Until = Time.unscaledTime + BattlePresentationTiming.ExitFlash;
        }
        private void OnArrival(GridPoint cell)
        {
            if (_bases.TryGetValue(cell, out var marker)) marker.Until = Time.unscaledTime + BattlePresentationTiming.BaseFlash;
        }

        private void LateUpdate()
        {
            var delta = Time.deltaTime;
            _battleTime += delta;
            foreach (var line in _lines.Values)
            {
                if (!line.Root.gameObject.activeSelf || delta <= 0f) continue;
                line.Remaining -= delta;
                if (line.Remaining <= -BattlePresentationTiming.PathCueFade)
                {
                    line.Root.gameObject.SetActive(false);
                    continue;
                }
                line.Offset = Mathf.Repeat(line.Offset + delta * 1.5f, line.Headings.Length);
                PositionArrows(line);
                if (line.Remaining < 0f)
                    ScaleLine(line, 1f + line.Remaining / BattlePresentationTiming.PathCueFade);
            }
            foreach (var marker in _exits.Values) PaintMarker(marker, Color.white);
            foreach (var marker in _bases.Values) PaintMarker(marker, UnitySlotTower.Ui.UiStyle.Danger);
        }

        private void PaintMarker(Marker marker, Color flash)
        {
            _block.SetColor(BaseColor, marker.Until > Time.unscaledTime ? flash : marker.Normal);
            for (var i = 0; i < marker.Parts.Length; i++) marker.Parts[i].SetPropertyBlock(_block);
        }

        public BattleMap Map { get; private set; }

        /// <summary>マスの中心（地面の高さ 0）のワールド座標。</summary>
        public Vector3 CellToWorld(GridPoint point) => transform.TransformPoint(CellToLocal(point));

        /// <summary>マスの箱の上面の中心（カーソルや味方を置く高さ）のワールド座標。</summary>
        public Vector3 CellTopToWorld(GridPoint point) =>
            transform.TransformPoint(CellToLocal(point) + Vector3.up * (TileHeight(Map.TileAt(point)) - groundHeight));

        /// <summary>
        /// 敵の位置（マス単位の連続値。マス (x, y) の中心が (x, y)）のワールド座標。地上は地面の上面、空中は空中経路の高さ。
        /// </summary>
        public Vector3 PositionToWorld(double x, double y, bool air) =>
            transform.TransformPoint(new Vector3((float)x - (Map.Width - 1) * 0.5f, air ? airPathHeight : 0f,
                (Map.Height - 1) * 0.5f - (float)y));

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
                if (pathsTable == null)
                {
                    throw new InvalidOperationException("経路の表が設定されていない");
                }

                _paths = CsvParser.Parse(BattleMap.PathsFileName, pathsTable.text);
                _block = new MaterialPropertyBlock();
                if (mapTables == null || mapTables.Length == 0 || mapTables[0] == null)
                {
                    throw new InvalidOperationException("マップの表が設定されていない");
                }

                Show(LoadMap(mapTables[0].name));
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                enabled = false;
            }
        }

        /// <summary>マップ id（maps/&lt;id&gt;.csv のファイル名）のマップを読む。表示は変えない。</summary>
        public BattleMap LoadMap(string id)
        {
            foreach (var table in mapTables)
            {
                if (table != null && table.name == id)
                {
                    return BattleMap.Load(id, table.text, _paths);
                }
            }

            throw new InvalidOperationException($"マップ '{id}' の表が設定されていない");
        }

        /// <summary>マップの表示を作り直す（ステージの切り替え）。</summary>
        public void Show(BattleMap map)
        {
            ResetPathCues();
            if (map == Map)
            {
                return;
            }

            if (_tilesRoot != null)
            {
                Destroy(_tilesRoot.gameObject);
                Destroy(_pathsRoot.gameObject);
                Destroy(_markersRoot.gameObject);
            }

            _lines.Clear();
            Map = map;
            _exits.Clear();
            _bases.Clear();
            BuildTiles();
            BuildPaths();
            FitCamera();
        }

        // 戦闘の領域の縦横比は縦長レイアウトで 1440:960 に固定なので、BattleCameraFit の既定値で計算する（plan 16.6・17.9）。
        private void FitCamera()
        {
            if (battleCamera == null)
            {
                return;
            }

            var pose = BattleCameraFit.Fit(Map.Width, Map.Height, height: airPathHeight, sideHeight: highGroundHeight);
            battleCamera.fieldOfView = (float)pose.VerticalFovDegrees;
            battleCamera.transform.SetPositionAndRotation(
                transform.TransformPoint(new Vector3(0f, (float)pose.PositionY, (float)pose.PositionZ)),
                transform.rotation * Quaternion.Euler((float)pose.PitchDegrees, 0f, 0f));
        }

        private Vector3 CellToLocal(GridPoint point) =>
            new Vector3(point.X - (Map.Width - 1) * 0.5f, 0f, (Map.Height - 1) * 0.5f - point.Y);

        private void BuildTiles()
        {
            var root = _tilesRoot = new GameObject("Tiles").transform;
            root.SetParent(transform, false);
            _markersRoot = new GameObject("Exit Markers").transform;
            _markersRoot.SetParent(transform, false);
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
                    if (tile == MapTile.Goal)
                        _bases.Add(point, new Marker { Parts = new[] { cube.GetComponent<Renderer>() }, Normal = goalColor });
                    if (tile == MapTile.Spawn) BuildExitMarker(point, _markersRoot);
                }
            }
        }

        private void BuildPaths()
        {
            var root = _pathsRoot = new GameObject("Paths").transform;
            root.SetParent(transform, false);
            foreach (var path in Map.Paths)
            {
                var segments = path.Cells.Count - 1;
                var line = new PathLine
                {
                    Root = new GameObject(path.Id).transform,
                    Points = new Vector3[path.Cells.Count],
                    Headings = new Quaternion[segments],
                    Arrows = new Transform[segments * 2],
                    Shadows = path.IsAir ? new Transform[segments * 2] : Array.Empty<Transform>()
                };
                line.Root.SetParent(root, false);
                line.Root.gameObject.SetActive(false);
                _lines.Add(path.Id, line);
                // Preserve the serialized brightness of the ground cue while drawing it in white.
                var color = path.IsAir ? airPathColor : Color.white * groundPathColor.maxColorComponent;
                for (var i = 0; i < path.Cells.Count; i++)
                    line.Points[i] = CellToLocal(path.Cells[i]) + Vector3.up * (path.IsAir ? airPathHeight : 0.03f);
                for (var i = 0; i < segments; i++)
                    line.Headings[i] = Quaternion.LookRotation(line.Points[i + 1] - line.Points[i]);

                for (var i = 0; i < line.Arrows.Length; i++)
                {
                    var arrow = line.Arrows[i] = new GameObject("Chevron").transform;
                    arrow.SetParent(line.Root, false);
                    for (var side = -1; side <= 1; side += 2)
                    {
                        var arm = CreateCube("Arm", arrow, color, false);
                        arm.localPosition = new Vector3(side * 0.065f, 0f, 0f);
                        arm.localRotation = Quaternion.Euler(0f, -side * 45f, 0f);
                        arm.localScale = new Vector3(0.045f, 0.015f, 0.2f);
                    }
                    if (path.IsAir)
                    {
                        var dash = line.Shadows[i] = CreateCube("Shadow", line.Root, Color.gray * 0.55f, false);
                        var segment = i / 2;
                        var fraction = i % 2 == 0 ? 0.25f : 0.75f;
                        var point = Vector3.Lerp(line.Points[segment], line.Points[segment + 1], fraction);
                        var cell = path.Cells[fraction < 0.5f ? segment : segment + 1];
                        point.y = TileHeight(Map.TileAt(cell)) - groundHeight + 0.02f;
                        dash.localPosition = point;
                        dash.localRotation = line.Headings[segment];
                    }
                }
            }
        }

        private static void PositionArrows(PathLine line)
        {
            for (var i = 0; i < line.Arrows.Length; i++)
            {
                var along = Mathf.Repeat(i * 0.5f + line.Offset, line.Headings.Length);
                var segment = Mathf.Min((int)along, line.Headings.Length - 1);
                var arrow = line.Arrows[i];
                arrow.localPosition = Vector3.Lerp(line.Points[segment], line.Points[segment + 1], along - segment);
                arrow.localRotation = line.Headings[segment];
            }
        }

        private static void ScaleLine(PathLine line, float scale)
        {
            foreach (var arrow in line.Arrows) arrow.localScale = Vector3.one * scale;
            foreach (var dash in line.Shadows) dash.localScale = new Vector3(0.065f, 0.012f, 0.22f) * scale;
        }

        public bool IsPathVisible(string id) => _lines.TryGetValue(id, out var line) && line.Root.gameObject.activeSelf;
        public bool PathHasGroundShadow(string id) => _lines.TryGetValue(id, out var line) && line.Shadows.Length > 0;

        public void ShowPath(string id, float seconds)
        {
            if (!_lines.TryGetValue(id, out var line)) return;
            line.Remaining = ShowingAllPaths ? float.PositiveInfinity : Mathf.Max(0f, seconds);
            PositionArrows(line);
            ScaleLine(line, 1f);
            line.Root.gameObject.SetActive(true);
        }

        public void ShowAllPaths()
        {
            ShowingAllPaths = true;
            foreach (var id in _lines.Keys) ShowPath(id, float.PositiveInfinity);
        }

        public void HideAllPaths()
        {
            ShowingAllPaths = false;
            foreach (var line in _lines.Values) line.Root.gameObject.SetActive(false);
        }

        private void ResetPathCues()
        {
            HideAllPaths();
            _battleTime = 0d;
            foreach (var line in _lines.Values)
            {
                line.LastSpawn = double.NegativeInfinity;
                line.Offset = 0f;
                line.Remaining = 0f;
            }
        }

        private void BuildExitMarker(GridPoint point, Transform parent)
        {
            var parts = new Renderer[4];
            for (var i = 0; i < 4; i++)
            {
                var edge = CreateCube($"Exit Frame {point.X},{point.Y} {i}", parent, UnitySlotTower.Ui.UiStyle.Danger, false);
                var horizontal = i < 2;
                edge.localScale = horizontal ? new Vector3(0.98f, 0.05f, 0.07f) : new Vector3(0.07f, 0.05f, 0.98f);
                var offset = i % 2 == 0 ? -0.46f : 0.46f;
                edge.localPosition = CellToLocal(point) + new Vector3(horizontal ? 0f : offset, 0.06f, horizontal ? offset : 0f);
                parts[i] = edge.GetComponent<Renderer>();
            }
            _exits.Add(point, new Marker { Parts = parts, Normal = UnitySlotTower.Ui.UiStyle.Danger });
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
            var cubeRenderer = cube.GetComponent<Renderer>();
            if (primitiveMaterial != null)
            {
                cubeRenderer.sharedMaterial = primitiveMaterial;
            }

            _block.SetColor(BaseColor, color);
            cubeRenderer.SetPropertyBlock(_block);
            return cube.transform;
        }
    }
}
