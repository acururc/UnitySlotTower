using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Guide;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Flow;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("T403 / FR-004: 文字なし説明が操作の順に出て、示した操作をすると次へ進み、説明中は戦闘の時間が止まる")]
    public sealed class GuideTests
    {
        private Keyboard _keyboard;
        private Mouse _mouse;
        private readonly System.Collections.Generic.List<InputDevice> _disabled = new System.Collections.Generic.List<InputDevice>();
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private BattleController _battle;
        private GuideOverlay _guide;
        private BattleFieldView _fieldView;
        private Camera _camera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _runInBackground = Application.runInBackground;
            Application.runInBackground = true;
            _editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
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

            GuideOverlay.DisabledForTests = false;
            // 当たりでコインが強化代に届くと、スロットの後に強化の説明が出てしまうので必ずハズレにする。
            SlotCabinet.RandomOverride = new AlwaysMissRandom();
            yield return SceneManager.LoadSceneAsync(GameSession.GameScene);
            _battle = Object.FindAnyObjectByType<BattleController>();
            for (var i = 0; i < 300 && _battle.Deployment == null; i++)
            {
                yield return null;
            }

            Assert.That(_battle?.Deployment, Is.Not.Null, "Game シーンの戦闘が始まらない");
            _guide = Object.FindAnyObjectByType<GuideOverlay>();
            Assert.That(_guide, Is.Not.Null, "Game シーンに文字なし説明がない");
            _fieldView = Object.FindAnyObjectByType<BattleFieldView>();
            _camera = GameObject.Find("Battle Camera").GetComponent<Camera>();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            GuideOverlay.DisabledForTests = true;
            SlotCabinet.RandomOverride = null;
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
        public IEnumerator Mouse_Follows_The_Guide_From_Reel_Custom_To_The_First_Spin()
        {
            // カスタムは 2026-11-14 に廃止（T341 で画面を削除）。説明の題目の書き直しは T351。
            var overlay = _guide.transform.Find("Overlay");
            var dim = overlay.Find("Dim Top").gameObject;
            yield return null;
            Assert.That((_guide.Current, dim.activeSelf), Is.EqualTo((GuideHint.Summon, true)), "開始時は召喚");

            var bar = Object.FindAnyObjectByType<OperationBarStatus>();
            yield return Click(bar.transform.Find("Start Button"));
            Assert.That(_battle.Run.Phase, Is.EqualTo(WavePhase.Preparing), "暗幕の上の ▶ は押せない");

            var icon = GameObject.Find("Unit Icon 1").transform;
            var target = FirstPlaceableCell();
            var cell = CellScreen(target);
            yield return MouseAt(ScreenOf(icon), false);
            yield return MouseAt(ScreenOf(icon), true);
            Assert.That(_guide.Current, Is.EqualTo(GuideHint.Place), "アイコンを押すとマスの説明");
            yield return MouseAt(cell, true);
            yield return MouseAt(cell, false);
            Assert.That(_guide.Current, Is.EqualTo(GuideHint.Facing), "マスを決めると向きの説明");

            var left = (Vector2)_camera.WorldToScreenPoint(_fieldView.CellTopToWorld(target) - _fieldView.transform.right * 1.5f);
            yield return MouseAt(left, false);
            yield return MouseAt(left, true);
            yield return MouseAt(left, false);
            Assert.That((_battle.Field.Units.Count, _guide.Current), Is.EqualTo((1, GuideHint.Lever)), "置くとスロットの説明");

            var cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            yield return Click(cabinet.LeverRect);
            Assert.That(_guide.Current, Is.EqualTo(GuideHint.Stop), "レバーを引くとリールが止まるまでの説明");

            // 自動停止は実時間（1.0 + 0.4×2 秒）で止まるので、フレーム数ではなく実時間で待つ。
            var until = Time.realtimeSinceStartup + 5f;
            while (cabinet.IsSpinning && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            yield return null;
            Assert.That((_guide.Current, overlay.gameObject.activeSelf, _guide.Sequence.IsDone(GuideTopic.Slot)),
                Is.EqualTo((GuideHint.None, false, true)), "リールが止まると説明を閉じる");
        }

        [UnityTest]
        public IEnumerator Battle_Time_Is_Held_While_A_Hint_Is_Shown()
        {
            yield return Tests.PlayMode.Battle.ReelCustomTestUtil.CloseWithDefault(_battle);
            _battle.StartWave();
            Assert.That((_battle.Run.Phase, _guide.Current), Is.EqualTo((WavePhase.Fighting, GuideHint.Summon)));

            yield return new WaitForSeconds(0.5f);
            Assert.That(_battle.Field.Tick, Is.EqualTo(0L), "説明中は戦闘が進まない");

            _guide.enabled = false;
            yield return new WaitForSeconds(0.5f);
            Assert.That(_battle.Field.Tick, Is.GreaterThan(0L), "説明を閉じれば進む");
        }

        private static Vector2 ScreenOf(Transform target) => RectTransformUtility.WorldToScreenPoint(null, target.position);

        private IEnumerator Click(Transform target)
        {
            var screen = ScreenOf(target);
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
