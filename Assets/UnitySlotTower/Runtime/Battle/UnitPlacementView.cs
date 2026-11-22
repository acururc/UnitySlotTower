using System.Collections.Generic;
using UnityEngine;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 配置操作の仮表示（T208）: マスを選ぶ間は置けるマス全部に緑の板、カーソルは置けるマスの上だけ別の色（T612、FR-115）。
    /// 置けないマスの上ではカーソルを出さない。向きを選ぶ間は向きの矢印と攻撃範囲の見本。
    /// 撤退の対象に選んだ配置中の味方には枠とその味方の攻撃範囲を出す（T308、D-82）。配置済みの味方と敵は BattleActorsView（T209）が表示する。
    /// </summary>
    public sealed class UnitPlacementView : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private UnitPlacementInput placementInput;
        [Tooltip("目印の箱に使う URP のマテリアル（Greybox.mat）。URP の既定マテリアルは Editor 専用で、未設定だとプレイヤーでマゼンタになる。")]
        [SerializeField] private Material primitiveMaterial;
        [SerializeField] private Color placeableColor = new Color(0.3f, 0.95f, 0.4f);
        [Tooltip("カーソルの下の置けるマスの色（T612、FR-115）。置けるマスの緑と見分けられる色にする。")]
        [SerializeField] private Color hoverColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color rangeColor = new Color(1f, 0.6f, 0.15f);
        [SerializeField] private Color selectedUnitColor = new Color(1f, 0.95f, 0.4f);

        private readonly List<Transform> _rangeMarkers = new List<Transform>();
        private readonly List<Transform> _placeableMarkers = new List<Transform>();
        private MaterialPropertyBlock _block;
        private Transform _cursor;
        private Transform _arrow;
        private Transform _selectedMarker;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _cursor = CreateCube("Placement Cursor", transform, hoverColor);
            _cursor.localScale = new Vector3(0.9f, 0.04f, 0.9f);
            _arrow = CreateCube("Facing Arrow", transform, placeableColor);
            _arrow.localScale = new Vector3(0.15f, 0.06f, 0.7f);
            _selectedMarker = CreateCube("Selected Unit Marker", transform, selectedUnitColor);
            _selectedMarker.localScale = new Vector3(1f, 0.03f, 1f);
        }

        private void LateUpdate()
        {
            var deployment = battle.Deployment;
            if (deployment == null)
            {
                return;
            }

            var selected = placementInput.SelectedUnit;
            ShowPlacement(deployment, selected);
            _selectedMarker.gameObject.SetActive(selected != null);
            if (selected != null)
            {
                _selectedMarker.position = fieldView.CellTopToWorld(selected.Cell) + Vector3.up * 0.015f;
            }
        }

        private void ShowPlacement(UnitDeployment deployment, BattleUnit selected)
        {
            var active = deployment.Phase != DeployPhase.Idle;
            // 向きを選ぶ間のカーソルは決めたマス（置ける）にある。マスを選ぶ間は置けるマスの上だけ出す。
            _cursor.gameObject.SetActive(active && deployment.CanPlace(deployment.Cursor));
            _arrow.gameObject.SetActive(deployment.Phase == DeployPhase.ChoosingFacing);
            var rangeCount = 0;
            if (active)
            {
                var top = fieldView.CellTopToWorld(deployment.Cursor);
                _cursor.position = top + Vector3.up * 0.03f;

                var direction = Direction(deployment.Facing);
                _arrow.position = top + Vector3.up * 0.1f + direction * 0.45f;
                _arrow.rotation = Quaternion.LookRotation(direction);

                if (deployment.Phase == DeployPhase.ChoosingFacing)
                {
                    foreach (var cell in deployment.PreviewRange)
                    {
                        if (fieldView.Map.Contains(cell))
                        {
                            RangeMarker(rangeCount++).position = fieldView.CellTopToWorld(cell) + Vector3.up * 0.02f;
                        }
                    }
                }
            }
            else if (selected != null)
            {
                // 配置した後の範囲は選んだ味方の分だけ出す（D-82）。
                foreach (var cell in selected.RangeCells)
                {
                    if (fieldView.Map.Contains(cell))
                    {
                        RangeMarker(rangeCount++).position = fieldView.CellTopToWorld(cell) + Vector3.up * 0.02f;
                    }
                }
            }

            for (var i = 0; i < _rangeMarkers.Count; i++)
            {
                _rangeMarkers[i].gameObject.SetActive(i < rangeCount);
            }

            ShowPlaceableCells(deployment);
        }

        /// <summary>マスを選ぶ間、置けるマス全部に緑の薄い板を出す（T612、FR-115）。板は使い回す。</summary>
        private void ShowPlaceableCells(UnitDeployment deployment)
        {
            var count = 0;
            if (deployment.Phase == DeployPhase.ChoosingCell)
            {
                var map = fieldView.Map;
                for (var y = 0; y < map.Height; y++)
                {
                    for (var x = 0; x < map.Width; x++)
                    {
                        var cell = new GridPoint(x, y);
                        if (deployment.CanPlace(cell))
                        {
                            PlaceableMarker(count++).position = fieldView.CellTopToWorld(cell) + Vector3.up * 0.01f;
                        }
                    }
                }
            }

            for (var i = 0; i < _placeableMarkers.Count; i++)
            {
                _placeableMarkers[i].gameObject.SetActive(i < count);
            }
        }

        private Transform PlaceableMarker(int index)
        {
            if (index == _placeableMarkers.Count)
            {
                var marker = CreateCube("Placeable Cell", transform, placeableColor);
                marker.localScale = new Vector3(0.82f, 0.02f, 0.82f);
                _placeableMarkers.Add(marker);
            }

            return _placeableMarkers[index];
        }

        /// <summary>向きのワールド方向（フィールドの +z が画面の奥＝マップの上）。</summary>
        private Vector3 Direction(Facing facing)
        {
            switch (facing)
            {
                case Facing.Down:
                    return -fieldView.transform.forward;
                case Facing.Left:
                    return -fieldView.transform.right;
                case Facing.Up:
                    return fieldView.transform.forward;
                default:
                    return fieldView.transform.right;
            }
        }

        private Transform RangeMarker(int index)
        {
            if (index == _rangeMarkers.Count)
            {
                var marker = CreateCube("Range Preview", transform, rangeColor);
                marker.localScale = new Vector3(0.7f, 0.02f, 0.7f);
                _rangeMarkers.Add(marker);
            }

            return _rangeMarkers[index];
        }

        private Transform CreateCube(string name, Transform parent, Color color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            // 配置のレイキャストがマスの箱だけに当たるよう、目印には当たり判定を持たせない。
            Destroy(cube.GetComponent<Collider>());
            cube.transform.SetParent(parent, false);
            var cubeRenderer = cube.GetComponent<Renderer>();
            if (primitiveMaterial != null)
            {
                cubeRenderer.sharedMaterial = primitiveMaterial;
            }

            SetColor(cubeRenderer, color);
            return cube.transform;
        }

        private void SetColor(Renderer target, Color color)
        {
            _block.SetColor(BaseColor, color);
            target.SetPropertyBlock(_block);
        }
    }
}
