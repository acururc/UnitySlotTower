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
            var board = Object.FindAnyObjectByType<SlotBoardEffect>();
            Assert.That(board, Is.Not.Null, "Game シーンに盤面全体の演出がない（Build Slot Board Effect (T613)）");

            // バー → 攻撃力の倍率。演出中に敵が進み、レバーを引ける。
            _cabinet.Wallet.Add(1);
            var attackScale = status.AttackRateRect.localScale;
            yield return SpinAndStopAll();
            Assert.That((effect.PlayCount, effect.LastSymbol, effect.LastTarget),
                Is.EqualTo((1, (SlotSymbol?)SlotSymbol.Bar, status.AttackRateRect)));
            Assert.That(effect.IsPlaying, Is.True, "止まり終えたら演出が始まる");
            AssertBoardPlaying(board, SlotSymbol.Bar, effect.PlayCount);
            var layer = ((RectTransform)board.transform).rect;
            Assert.That(board.BoardRect.width > 0f && board.BoardRect.height > 0f &&
                        board.BoardRect.width <= layer.width + 1f && board.BoardRect.height <= layer.height + 1f, Is.True,
                $"盤面の範囲がおかしい: {board.BoardRect} / 層 {layer}");
            yield return null;
            Assert.That(status.AttackRingFill, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f), "攻撃力の残り時間の円が出ない（T611）");
            var tick = _battle.Field.Tick;
            yield return Tap(Key.Space);
            Assert.That(_cabinet.IsSpinning, Is.True, "演出中もレバーを引ける");
            yield return new WaitForSeconds(0.1f);
            Assert.That(_battle.Field.Tick, Is.GreaterThan(tick), "演出中も戦闘が進む");
            yield return WaitForStop();
            yield return WaitForEffect(effect);
            yield return WaitForBoard(board);
            Assert.That(status.AttackRateRect.localScale, Is.EqualTo(attackScale), "行き先の大きさが元に戻る");

            // コイン → 所持金の倍率。
            Select(SlotSymbol.Coin);
            yield return SpinAndStopAll();
            Assert.That((effect.LastSymbol, effect.LastTarget), Is.EqualTo(((SlotSymbol?)SlotSymbol.Coin, status.MoneyRateRect)));
            AssertBoardPlaying(board, SlotSymbol.Coin, effect.PlayCount);
            yield return WaitForEffect(effect);
            yield return WaitForBoard(board);

            // 青7 → 停止のアイコン、黄7 → 毒のアイコン（T611、上端の帯）。発動中はアイコンが濃く、残り時間の円が出る。
            _cabinet.Wallet.Add(3);
            Assert.That((status.IsStopEffectShown, status.StopRingFill), Is.EqualTo((false, 0f)), "揃う前から停止のアイコンが発動中");
            Select(SlotSymbol.Blue7);
            yield return SpinAndStopAll();
            yield return null;
            Assert.That((effect.LastSymbol, effect.LastTarget), Is.EqualTo(((SlotSymbol?)SlotSymbol.Blue7, status.StopEffectRect)));
            AssertBoardPlaying(board, SlotSymbol.Blue7, effect.PlayCount);
            Assert.That((status.IsStopEffectShown, status.StopRingFill > 0f && status.StopRingFill <= 1f), Is.EqualTo((true, true)),
                "停止のアイコンと残り時間の円が出ない");
            yield return WaitForEffect(effect);

            Select(SlotSymbol.Yellow7);
            yield return SpinAndStopAll();
            yield return null;
            Assert.That((effect.LastSymbol, effect.LastTarget), Is.EqualTo(((SlotSymbol?)SlotSymbol.Yellow7, status.PoisonEffectRect)));
            AssertBoardPlaying(board, SlotSymbol.Yellow7, effect.PlayCount);
            Assert.That((status.IsPoisonEffectShown, status.PoisonRingFill > 0f && status.PoisonRingFill <= 1f), Is.EqualTo((true, true)),
                "毒のアイコンと残り時間の円が出ない");
            yield return WaitForEffect(effect);

            // 赤7 → その場の効果なので帯に行き先はない。
            Select(SlotSymbol.Red7);
            yield return SpinAndStopAll();
            Assert.That((effect.LastSymbol, effect.LastTarget), Is.EqualTo(((SlotSymbol?)SlotSymbol.Red7, (RectTransform)null)));
            AssertBoardPlaying(board, SlotSymbol.Red7, effect.PlayCount);
            yield return WaitForEffect(effect);

            // 緑7 → その場の効果。盤面全体の演出だけが出る（T613）。
            _cabinet.Wallet.Add(1);
            Select(SlotSymbol.Green7);
            yield return SpinAndStopAll();
            Assert.That((effect.LastSymbol, effect.LastTarget), Is.EqualTo(((SlotSymbol?)SlotSymbol.Green7, (RectTransform)null)));
            AssertBoardPlaying(board, SlotSymbol.Green7, effect.PlayCount);
            yield return WaitForEffect(effect);
            yield return WaitForBoard(board);
            Assert.That(board.ActivePieceCount, Is.EqualTo(0), "盤面全体の演出の部品が残る");
        }

        [UnityTest]
        [Description("AC-110（T614、FR-116）: 中身は外枠の内側に縮み、外枠の LED は普段流れ、揃うと出目の色で点滅して流れに戻る")]
        public IEnumerator Cabinet_Frame_Lights_Flow_And_Flash_On_Align()
        {
            yield return LoadGame();
            var lights = Object.FindAnyObjectByType<SlotCabinetLights>();
            Assert.That(lights, Is.Not.Null, "Game シーンに外枠の LED がない（Build Cabinet Frame (T614)）");
            Assert.That(lights.LightCount, Is.GreaterThan(40), "LED が少ない");
            Assert.That(lights.WindowLightCount, Is.GreaterThan(20), "リール窓まわりの LED が少ない");
            var root = (RectTransform)GameObject.Find("Portrait Root").transform;
            var scale = (float)UnitySlotTower.Core.Layout.PortraitLayout.ContentScale;
            Assert.That(new[] { root.rect.width, root.rect.height }, Is.EqualTo(new[] { 1440f * scale, 1920f * scale }).Within(0.01f),
                "中身が外枠の内側に縮んでいない");
            foreach (var graphic in lights.GetComponentsInChildren<Graphic>(true))
            {
                Assert.That(graphic.raycastTarget, Is.False, $"外枠の {graphic.name} がクリックを受ける");
            }

            // 普段: 実時間で光が流れる（どれかの LED の色が変わる）。
            var before = new Color[lights.LightCount];
            var windowBefore = new Color[lights.WindowLightCount];
            for (var i = 0; i < before.Length; i++) before[i] = lights.LightColor(i);
            for (var i = 0; i < windowBefore.Length; i++) windowBefore[i] = lights.WindowLightColor(i);
            yield return new WaitForSecondsRealtime(0.3f);
            var changed = false;
            var windowChanged = false;
            for (var i = 0; i < before.Length && !changed; i++) changed = lights.LightColor(i) != before[i];
            for (var i = 0; i < windowBefore.Length && !windowChanged; i++) windowChanged = lights.WindowLightColor(i) != windowBefore[i];
            Assert.That((changed, windowChanged, lights.IsFlashing), Is.EqualTo((true, true, false)), "普段の LED が流れない");

            // 揃うと出目の色で点滅し、しばらくして流れに戻る。
            _cabinet.Wallet.Add(1);
            Select(SlotSymbol.Green7);
            yield return SpinAndStopAll();
            Assert.That((lights.IsFlashing, lights.FlashSymbol), Is.EqualTo((true, (SlotSymbol?)SlotSymbol.Green7)),
                "揃っても LED が出目の色で光らない");
            var until = Time.realtimeSinceStartup + 5f;
            while (lights.IsFlashing && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That((lights.IsFlashing, lights.FlashSymbol), Is.EqualTo((false, (SlotSymbol?)null)), "LED の点滅が終わらない");
        }

        // 揃うと盤面全体の演出がその出目で始まり、盤面の色（少なくとも1つの部品）が出る（T613、FR-113）。
        private static void AssertBoardPlaying(SlotBoardEffect board, SlotSymbol symbol, int count)
        {
            Assert.That((board.PlayCount, board.LastSymbol, board.IsPlaying), Is.EqualTo((count, (SlotSymbol?)symbol, true)),
                $"{symbol} で盤面全体の演出が始まらない");
            Assert.That(board.ActivePieceCount, Is.GreaterThan(0), $"{symbol} の盤面全体の演出の部品が出ない");
        }

        private static IEnumerator WaitForBoard(SlotBoardEffect board)
        {
            var until = Time.realtimeSinceStartup + 5f;
            while (board.IsPlaying && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(board.IsPlaying, Is.False, "盤面全体の演出が終わらない");
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
            var effects = _battle.Field.Effects;
            _random.Index = _random.IndexOf(_cabinet, outcome, s => effects == null || effects.CanRoll(s));
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

            // 発動中の出目は抽選から外れ、残りのパターンの中で数える（SlotMachine.TryLever の canAppear）。
            public int IndexOf(SlotCabinet cabinet, SlotSymbol? outcome, System.Func<SlotSymbol, bool> canAppear = null)
            {
                var allowed = 0;
                for (var i = 0; i < cabinet.Patterns.Count; i++)
                {
                    var pattern = cabinet.Patterns[i];
                    if (pattern.HasValue && canAppear != null && !canAppear(pattern.Value))
                    {
                        continue;
                    }

                    if (pattern == outcome)
                    {
                        return allowed;
                    }

                    allowed++;
                }

                return -1;
            }

            public int NextInt(int minInclusive, int maxExclusive) => Index;

            public double NextDouble() => 0.999;
        }
    }
}
