using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Flow;
using UnitySlotTower.Battle;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("AC-004 の一部: タイトルの ▶ ですぐ Game のステージ1が始まる（FR-005, T401, spec 002 E-18）")]
    public sealed class TitleFlowTests
    {
        private Keyboard _keyboard;
        private Mouse _mouse;
        private readonly List<InputDevice> _disabled = new List<InputDevice>();
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private TitleScreen _title;
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
            // 実機の入力（ゲームパッドを含む）は UI のナビゲーションや決定に混ざるので、全部止める。
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

            yield return SceneManager.LoadSceneAsync(GameSession.TitleScene);
            _title = Object.FindAnyObjectByType<TitleScreen>();
            Assert.That(_title, Is.Not.Null, "Title シーンにタイトル画面がない");
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
        public IEnumerator Keyboard_Enter_Starts_The_Game_At_Stage_One()
        {
            Assert.That(IsSelected("Start Button"), Is.True, "タイトルの初期選択が ▶ ではない");
            Assert.That(GameObject.Find("Difficulty Panel"), Is.Null, "難易度選択の画面が残っている（spec 002 E-18）");

            yield return Press(Key.Enter, () => SceneManager.GetActiveScene().name == GameSession.GameScene);
            yield return WaitForStageOne();
        }

        [UnityTest]
        public IEnumerator Mouse_Click_On_Start_Starts_The_Game_At_Stage_One()
        {
            yield return Click("Start Button");
            yield return WaitForStageOne();
        }

        // Game シーンに移り、ステージ1が始まる（難易度の選択を通らない: spec 002 FR-101）。
        private static IEnumerator WaitForStageOne()
        {
            yield return WaitForGame();
            BattleController battle = null;
            for (var i = 0; i < 120 && (battle == null || battle.Run == null); i++)
            {
                battle = Object.FindAnyObjectByType<BattleController>();
                yield return null;
            }

            Assert.That(battle != null && battle.Run != null, Is.True, "ステージ1が始まらない");
            Assert.That(battle.StageNumber, Is.EqualTo(1));
        }

        private static IEnumerator WaitForGame()
        {
            for (var i = 0; i < 300 && SceneManager.GetActiveScene().name != GameSession.GameScene; i++)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameSession.GameScene), "Game シーンに移らない");
            yield return null;
        }

        private static bool IsSelected(string buttonName)
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.name == buttonName;
        }

        // 1フレームだけ押すと、Editor のフレーム時間しだいで取りこぼしや（遅いフレームでの）ナビゲーションのリピートが起きる。
        // 効果が出るまで押し続け、出たらすぐ離す。
        private IEnumerator Press(Key key, System.Func<bool> done)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            for (var i = 0; i < 60; i++)
            {
                yield return null;
                if (done())
                {
                    break;
                }
            }

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        private IEnumerator Click(string buttonName)
        {
            var button = GameObject.Find(buttonName);
            Assert.That(button, Is.Not.Null, $"{buttonName} が表示されていない");
            var rect = (RectTransform)button.transform;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position });
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position });
            yield return null;
        }
    }
}
