using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    [Description("AC-002: マウスだけ・キーボードだけで味方を選び、マスと向きを決めて置ける")]
    public sealed class UnitPlacementInputTests
    {
        private Keyboard _keyboard;
        private Mouse _mouse;
        private readonly System.Collections.Generic.List<InputDevice> _disabled = new System.Collections.Generic.List<InputDevice>();
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private BattleController _battle;
        private BattleFieldView _fieldView;
        private UnitPlacementInput _input;
        private Camera _camera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Editor が非フォーカスでもフレームと入力が進むようにする（後で戻す）。
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            _editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            // 実機の入力（ゲームパッドを含む）が試験中のポインターやキーに混ざらないよう全部止める。
            foreach (var device in InputSystem.devices)
            {
                if (device.enabled)
                {
                    _disabled.Add(device);
                }
            }

            foreach (var device in _disabled)
            {
                InputSystem.DisableDevice(device);
            }

            _keyboard = InputSystem.AddDevice<Keyboard>("Test Keyboard");
            _mouse = InputSystem.AddDevice<Mouse>("Test Mouse");

            // 前のテストのシーンが消えてから探す（同じフィクスチャのインスタンスが続けて使われる）。
            yield return SceneManager.LoadSceneAsync("Game");
            _battle = Object.FindAnyObjectByType<BattleController>();
            for (var i = 0; i < 300 && _battle.Deployment == null; i++)
            {
                yield return null;
            }

            Assert.That(_battle?.Deployment, Is.Not.Null, "Game シーンの戦闘が始まらない");
            yield return RouteTestUtil.WaitForIntro(_battle);
            _fieldView = Object.FindAnyObjectByType<BattleFieldView>();
            _input = Object.FindAnyObjectByType<UnitPlacementInput>();
            _camera = GameObject.Find("Battle Camera").GetComponent<Camera>();
            // 物理の当たり判定が揃うまで待つ。
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.RemoveDevice(_keyboard);
            InputSystem.RemoveDevice(_mouse);
            foreach (var device in _disabled)
            {
                InputSystem.EnableDevice(device);
            }

            _disabled.Clear();
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorBehavior;
            InputSystem.settings.backgroundBehavior = _backgroundBehavior;
            Application.runInBackground = _runInBackground;
        }

        [UnityTest]
        public IEnumerator Keyboard_Alone_Selects_Places_And_Faces_A_Unit()
        {
            yield return Tap(Key.Digit1);
            var deployment = _battle.Deployment;
            var target = FirstPlaceableCell();
            yield return MoveCursorTo(target);
            yield return Tap(Key.Enter);
            yield return Tap(Key.UpArrow);
            yield return Tap(Key.Enter);

            var unit = _battle.Field.Units[0];
            Assert.That((unit.Definition.Id, unit.Cell, unit.Facing, deployment.Phase),
                Is.EqualTo(("tank", target, Facing.Up, DeployPhase.Idle)));
        }

        [UnityTest]
        public IEnumerator Escape_Steps_Back_From_Facing_To_Cell_To_Nothing()
        {
            yield return Tap(Key.Digit1);
            yield return MoveCursorTo(FirstPlaceableCell());
            yield return Tap(Key.Enter);
            var deployment = _battle.Deployment;

            yield return Tap(Key.Escape);
            var afterFirst = deployment.Phase;
            yield return Tap(Key.Escape);

            Assert.That((afterFirst, deployment.Phase, _battle.Field.Units.Count),
                Is.EqualTo((DeployPhase.ChoosingCell, DeployPhase.Idle, 0)));
        }

        [UnityTest]
        public IEnumerator Shift_With_A_Digit_Does_Not_Select_A_Unit()
        {
            yield return Tap(Key.LeftShift, Key.Digit1);

            Assert.That(_battle.Deployment.Phase, Is.EqualTo(DeployPhase.Idle));
        }

        [UnityTest]
        public IEnumerator Mouse_Alone_Drags_A_Unit_Onto_A_Cell_And_Clicks_Its_Facing()
        {
            var icon = Object.FindObjectsByType<UnitBarIcon>(FindObjectsSortMode.None);
            var firstIcon = System.Array.Find(icon, i => i.name == "Unit Icon 1").GetComponent<RectTransform>();
            var target = FirstPlaceableCell();
            var cellScreen = CellScreen(target);

            yield return MouseAt(RectTransformUtility.WorldToScreenPoint(null, firstIcon.position), false);
            yield return MouseAt(RectTransformUtility.WorldToScreenPoint(null, firstIcon.position), true);
            yield return MouseAt(cellScreen, true);
            yield return MouseAt(cellScreen, false);
            var deployment = _battle.Deployment;
            Assert.That((deployment.Phase, deployment.Cursor), Is.EqualTo((DeployPhase.ChoosingFacing, target)));

            var left = (Vector2)_camera.WorldToScreenPoint(_fieldView.CellTopToWorld(target) - _fieldView.transform.right * 1.5f);
            yield return MouseAt(left, false);
            Assert.That((deployment.Facing, _battle.Field.Units.Count), Is.EqualTo((Facing.Left, 0)),
                "クリック前からポインターの方向を向く");

            yield return MouseAt(left, true);
            yield return MouseAt(left, false);

            var unit = _battle.Field.Units[0];
            Assert.That((unit.Definition.Id, unit.Cell, unit.Facing), Is.EqualTo(("tank", target, Facing.Left)));
        }

        [UnityTest]
        public IEnumerator Clicking_Near_The_Cell_While_Choosing_The_Facing_Returns_To_Choosing_A_Cell()
        {
            yield return Tap(Key.Digit1);
            var target = FirstPlaceableCell();
            yield return MoveCursorTo(target);
            yield return Tap(Key.Enter);

            var cellScreen = CellScreen(target);
            yield return MouseAt(cellScreen, false);
            yield return MouseAt(cellScreen, true);
            yield return MouseAt(cellScreen, false);

            Assert.That((_battle.Deployment.Phase, _battle.Field.Units.Count), Is.EqualTo((DeployPhase.ChoosingCell, 0)));
        }

        [UnityTest]
        public IEnumerator Keyboard_Upgrades_With_Shift_Digit_And_Retreats_With_Tab_And_X()
        {
            yield return Tap(Key.Digit1);
            yield return MoveCursorTo(FirstPlaceableCell());
            yield return Tap(Key.Enter);
            yield return Tap(Key.Enter);
            var placed = _battle.Field.Units[0];
            var afterSummon = _battle.Wallet.Coins;
            yield return Tap(Key.Tab);
            Assert.That(_input.SelectedUnit, Is.SameAs(placed), "Tab で配置中の味方を選ぶ");

            yield return Tap(Key.X);
            var refund = (int)System.Math.Floor(placed.Definition.Cost * 0.5);
            Assert.That((_battle.Field.Units.Count, _battle.Wallet.Coins - afterSummon, _input.SelectedUnit),
                Is.EqualTo((0, refund, (BattleUnit)null)), "X で撤退して返還する");

            var coins = _battle.Wallet.Coins;
            yield return Tap(Key.LeftShift, Key.Digit1);
            Assert.That((_battle.Upgrades.Level("tank"), coins - _battle.Wallet.Coins),
                Is.EqualTo((2, _battle.Upgrades.Current("tank").Cost)), "Shift + 1 で強化する");
        }

        [UnityTest]
        public IEnumerator Mouse_Upgrades_Selects_And_Retreats_A_Unit_From_The_Bar()
        {
            var bar = Object.FindAnyObjectByType<OperationBarStatus>();
            yield return Tap(Key.Digit1);
            var target = FirstPlaceableCell();
            yield return MoveCursorTo(target);
            yield return Tap(Key.Enter);
            yield return Tap(Key.Enter);
            yield return MouseAt(CellScreen(target), false);
            yield return MouseAt(CellScreen(target), true);
            yield return MouseAt(CellScreen(target), false);
            Assert.That(_input.SelectedUnit, Is.SameAs(_battle.Field.Units[0]), "味方をクリックで選ぶ");

            yield return Click(bar.transform.Find("Retreat Button"));
            Assert.That((_battle.Field.Units.Count, bar.transform.Find("Retreat Button").gameObject.activeSelf),
                Is.EqualTo((0, false)), "撤退アイコンで撤退し、選びがなくなるとアイコンを隠す");

            yield return Click(GameObject.Find("Upgrade Icon 2").transform);
            Assert.That(_battle.Upgrades.Level("sword"), Is.EqualTo(2), "強化アイコンで強化する");
        }

        [UnityTest]
        [Description("FR-019, plan 17.2: 準備フェーズがないので操作バーに ▶ とウェーブ n/2 はない")]
        public IEnumerator Bar_Has_No_Start_Button_Or_Wave_Count()
        {
            yield return null;
            var bar = Object.FindAnyObjectByType<OperationBarStatus>().transform;

            Assert.That((bar.Find("Start Button") == null, bar.Find("Wave") == null, bar.Find("Wave Icon") == null),
                Is.EqualTo((true, true, true)));
        }

        [UnityTest]
        [Description("AC-020, D-56: 説明の撤退の題目を表示中（FullRefundRetreat）に撤退アイコンで撤退すると、召喚コストの全額が戻る")]
        public IEnumerator Retreat_During_The_Retreat_Hint_Refunds_The_Whole_Cost()
        {
            var bar = Object.FindAnyObjectByType<OperationBarStatus>();
            yield return Tap(Key.Digit1);
            var target = FirstPlaceableCell();
            yield return MoveCursorTo(target);
            yield return Tap(Key.Enter);
            yield return Tap(Key.Enter);
            var placed = _battle.Field.Units[0];
            var afterSummon = _battle.Wallet.Coins;
            yield return MouseAt(CellScreen(target), false);
            yield return MouseAt(CellScreen(target), true);
            yield return MouseAt(CellScreen(target), false);

            _battle.FullRefundRetreat = true;
            yield return Click(bar.transform.Find("Retreat Button"));
            _battle.FullRefundRetreat = false;

            Assert.That((_battle.Field.Units.Count, _battle.Wallet.Coins - afterSummon), Is.EqualTo((0, placed.Definition.Cost)));
        }

        [UnityTest]
        public IEnumerator Bar_Shows_Cost_Life_Coins_And_Upgrade_Cost_As_Numbers()
        {
            yield return null;
            var status = Object.FindAnyObjectByType<OperationBarStatus>().transform;
            var icon = GameObject.Find("Unit Icon 3").transform;
            var upgrade = GameObject.Find("Upgrade Icon 3").transform;

            Assert.That(
                (Text(icon, "Cost"), Text(status, "Life"), Text(status, "Coins"), Text(upgrade, "Cost")),
                Is.EqualTo(("¥" + _battle.Units.Get("archer").Cost, _battle.Field.Life.ToString(),
                    "¥" + _battle.Wallet.Coins, "¥" + _battle.Upgrades.NextCost("archer"))));
        }

        [UnityTest]
        [Description("AC-023: ゲージが満タンのときゲージをクリックすると、配置中のその職にスキルが付き、ゲージが0になる")]
        public IEnumerator Clicking_A_Full_Skill_Gauge_Activates_The_Skill_And_Empties_It()
        {
            var tank = default(BattleUnit);
            yield return PlaceTank(placed => tank = placed);
            var color = FillGaugeOf("tank");

            yield return Click(GameObject.Find("Skill Gauge 1").transform);

            Assert.That((_battle.Run.State.Gauges.Level(color), tank.SkillStepsLeft > 0), Is.EqualTo((0, true)));
        }

        [UnityTest]
        [Description("AC-023: その職が配置0人なら、満タンのゲージをクリックしても発動せずゲージは減らない")]
        public IEnumerator Clicking_A_Full_Skill_Gauge_Without_Units_Does_Nothing()
        {
            var color = FillGaugeOf("tank");

            yield return Click(GameObject.Find("Skill Gauge 1").transform);

            Assert.That(_battle.Run.State.Gauges.IsFull(color), Is.True);
        }

        [UnityTest]
        [Description("AC-011: 一時停止中は満タンのゲージをクリックしても発動しない")]
        public IEnumerator Clicking_A_Full_Skill_Gauge_While_Paused_Does_Nothing()
        {
            var tank = default(BattleUnit);
            yield return PlaceTank(placed => tank = placed);
            var color = FillGaugeOf("tank");
            _battle.SetPaused(true);
            try
            {
                yield return Click(GameObject.Find("Skill Gauge 1").transform);
            }
            finally
            {
                _battle.SetPaused(false);
            }

            Assert.That((_battle.Run.State.Gauges.IsFull(color), tank.SkillStepsLeft), Is.EqualTo((true, 0)));
        }

        [UnityTest]
        [Description("T355: 戦闘中に召喚すると待ち時間の残り秒数がアイコンに出て、待ち時間中に押すと選ばれずに赤く光る")]
        public IEnumerator Cooldown_Shows_Seconds_And_Clicking_During_It_Flashes_Instead_Of_Selecting()
        {
            var icon = GameObject.Find("Unit Icon 1").GetComponent<UnitBarIcon>();
            Assert.That(icon.CooldownText.enabled, Is.False, "召喚する前に待ち時間の数字が出ている");

            yield return PlaceTank(_ => { });
            yield return null;
            var cooldown = _battle.Units.Get("tank").Cooldown;
            var remaining = _battle.Summoner.CooldownRemaining("tank");
            Assert.That((icon.CooldownText.enabled, icon.CooldownText.text),
                Is.EqualTo((true, ((int)System.Math.Ceiling(remaining)).ToString())), "待ち時間の秒数が出ない");
            Assert.That(remaining, Is.GreaterThan(cooldown - 1d), "待ち時間が始まっていない");

            yield return Click(icon.transform);
            Assert.That((_battle.Deployment.Phase, icon.IsShowingReject), Is.EqualTo((DeployPhase.Idle, true)),
                "待ち時間中に押して選べたか、光らない");
        }

        private IEnumerator PlaceTank(System.Action<BattleUnit> placed)
        {
            yield return Tap(Key.Digit1);
            yield return MoveCursorTo(FirstPlaceableCell());
            yield return Tap(Key.Enter);
            yield return Tap(Key.Enter);
            Assert.That(_battle.Field.Units.Count, Is.EqualTo(1), "タンクを置けない");
            placed(_battle.Field.Units[0]);
        }

        private Core.Slot.SlotSymbol FillGaugeOf(string unitId)
        {
            Assert.That(_battle.Skills.TryGetColor(unitId, out var color), Is.True, unitId);
            var gauges = _battle.Run.State.Gauges;
            for (var i = 0; i < gauges.Max; i++)
            {
                gauges.Add(color);
            }

            return color;
        }

        private static string Text(Transform parent, string child) =>
            parent.Find(child).GetComponent<TMPro.TMP_Text>().text;

        private IEnumerator Click(Transform target)
        {
            var screen = RectTransformUtility.WorldToScreenPoint(null, target.position);
            yield return MouseAt(screen, false);
            yield return MouseAt(screen, true);
            yield return MouseAt(screen, false);
        }

        private GridPoint FirstPlaceableCell()
        {
            var map = _fieldView.Map;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    if (UnitPlacement.CanPlace(map, new GridPoint(x, y), _battle.Units.Get("tank")))
                    {
                        return new GridPoint(x, y);
                    }
                }
            }

            Assert.Fail("置けるマスがない");
            return default;
        }

        private Vector2 CellScreen(GridPoint cell) => _camera.WorldToScreenPoint(_fieldView.CellTopToWorld(cell));

        private IEnumerator MoveCursorTo(GridPoint target)
        {
            var deployment = _battle.Deployment;
            while (deployment.Cursor.X != target.X)
            {
                yield return Tap(deployment.Cursor.X < target.X ? Key.RightArrow : Key.LeftArrow);
            }

            while (deployment.Cursor.Y != target.Y)
            {
                yield return Tap(deployment.Cursor.Y < target.Y ? Key.DownArrow : Key.UpArrow);
            }
        }

        private IEnumerator Tap(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }

        private IEnumerator MouseAt(Vector2 position, bool pressed)
        {
            var state = new MouseState { position = position };
            if (pressed)
            {
                state = state.WithButton(MouseButton.Left);
            }

            InputSystem.QueueStateEvent(_mouse, state);
            yield return null;
            yield return null;
        }
    }
}
