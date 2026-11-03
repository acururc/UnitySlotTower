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
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("AC-004 の一部: タイトルから難易度選択を通って、選んだ設定で Game が始まる（FR-005, T401）")]
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
        public IEnumerator Keyboard_Enter_Opens_Difficulty_And_Arrows_Choose_Setting_For_Game()
        {
            Assert.That(_title.IsChoosingDifficulty, Is.False, "最初からタイトルではなく難易度選択が出ている");

            yield return Press(Key.Enter, () => _title.IsChoosingDifficulty);
            Assert.That(_title.IsChoosingDifficulty, Is.True, "Enter で難易度選択に進まない");

            yield return Press(Key.Escape, () => !_title.IsChoosingDifficulty);
            Assert.That(_title.IsChoosingDifficulty, Is.False, "Esc でタイトルに戻らない");

            yield return Press(Key.Enter, () => IsSelected("Setting 1"));
            Assert.That(IsSelected("Setting 1"), Is.True, "難易度選択の初期選択が設定1ではない");
            yield return Press(Key.RightArrow, () => IsSelected("Setting 2"));
            Assert.That(IsSelected("Setting 2"), Is.True, "→ で設定2に移らない");
            yield return Press(Key.RightArrow, () => IsSelected("Setting 3"));
            Assert.That(IsSelected("Setting 3"), Is.True, "→ で設定3に移らない");
            yield return Press(Key.Enter, () => GameSession.SelectedSetting == 3);

            yield return WaitForGame();
            Assert.That(Object.FindAnyObjectByType<SlotCabinet>().Setting, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator Mouse_Clicks_Start_Back_And_Plate_Six_For_Game()
        {
            yield return Click("Start Button");
            Assert.That(_title.IsChoosingDifficulty, Is.True, "▶ のクリックで難易度選択に進まない");

            yield return Click("Back Button");
            Assert.That(_title.IsChoosingDifficulty, Is.False, "◀ のクリックでタイトルに戻らない");

            yield return Click("Start Button");
            yield return Click("Setting 6");

            yield return WaitForGame();
            Assert.That(Object.FindAnyObjectByType<SlotCabinet>().Setting, Is.EqualTo(6));
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
