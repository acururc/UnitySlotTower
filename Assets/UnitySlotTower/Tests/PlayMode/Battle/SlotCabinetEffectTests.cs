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
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Slot;

namespace UnitySlotTower.Tests.PlayMode.Battle
{
    [Description("AC-011 の一部: キーでスロットを回して揃えた効果が戦闘に届き、操作バーに出る（FR-042: バーで攻撃力の倍率が上がる）")]
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
        // 操作バーの攻撃力の印の横にバーの倍率を出す（T349、plan 16.8）。
        public IEnumerator Aligning_Bar_Raises_The_Attack_Rate_Of_The_Battle_And_Shows_It()
        {
            yield return LoadGame();
            var status = Object.FindAnyObjectByType<OperationBarStatus>();
            var attack = status.transform.parent.Find("Rates/Attack Rate");
            var rate = attack.Find("Value").GetComponent<TMP_Text>();
            Assert.That((rate.text, attack.GetComponent<CanvasGroup>().alpha < 1f), Is.EqualTo(("×1.0", true)),
                "倍率がないときは薄く ×1.0");

            yield return SpinAndStopAll();

            var tick = _battle.Field.Tick;
            Assert.That(_battle.Field.Effects.AttackRate, Is.EqualTo(_cabinet.Rules.BarAttackRate).Within(1e-9));
            yield return null;
            yield return null;

            Assert.That(rate.text, Is.EqualTo("×" + _cabinet.Rules.BarAttackRate.ToString("0.0",
                System.Globalization.CultureInfo.InvariantCulture)));
            Assert.That(attack.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));

            // 戦闘は実時間の 1/60 秒ごとに進むため、フレームが速いと2フレームでは tick が増えない。実時間で待つ。
            yield return new WaitForSeconds(0.1f);
            Assert.That(_battle.Field.Tick, Is.GreaterThan(tick), "スロットのあとも戦闘が進む");
        }

        [UnityTest]
        [Description("AC-046: 揃いで中段が光って効果の行き先へ光が飛ぶ。演出中も敵が進み、レバーを引ける")]
        public IEnumerator Aligning_Plays_The_Effect_Toward_Its_Target_Without_Stopping_The_Battle()
        {
            yield return LoadGame();
            var effect = Object.FindAnyObjectByType<SlotAlignEffect>();
            Assert.That(effect, Is.Not.Null, "Game シーンに揃った時の演出がない（Build Slot Align Effect (T366)）");
            var status = Object.FindAnyObjectByType<OperationBarStatus>();

            // バー → 攻撃力の倍率。演出中に敵が進み、レバーを引ける。
            _cabinet.Wallet.Add(1);
            var attackScale = status.AttackRateRect.localScale;
            yield return SpinAndStopAll();
            Assert.That((effect.PlayCount, effect.LastSymbol, effect.LastTarget),
                Is.EqualTo((1, (SlotSymbol?)SlotSymbol.Bar, status.AttackRateRect)));
            Assert.That(effect.IsPlaying, Is.True, "止まり終えたら演出が始まる");
            var tick = _battle.Field.Tick;
            yield return Tap(Key.Space);
            Assert.That(_cabinet.IsSpinning, Is.True, "演出中もレバーを引ける");
            yield return new WaitForSeconds(0.1f);
            Assert.That(_battle.Field.Tick, Is.GreaterThan(tick), "演出中も戦闘が進む");
            yield return WaitForStop();
            yield return WaitForEffect(effect);
            Assert.That(status.AttackRateRect.localScale, Is.EqualTo(attackScale), "行き先の大きさが元に戻る");

            // コイン → 所持金の倍率。
            Select(SlotSymbol.Coin);
            yield return SpinAndStopAll();
            Assert.That((effect.LastSymbol, effect.LastTarget), Is.EqualTo(((SlotSymbol?)SlotSymbol.Coin, status.MoneyRateRect)));
            yield return WaitForEffect(effect);

            // 色7 → 行き先はまだない（スキルゲージは削除。上端の帯の効果のアイコンは T611）。
            SlotSymbol? color = null;
            foreach (var c in new[] { SlotSymbol.Red7, SlotSymbol.Blue7, SlotSymbol.Green7, SlotSymbol.Yellow7 })
            {
                if (color == null && _random.IndexOf(_cabinet, c) >= 0)
                {
                    color = c;
                }
            }

            Assert.That(color, Is.Not.Null, "色7が抽選表にない");
            Select(color.Value);
            yield return SpinAndStopAll();
            Assert.That((effect.LastSymbol, effect.LastTarget), Is.EqualTo((color, (RectTransform)null)), "色7の行き先はまだない");
            yield return WaitForEffect(effect);
        }

        private FixedRandom _random;

        private IEnumerator LoadGame()
        {
            _random = new FixedRandom();
            SlotCabinet.RandomOverride = _random;
            yield return SceneManager.LoadSceneAsync("Game");
            _battle = Object.FindAnyObjectByType<BattleController>();
            _cabinet = Object.FindAnyObjectByType<SlotCabinet>();
            for (var i = 0; i < 300 && _battle.Field == null; i++)
            {
                yield return null;
            }

            Assert.That(_battle.Field, Is.Not.Null, "Game シーンの戦闘が始まらない");
            yield return StageTestUtil.WaitForIntro(_battle);
            Select(SlotSymbol.Bar);
            yield return null;
            _reels = _cabinet.GetComponentsInChildren<SlotReelView>();
        }

        // 20パターンからその出目（null はハズレ）の番号を選ぶ。
        private void Select(SlotSymbol? outcome)
        {
            _random.Index = _random.IndexOf(_cabinet, outcome);
            Assert.That(_random.Index, Is.GreaterThanOrEqualTo(0), $"抽選表に {outcome?.ToString() ?? "ハズレ"} がない");
        }

        private IEnumerator WaitForStop()
        {
            var until = Time.realtimeSinceStartup + 5f;
            while (_cabinet.IsSpinning && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(_cabinet.IsSpinning, Is.False, "リールが止まらない");
        }

        private static IEnumerator WaitForEffect(SlotAlignEffect effect)
        {
            var until = Time.realtimeSinceStartup + 5f;
            while (effect.IsPlaying && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(effect.IsPlaying, Is.False, "演出が終わらない");
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

            public int IndexOf(SlotCabinet cabinet, SlotSymbol? outcome)
            {
                for (var i = 0; i < cabinet.Patterns.Count; i++)
                {
                    if (cabinet.Patterns[i] == outcome)
                    {
                        return i;
                    }
                }

                return -1;
            }

            public int NextInt(int minInclusive, int maxExclusive) => Index;

            public double NextDouble() => 0.999;
        }
    }
}
