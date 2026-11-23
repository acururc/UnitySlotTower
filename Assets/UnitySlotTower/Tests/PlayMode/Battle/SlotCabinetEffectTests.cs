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
using UnitySlotTower.Core.Battle;
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

            var power = Object.FindAnyObjectByType<BarPowerEffect>();
            var bursts = power.BurstCount;
            yield return SpinAndStopAll();
            // 攻撃力アップは闘気が爆発する瞬間から（spec 007 FR-003）。
            Assert.That(_battle.Field.Effects.AttackRate, Is.EqualTo(1d), "揃った瞬間に攻撃力が上がる");
            yield return WaitForBarBurst(power, bursts);

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

            // 攻撃力アップの間は味方ごとにオーラが付き、攻撃ごとに火花が出て、切れるとオーラが消えて部品が残らない（spec 007 AC-003）。
            var attacks = 0;
            var field = _battle.Field;
            void Count(BattleUnit unit, BattleEnemy enemy) => attacks++;
            field.UnitAttacked += Count;
            var sparks = power.SparkCount;
            var fades = power.FadeCount;
            var until = Time.realtimeSinceStartup + _cabinet.Rules.BarSeconds + 3f;
            while (field.Effects.IsAttackUp && Time.realtimeSinceStartup < until)
            {
                Assert.That(power.AuraCount, Is.EqualTo(AliveUnits()), "オーラの数が味方の数と合わない");
                yield return null;
            }

            field.UnitAttacked -= Count;
            yield return null;
            Assert.That((field.Effects.IsAttackUp, power.AuraCount, power.FadeCount, power.SparkCount - sparks >= attacks),
                Is.EqualTo((false, 0, fades + 1, true)), $"攻撃力アップが切れない・オーラが残る・消えない・火花が攻撃の数（{attacks}）より少ない");
            yield return WaitForPowerEnd(power);
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

            // バー → 攻撃力の倍率。盤面全体の演出の代わりに足元の輪が縮まり、待ち時間の後に闘気が爆発する（spec 007）。
            // 演出中に敵が進み、レバーを引ける。
            var power = Object.FindAnyObjectByType<BarPowerEffect>();
            Assert.That(power, Is.Not.Null, "Game シーンにバーの闘気の演出がない（Build Bar Power Effect (spec 007)）");
            _cabinet.Wallet.Add(1);
            var attackScale = status.AttackRateRect.localScale;
            var barBursts = power.BurstCount;
            yield return SpinAndStopAll();
            Assert.That((effect.PlayCount, effect.LastSymbol, effect.LastTarget),
                Is.EqualTo((1, (SlotSymbol?)SlotSymbol.Bar, status.AttackRateRect)));
            Assert.That(effect.IsPlaying, Is.True, "止まり終えたら演出が始まる");
            yield return null;
            Assert.That((power.PlayCount, power.IsCharging, board.PlayCount, power.BurstCount, _battle.Field.Effects.AttackRate),
                Is.EqualTo((1, true, 0, barBursts, 1d)), "バーで輪が縮まらない・盤面全体の演出が出る・すぐ攻撃力が上がる");
            Assert.That(power.RingCount, Is.EqualTo(AliveUnits()), "味方の足元に輪が出ない");
            var tick = _battle.Field.Tick;
            yield return Tap(Key.Space);
            Assert.That(_cabinet.IsSpinning, Is.True, "演出中もレバーを引ける");
            yield return new WaitForSeconds(0.1f);
            Assert.That(_battle.Field.Tick, Is.GreaterThan(tick), "演出中も戦闘が進む");
            yield return WaitForBarBurst(power, barBursts);

            // 爆発の瞬間: 弱く揺れ、ヒットストップはしない（spec 007 FR-002）。
            Assert.That((_battle.IsHitStopping, power.IsShaking, power.IsCharging, _battle.Field.Effects.IsAttackUp),
                Is.EqualTo((false, true, false, true)), "爆発して揺れない・ヒットストップする・輪が止まらない・攻撃力が上がらない");
            yield return null;
            Assert.That(status.AttackRingFill, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f), "攻撃力の残り時間の円が出ない（T611）");
            Assert.That(power.AuraCount, Is.EqualTo(AliveUnits()), "攻撃力アップの間の味方にオーラが付かない");
            yield return WaitForStop();
            yield return WaitForEffect(effect);
            Assert.That(status.AttackRateRect.localScale, Is.EqualTo(attackScale), "行き先の大きさが元に戻る");

            // コイン → 所持金の倍率。溜めの間に引いたレバーの揃いでも盤面全体の演出が出ていることがあるので、ここからの回数で数える。
            yield return WaitForBoard(board);
            var coinBoardCount = board.PlayCount;
            Select(SlotSymbol.Coin);
            yield return SpinAndStopAll();
            Assert.That((effect.LastSymbol, effect.LastTarget), Is.EqualTo(((SlotSymbol?)SlotSymbol.Coin, status.MoneyRateRect)));
            AssertBoardPlaying(board, SlotSymbol.Coin, coinBoardCount + 1);
            var layer = ((RectTransform)board.transform).rect;
            Assert.That(board.BoardRect.width > 0f && board.BoardRect.height > 0f &&
                        board.BoardRect.width <= layer.width + 1f && board.BoardRect.height <= layer.height + 1f, Is.True,
                $"盤面の範囲がおかしい: {board.BoardRect} / 層 {layer}");
            yield return WaitForEffect(effect);
            yield return WaitForBoard(board);

            // 青7 → 停止のアイコン、黄7 → 毒のアイコン（T611、上端の帯）。発動中はアイコンが濃く、残り時間の円が出る。
            _cabinet.Wallet.Add(3);
            Assert.That((status.IsStopEffectShown, status.StopRingFill), Is.EqualTo((false, 0f)), "揃う前から停止のアイコンが発動中");
            // 青7は盤面全体の演出の代わりに冷気が出て、待ち時間の後に凍りついて停止が始まる（spec 004）。
            var freeze = Object.FindAnyObjectByType<Blue7FreezeEffect>();
            Assert.That(freeze, Is.Not.Null, "Game シーンに青7の凍結の演出がない（Build Blue7 Freeze Effect (spec 004)）");
            var blueBoardCount = board.PlayCount;
            var freezes = freeze.FreezeCount;
            Select(SlotSymbol.Blue7);
            yield return SpinAndStopAll();
            yield return null;
            Assert.That((effect.LastSymbol, effect.LastTarget), Is.EqualTo(((SlotSymbol?)SlotSymbol.Blue7, status.StopEffectRect)));
            Assert.That((freeze.PlayCount, freeze.IsChilling, board.PlayCount, freeze.FreezeCount, _battle.Field.Effects.IsStopped),
                Is.EqualTo((1, true, blueBoardCount, freezes, false)), "青7で冷気が始まらない・盤面全体の演出が出る・すぐ止まる");
            Assert.That(freeze.ActiveParticleCount, Is.GreaterThan(0), "予兆の粒が出ない");
            yield return WaitForFreeze(freeze, freezes);

            // 凍結の瞬間に戦闘だけが一瞬止まり、その間もレバーを引ける（spec 004 AC-004）。
            Assert.That((_battle.IsHitStopping, freeze.IsShaking, freeze.IsChilling, _battle.Field.Effects.IsStopped),
                Is.EqualTo((true, true, false, true)), "凍結で戦闘が止まらない・揺れない・冷気が止まらない・停止が始まらない");
            var frozenTick = _battle.Field.Tick;
            _cabinet.Wallet.Add(1);
            Select(SlotSymbol.Coin);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
            yield return null;
            if (_battle.IsHitStopping)
            {
                Assert.That(_battle.Field.Tick, Is.EqualTo(frozenTick), "ヒットストップ中に戦闘が進む");
            }

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            Assert.That(_cabinet.IsSpinning, Is.True, "凍結の直後もレバーを引ける");
            Assert.That((status.IsStopEffectShown, status.StopRingFill > 0f && status.StopRingFill <= 1f), Is.EqualTo((true, true)),
                "停止のアイコンと残り時間の円が出ない");

            // 停止中は生きている敵ごとに氷の塊が付き、停止が切れると砕けて消える（spec 004 AC-005）。
            // 前のフレームの数なので、その間に倒された1体ぶんのずれは許す。
            Assert.That(freeze.IceCount, Is.EqualTo(AliveEnemies()).Within(1), "停止中の敵に氷の塊が付かない");
            var shatters = freeze.ShatterCount;
            yield return WaitForStop();
            yield return WaitForEffect(effect);
            yield return WaitForBoard(board);
            var until = Time.realtimeSinceStartup + 8f;
            while (_battle.Field.Effects.IsStopped && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            yield return null;
            Assert.That((_battle.Field.Effects.IsStopped, freeze.IceCount, freeze.ShatterCount), Is.EqualTo((false, 0, shatters + 1)),
                "停止が切れない・氷が残る・砕けない");
            yield return WaitForFreezeEnd(freeze);

            // 黄7は盤面全体の演出の代わりに毒の沼が湧き、待ち時間の後に弾けて毒が始まる（spec 006）。
            var poison = Object.FindAnyObjectByType<Yellow7PoisonEffect>();
            Assert.That(poison, Is.Not.Null, "Game シーンに黄7の毒の演出がない（Build Yellow7 Poison Effect (spec 006)）");
            var yellowBoardCount = board.PlayCount;
            var bursts = poison.BurstCount;
            Select(SlotSymbol.Yellow7);
            yield return SpinAndStopAll();
            yield return null;
            Assert.That((effect.LastSymbol, effect.LastTarget), Is.EqualTo(((SlotSymbol?)SlotSymbol.Yellow7, status.PoisonEffectRect)));
            Assert.That((poison.PlayCount, poison.IsBubbling, board.PlayCount, poison.BurstCount, _battle.Field.Effects.IsPoisoned),
                Is.EqualTo((1, true, yellowBoardCount, bursts, false)), "黄7で沼が湧かない・盤面全体の演出が出る・すぐ毒が始まる");
            Assert.That(poison.ActiveParticleCount, Is.GreaterThan(0), "沼の粒が出ない");
            // 敵の足元にも沼が湧く（前のフレームの数なので1体ぶんのずれは許す）。
            Assert.That(poison.EnemySwampCount, Is.EqualTo(AliveEnemies()).Within(1), "敵の足元に沼が湧かない");
            yield return WaitForBurst(poison, bursts);

            // 弾ける瞬間: 弱く揺れ、ヒットストップはしない（spec 006 FR-002）。
            Assert.That((_battle.IsHitStopping, poison.IsShaking, poison.IsBubbling, _battle.Field.Effects.IsPoisoned),
                Is.EqualTo((false, true, false, true)), "弾けて揺れない・ヒットストップする・沼が止まらない・毒が始まらない");
            yield return null;
            Assert.That((status.IsPoisonEffectShown, status.PoisonRingFill > 0f && status.PoisonRingFill <= 1f), Is.EqualTo((true, true)),
                "毒のアイコンと残り時間の円が出ない");

            // 毒の間は生きている敵ごとにオーラが付き、ダメージごとに点滅し、毒が切れると消える（spec 006 AC-003）。
            Assert.That(poison.AuraCount, Is.EqualTo(AliveEnemies()).Within(1), "毒の間の敵にオーラが付かない");
            var flashes = poison.FlashCount;
            var fades = poison.FadeCount;
            var flashUntil = Time.realtimeSinceStartup + 3f;
            while (poison.FlashCount == flashes && Time.realtimeSinceStartup < flashUntil)
            {
                yield return null;
            }

            Assert.That(poison.FlashCount, Is.GreaterThan(flashes), "毒のダメージでオーラが点滅しない");
            var poisonUntil = Time.realtimeSinceStartup + 8f;
            while (_battle.Field.Effects.IsPoisoned && Time.realtimeSinceStartup < poisonUntil)
            {
                yield return null;
            }

            yield return null;
            Assert.That((_battle.Field.Effects.IsPoisoned, poison.AuraCount, poison.FadeCount), Is.EqualTo((false, 0, fades + 1)),
                "毒が切れない・オーラが残る・消えない");
            yield return WaitForEffect(effect);
            yield return WaitForPoisonEnd(poison);

            // 赤7 → その場の効果なので帯に行き先はない。盤面全体の演出の代わりに息吹が出て、待ち時間の後に爆発する（spec 003）。
            var breath = Object.FindAnyObjectByType<Red7BreathEffect>();
            Assert.That(breath, Is.Not.Null, "Game シーンに赤7の息吹の演出がない（Build Red7 Breath Effect (spec 003)）");
            var boardCount = board.PlayCount;
            var strikes = breath.StrikeCount;
            Select(SlotSymbol.Red7);
            yield return SpinAndStopAll();
            Assert.That((effect.LastSymbol, effect.LastTarget), Is.EqualTo(((SlotSymbol?)SlotSymbol.Red7, (RectTransform)null)));
            Assert.That((breath.PlayCount, breath.IsBreathing, board.PlayCount, breath.StrikeCount),
                Is.EqualTo((1, true, boardCount, strikes)), "赤7で息吹が始まらない・盤面全体の演出が出る・すぐ爆発する");
            yield return null;
            Assert.That(breath.ActiveParticleCount, Is.GreaterThan(0), "予兆の粒が出ない");
            yield return WaitForStrike(breath, strikes);

            // 爆発の瞬間に戦闘だけが一瞬止まり、その間もレバーを引ける（spec 003 AC-006）。
            Assert.That((_battle.IsHitStopping, breath.IsShaking, breath.IsBreathing), Is.EqualTo((true, true, false)),
                "爆発で戦闘が止まらない・揺れない・息吹が止まらない");
            var struckTick = _battle.Field.Tick;
            _cabinet.Wallet.Add(1);
            Select(SlotSymbol.Coin);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Space));
            yield return null;
            if (_battle.IsHitStopping)
            {
                Assert.That(_battle.Field.Tick, Is.EqualTo(struckTick), "ヒットストップ中に戦闘が進む");
            }

            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            Assert.That(_cabinet.IsSpinning, Is.True, "爆発の直後もレバーを引ける");
            yield return new WaitForSeconds(0.3f);
            Assert.That((_battle.IsHitStopping, _battle.Field.Tick > struckTick), Is.EqualTo((false, true)), "ヒットストップの後に戦闘が進まない");
            yield return WaitForStop();
            yield return WaitForEffect(effect);
            yield return WaitForBreath(breath);

            // 緑7 → その場の効果で帯に行き先はない。盤面全体の演出の代わりに光が降り、待ち時間の後に光の柱が立って回復する（spec 005）。
            var heal = Object.FindAnyObjectByType<Green7HealEffect>();
            Assert.That(heal, Is.Not.Null, "Game シーンに緑7の回復の演出がない（Build Green7 Heal Effect (spec 005)）");
            var greenBoardCount = board.PlayCount;
            for (var round = 0; round < 2; round++)
            {
                // 続けて揃えても同じように出て、部品が残らない（AC-003）。
                var heals = heal.HealCount;
                _cabinet.Wallet.Add(1);
                Select(SlotSymbol.Green7);
                yield return SpinAndStopAll();
                yield return null;
                Assert.That((effect.LastSymbol, effect.LastTarget), Is.EqualTo(((SlotSymbol?)SlotSymbol.Green7, (RectTransform)null)));
                Assert.That((heal.PlayCount, heal.IsShowering, board.PlayCount, heal.HealCount),
                    Is.EqualTo((round + 1, true, greenBoardCount, heals)), "緑7で光が降り始めない・盤面全体の演出が出る・すぐ回復する");
                Assert.That(heal.ActiveParticleCount, Is.GreaterThan(0), "予兆の粒が出ない");
                Assert.That(heal.IsCircleShown, Is.True, "待ちの間に盤面全体の魔法陣が出ない（spec 008 FR-003）");
                var circleFlashes = heal.CircleFlashCount;
                yield return WaitForHeal(heal, heals);

                // 柱の瞬間: 生きている味方ごとに柱が立ち、戦闘は止まらない（FR-003）。魔法陣は強く光って消えていく（spec 008）。
                Assert.That((heal.PillarCount, heal.IsShowering, _battle.IsHitStopping), Is.EqualTo((AliveUnits(), false, false)),
                    "味方ごとに光の柱が立たない・光が降り止まない・ヒットストップする");
                Assert.That((heal.CircleFlashCount, heal.IsCircleShown), Is.EqualTo((circleFlashes + 1, false)),
                    "回復の瞬間に魔法陣が光らない・残る");
                yield return WaitForStop();
                yield return WaitForEffect(effect);
                yield return WaitForHealEnd(heal);
            }

            Assert.That(board.ActivePieceCount, Is.EqualTo(0), "盤面全体の演出の部品が残る");
        }

        [UnityTest]
        [Description("AC-110（T614、FR-116）: 中身は外枠の内側に縮み、LED は普段落ち着いて光り、揃うとリール窓から出目の色の波が広がって普段に戻る")]
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

            // 普段: 実時間でゆっくり明るさが揺れる（どれかの LED の色が変わる）。
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
            // 波はリール窓から広がる: 窓の LED のほうが外枠の LED より中心に近い。
            var nearestWindow = float.MaxValue;
            var nearestFrame = float.MaxValue;
            for (var i = 0; i < lights.WindowLightCount; i++) nearestWindow = Mathf.Min(nearestWindow, lights.WindowLightDistance(i));
            for (var i = 0; i < lights.LightCount; i++) nearestFrame = Mathf.Min(nearestFrame, lights.LightDistance(i));
            Assert.That(nearestWindow, Is.LessThan(nearestFrame), "光の波がリール窓から広がらない");
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

        // 赤7の爆発を待つ（待ち時間は slot_rules.csv の red_delay_seconds、戦闘の時間）。
        private static IEnumerator WaitForStrike(Red7BreathEffect breath, int strikes)
        {
            var until = Time.realtimeSinceStartup + 5f;
            while (breath.StrikeCount == strikes && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(breath.StrikeCount, Is.EqualTo(strikes + 1), "赤7の息吹が爆発しない");
        }

        // 息吹・揺れ・粒がすべて終わる（部品が残らない、spec 003 AC-002）。
        private static IEnumerator WaitForBreath(Red7BreathEffect breath)
        {
            var until = Time.realtimeSinceStartup + 6f;
            while (breath.IsPlaying && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That((breath.IsPlaying, breath.ActiveParticleCount), Is.EqualTo((false, 0)), "赤7の息吹の演出が終わらない・粒が残る");
        }

        // 青7の凍結を待つ（待ち時間は slot_rules.csv の blue_delay_seconds、戦闘の時間）。
        private static IEnumerator WaitForFreeze(Blue7FreezeEffect freeze, int freezes)
        {
            var until = Time.realtimeSinceStartup + 5f;
            while (freeze.FreezeCount == freezes && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(freeze.FreezeCount, Is.EqualTo(freezes + 1), "青7で盤面が凍りつかない");
        }

        // 冷気・氷・揺れ・粒がすべて終わる（部品が残らない、spec 004 AC-005）。
        private static IEnumerator WaitForFreezeEnd(Blue7FreezeEffect freeze)
        {
            var until = Time.realtimeSinceStartup + 6f;
            while (freeze.IsPlaying && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That((freeze.IsPlaying, freeze.ActiveParticleCount), Is.EqualTo((false, 0)), "青7の凍結の演出が終わらない・粒が残る");
        }

        // バーの闘気が爆発するのを待つ（待ち時間は slot_rules.csv の bar_delay_seconds、戦闘の時間）。
        private static IEnumerator WaitForBarBurst(BarPowerEffect power, int bursts)
        {
            var until = Time.realtimeSinceStartup + 5f;
            while (power.BurstCount == bursts && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(power.BurstCount, Is.EqualTo(bursts + 1), "バーの闘気が爆発しない");
        }

        // 輪・オーラ・揺れ・粒がすべて終わる（部品が残らない、spec 007 AC-003）。
        private static IEnumerator WaitForPowerEnd(BarPowerEffect power)
        {
            var until = Time.realtimeSinceStartup + 6f;
            while (power.IsPlaying && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That((power.IsPlaying, power.ActiveParticleCount), Is.EqualTo((false, 0)), "バーの闘気の演出が終わらない・粒が残る");
        }

        // 黄7の沼が弾けるのを待つ（待ち時間は slot_rules.csv の yellow_delay_seconds、戦闘の時間）。
        private static IEnumerator WaitForBurst(Yellow7PoisonEffect poison, int bursts)
        {
            var until = Time.realtimeSinceStartup + 5f;
            while (poison.BurstCount == bursts && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(poison.BurstCount, Is.EqualTo(bursts + 1), "黄7の沼が弾けない");
        }

        // 沼・オーラ・揺れ・粒がすべて終わる（部品が残らない、spec 006 AC-003）。
        private static IEnumerator WaitForPoisonEnd(Yellow7PoisonEffect poison)
        {
            var until = Time.realtimeSinceStartup + 6f;
            while (poison.IsPlaying && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That((poison.IsPlaying, poison.ActiveParticleCount), Is.EqualTo((false, 0)), "黄7の毒の演出が終わらない・粒が残る");
        }

        // 緑7の光の柱を待つ（待ち時間は slot_rules.csv の green_delay_seconds、戦闘の時間）。
        private static IEnumerator WaitForHeal(Green7HealEffect heal, int heals)
        {
            var until = Time.realtimeSinceStartup + 5f;
            while (heal.HealCount == heals && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That(heal.HealCount, Is.EqualTo(heals + 1), "緑7の光の柱が立たない");
        }

        // 降る光・柱・粒がすべて終わる（部品が残らない、spec 005 AC-003）。
        private static IEnumerator WaitForHealEnd(Green7HealEffect heal)
        {
            var until = Time.realtimeSinceStartup + 6f;
            while (heal.IsPlaying && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.That((heal.IsPlaying, heal.ActiveParticleCount), Is.EqualTo((false, 0)), "緑7の回復の演出が終わらない・粒が残る");
        }

        private int AliveUnits()
        {
            var count = 0;
            foreach (var unit in _battle.Field.Units)
            {
                if (unit.IsAlive) count++;
            }

            return count;
        }

        private int AliveEnemies()
        {
            var count = 0;
            foreach (var enemy in _battle.Field.Enemies)
            {
                if (enemy.IsAlive) count++;
            }

            return count;
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
