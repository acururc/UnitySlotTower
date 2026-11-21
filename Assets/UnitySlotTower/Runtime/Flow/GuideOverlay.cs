using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Guide;
using UnitySlotTower.Slot;
using UnitySlotTower.Ui;

namespace UnitySlotTower.Flow
{
    /// <summary>
    /// 文字なし説明の表示（T403・T351、FR-004、plan 16.10）。進行は <see cref="GuideSequence"/> が決め、ここは毎フレーム様子を渡して
    /// 結果を描く: 対象の周りだけを残して画面を薄暗くし、対象を光る枠で囲み、手（白い指先の三角と、押した所に広がる輪）の
    /// アニメで操作を示す。示すのはおすすめだけで、暗い部分もクリックを通す（FR-083）。題目は右上の × で閉じられる。
    /// 向き選びでは4方向の矢印と、敵の来る方向へ動く手を出す。
    /// 説明中は戦闘の時間を止める（<see cref="BattleController.IsHeldByGuide"/>）。ショップ・回復の画面中は出さない。
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class GuideOverlay : MonoBehaviour
    {
        private const float TapSeconds = 1.2f;
        private const float DragSeconds = 1.8f;
        // 押して閉じる説明（足止め・マスの中身）は、出てすぐの押下を数えない（Claude の仮置き）。
        private const float MinTapSeconds = 0.6f;
        private const float PanelGap = 24f;
        // 閉じるボタン（×）の大きさと、向き選びの手が道を探す距離（マス、Claude の仮置き）。
        private const float CloseSize = 96f;
        private const int FacingPathReach = 3;

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

        [Header("Text mode (FR-006)")]
        [Tooltip("文字あり版だけに出る説明文。穴の上か下の空いている側に置く。")]
        [SerializeField] private TextModeLabel caption;
        [SerializeField] private float captionGap = 28f;

        private readonly GuideSequence _sequence = new GuideSequence();
        private readonly Vector3[] _corners = new Vector3[4];
        private readonly RectTransform[] _skillButtons = new RectTransform[4];
        private Canvas _canvas;
        private GuideHint _shownHint = (GuideHint)(-1);
        private float _hintStarted;
        private Rect _hole;
        // T369 で実行時に作る部品（初めて要るときに作る）。
        private RectTransform _blocker;
        private RectTransform _blockPanel;
        private RectTransform _routePanel;
        private readonly RectTransform[] _blockArrows = new RectTransform[2];
        // T426 の閉じるボタン（初めて説明を出すときに作る）。押されたら次の Update で Dismissed として渡す。
        private RectTransform _close;
        private bool _dismissRequested;

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

            // 暗幕・枠・説明文はクリックを止めない（FR-083: 示した以外の対象も押せる）。
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
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

            var observation = Observe(run);
            observation.Tapped = IsTapToClose(_shownHint) && Time.unscaledTime - _hintStarted >= MinTapSeconds
                && Pointer.current != null && Pointer.current.press.wasPressedThisFrame && !IsPointerOnClose();
            observation.Dismissed = _dismissRequested;
            _dismissRequested = false;
            var hint = _sequence.Update(observation);
            battle.IsHeldByGuide = _sequence.HoldsBattle;
            battle.FullRefundRetreat = _sequence.ShowsRetreat;
            if (hint != _shownHint)
            {
                _shownHint = hint;
                _hintStarted = Time.unscaledTime;
                if (hint != GuideHint.None && _close == null)
                {
                    BuildClose();
                }

                root.gameObject.SetActive(hint != GuideHint.None);
                ShowPictures(hint);
                if (caption != null && hint != GuideHint.None)
                {
                    caption.SetIds(CaptionId(hint));
                }
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
            var fighting = run.Phase == WavePhase.Fighting;
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

                // 発動できる条件（plan 16.4）: 満タン・その職が配置中・戦闘中。
                if (skillSlot < 0 && fighting && deployed && slot < _skillButtons.Length && _skillButtons[slot] != null
                    && battle.Skills != null && battle.Skills.TryGetColor(id, out var color) && run.State.Gauges.IsFull(color))
                {
                    skillSlot = slot;
                }
            }

            return new GuideObservation
            {
                Paused = battle.IsPaused,
                Finished = battle.IsFinished,
                // ルートの移動のアニメ中（D-90）も画面中として扱い、戦闘の題目を始めない。
                OnScreen = runController != null && runController.OpenScreen != null || routeScreen != null && routeScreen.IsMoving,
                // ルート選びのリールは入った時点で自動で回る（D-90）ので、説明はレバーを指さずに行き先のリールを指す。
                ChoosingRoute = runController != null && runController.IsChoosingRoute,
                Intro = run.Phase == WavePhase.Intro,
                BattleNode = battle.NodeCount,
                Deploy = run.Deployment.Phase,
                // どの味方でも召喚できれば召喚の説明を出す（手は召喚できる最初の札を示す、FR-083）。
                CanSummon = SummonSlot(run) >= 0,
                FieldUnits = alive,
                UnitSelected = placementInput.SelectedUnit != null,
                SlotSpinning = slotCabinet.IsSpinning,
                SpinCount = slotCabinet.SpinCount,
                CanLever = slotCabinet.CanLever,
                SkillSlot = skillSlot,
                SkillActivations = battle.SkillActivations,
                UpgradeSlot = upgradeSlot,
                UpgradeLevels = levels,
                GroundBlocked = BlockedEnemy(run) != null,
            };
        }

