using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    [Description("AC-011 の一部: 一時停止中は敵が止まり、閲覧以外の操作が拒否される（FR-012, T307）")]
    public sealed class PauseTests
    {
        private Keyboard _keyboard;
        private readonly List<InputDevice> _disabled = new List<InputDevice>();
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private BattleController _battle;
        private SlotCabinet _cabinet;

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

            yield return SceneManager.LoadSceneAsync("Game");
            _battle = Object.FindAnyObjectByType<BattleController>();
            _cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            for (var i = 0; i < 300 && _battle.Run == null; i++)
            {
                yield return null;
            }

            yield return StageTestUtil.WaitForIntro(_battle);
            for (var i = 0; i < 300 && (_battle.Field == null || _battle.Field.Enemies.Count == 0); i++)
            {
                yield return null;
            }

            Assert.That(_battle?.Field?.Enemies.Count, Is.GreaterThan(0), "Game シーンの敵が出てこない");
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            InputSystem.RemoveDevice(_keyboard);
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
        public IEnumerator P_Stops_Time_And_Refuses_Slot_And_Placement_Until_Resumed()
        {
            var reels = _cabinet.GetComponentsInChildren<SlotReelView>();
            yield return Tap(Key.P);
            Assert.That(_battle.IsPaused, Is.True, "P で一時停止しない");

            var enemy = _battle.Field.Enemies[0];
            var tick = _battle.Field.Tick;
            var distance = enemy.Distance;
            var coins = _battle.Wallet.Coins;
            yield return Tap(Key.Space);
            yield return Tap(Key.Digit1);
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.That((_battle.Field.Tick, enemy.Distance, reels[0].IsSpinning, _battle.Wallet.Coins,
                    _battle.Deployment.Phase),
                Is.EqualTo((tick, distance, false, coins, DeployPhase.Idle)));

            yield return Tap(Key.P);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That((_battle.IsPaused, Time.timeScale, _battle.Field.Tick > tick),
                Is.EqualTo((false, 1f, true)), "P で再開しない");
        }

        [UnityTest]
        public IEnumerator Escape_Steps_Back_While_Placing_And_Otherwise_Toggles_Pause()
        {
            yield return Tap(Key.Digit1);
            Assert.That(_battle.Deployment.Phase, Is.EqualTo(DeployPhase.ChoosingCell));

            yield return Tap(Key.Escape);
            var afterCancel = (_battle.Deployment.Phase, _battle.IsPaused);
            yield return Tap(Key.Escape);
            var afterPause = _battle.IsPaused;
            yield return Tap(Key.Escape);

            Assert.That((afterCancel, afterPause, _battle.IsPaused),
                Is.EqualTo(((DeployPhase.Idle, false), true, false)));
        }

        [UnityTest]
        [Description("AC-015: 倍速のボタンで 1→2→3→1倍と変わり、Time.timeScale と数字（×2）も変わる")]
        public IEnumerator Speed_Button_Cycles_One_Two_Three_One()
        {
            var shown = new List<(int, float, string)>();
            for (var i = 0; i < 3; i++)
            {
                yield return PressSpeed();
                shown.Add((_battle.BattleSpeed, Time.timeScale, SpeedText()));
            }

            Assert.That(shown, Is.EqualTo(new[] { (2, 2f, "×2"), (3, 3f, "×3"), (1, 1f, "×1") }));
        }

        [UnityTest]
        [Description("AC-015: 2倍では戦闘の時間（敵の移動・召喚の待ち時間・スキルの時間はこの時計で進む）が実時間あたり約2倍進む")]
        public IEnumerator Double_Speed_Advances_Battle_Time_Twice_As_Fast()
        {
            var tick = _battle.Field.Tick;
            yield return new WaitForSecondsRealtime(1f);
            var normal = _battle.Field.Tick - tick;
            yield return PressSpeed();
            tick = _battle.Field.Tick;
            yield return new WaitForSecondsRealtime(1f);
            var fast = _battle.Field.Tick - tick;

            Assert.That((double)fast / normal, Is.InRange(1.6d, 2.4d), $"1倍 {normal}・2倍 {fast} ステップ");
        }

        [UnityTest]
        [Description("AC-015: 3倍ではスロットのリールが止まるまでの時間も短くなる（自動停止は1倍で約1.8秒）")]
        public IEnumerator Triple_Speed_Shortens_The_Reel_Spin()
        {
            yield return PressSpeed();
            yield return PressSpeed();
            var start = Time.realtimeSinceStartup;
            yield return Tap(Key.Space);
            Assert.That(_cabinet.IsSpinning, Is.True, "レバーで回らない");
            while (_cabinet.IsSpinning && Time.realtimeSinceStartup - start < 5f)
            {
                yield return null;
            }

            Assert.That(Time.realtimeSinceStartup - start, Is.LessThan(1.2f));
        }

        [UnityTest]
        [Description("AC-015, D-17: 一時停止中は時間が止まり倍速を変えられず、再開すると前の速さに戻る")]
        public IEnumerator Resuming_From_Pause_Returns_To_The_Chosen_Speed()
        {
            yield return PressSpeed();
            yield return PressSpeed();
            yield return Tap(Key.P);
            var paused = Time.timeScale;
            yield return PressSpeed();
            var speedWhilePaused = _battle.BattleSpeed;
            yield return Tap(Key.P);

            Assert.That((paused, speedWhilePaused, _battle.BattleSpeed, Time.timeScale), Is.EqualTo((0f, 3, 3, 3f)));
        }

        [UnityTest]
        [Description("AC-055, D-99: 札を選んでから配置をやめるまで 3倍でも 0.2 倍で進み、一時停止中は 0、やめると 3倍に戻る")]
        public IEnumerator Placing_Slows_Time_Regardless_Of_Speed_Until_Cancelled()
        {
            yield return PressSpeed();
            yield return PressSpeed();
            var slow = (float)_battle.Config.PlacementTimeScale;

            yield return Tap(Key.Digit1);
            yield return null;
            var choosingCell = (_battle.Deployment.Phase, Time.timeScale);
            var tick = _battle.Field.Tick;
            yield return new WaitForSecondsRealtime(1f);
            var slowSteps = _battle.Field.Tick - tick;

            yield return Tap(Key.P);
            var paused = Time.timeScale;
            yield return Tap(Key.P);
            yield return null;
            var resumed = Time.timeScale;

            yield return Tap(Key.Escape);
            yield return null;
            var cancelled = (_battle.Deployment.Phase, Time.timeScale, _battle.BattleSpeed);

            Assert.That((choosingCell, paused, resumed, cancelled),
                Is.EqualTo(((DeployPhase.ChoosingCell, slow), 0f, slow, (DeployPhase.Idle, 3f, 3))));
            // 1倍で約 SimulationClock.StepsPerSecond ステップ/秒。0.2 倍なら 1 秒で 1/5 前後。
            Assert.That(slowSteps, Is.LessThanOrEqualTo(UnitySlotTower.Core.Simulation.SimulationClock.StepsPerSecond * slow * 2 + 1),
                $"配置中に {slowSteps} ステップ進んだ");
        }

        [UnityTest]
        [Description("AC-055: 向きを決めて置き終えると選んでいた倍速（2倍）に戻る")]
        public IEnumerator Finishing_Placement_Returns_To_The_Chosen_Speed()
        {
            yield return PressSpeed();
            yield return Tap(Key.Digit1);
            yield return null;
            var placing = Time.timeScale;

            // 置けるマスまでカーソルを動かし、向きを決める（Enter 2回）。
            var deployment = _battle.Deployment;
            var map = _battle.Run.Map;
            var target = deployment.Cursor;
            for (var y = map.Height - 1; y >= 0; y--)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    if (deployment.CanPlace(new GridPoint(x, y)))
                    {
                        target = new GridPoint(x, y);
                    }
                }
            }

            while (deployment.Cursor.X != target.X)
            {
                yield return Tap(deployment.Cursor.X < target.X ? Key.RightArrow : Key.LeftArrow);
            }

            while (deployment.Cursor.Y != target.Y)
            {
                yield return Tap(deployment.Cursor.Y < target.Y ? Key.DownArrow : Key.UpArrow);
            }

            Assert.That(deployment.CanPlace(target), Is.True, "置けるマスがない");
            yield return Tap(Key.Enter);
            yield return Tap(Key.Enter);
            yield return null;

            Assert.That((placing, deployment.Phase, _battle.Field.Units.Count > 0, Time.timeScale),
                Is.EqualTo(((float)_battle.Config.PlacementTimeScale, DeployPhase.Idle, true, 2f)));
        }

        private IEnumerator PressSpeed()
        {
            Object.FindAnyObjectByType<OperationBarStatus>().transform.Find("Speed Button")
                .GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
        }

        private static string SpeedText() =>
            Object.FindAnyObjectByType<OperationBarStatus>().transform.Find("Speed Button/Speed")
                .GetComponent<TMPro.TMP_Text>().text;

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }
    }
}
