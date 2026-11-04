using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Ui;
using UnitySlotTower.Flow;
using UnitySlotTower.Tests.PlayMode.Battle;

namespace UnitySlotTower.Tests.PlayMode.Flow
{
    [Description("FR-012 の一部: 一時停止中だけ「?」から図柄・確率・味方の早見表を開け、時間は止まったまま（T404）")]
    public sealed class HelpTests
    {
        private Keyboard _keyboard;
        private readonly List<InputDevice> _disabled = new List<InputDevice>();
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;

        [SetUp]
        public void SetUp()
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
        public IEnumerator Help_Opens_Only_While_Paused_And_Shows_Current_Odds()
        {
            GameSession.StartGame(3);
            BattleController battle = null;
            for (var i = 0; i < 300 && (battle == null || battle.Run == null); i++)
            {
                yield return null;
                battle = SceneManager.GetActiveScene().name == GameSession.GameScene
                    ? Object.FindAnyObjectByType<BattleController>()
                    : null;
            }

            Assert.That(battle != null && battle.Run != null, Is.True, "Game シーンの進行が作られない");
            yield return ReelCustomTestUtil.CloseWithDefault(battle);
            var help = Object.FindAnyObjectByType<HelpScreen>(FindObjectsInactive.Include);
            Assert.That(help, Is.Not.Null, "説明画面がシーンにない");

            // 一時停止していないあいだは「?」が出ず、開けない。
            yield return null;
            Assert.That(help.OpenButton.gameObject.activeInHierarchy, Is.False, "一時停止前に「?」が出ている");
            help.Open();
            Assert.That(help.IsOpen, Is.False, "一時停止前に開いた");

            // 一時停止すると「?」が出て、マウス（ボタン）で開ける。
            battle.SetPaused(true);
            yield return null;
            Assert.That(help.OpenButton.gameObject.activeInHierarchy, Is.True, "一時停止中に「?」が出ない");
            help.OpenButton.onClick.Invoke();
            Assert.That(help.IsOpen, Is.True, "「?」で開かない");
            Assert.That(Text(help, "missText"), Is.EqualTo("79.0%"), "設定3のハズレ率");
            var chances = (TMP_Text[])Field(help, "chanceTexts");
            Assert.That(chances[(int)SlotSymbol.Seven].text, Is.EqualTo("1.8%"), "既定配列の 7（21% × 1/12）");
            Assert.That(chances[(int)SlotSymbol.Coin].text, Is.EqualTo("7.0%"), "既定配列のコイン（21% × 4/12）");
            Assert.That(((TMP_Text[])Field(help, "effectTexts"))[(int)SlotSymbol.Seven].text, Is.EqualTo("+10×12"));
            foreach (var text in help.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.That(NumericText.FindInvalid(text.text), Is.EqualTo(-1), $"{text.name} に許可されない文字: {text.text}");
            }

            // 開いているあいだも時間は止まったまま。◀ で閉じても一時停止は続く。
            Assert.That(battle.IsPaused && battle.IsInputBlocked, Is.True);
            help.CloseButton.onClick.Invoke();
            Assert.That(help.IsOpen || !battle.IsPaused, Is.False, "◀ で閉じて一時停止のまま");

            // キーボード: ?（/）で開き、P で再開すると閉じる。
            yield return Press(Key.Slash, () => help.IsOpen);
            Assert.That(help.IsOpen, Is.True, "/ キーで開かない");
            yield return Press(Key.P, () => !battle.IsPaused);
            yield return null;
            Assert.That(battle.IsPaused || help.IsOpen, Is.False, "再開すると閉じる");

            // 一時停止していないときの / は一時停止して開く。
            yield return Press(Key.Slash, () => help.IsOpen);
            Assert.That(battle.IsPaused && help.IsOpen, Is.True, "/ キーで一時停止して開かない");
        }

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

        private static object Field(HelpScreen help, string name) =>
            typeof(HelpScreen).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(help);

        private static string Text(HelpScreen help, string name) => ((TMP_Text)Field(help, name)).text;
    }
}
