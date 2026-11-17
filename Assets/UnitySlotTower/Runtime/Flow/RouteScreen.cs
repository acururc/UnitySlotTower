using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnitySlotTower.Core.Run;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Ui;
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
        [Tooltip("文字あり版で行き先の短い説明を出すラベル（左・中・右、T359d）。")]
        [SerializeField] private TextModeLabel[] directionDescriptions = new TextModeLabel[3];

        [Header("Look")]
        [SerializeField] private Color ringColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color lineColor = new Color(0.55f, 0.55f, 0.6f, 0.5f);
        [SerializeField] private Color pathColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color nextLineColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float nodeFill = 0.6f;
        [SerializeField] private float lineWidth = 6f;
        [Tooltip("今のマスから行ける線の太さ（lineWidth の倍率、FR-073）。")]
        [SerializeField] private float nextLineWidthRate = 1.8f;
        [Tooltip("通ってきた道の線の太さ（lineWidth の倍率、FR-073）。")]
        [SerializeField] private float pathLineWidthRate = 0.6f;
        [Tooltip("それ以外の線の不透明度（FR-073）。")]
        [SerializeField, Range(0f, 1f)] private float faintLineAlpha = 0.15f;
        [Tooltip("今から行けないマスの不透明度（FR-073）。")]
        [SerializeField, Range(0f, 1f)] private float unreachableNodeAlpha = 0.22f;
        [Tooltip("通ったマスの小さな印の大きさ（マスに対する割合）。")]
        [SerializeField, Range(0f, 1f)] private float visitedMarkSize = 0.32f;
        [SerializeField, Range(0f, 1f)] private float emptyDotAlpha = 0.2f;

        [Header("Move (FR-064)")]
        [Tooltip("行き先が決まった後、今のマスから行き先へ印が線に沿って進む秒数（実時間）。")]
        [SerializeField] private float moveSeconds = 0.5f;
        [Tooltip("印が行き先に着いてから次の画面に移るまでの秒数（実時間）。")]
        [SerializeField] private float arriveHoldSeconds = 0.4f;
        [SerializeField] private Color markerColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float markerSize = 0.55f;

        private sealed class NodeView
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public RectTransform Ring;
            public GameObject VisitedMark;
        }

        /// <summary>ルートの線の見た目（FR-073）。</summary>
        public enum LineLook
        {
            /// <summary>今のマスから行ける線（太く明るい）。</summary>
            Next,
            /// <summary>通ってきた道（細い）。</summary>
            Path,
            /// <summary>それ以外（薄い）。</summary>
            Faint,
        }

        private readonly Dictionary<RouteNode, NodeView> _nodes = new Dictionary<RouteNode, NodeView>();
        private readonly List<(RouteNode From, RouteNode To, Image Line)> _lines = new List<(RouteNode, RouteNode, Image)>();
        private readonly Dictionary<(RouteNode, RouteNode), LineLook> _lineLooks = new Dictionary<(RouteNode, RouteNode), LineLook>();
        private readonly HashSet<RouteNode> _reachable = new HashSet<RouteNode>();
        private readonly Stack<RouteNode> _walk = new Stack<RouteNode>();
        private readonly List<RectTransform> _pulsing = new List<RectTransform>();
        private readonly int[] _shownGauges = new int[ColorCount];
        private RouteMap _builtFor;
        private Vector2 _builtSize;
        private IReadOnlyList<RouteNode> _visited;
        private RunState _state;
        private int _shownLife = -1;
        private int _shownMoney = -1;
        private int _shownCoins = -1;
        private RectTransform _marker;
        private RouteNode _moveFrom;
        private RouteNode _moveTo;
        private float _moveElapsed;
        private Action _onArrived;

        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary>行き先へ進む移動のアニメ中か（<see cref="BeginMove"/> から次の画面に移るまで。この間は操作を受けない）。</summary>
        public bool IsMoving => _onArrived != null;

        /// <summary>移動のアニメの印（テスト・撮影用。作る前は null）。</summary>
        public RectTransform Marker => _marker;

        /// <summary>行き先の矢印（左・中・右）。</summary>
        public IReadOnlyList<Button> ArrowButtons => arrowButtons;

        /// <summary>マスのアイコンの丸と箱の絵（説明でアイコンを作るのに使う: T369）。</summary>
        public Sprite KnobSprite => knobSprite;

        public Sprite BoxSprite => boxSprite;

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
            // マップの枠の大きさは文字あり版で変わる（T359d）。線とマスの大きさは作った時の大きさで決まるので作り直す。
            if (_builtFor != state.Route || mapArea.rect.size != _builtSize)
            {
                Build(state.Route);
            }

            _visited = visited;
            Refresh(visited);
            RefreshStatus(true);
        }

        public void Hide()
        {
            panel.SetActive(false);
            _pulsing.Clear();
            _visited = null;
            _onArrived = null;
            if (_marker != null)
            {
                _marker.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 今のマスから <paramref name="to"/> へ印が線に沿って進むアニメを見せ、着いて少し待ってから <paramref name="onArrived"/> を呼ぶ
        /// （D-90、plan 17.8）。アニメ中は行き先の矢印を押せない。画面が開いていなければすぐ呼ぶ。
        /// </summary>
        public void BeginMove(RouteNode to, Action onArrived)
        {
            if (onArrived == null)
            {
                throw new ArgumentNullException(nameof(onArrived));
            }

            var from = _state?.Current;
            if (!IsOpen || from == null || to == null || _marker == null)
            {
                onArrived();
                return;
            }

            _moveFrom = from;
            _moveTo = to;
            _moveElapsed = 0f;
            _onArrived = onArrived;

            // 行き先だけを光らせ、そこへの線を通ってきた道の色にする。
            _pulsing.Clear();
            foreach (var pair in _nodes)
            {
                var keep = pair.Key == from || pair.Key == to;
                pair.Value.Ring.gameObject.SetActive(keep);
                pair.Value.Ring.localScale = Vector3.one;
                if (pair.Key == to)
                {
                    pair.Value.Group.alpha = 1f;
                    _pulsing.Add(pair.Value.Ring);
                }
            }

            foreach (var (lineFrom, lineTo, line) in _lines)
            {
                if (lineFrom == from)
                {
                    SetLine(lineFrom, lineTo, line, lineTo == to ? LineLook.Next : LineLook.Faint);
                }
            }

            for (var reel = 0; reel < arrowButtons.Length; reel++)
            {
                if (arrowButtons[reel] != null)
                {
                    arrowButtons[reel].interactable = false;
                }

                if (arrowGroups[reel] != null && from.NextOnReel(reel) != to)
                {
                    arrowGroups[reel].alpha = 0.25f;
                }
            }

            PlaceMarker(0f);
            _marker.gameObject.SetActive(true);
        }

        private void PlaceMarker(float t)
        {
            var route = _state.Route;
            var point = Vector2.Lerp(Normalized(route, _moveFrom), Normalized(route, _moveTo), Mathf.SmoothStep(0f, 1f, t));
            _marker.anchorMin = _marker.anchorMax = point;
            _marker.anchoredPosition = Vector2.zero;
        }

        private void UpdateMove()
        {
            _moveElapsed += Time.unscaledDeltaTime;
            PlaceMarker(moveSeconds > 0f ? Mathf.Clamp01(_moveElapsed / moveSeconds) : 1f);
            if (_moveElapsed < moveSeconds + arriveHoldSeconds)
            {
                return;
            }

            var arrived = _onArrived;
            _onArrived = null;
            _marker.gameObject.SetActive(false);
            arrived();
        }

        /// <summary>そのマスが行き先として光っているか（テスト用）。</summary>
        public bool IsHighlighted(RouteNode node) => _nodes.TryGetValue(node, out var view) && _pulsing.Contains(view.Ring);

        /// <summary>from から to への線の見た目（テスト用、FR-073）。線がなければ null。</summary>
        public LineLook? LookOf(RouteNode from, RouteNode to) => _lineLooks.TryGetValue((from, to), out var look) ? look : null;

        /// <summary>今から行けないマスとして暗くしているか（テスト用、FR-073）。</summary>
        public bool IsUnreachable(RouteNode node) => _nodes.ContainsKey(node) && _state?.Current != null
            && node.Floor > _state.Current.Floor && !_reachable.Contains(node);

        /// <summary>通ったマスの印を出しているか（テスト用）。</summary>
        public bool HasVisitedMark(RouteNode node) => _nodes.TryGetValue(node, out var view) && view.VisitedMark.activeSelf;

        private void Update()
        {
            if (!IsOpen || _state == null)
            {
                return;
            }

            // 開いたまま文字あり／なしが切り替わった（移動のアニメ中は作り直さない）。
            if (!IsMoving && mapArea.rect.size != _builtSize)
            {
                Build(_state.Route);
                Refresh(_visited);
            }

            // 一時停止中（timeScale 0）でも光らせる。
            var scale = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
            foreach (var ring in _pulsing)
            {
                ring.localScale = new Vector3(scale, scale, 1f);
            }

            RefreshStatus(false);
            if (IsMoving)
            {
                UpdateMove();
            }
        }

        private void Build(RouteMap route)
        {
            for (var i = mapArea.childCount - 1; i >= 0; i--)
            {
                Destroy(mapArea.GetChild(i).gameObject);
            }

            _nodes.Clear();
            _lines.Clear();
            _lineLooks.Clear();
            _builtFor = route;

            var size = mapArea.rect.size;
            _builtSize = size;
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
                // 通ったマスの小さな印（右下、通ってきた道の色）。
                var mark = RouteNodeIcons.CreateImage("Visited Mark", root, new Vector2(1f - visitedMarkSize, 0f),
                    new Vector2(1f, visitedMarkSize), pathColor, knobSprite).gameObject;
                mark.SetActive(false);
                _nodes[node] = new NodeView
                {
                    Root = root,
                    Group = root.gameObject.AddComponent<CanvasGroup>(),
                    Ring = ring,
                    VisitedMark = mark,
                };
            }

            // 移動のアニメの印（マスより手前）。縁取りの金の円の上に白い円。
            _marker = RouteNodeIcons.CreateRect("Marker", nodes, Vector2.zero, Vector2.zero);
            _marker.sizeDelta = new Vector2(cell, cell) * markerSize;
            RouteNodeIcons.CreateImage("Rim", _marker, new Vector2(-0.15f, -0.15f), new Vector2(1.15f, 1.15f), ringColor, knobSprite);
            RouteNodeIcons.CreateImage("Dot", _marker, Vector2.zero, Vector2.one, markerColor, knobSprite);
            _marker.gameObject.SetActive(false);
        }

        // 階は下から上、列は左から右。マスはその区画の中心（3列の格子なので、まっすぐと斜めがそのまま見分けられる: D-88）。
        private static Vector2 Normalized(RouteMap route, RouteNode node) =>
            new Vector2((node.Column + 0.5f) / route.Columns, (node.Floor - 0.5f) / route.Floors);

        private void Refresh(IReadOnlyList<RouteNode> visited)
        {
            var current = _state.Current;
            _pulsing.Clear();
            CollectReachable(current);
            foreach (var pair in _nodes)
            {
                var node = pair.Key;
                var view = pair.Value;
                var isNext = current != null && Contains(current.Next, node);
                var isCurrent = node == current;
                var wasVisited = Contains(visited, node);
                // 今から行けないマス（今の階より上で、今のマスからたどれない）は暗くする（FR-073）。
                view.Group.alpha = isCurrent || isNext ? 1f
                    : wasVisited ? 0.55f
                    : current != null && node.Floor <= current.Floor ? 0.2f
                    : current != null && !_reachable.Contains(node) ? unreachableNodeAlpha
                    : 0.8f;
                view.Ring.gameObject.SetActive(isCurrent || isNext);
                view.VisitedMark.SetActive(wasVisited && !isCurrent);
                view.Ring.localScale = Vector3.one;
                view.Root.localScale = isCurrent ? new Vector3(1.15f, 1.15f, 1f) : Vector3.one;
                if (isNext)
                {
                    _pulsing.Add(view.Ring);
                }
            }

            foreach (var (from, to, line) in _lines)
            {
                SetLine(from, to, line, current != null && from == current ? LineLook.Next
                    : IsStep(visited, from, to) ? LineLook.Path
                    : LineLook.Faint);
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

                SetLabel(directionLabels, reel, target != null ? "node_" + target.Kind : null);
                SetLabel(directionDescriptions, reel, target != null ? "node_" + target.Kind + "_desc" : null);

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

        private void SetLine(RouteNode from, RouteNode to, Image line, LineLook look)
        {
            _lineLooks[(from, to)] = look;
            var faint = lineColor;
            faint.a = faintLineAlpha;
            line.color = look == LineLook.Next ? nextLineColor : look == LineLook.Path ? pathColor : faint;
            var width = look == LineLook.Next ? lineWidth * nextLineWidthRate
                : look == LineLook.Path ? lineWidth * pathLineWidthRate
                : lineWidth;
            line.rectTransform.sizeDelta = new Vector2(line.rectTransform.sizeDelta.x, width);
        }

        // 今のマスから線をたどって行けるマス（今のマスは含まない）。今のマスがなければ空。
        private void CollectReachable(RouteNode current)
        {
            _reachable.Clear();
            if (current == null)
            {
                return;
            }

            _walk.Clear();
            _walk.Push(current);
            while (_walk.Count > 0)
            {
                foreach (var next in _walk.Pop().Next)
                {
                    if (_reachable.Add(next))
                    {
                        _walk.Push(next);
                    }
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
                moneyText.SetText(NumericText.YenFormat, _shownMoney);
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

        // id が null なら文言なし（文字あり版でも出さない）。
        private static void SetLabel(TextModeLabel[] labels, int index, string id)
        {
            if (labels == null || index >= labels.Length || labels[index] == null)
            {
                return;
            }

            if (id != null)
            {
                labels[index].SetIds(id);
            }
            else
            {
                labels[index].SetIds();
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
