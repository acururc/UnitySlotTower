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
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private BattleController _battle;
        private BattleFieldView _fieldView;
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
            _fieldView = Object.FindAnyObjectByType<BattleFieldView>();
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
        public IEnumerator Mouse_Alone_Drags_A_Unit_Onto_A_Cell_And_Drags_Its_Facing()
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
            yield return MouseAt(cellScreen, true);
            yield return MouseAt(left, true);
            yield return MouseAt(left, false);

            var unit = _battle.Field.Units[0];
            Assert.That((unit.Definition.Id, unit.Cell, unit.Facing), Is.EqualTo(("tank", target, Facing.Left)));
        }

        [UnityTest]
        public IEnumerator Releasing_The_Facing_Drag_Near_The_Cell_Returns_To_Choosing_A_Cell()
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
