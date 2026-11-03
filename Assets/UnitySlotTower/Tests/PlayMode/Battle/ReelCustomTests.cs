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
using UnitySlotTower.Core.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    [Description("AC-044, AC-045, T316: ゲーム開始前にリールのカスタム画面が開き、キーボードだけ・マウスだけで配列を組んで確定できる")]
    public sealed class ReelCustomTests
    {
        private Keyboard _keyboard;
        private Mouse _mouse;
        private readonly List<InputDevice> _disabled = new List<InputDevice>();
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private BattleController _battle;
        private SlotCabinet _cabinet;
        private ReelCustomScreen _screen;

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

            yield return SceneManager.LoadSceneAsync("Game");
            _battle = Object.FindAnyObjectByType<BattleController>();
            _cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            _screen = Object.FindAnyObjectByType<ReelCustomScreen>();
            for (var i = 0; i < 300 && _battle.Run == null; i++)
            {
                yield return null;
            }

            Assert.That(_battle.Run, Is.Not.Null, "Game シーンの進行が作られない");
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
        public IEnumerator Keyboard_Alone_Edits_And_Confirms_Layout_For_All_Reels()
        {
            // 開始前に開き、そのあいだは ▶ もスロットも動かない（FR-047、plan 4章）。
            Assert.That((_screen.IsOpen, _battle.IsCustomizing), Is.EqualTo((true, true)), "開始時にカスタム画面が開かない");
            _battle.StartWave();
            yield return Tap(Key.Space); // マス1（7）→ BAR。7 が0・BAR が2で条件を満たさない。
            var customizer = _screen.Customizer;
            Assert.That((customizer.At(0), customizer.IsValid, _screen.ConfirmButton.interactable, AnyReelSpinning(),
                    _battle.Run.Phase),
                Is.EqualTo((SlotSymbol.Bar, false, false, false, WavePhase.Preparing)),
                "Space で図柄が変わらないか、カスタム中にスロット・戦闘が動いた");

            yield return Tap(Key.Enter);
            Assert.That(_screen.IsOpen, Is.True, "条件を満たさないのに Enter で確定した");

            yield return Tap(Key.DownArrow); // BAR → 7 に戻す。
            yield return Tap(Key.RightArrow); // マス2（攻）を選ぶ。
            yield return Tap(Key.UpArrow); // 攻 → 速。
            Assert.That((customizer.At(0), customizer.Selected, customizer.At(1), customizer.IsValid),
                Is.EqualTo((SlotSymbol.Seven, 1, SlotSymbol.Speed, true)));
            // AC-045: 当たり時の%がすぐ変わる（速 3/12=25.0%、攻 1/12=8.3%）。
            Assert.That((PercentText(SlotSymbol.Speed), PercentText(SlotSymbol.Atk)), Is.EqualTo(("25.0%", "8.3%")));

            yield return Tap(Key.Enter);
            Assert.That((_screen.IsOpen, _battle.IsCustomizing, _battle.Run.Phase),
                Is.EqualTo((false, false, WavePhase.Preparing)), "Enter で確定しないか、同じ Enter で戦闘まで始まった");
            AssertAllReelsUse(SlotSymbol.Speed, 1);
        }

        [UnityTest]
        public IEnumerator Mouse_Alone_Edits_And_Confirms_Layout_For_All_Reels()
        {
            var cell = _screen.Cells[2].transform; // 既定配列のマス3はコイン。
            yield return Click(cell); // 選ぶだけ
            var customizer = _screen.Customizer;
            Assert.That((customizer.Selected, customizer.At(2)), Is.EqualTo((2, SlotSymbol.Coin)));

            yield return Click(cell); // コイン → 7（7 が2つ）
            yield return Click(_screen.ConfirmButton.transform);
            Assert.That((customizer.At(2), _screen.IsOpen), Is.EqualTo((SlotSymbol.Seven, true)),
                "条件を満たさないのに ✔ で確定した");

            yield return Click(cell); // 7 → BAR（BAR が2つ）
            yield return Click(cell); // BAR → 攻
            Assert.That((customizer.At(2), customizer.IsValid, _screen.ConfirmButton.interactable),
                Is.EqualTo((SlotSymbol.Atk, true, true)));

            yield return Click(_screen.ConfirmButton.transform);
            Assert.That((_screen.IsOpen, _battle.IsCustomizing), Is.EqualTo((false, false)), "✔ で確定しない");
            AssertAllReelsUse(SlotSymbol.Atk, 2);
        }

        // AC-044: 確定した配列が筐体の抽選と3本すべてのリール表示に使われる。
        private void AssertAllReelsUse(SlotSymbol symbol, int index)
        {
            var layout = _cabinet.Layout;
            Assert.That((layout.At(index), layout.IsValid(_cabinet.Rules.ReelLength)), Is.EqualTo((symbol, true)));
            var field = typeof(SlotReelView).GetField("_layout", BindingFlags.Instance | BindingFlags.NonPublic);
            var reels = _cabinet.GetComponentsInChildren<SlotReelView>();
            Assert.That(reels.Length, Is.EqualTo(SlotMachine.ReelCount));
            foreach (var reel in reels)
            {
                Assert.That(field.GetValue(reel), Is.SameAs(layout), $"{reel.name} が確定した配列を使っていない");
            }
        }

        private string PercentText(SlotSymbol symbol)
        {
            var texts = (TMP_Text[])typeof(ReelCustomScreen)
                .GetField("symbolPercentTexts", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_screen);
            var text = texts[(int)symbol];
            text.ForceMeshUpdate();
            return text.GetParsedText();
        }

        private bool AnyReelSpinning()
        {
            foreach (var reel in _cabinet.GetComponentsInChildren<SlotReelView>())
            {
                if (reel.IsSpinning)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }

        private IEnumerator Click(Transform target)
        {
            var screen = RectTransformUtility.WorldToScreenPoint(null, target.position);
            yield return MouseAt(screen, false);
            yield return MouseAt(screen, true);
            yield return MouseAt(screen, false);
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
