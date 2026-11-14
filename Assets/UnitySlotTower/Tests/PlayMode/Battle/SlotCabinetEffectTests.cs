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
    [Description("AC-011 の一部: キーでスロットを回して揃えた効果が戦闘に届き、操作バーに出る（FR-042。T342 までのつなぎでバー＝旧の攻撃力アップ）")]
    public sealed class SlotCabinetEffectTests
    {
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
        // 操作バーの作り直し（T349）までは、旧「攻撃力アップ」の表示にバーの上がり幅（%）を出す。
        public IEnumerator Aligning_Bar_Raises_The_Attack_Rate_Of_The_Battle_And_Shows_It()
        {
            yield return LoadGame();
            var status = Object.FindAnyObjectByType<OperationBarStatus>();
            var attack = status.transform.parent.Find("Slot Effects/Attack Effect");
            var percent = attack.Find("Seconds").GetComponent<TMP_Text>();
            Assert.That((percent.text, attack.GetComponent<CanvasGroup>().alpha < 1f), Is.EqualTo(("0", true)),
                "効果がないときは薄く 0");

            yield return SpinAndStopAll();

            var tick = _battle.Field.Tick;
            Assert.That(_battle.Field.Boosts.AttackRate, Is.EqualTo(1 + _cabinet.Rules.BarStep).Within(1e-9));
            yield return null;
            yield return null;

            Assert.That(percent.text, Is.EqualTo(((int)System.Math.Round(_cabinet.Rules.BarStep * 100)).ToString()));
            Assert.That(attack.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));

            // 戦闘は実時間の 1/60 秒ごとに進むため、フレームが速いと2フレームでは tick が増えない。実時間で待つ。
            yield return new WaitForSeconds(0.1f);
            Assert.That(_battle.Field.Tick, Is.GreaterThan(tick), "スロットのあとも戦闘が進む");
        }

        private IEnumerator LoadGame()
        {
            var random = new FixedRandom();
            SlotCabinet.RandomOverride = random;
            yield return SceneManager.LoadSceneAsync("Game");
            _battle = Object.FindAnyObjectByType<BattleController>();
            _cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            for (var i = 0; i < 300 && _battle.Field == null; i++)
            {
                yield return null;
            }

            Assert.That(_battle?.Field, Is.Not.Null, "Game シーンの戦闘が始まらない");
            // この難易度の20パターンからバーの番号を選ぶ。
            random.Index = -1;
            for (var i = 0; i < _cabinet.Patterns.Count && random.Index < 0; i++)
            {
                if (_cabinet.Patterns[i] == Core.Slot.SlotSymbol.Bar)
                {
                    random.Index = i;
                }
            }

            Assert.That(random.Index, Is.GreaterThanOrEqualTo(0), "抽選表にバーがない");
            yield return null;
            _reels = _cabinet.GetComponentsInChildren<SlotReelView>();
            _battle.StartWave();
        }

        // Space で回し、リールが自動で止まるまで実時間で待つ。
        private IEnumerator SpinAndStopAll()
        {
            yield return Tap(Key.Space);
            for (var i = 0; i < 180 && !_reels[0].IsSpinning; i++)
            {
                yield return null;
            }

            Assert.That(_reels[0].IsSpinning, Is.True, "Space でリールが回らない");
            var until = Time.realtimeSinceStartup + 5f;
            while (_cabinet.IsSpinning && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(_cabinet.IsSpinning, Is.False, "リールが止まらない");
        }

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }

        // 20パターンの Index 番目を選ぶ。
        private sealed class FixedRandom : IRandom
        {
            public int Index;

            public int NextInt(int minInclusive, int maxExclusive) => Index;

            public double NextDouble() => 0.999;
        }
    }
}
