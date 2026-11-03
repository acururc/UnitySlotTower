using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Simulation;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    [Description("AC-011 の一部・T315: キーでスロットを回して揃えた効果が戦闘に届き、操作バーと筐体の表示に出る（FR-042, FR-043）")]
    public sealed class SlotCabinetEffectTests
    {
        // slot_default_reel.csv のコマ番号（0: seven、1: atk）。
        private const int SevenIndex = 0;
        private const int AttackIndex = 1;

        private Keyboard _keyboard;
        private readonly List<InputDevice> _disabled = new List<InputDevice>();
        private bool _runInBackground;
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private BattleController _battle;
        private SlotCabinet _cabinet;
        private SlotReelView[] _reels;

        [SetUp]
        public void SetUp()
        {
            // Editor が非フォーカスでもフレームと入力が進むようにし、実機のデバイスは止める（後で戻す）。
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
            SlotCabinet.RandomOverride = null;
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
        public IEnumerator Aligning_Attack_Grants_The_Effect_To_The_Battle_And_Shows_Its_Seconds()
        {
            yield return LoadGame(AttackIndex);
            var status = Object.FindAnyObjectByType<OperationBarStatus>();
            var attack = status.transform.parent.Find("Slot Effects/Attack Effect");
            var seconds = attack.Find("Seconds").GetComponent<TMP_Text>();
            Assert.That((seconds.text, attack.GetComponent<CanvasGroup>().alpha < 1f), Is.EqualTo(("0", true)),
                "効果がないときは薄く 0");

            yield return SpinAndStopAll();

            var effects = _battle.Run.Effects;
            var tick = _battle.Field.Tick;
            Assert.That(effects.AttackSeconds, Is.GreaterThan(_cabinet.Rules.AtkSeconds - 2.0).And.AtMost(_cabinet.Rules.AtkSeconds));
            Assert.That(effects.AttackRate, Is.EqualTo(_cabinet.Rules.AtkRate));
            yield return null;
            yield return null;

            Assert.That(int.Parse(seconds.text), Is.EqualTo((int)System.Math.Ceiling(effects.AttackSeconds)).Within(1));
            Assert.That(attack.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(_battle.Field.Tick, Is.GreaterThan(tick), "スロットのあとも戦闘が進む");
        }

        [UnityTest]
        public IEnumerator Aligning_Seven_Lights_The_Bonus_Lamp_With_The_Games_Left()
        {
            yield return LoadGame(SevenIndex);
            var games = _cabinet.transform.Find("Bonus Games").GetComponent<TMP_Text>();
            var lamp = _cabinet.transform.Find("Bonus Lamp").GetComponent<Graphic>();
            var offColor = lamp.color;
            Assert.That(games.enabled, Is.False, "通常時は残りゲーム数を出さない");

            yield return SpinAndStopAll();
            yield return null;

            Assert.That((games.enabled, games.text), Is.EqualTo((true, _cabinet.Rules.BigGames.ToString())));
            Assert.That(lamp.color, Is.Not.EqualTo(offColor));
        }

        private IEnumerator LoadGame(int index)
        {
            SlotCabinet.RandomOverride = new FixedRandom(index);
            yield return SceneManager.LoadSceneAsync("Game");
            _battle = Object.FindAnyObjectByType<BattleController>();
            _cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            for (var i = 0; i < 300 && _battle.Field == null; i++)
            {
                yield return null;
            }

            Assert.That(_battle?.Field, Is.Not.Null, "Game シーンの戦闘が始まらない");
            _reels = _cabinet.GetComponentsInChildren<SlotReelView>();
            _battle.StartWave();
        }

        // Space で回し、J/K/L で止めて、リールが見た目の上でも止まるまで待つ。
        private IEnumerator SpinAndStopAll()
        {
            yield return Tap(Key.Space);
            for (var i = 0; i < 180 && !_reels[0].IsSpinning; i++)
            {
                yield return null;
            }

            Assert.That(_reels[0].IsSpinning, Is.True, "Space でリールが回らない");
            foreach (var key in new[] { Key.J, Key.K, Key.L })
            {
                yield return Tap(key);
            }

            for (var i = 0; i < 300 && System.Array.Exists(_reels, reel => reel.IsSpinning); i++)
            {
                yield return null;
            }

            Assert.That(System.Array.Exists(_reels, reel => reel.IsSpinning), Is.False, "リールが止まらない");
        }

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }

        // ハズレ抽選は必ず当たり、揃う図柄は決めたコマにする。
        private sealed class FixedRandom : IRandom
        {
            private readonly int _index;

            public FixedRandom(int index) => _index = index;

            public int NextInt(int minInclusive, int maxExclusive) => _index;

            public double NextDouble() => 0.999;
        }
    }
}
