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
    [Description("AC-044, AC-045, T322: ゲーム開始前にリールのカスタム画面が開き、キーボードだけ・マウスのドラッグだけで空きを埋めて確定できる")]
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
        public IEnumerator Keyboard_Alone_Fills_Empty_Cells_And_Confirms_Layout_For_All_Reels()
        {
            // 開始前に開き、ロック以外は空き。そのあいだは ▶ もスロットも動かない（FR-047、plan 4章）。
            Assert.That((_screen.IsOpen, _battle.IsCustomizing), Is.EqualTo((true, true)), "開始時にカスタム画面が開かない");
            var customizer = _screen.Customizer;
            var first = _screen.FirstEmptyCell;
            Assert.That((customizer.EmptyCount, customizer.Selected, customizer.IsLocked(first), customizer.Brush),
                Is.EqualTo((_cabinet.Locks.EmptyCount, first, false, SlotSymbol.Atk)), "空きの最初のマス・攻撃力から始まらない");
            _battle.StartWave();
            yield return Tap(Key.Enter);
            Assert.That((_screen.IsOpen, _screen.ConfirmButton.interactable, AnyReelSpinning(), _battle.Run.Phase),
                Is.EqualTo((true, false, false, WavePhase.Preparing)), "空きがあるのに確定したか、カスタム中にスロット・戦闘が動いた");

            // 下で次の図柄（攻 → 速）、Space で選んだマスに入れる。AC-045: 当たり時の%がすぐ変わる。
            var speedBefore = PercentText(SlotSymbol.Speed);
            yield return Tap(Key.DownArrow);
            yield return Tap(Key.Space);
            Assert.That((customizer.Brush, customizer.At(first)), Is.EqualTo((SlotSymbol.Speed, (SlotSymbol?)SlotSymbol.Speed)),
                "下と Space で速を入れられない");
            Assert.That(PercentText(SlotSymbol.Speed),
                Is.EqualTo($"{customizer.SymbolTenths(SlotSymbol.Speed) / 10f:0.0}%").And.Not.EqualTo(speedBefore));

            // 右で次の空きマスへ（ロックは飛ばす）。上で前の図柄（速 → 攻）。Backspace で空きに戻す。
            yield return Tap(Key.RightArrow);
            var second = customizer.Selected;
            Assert.That((second > first, customizer.IsLocked(second)), Is.EqualTo((true, false)), "右で次の空きマスへ動かない");
            yield return Tap(Key.UpArrow);
            yield return Tap(Key.Space);
            Assert.That(customizer.At(second), Is.EqualTo((SlotSymbol?)SlotSymbol.Atk));
            yield return Tap(Key.Backspace);
            Assert.That(customizer.At(second), Is.Null, "Backspace で空きに戻らない");

            // 残りの空きを Space と右で埋める。
            for (var i = 0; i < 40 && customizer.EmptyCount > 0; i++)
            {
                yield return Tap(customizer.At(customizer.Selected) == null ? Key.Space : Key.RightArrow);
            }

            Assert.That((customizer.EmptyCount, _screen.ConfirmButton.interactable), Is.EqualTo((0, true)));
            yield return Tap(Key.Enter);
            Assert.That((_screen.IsOpen, _battle.IsCustomizing, _battle.Run.Phase),
                Is.EqualTo((false, false, WavePhase.Preparing)), "Enter で確定しないか、同じ Enter で戦闘まで始まった");
            AssertAllReelsUse(SlotSymbol.Speed, first);
        }

        [UnityTest]
        public IEnumerator Mouse_Alone_Drags_Symbols_Into_Empty_Cells_And_Confirms_Layout_For_All_Reels()
        {
            var customizer = _screen.Customizer;
            var first = _screen.FirstEmptyCell;
            var heal = System.Array.IndexOf(ReelLocks.Fillable, SlotSymbol.Heal);
            var coin = System.Array.IndexOf(ReelLocks.Fillable, SlotSymbol.Coin);

            // 下の図柄から空きマスへドラッグして入れる。
            yield return Drag(_screen.PaletteItems[heal], _screen.Cells[first]);
            Assert.That(customizer.At(first), Is.EqualTo((SlotSymbol?)SlotSymbol.Heal), "空きマスへドラッグで入れられない");

            // ロックのマスには入らない。
            var lockedSymbol = customizer.At(0);
            Assert.That(customizer.IsLocked(0), Is.True);
            yield return Drag(_screen.PaletteItems[coin], _screen.Cells[0]);
            Assert.That(customizer.At(0), Is.EqualTo(lockedSymbol), "ロックのマスが変わった");

            // 入れたマスから別の空きマスへ動かすと元は空きに戻り、マスの外へ出すと消える。
            var second = first + 1;
            Assert.That(customizer.IsLocked(second), Is.False, "ロック配置が想定と違う（空きが続いていない）");
            yield return Drag(_screen.Cells[first], _screen.Cells[second]);
            Assert.That((customizer.At(first), customizer.At(second)), Is.EqualTo(((SlotSymbol?)null, (SlotSymbol?)SlotSymbol.Heal)),
                "マスからマスへ動かせない");
            yield return Drag(_screen.Cells[second], _screen.PaletteItems[heal]);
            Assert.That(customizer.At(second), Is.Null, "マスの外へ出しても消えない");

            // 空きがあるうちは ✔ で確定しない。
            yield return Click(_screen.ConfirmButton.transform);
            Assert.That(_screen.IsOpen, Is.True, "空きがあるのに ✔ で確定した");

            yield return Drag(_screen.PaletteItems[heal], _screen.Cells[first]);
            for (var i = 0; i < 20 && _screen.FirstEmptyCell >= 0; i++)
            {
                yield return Drag(_screen.PaletteItems[coin], _screen.Cells[_screen.FirstEmptyCell]);
            }

            Assert.That((customizer.EmptyCount, _screen.ConfirmButton.interactable), Is.EqualTo((0, true)), "ドラッグで埋めきれない");
            yield return Click(_screen.ConfirmButton.transform);
            Assert.That((_screen.IsOpen, _battle.IsCustomizing), Is.EqualTo((false, false)), "✔ で確定しない");
            AssertAllReelsUse(SlotSymbol.Heal, first);
        }

        // AC-044: 確定した配列が筐体の抽選と3本すべてのリール表示に使われる。
        private void AssertAllReelsUse(SlotSymbol symbol, int index)
        {
            var layout = _cabinet.Layout;
            Assert.That((layout.At(index), _cabinet.Locks.Accepts(layout)), Is.EqualTo((symbol, true)));
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

        // 押して、途中を通って目的地まで動かし、離す。
        private IEnumerator Drag(Transform from, Transform to)
        {
            var start = RectTransformUtility.WorldToScreenPoint(null, from.position);
            var end = RectTransformUtility.WorldToScreenPoint(null, to.position);
            yield return MouseAt(start, false);
            yield return MouseAt(start, true);
            yield return MouseAt(Vector2.Lerp(start, end, 0.5f), true);
            yield return MouseAt(end, true);
            yield return MouseAt(end, false);
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