        private static bool IsTapToClose(GuideHint hint) => hint == GuideHint.Block || hint == GuideHint.RouteNodes;

        // 操作バーのうち、いま召喚できる最初の味方の位置。なければ -1。
        private int SummonSlot(BattleNodeRun run)
        {
            var units = battle.Units.All;
            for (var slot = 0; slot < units.Count && slot < unitIcons.Length; slot++)
            {
                if (run.Summoner.Check(units[slot].Id) == SummonResult.Summoned)
                {
                    return slot;
                }
            }

            return -1;
        }

        private bool IsPointerOnClose()
        {
            if (_close == null || !_close.gameObject.activeInHierarchy)
            {
                return false;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(_close, Pointer.current.position.ReadValue(), UiCamera);
        }

        private Camera UiCamera => _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

        // 閉じるボタン: 暗い丸に白い ×（Claude の仮置き）。押したら選択を外す（Space・Enter で押し直さない）。
        private void BuildClose()
        {
            _close = RouteNodeIcons.CreateRect("Close Button", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _close.pivot = new Vector2(0.5f, 0.5f);
            _close.sizeDelta = new Vector2(CloseSize, CloseSize);
            var image = _close.gameObject.AddComponent<Image>();
            image.sprite = routeScreen.KnobSprite;
            image.color = new Color(0.08f, 0.09f, 0.12f, 0.92f);
            var outline = _close.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.7f);
            outline.effectDistance = new Vector2(3f, -3f);
            var button = _close.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                _dismissRequested = true;
                if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                }
            });

            for (var i = 0; i < 2; i++)
            {
                var bar = RouteNodeIcons.CreateRect(i == 0 ? "Cross A" : "Cross B", _close, new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f));
                bar.sizeDelta = new Vector2(CloseSize * 0.5f, CloseSize * 0.09f);
                bar.localEulerAngles = new Vector3(0f, 0f, i == 0 ? 45f : -45f);
                var cross = bar.gameObject.AddComponent<Image>();
                cross.color = Color.white;
                cross.raycastTarget = false;
            }

            _close.SetAsLastSibling();
        }

        // × を穴（板があれば板を含む）の右上の角の少し外に置き、説明文がそれを避けるよう穴に含める。
        private void PlaceClose()
        {
            var area = root.rect;
            const float margin = 12f;
            var half = CloseSize * 0.5f;
            var x = Mathf.Clamp(_hole.xMax + half * 0.5f, area.xMin + half + margin, area.xMax - half - margin);
            var y = Mathf.Clamp(_hole.yMax + half * 0.5f, area.yMin + half + margin, area.yMax - half - margin);
            _close.anchoredPosition = new Vector2(x, y);
            _hole = Rect.MinMaxRect(Mathf.Min(_hole.xMin, x - half), Mathf.Min(_hole.yMin, y - half),
                Mathf.Max(_hole.xMax, x + half), Mathf.Max(_hole.yMax, y + half));
        }

        // 向き選びの手の行き先: 置くマスから一番近い道のマスの、出口側の隣へ向かう方向（4方向に丸める、plan 19.7）。
        // 近くに道がなければ右。
        private Vector2 FacingHandTarget(BattleNodeRun run, GridPoint cell, Rect cellRect)
        {
            var fallback = cellRect.center + Vector2.right * cellRect.width;
            MapPath nearest = null;
            var index = -1;
            var best = FacingPathReach + 1;
            var paths = run.Map.Paths;
            for (var p = 0; p < paths.Count; p++)
            {
                var cells = paths[p].Cells;
                for (var i = 0; i < cells.Count; i++)
                {
                    var distance = System.Math.Abs(cells[i].X - cell.X) + System.Math.Abs(cells[i].Y - cell.Y);
                    // 同じ距離なら地上の道を優先する。
                    if (distance < best || distance == best && nearest != null && nearest.IsAir && !paths[p].IsAir)
                    {
                        best = distance;
                        nearest = paths[p];
                        index = i;
                    }
                }
            }

            if (nearest == null)
            {
                return fallback;
            }

            var path = nearest.Cells;
            var exitSide = path[System.Math.Max(index - 1, 0)];
            var dx = exitSide.X - cell.X;
            var dy = exitSide.Y - cell.Y;
            if (dx == 0 && dy == 0 && index + 1 < path.Count)
            {
                // 出口のマスに置いた: 道を逆にたどる向き。
                dx = path[index].X - path[index + 1].X;
                dy = path[index].Y - path[index + 1].Y;
            }

            if (dx == 0 && dy == 0)
            {
                return fallback;
            }

            int sx = 0, sy = 0;
            if (System.Math.Abs(dx) >= System.Math.Abs(dy))
            {
                sx = System.Math.Sign(dx);
            }
            else
            {
                sy = System.Math.Sign(dy);
            }

            var from = fieldView.CellTopToWorld(cell);
            var step = fieldView.PositionToWorld(cell.X + sx, cell.Y + sy, false) - fieldView.PositionToWorld(cell.X, cell.Y, false);
            return WorldToLocal(from + step);
        }

        private Vector2 WorldToLocal(Vector3 world)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, battleCamera.WorldToScreenPoint(world), UiCamera,
                out var point);
            return point;
        }

        // 地上の味方に止められている地上の敵（最初の1体）。
        private static BattleEnemy BlockedEnemy(BattleNodeRun run)
        {
            var enemies = run.Field.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy.IsAlive && !enemy.Definition.IsAir && enemy.BlockedBy != null && enemy.BlockedBy.IsAlive)
                {
                    return enemy;
                }
            }

            return null;
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
                    TapTarget(UiRect(unitIcons[Mathf.Max(0, SummonSlot(run))]), t, dimmed);
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
                    var cursor = run.Deployment.Cursor;
                    var cell = CellRect(cursor);
                    var around = new Rect(cell.center - cell.size * 1.5f, cell.size * 3f);
                    ShowTarget(around, dimmed);
                    SetArrows(true, cell);
                    Drag(cell.center, FacingHandTarget(run, cursor, cell), t);
                    break;
                }
                case GuideHint.Lever:
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
                case GuideHint.Block:
                    DrawBlock(run, t, dimmed);
                    break;
                case GuideHint.RouteNodes:
                {
                    // マップの上の方に一覧を出し、手でその板を押して見せる（押すと行き先のリールの説明へ）。
                    var area = root.rect;
                    var size = _routePanel.sizeDelta;
                    var panel = new Rect(area.center.x - size.x * 0.5f, area.yMax - area.height * 0.08f - size.y, size.x, size.y);
                    ShowTarget(panel, dimmed);
                    Place(_routePanel, panel);
                    Tap(new Vector2(panel.xMax - 80f, panel.yMin + 60f), Mathf.Repeat(t, TapSeconds));
                    break;
                }
            }

            PlaceClose();
            PlaceCaption();
        }

        // 止めている味方と止められている敵を1つの穴にして上から矢印で指し、職ごとに止められる数の板を空いている側に置く。
        private void DrawBlock(BattleNodeRun run, float t, bool dimmed)
        {
            var enemy = BlockedEnemy(run);
            var unitRect = enemy != null ? CellRect(enemy.BlockedBy.Cell) : CellRect(run.Deployment.Cursor);
            var enemyRect = enemy != null ? EnemyRect(enemy, unitRect.size) : unitRect;
            var hole = Rect.MinMaxRect(Mathf.Min(unitRect.xMin, enemyRect.xMin), Mathf.Min(unitRect.yMin, enemyRect.yMin),
                Mathf.Max(unitRect.xMax, enemyRect.xMax), Mathf.Max(unitRect.yMax, enemyRect.yMax));
            ShowTarget(hole, dimmed);

            var bob = 14f * Mathf.Sin(Time.unscaledTime * 6f);
            PlaceArrow(_blockArrows[0], unitRect, bob);
            PlaceArrow(_blockArrows[1], enemyRect, bob);

            var area = root.rect;
            var size = _blockPanel.sizeDelta;
            var below = _hole.yMin - area.yMin >= area.yMax - _hole.yMax;
            var y = below ? _hole.yMin - PanelGap - size.y : _hole.yMax + PanelGap;
            y = Mathf.Clamp(y, area.yMin + PanelGap, area.yMax - PanelGap - size.y);
            var x = Mathf.Clamp(_hole.center.x - size.x * 0.5f, area.xMin + PanelGap, area.xMax - PanelGap - size.x);
            var panel = new Rect(x, y, size.x, size.y);
            Place(_blockPanel, panel);
            // 文字あり版の説明文は穴と板の外に置く。
            _hole = Rect.MinMaxRect(Mathf.Min(_hole.xMin, panel.xMin), Mathf.Min(_hole.yMin, panel.yMin),
                Mathf.Max(_hole.xMax, panel.xMax), Mathf.Max(_hole.yMax, panel.yMax));
            Tap(new Vector2(panel.xMax - 60f, panel.yMin + 50f), Mathf.Repeat(t, TapSeconds));
        }

        // 下向きの矢印を対象の真上に置く。
        private void PlaceArrow(RectTransform arrow, Rect target, float bob)
        {
            var size = Mathf.Clamp(target.width * 0.6f, 48f, 110f);
            arrow.sizeDelta = new Vector2(size, size);
            arrow.anchoredPosition = new Vector2(target.center.x, target.yMax + framePadding + size * 0.6f + bob);
        }

        // 敵の足元を画面に写し、マスと同じ大きさの矩形にする。
        private Rect EnemyRect(BattleEnemy enemy, Vector2 size)
        {
            var world = fieldView.PositionToWorld(enemy.X, enemy.Y, false);
            var uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, battleCamera.WorldToScreenPoint(world), uiCamera,
                out var point);
            return new Rect(point - size * 0.5f, size);
        }

        // 足止め・マスの中身の絵を、その説明のときだけ出す。初めて要るときに作る。
        private void ShowPictures(GuideHint hint)
        {
            var block = hint == GuideHint.Block;
            var nodes = hint == GuideHint.RouteNodes;
            if ((block || nodes) && _blocker == null)
            {
                BuildPictures();
            }

            if (_blocker == null)
            {
                return;
            }

            // 押して閉じる説明のあいだは、穴の中も含めて下の画面に押下を通さない。
            SetActive(_blocker, block || nodes);
            SetActive(_blockPanel, block);
            SetActive(_blockArrows[0], block);
            SetActive(_blockArrows[1], block);
            SetActive(_routePanel, nodes);
        }

        private static void SetActive(Component target, bool active)
        {
            if (target.gameObject.activeSelf != active)
            {
                target.gameObject.SetActive(active);
            }
        }

        private void BuildPictures()
        {
            var knob = routeScreen.KnobSprite;
            var box = routeScreen.BoxSprite;
            _blocker = RouteNodeIcons.CreateRect("Tap Blocker", root, Vector2.zero, Vector2.one);
            var blocker = _blocker.gameObject.AddComponent<Image>();
            blocker.color = Color.clear;
            _blocker.SetSiblingIndex(0);

            // 足止め: 操作バーの並びのうちバッファー以外の職（タンク・剣士・弓兵）。
            var units = new System.Collections.Generic.List<(RectTransform art, Color color, int block)>();
            TMPro.TMP_FontAsset font = null;
            var all = battle.Units.All;
            for (var slot = 0; slot < all.Count && slot < unitIcons.Length; slot++)
            {
                var icon = unitIcons[slot] != null ? unitIcons[slot].GetComponent<UnitBarIcon>() : null;
                if (icon == null)
                {
                    continue;
                }

                font = font != null ? font : icon.CooldownText.font;
                if (all[slot].Role != UnitRole.Buff)
                {
                    units.Add((icon.JobArt, icon.JobColor, all[slot].Block));
                }
            }

            _blockPanel = GuidePictures.BlockPanel(root, units, font, box);
            _blockPanel.sizeDelta = new Vector2(GuidePictures.BlockPanelWidth, GuidePictures.BlockRowHeight * units.Count);
            for (var i = 0; i < _blockArrows.Length; i++)
            {
                _blockArrows[i] = GuidePictures.PointerArrow(root);
                _blockArrows[i].pivot = new Vector2(0.5f, 0.5f);
                _blockArrows[i].localEulerAngles = new Vector3(0f, 0f, -90f);
                _blockArrows[i].name = i == 0 ? "Block Arrow Unit" : "Block Arrow Enemy";
            }

            _routePanel = GuidePictures.RoutePanel(root, knob, box);
            _routePanel.sizeDelta = new Vector2(GuidePictures.RoutePanelWidth,
                GuidePictures.RouteRowHeight * GuidePictures.RouteKinds.Length);

            // 手と輪は板より前に出す。
            ripple.SetAsLastSibling();
            hand.SetAsLastSibling();
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
            _hole = hole;
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

        /// <summary>説明の id（ui_texts.csv の guide_〜）。切り替わったときだけ呼ぶ。</summary>
        public static string CaptionId(GuideHint hint) => "guide_" + hint.ToString().ToLowerInvariant();

        // 説明文を穴の上下の広い側に、画面の横幅いっぱい（左右に余白）で置く。
        private void PlaceCaption()
        {
            if (caption == null)
            {
                return;
            }

            var area = root.rect;
            var rect = (RectTransform)caption.transform;
            var height = rect.sizeDelta.y;
            var width = area.width - captionGap * 2f;
            var above = area.yMax - _hole.yMax >= _hole.yMin - area.yMin;
            var y = above ? _hole.yMax + captionGap : _hole.yMin - captionGap - height;
            y = Mathf.Clamp(y, area.yMin + captionGap, area.yMax - captionGap - height);
            Place(rect, new Rect(area.xMin + captionGap, y, width, height));
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
