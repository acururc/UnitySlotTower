using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// ルート画面（plan 16.7、D-75）。上6割を覆う2Dのオーバーレイに Slay the Spire 風のマップ（下から上へ階、マスは種類のアイコン、
    /// つながりは線、今のマスと行き先が光る）、上端にライフ・所持金・コイン・ゲージ、下端（筐体の各リールの真上）に
    /// 行き先の矢印とアイコンを出す。行き先は筐体のリールで選ぶ（矢印のクリックも同じ）。進行は RunController が持つ。
    /// </summary>
    public sealed class RouteScreen : MonoBehaviour
    {
        private const int ColorCount = 4;

        [Tooltip("開いているあいだ出す本体（戦闘フィールドと操作バーへのクリックを遮る）。")]
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform mapArea;
        [SerializeField] private Sprite knobSprite;
        [SerializeField] private Sprite boxSprite;

        [Header("Status")]
        [SerializeField] private TMP_Text lifeText;
        [SerializeField] private TMP_Text moneyText;
        [SerializeField] private TMP_Text coinsText;
        [Tooltip("ゲージの点。色7（赤・青・緑・黄）の順に同じ数ずつ。")]
        [SerializeField] private Image[] gaugeDots;

        [Header("Directions (left, center, right)")]
        [SerializeField] private Button[] arrowButtons = new Button[3];
        [SerializeField] private CanvasGroup[] arrowGroups = new CanvasGroup[3];
        [Tooltip("行き先のアイコンを入れる枠。")]
        [SerializeField] private RectTransform[] arrowTargets = new RectTransform[3];
        [Tooltip("文字あり版で行き先の種類を出すラベル（左・中・右、FR-006）。")]
        [SerializeField] private TextModeLabel[] directionLabels = new TextModeLabel[3];

        [Header("Look")]
        [SerializeField] private Color ringColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color lineColor = new Color(0.55f, 0.55f, 0.6f, 0.5f);
        [SerializeField] private Color pathColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color nextLineColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float nodeFill = 0.6f;
        [SerializeField] private float lineWidth = 6f;
        [SerializeField, Range(0f, 1f)] private float emptyDotAlpha = 0.2f;

        private sealed class NodeView
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public RectTransform Ring;
        }

        private readonly Dictionary<RouteNode, NodeView> _nodes = new Dictionary<RouteNode, NodeView>();
        private readonly List<(RouteNode From, RouteNode To, Image Line)> _lines = new List<(RouteNode, RouteNode, Image)>();
        private readonly List<RectTransform> _pulsing = new List<RectTransform>();
        private readonly int[] _shownGauges = new int[ColorCount];
        private RouteMap _builtFor;
        private RunState _state;
        private int _shownLife = -1;
        private int _shownMoney = -1;
        private int _shownCoins = -1;

        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary>行き先の矢印（左・中・右）。</summary>
        public IReadOnlyList<Button> ArrowButtons => arrowButtons;

        /// <summary>マップに並べたマスの数（作る前は0）。</summary>
        public int NodeViewCount => _nodes.Count;

        /// <summary>行き先の矢印がクリックされた（リールの番号）。</summary>
        public event Action<int> ArrowClicked;

        private void Awake()
        {
            for (var i = 0; i < arrowButtons.Length; i++)
            {
                if (arrowButtons[i] == null)
                {
                    continue;
                }

                var reel = i;
                arrowButtons[i].onClick.AddListener(() =>
                {
                    if (EventSystem.current != null)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                    }

                    ArrowClicked?.Invoke(reel);
                });
            }
        }

        /// <summary>今のマスから先を選ぶ状態でルート画面を開く。<paramref name="visited"/> は入った順のマス。</summary>
        public void Show(RunState state, IReadOnlyList<RouteNode> visited)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            panel.SetActive(true);
            if (_builtFor != state.Route)
            {
                Build(state.Route);
            }

            Refresh(visited);
            RefreshStatus(true);
        }

        public void Hide()
        {
            panel.SetActive(false);
            _pulsing.Clear();
        }

        /// <summary>そのマスが行き先として光っているか（テスト用）。</summary>
        public bool IsHighlighted(RouteNode node) => _nodes.TryGetValue(node, out var view) && _pulsing.Contains(view.Ring);

        private void Update()
        {
            if (!IsOpen || _state == null)
            {
                return;
            }

            // 一時停止中（timeScale 0）でも光らせる。
            var scale = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
            foreach (var ring in _pulsing)
            {
                ring.localScale = new Vector3(scale, scale, 1f);
            }

            RefreshStatus(false);
        }

        private void Build(RouteMap route)
        {
            for (var i = mapArea.childCount - 1; i >= 0; i--)
            {
                Destroy(mapArea.GetChild(i).gameObject);
            }

            _nodes.Clear();
            _lines.Clear();
            _builtFor = route;

            var size = mapArea.rect.size;
            var cell = Mathf.Min(size.x / route.Columns, size.y / route.Floors) * nodeFill;
            var lines = RouteNodeIcons.CreateRect("Lines", mapArea, Vector2.zero, Vector2.one);
            var nodes = RouteNodeIcons.CreateRect("Nodes", mapArea, Vector2.zero, Vector2.one);

            foreach (var node in route.All)
            {
                foreach (var next in node.Next)
                {
                    var line = RouteNodeIcons.CreateImage($"Line {node.Floor}-{node.Column} {next.Floor}-{next.Column}", lines,
                        Vector2.zero, Vector2.zero, lineColor, null);
                    var from = Vector2.Scale(Normalized(route, node), size);
                    var to = Vector2.Scale(Normalized(route, next), size);
                    var rect = line.rectTransform;
                    rect.anchoredPosition = (from + to) / 2f;
                    rect.sizeDelta = new Vector2(Vector2.Distance(from, to), lineWidth);
                    rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
                    _lines.Add((node, next, line));
                }
            }

            foreach (var node in route.All)
            {
                var point = Normalized(route, node);
                var root = RouteNodeIcons.CreateRect($"Node {node.Floor}-{node.Column} {node.Kind}", nodes, point, point);
                var bossScale = node.Kind == RouteNodeKinds.Boss ? 1.4f : 1f;
                root.sizeDelta = new Vector2(cell, cell) * bossScale;
                var ring = RouteNodeIcons.CreateImage("Ring", root, new Vector2(-0.18f, -0.18f), new Vector2(1.18f, 1.18f),
                    ringColor, knobSprite).rectTransform;
                RouteNodeIcons.Create(root, node.Kind, knobSprite, boxSprite);
                _nodes[node] = new NodeView
                {
                    Root = root,
                    Group = root.gameObject.AddComponent<CanvasGroup>(),
                    Ring = ring,
                };
            }
        }

        // 階は下から上、列は左から右。マスはその区画の中心。
        private static Vector2 Normalized(RouteMap route, RouteNode node) =>
            new Vector2((node.Column + 0.5f) / route.Columns, (node.Floor - 0.5f) / route.Floors);

        private void Refresh(IReadOnlyList<RouteNode> visited)
        {
            var current = _state.Current;
            _pulsing.Clear();
            foreach (var pair in _nodes)
            {
                var node = pair.Key;
                var view = pair.Value;
                var isNext = current != null && Contains(current.Next, node);
                var isCurrent = node == current;
                var wasVisited = Contains(visited, node);
                view.Group.alpha = isCurrent || isNext ? 1f
                    : wasVisited ? 0.55f
                    : current != null && node.Floor <= current.Floor ? 0.2f
                    : 0.8f;
                view.Ring.gameObject.SetActive(isCurrent || isNext);
                view.Ring.localScale = Vector3.one;
                view.Root.localScale = isCurrent ? new Vector3(1.15f, 1.15f, 1f) : Vector3.one;
                if (isNext)
                {
                    _pulsing.Add(view.Ring);
                }
            }

            foreach (var (from, to, line) in _lines)
            {
                var onPath = IsStep(visited, from, to);
                line.color = onPath ? pathColor : from == current ? nextLineColor : lineColor;
                line.rectTransform.sizeDelta = new Vector2(line.rectTransform.sizeDelta.x, onPath ? lineWidth * 1.6f : lineWidth);
            }

            for (var reel = 0; reel < arrowButtons.Length; reel++)
            {
                var target = current?.NextOnReel(reel);
                if (arrowButtons[reel] != null)
                {
                    arrowButtons[reel].interactable = target != null;
                }

                if (arrowGroups[reel] != null)
                {
                    arrowGroups[reel].alpha = target != null ? 1f : 0.25f;
                }

                if (reel < directionLabels.Length && directionLabels[reel] != null)
                {
                    if (target != null)
                    {
                        directionLabels[reel].SetIds("node_" + target.Kind);
                    }
                    else
                    {
                        directionLabels[reel].SetIds();
                    }
                }

                var frame = arrowTargets[reel];
                if (frame == null)
                {
                    continue;
                }

                for (var i = frame.childCount - 1; i >= 0; i--)
                {
                    Destroy(frame.GetChild(i).gameObject);
                }

                if (target != null)
                {
                    RouteNodeIcons.Create(frame, target.Kind, knobSprite, boxSprite);
                }
            }
        }

        private void RefreshStatus(bool force)
        {
            if (force || _state.Life != _shownLife)
            {
                _shownLife = _state.Life;
                lifeText.SetText("{0}", _shownLife);
            }

            if (force || _state.Money.Coins != _shownMoney)
            {
                _shownMoney = _state.Money.Coins;
                moneyText.SetText("{0}", _shownMoney);
            }

            if (force || _state.Coins.Coins != _shownCoins)
            {
                _shownCoins = _state.Coins.Coins;
                coinsText.SetText("{0}", _shownCoins);
            }

            if (gaugeDots == null || gaugeDots.Length < ColorCount)
            {
                return;
            }

            var perColor = gaugeDots.Length / ColorCount;
            for (var c = 0; c < ColorCount; c++)
            {
                var level = _state.Gauges.Level((SlotSymbol)c);
                if (!force && level == _shownGauges[c])
                {
                    continue;
                }

                _shownGauges[c] = level;
                for (var k = 0; k < perColor; k++)
                {
                    var dot = gaugeDots[c * perColor + k];
                    dot.gameObject.SetActive(k < _state.Gauges.Max);
                    var color = dot.color;
                    color.a = k < level ? 1f : emptyDotAlpha;
                    dot.color = color;
                }
            }
        }

        private static bool Contains(IReadOnlyList<RouteNode> list, RouteNode node)
        {
            if (list == null)
            {
                return false;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == node)
                {
                    return true;
                }
            }

            return false;
        }

        // 入った順で from の次に to に入ったか（通ってきた道）。
        private static bool IsStep(IReadOnlyList<RouteNode> visited, RouteNode from, RouteNode to)
        {
            if (visited == null)
            {
                return false;
            }

            for (var i = 0; i + 1 < visited.Count; i++)
            {
                if (visited[i] == from && visited[i + 1] == to)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
