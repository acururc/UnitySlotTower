using UnityEngine;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Guide;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// 文字なし説明の表示（T403・T351、FR-004、plan 16.10）。進行は <see cref="GuideSequence"/> が決め、ここは毎フレーム様子を渡して
    /// 結果を描く: 対象の周りだけを残して画面を薄暗くし（暗い部分はクリックを通さない）、対象を光る枠で囲み、
    /// 手（白い指先の三角と、押した所に広がる輪）のアニメで操作を示す。向き選びでは4方向の矢印も出す。
    /// 説明中は戦闘の時間を止める（<see cref="BattleController.IsHeldByGuide"/>）。ショップ・回復の画面中は出さない。
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class GuideOverlay : MonoBehaviour
    {
        private const float TapSeconds = 1.2f;
        private const float DragSeconds = 1.8f;

        [SerializeField] private BattleController battle;
        [SerializeField] private UnitPlacementInput placementInput;
        [SerializeField] private SlotCabinet slotCabinet;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private Camera battleCamera;
        [SerializeField] private RunController runController;
        [SerializeField] private RouteScreen routeScreen;

        [Header("Targets")]
        [Tooltip("操作バーの味方アイコン（units.csv の並び）。")]
        [SerializeField] private RectTransform[] unitIcons = new RectTransform[4];
        [Tooltip("操作バーの強化アイコン（units.csv の並び）。")]
        [SerializeField] private RectTransform[] upgradeIcons = new RectTransform[4];
        [SerializeField] private RectTransform retreatButton;

        [Header("View")]
        [Tooltip("説明を出しているあいだだけ表示する全体（画面いっぱい、Portrait Root の前面）。")]
        [SerializeField] private RectTransform root;
        [Tooltip("上・下・左・右の暗幕。対象の穴の周りに並べる。")]
        [SerializeField] private RectTransform[] dims = new RectTransform[4];
        [Tooltip("対象を囲む光る枠。")]
        [SerializeField] private RectTransform frame;
        [SerializeField] private CanvasGroup frameGroup;
        [Tooltip("手（指先が pivot）。")]
        [SerializeField] private RectTransform hand;
        [Tooltip("押した所に広がる輪。")]
        [SerializeField] private RectTransform ripple;
        [SerializeField] private CanvasGroup rippleGroup;
        [Tooltip("向き選びの矢印（右・上・左・下）。")]
        [SerializeField] private RectTransform[] facingArrows = new RectTransform[4];
        [SerializeField] private float framePadding = 12f;
        [SerializeField] private float rippleSize = 120f;

        private readonly GuideSequence _sequence = new GuideSequence();
        private readonly Vector3[] _corners = new Vector3[4];
        private readonly RectTransform[] _skillButtons = new RectTransform[4];
        private Canvas _canvas;
        private GuideHint _shownHint = (GuideHint)(-1);
        private float _hintStarted;

        /// <summary>テスト用: true のあいだに読み込まれた Game シーンでは説明を出さない（既存の操作テストを妨げないため）。</summary>
        public static bool DisabledForTests { get; set; }

        /// <summary>いま見せている説明。</summary>
        public GuideHint Current => enabled ? _sequence.Current : GuideHint.None;

        public GuideSequence Sequence => _sequence;

        private void Awake()
        {
            _canvas = root.GetComponentInParent<Canvas>();
            for (var i = 0; i < unitIcons.Length && i < _skillButtons.Length; i++)
            {
                var icon = unitIcons[i] != null ? unitIcons[i].GetComponent<UnitBarIcon>() : null;
                _skillButtons[i] = icon != null ? icon.SkillButtonRect : null;
            }

            root.gameObject.SetActive(false);
            if (DisabledForTests)
            {
                enabled = false;
            }
        }

        private void OnDisable()
        {
            if (battle != null)
            {
                battle.IsHeldByGuide = false;
                battle.FullRefundRetreat = false;
            }

            root.gameObject.SetActive(false);
        }

        private void Update()
        {
            var run = battle.Run;
            if (run == null)
            {
                return;
            }

            var hint = _sequence.Update(Observe(run));
            battle.IsHeldByGuide = _sequence.HoldsBattle;
            battle.FullRefundRetreat = _sequence.ShowsRetreat;
            if (hint != _shownHint)
            {
                _shownHint = hint;
                _hintStarted = Time.unscaledTime;
                root.gameObject.SetActive(hint != GuideHint.None);
            }

            if (hint != GuideHint.None)
            {
                Draw(hint, run, Time.unscaledTime - _hintStarted);
            }
        }

        private GuideObservation Observe(BattleNodeRun run)
        {
            var units = battle.Units.All;
            var field = run.Field.Units;
            var alive = 0;
            for (var i = 0; i < field.Count; i++)
            {
                if (field[i].IsAlive)
                {
                    alive++;
                }
            }

            var upgradeSlot = -1;
            var skillSlot = -1;
            var levels = 0;
            var fighting = run.Phase == WavePhase.Preparing || run.Phase == WavePhase.Fighting;
            for (var slot = 0; slot < units.Count; slot++)
            {
                var id = units[slot].Id;
                levels += run.Upgrades.Level(id);
                var deployed = IsDeployed(field, id);
                if (upgradeSlot < 0 && slot < upgradeIcons.Length && deployed
                    && run.Upgrades.Check(id) == UpgradeResult.Upgraded)
                {
                    upgradeSlot = slot;
                }

                // 発動できる条件（plan 16.4）: 満タン・その職が配置中・準備中か戦闘中。
                if (skillSlot < 0 && fighting && deployed && slot < _skillButtons.Length && _skillButtons[slot] != null
                    && battle.Skills != null && battle.Skills.TryGetColor(id, out var color) && run.State.Gauges.IsFull(color))
                {
                    skillSlot = slot;
                }
            }

            var choosingRoute = runController != null && runController.IsChoosingRoute;
            return new GuideObservation
            {
                Paused = battle.IsPaused,
                Finished = battle.IsFinished,
                OnScreen = runController != null && runController.OpenScreen != null,
                ChoosingRoute = choosingRoute,
                RouteSpinning = choosingRoute && slotCabinet.IsRouteSpinning,
                Preparing = run.Phase == WavePhase.Preparing,
                Wave = run.Wave,
                Deploy = run.Deployment.Phase,
                CanSummon = units.Count > 0 && run.Summoner.Check(units[0].Id) == SummonResult.Summoned,
                FieldUnits = alive,
                UnitSelected = placementInput.SelectedUnit != null,
                SlotSpinning = slotCabinet.IsSpinning,
                SpinCount = slotCabinet.SpinCount,
                CanLever = slotCabinet.CanLever,
                SkillSlot = skillSlot,
                SkillActivations = battle.SkillActivations,
                UpgradeSlot = upgradeSlot,
                UpgradeLevels = levels,
            };
        }

        private static bool IsDeployed(System.Collections.Generic.IReadOnlyList<BattleUnit> field, string id)
        {
            for (var i = 0; i < field.Count; i++)
            {
                if (field[i].IsAlive && field[i].Definition.Id == id)
                {
                    return true;
                }
            }

            return false;
        }

        private void Draw(GuideHint hint, BattleNodeRun run, float t)
        {
            const bool dimmed = true;
            SetArrows(false, default);
            // リールが止まるのを待つあいだは押す操作がないので手を出さない。
            var showHand = hint != GuideHint.Spinning;
            if (hand.gameObject.activeSelf != showHand)
            {
                hand.gameObject.SetActive(showHand);
                ripple.gameObject.SetActive(showHand);
            }

            switch (hint)
            {
                case GuideHint.Summon:
                    TapTarget(UiRect(unitIcons[0]), t, dimmed);
                    break;
                case GuideHint.Place:
                {
                    var cell = CellRect(PlaceTarget(run));
                    ShowTarget(cell, dimmed);
                    Drag(UiRect(unitIcons[SelectedSlot(run)]).center, cell.center, t);
                    break;
                }
                case GuideHint.Facing:
                {
                    var cell = CellRect(run.Deployment.Cursor);
                    var around = new Rect(cell.center - cell.size * 1.5f, cell.size * 3f);
                    ShowTarget(around, dimmed);
                    SetArrows(true, cell);
                    Drag(cell.center, cell.center + Vector2.right * cell.width, t);
                    break;
                }
                case GuideHint.Lever:
                case GuideHint.RouteLever:
                    TapTarget(UiRect(slotCabinet.LeverRect), t, dimmed);
                    break;
                case GuideHint.Spinning:
                    // リールは自動で止まる（plan 16.3）。止まるまでリール窓を見せる。
                    ShowTarget(UiRect(slotCabinet.ReelWindowRect), dimmed);
                    break;
                case GuideHint.Skill:
                    TapTarget(UiRect(_skillButtons[Mathf.Clamp(_sequence.TargetSlot, 0, _skillButtons.Length - 1)]), t, dimmed);
                    break;
                case GuideHint.RoutePick:
                    // どの行き先も選べるよう、行き先のある矢印をまとめて穴にし、手はそのうち1つを押す。
                    ShowTarget(RouteArrowsRect(), dimmed);
                    Tap(UiRect(RoutePickTarget()).center, Mathf.Repeat(t, TapSeconds));
                    break;
                case GuideHint.Upgrade:
                    TapTarget(UiRect(upgradeIcons[Mathf.Clamp(_sequence.TargetSlot, 0, upgradeIcons.Length - 1)]), t, dimmed);
                    break;
                case GuideHint.SelectUnit:
                    TapTarget(CellRect(FirstUnitCell(run)), t, dimmed);
                    break;
                case GuideHint.Retreat:
                    TapTarget(UiRect(retreatButton), t, dimmed);
                    break;
            }
        }

        private void TapTarget(Rect target, float t, bool dimmed)
        {
            ShowTarget(target, dimmed);
            Tap(target.center, Mathf.Repeat(t, TapSeconds));
        }

        // 穴の周りに暗幕を並べ、穴を光る枠で囲む。
        private void ShowTarget(Rect hole, bool dimmed)
        {
            var area = root.rect;
            hole = new Rect(hole.position - Vector2.one * framePadding, hole.size + Vector2.one * framePadding * 2f);
            for (var i = 0; i < dims.Length; i++)
            {
                dims[i].gameObject.SetActive(dimmed);
            }

            if (dimmed)
            {
                Place(dims[0], Rect.MinMaxRect(area.xMin, hole.yMax, area.xMax, area.yMax));
                Place(dims[1], Rect.MinMaxRect(area.xMin, area.yMin, area.xMax, hole.yMin));
                Place(dims[2], Rect.MinMaxRect(area.xMin, hole.yMin, hole.xMin, hole.yMax));
                Place(dims[3], Rect.MinMaxRect(hole.xMax, hole.yMin, area.xMax, hole.yMax));
            }

            Place(frame, hole);
            frameGroup.alpha = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
        }

        // 手を少し離れた所から近づけて押し、輪を広げる（phase は 0〜TapSeconds）。
        private void Tap(Vector2 point, float phase)
        {
            var approach = Mathf.Clamp01(phase / (TapSeconds * 0.4f));
            var offset = Vector2.Lerp(new Vector2(60f, -60f), Vector2.zero, Mathf.SmoothStep(0f, 1f, approach));
            hand.anchoredPosition = point + offset;
            var pressed = Mathf.Clamp01((phase - TapSeconds * 0.4f) / (TapSeconds * 0.4f));
            ShowRipple(point, phase >= TapSeconds * 0.4f ? pressed : 0f);
        }

        // 押してから目的地まで動かして離す。
        private void Drag(Vector2 from, Vector2 to, float t)
        {
            var phase = Mathf.Repeat(t, DragSeconds) / DragSeconds;
            var move = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.8f, phase));
            var point = Vector2.Lerp(from, to, move);
            hand.anchoredPosition = point;
            ShowRipple(phase < 0.2f ? from : to, phase < 0.2f ? phase / 0.2f : phase > 0.8f ? (phase - 0.8f) / 0.2f : 0f);
        }

        private void ShowRipple(Vector2 point, float progress)
        {
            ripple.anchoredPosition = point;
            var size = rippleSize * (0.3f + 0.7f * progress);
            ripple.sizeDelta = new Vector2(size, size);
            rippleGroup.alpha = progress > 0f ? 1f - progress : 0f;
        }

        private void SetArrows(bool visible, Rect cell)
        {
            for (var i = 0; i < facingArrows.Length; i++)
            {
                var arrow = facingArrows[i];
                if (arrow.gameObject.activeSelf != visible)
                {
                    arrow.gameObject.SetActive(visible);
                }

                if (!visible)
                {
                    continue;
                }

                // 右・上・左・下に1マス離して外向きに置き、少し脈打たせる。
                var angle = i * 90f;
                var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f);
                arrow.anchoredPosition = cell.center + direction * cell.width * pulse;
                arrow.sizeDelta = cell.size * 0.6f;
                arrow.localEulerAngles = new Vector3(0f, 0f, angle);
            }
        }

        private static void Place(RectTransform rect, Rect local)
        {
            rect.anchoredPosition = local.position;
            rect.sizeDelta = local.size;
        }

        // UI 部品の矩形を root のローカル座標（中心が原点）にする。
        private Rect UiRect(Transform target)
        {
            ((RectTransform)target).GetWorldCorners(_corners);
            Vector2 min = root.InverseTransformPoint(_corners[0]);
            Vector2 max = root.InverseTransformPoint(_corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // フィールドのマスの上面を画面に写した矩形（root のローカル座標）。
        private Rect CellRect(GridPoint cell)
        {
            var center = fieldView.CellTopToWorld(cell);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < 4; i++)
            {
                var local = new Vector3(i % 2 == 0 ? -0.5f : 0.5f, 0f, i < 2 ? -0.5f : 0.5f);
                var screen = battleCamera.WorldToScreenPoint(center + fieldView.transform.TransformVector(local));
                var uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out var point);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // 選んだ味方を置けるマスのうち、マップ中央に一番近いマス（ポインターを動かしても示す先が動かないようにする）。
        private static GridPoint PlaceTarget(BattleNodeRun run)
        {
            var deployment = run.Deployment;
            var cursor = new GridPoint(run.Map.Width / 2, run.Map.Height / 2);
            var best = cursor;
            var bestDistance = int.MaxValue;
            for (var y = 0; y < run.Map.Height; y++)
            {
                for (var x = 0; x < run.Map.Width; x++)
                {
                    var cell = new GridPoint(x, y);
                    var distance = System.Math.Abs(x - cursor.X) + System.Math.Abs(y - cursor.Y);
                    if (distance < bestDistance && deployment.CanPlace(cell))
                    {
                        best = cell;
                        bestDistance = distance;
                    }
                }
            }

            return best;
        }

        private int SelectedSlot(BattleNodeRun run)
        {
            var selected = run.Deployment.Selected;
            var units = battle.Units.All;
            for (var i = 0; i < units.Count && i < unitIcons.Length; i++)
            {
                if (selected != null && units[i].Id == selected.Id)
                {
                    return i;
                }
            }

            return 0;
        }

        private Rect RouteArrowsRect()
        {
            var arrows = routeScreen.ArrowButtons;
            var any = false;
            var union = default(Rect);
            for (var reel = 0; reel < arrows.Count; reel++)
            {
                if (!slotCabinet.HasRouteDirection(reel))
                {
                    continue;
                }

                var rect = UiRect(arrows[reel].transform);
                union = any ? Rect.MinMaxRect(Mathf.Min(union.xMin, rect.xMin), Mathf.Min(union.yMin, rect.yMin),
                    Mathf.Max(union.xMax, rect.xMax), Mathf.Max(union.yMax, rect.yMax)) : rect;
                any = true;
            }

            return any ? union : UiRect(RoutePickTarget());
        }

        // 行き先のあるリールの矢印（中央を優先、なければ左から）。
        private Transform RoutePickTarget()
        {
            var arrows = routeScreen.ArrowButtons;
            const int center = 1;
            if (center < arrows.Count && slotCabinet.HasRouteDirection(center))
            {
                return arrows[center].transform;
            }

            for (var reel = 0; reel < arrows.Count; reel++)
            {
                if (slotCabinet.HasRouteDirection(reel))
                {
                    return arrows[reel].transform;
                }
            }

            return arrows[center].transform;
        }

        private static GridPoint FirstUnitCell(BattleNodeRun run)
        {
            var units = run.Field.Units;
            for (var i = 0; i < units.Count; i++)
            {
                if (units[i].IsAlive)
                {
                    return units[i].Cell;
                }
            }

            return run.Deployment.Cursor;
        }
    }
}
