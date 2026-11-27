using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnitySlotTower.Battle;

namespace UnitySlotTower.Tests.PlayMode
{
    /// <summary>
    /// PlayMode テスト全体の前後処理。ステージ1の見せ場（spec 015 FR-001、ウェーブ1で止まってレバーを待つ）は既定で飛ばす。
    /// 見せ場を見るテスト（<c>SlotBetShowcaseTests</c>・<c>ScreenCaptureTests</c>）は自分の SetUp で false にし、TearDown で true に戻す。
    /// 入力のテストは SetUp で実機のデバイスを止めてテスト用のキーボードなどを足し、TearDown で戻す。SetUp が失敗すると
    /// TearDown が走らず、実機のマウスとキーボードが止まったまま Editor に残る（Game ビューを触れなくなる）ので、最後にまとめて戻す。
    /// </summary>
    [SetUpFixture]
    public sealed class PlayModeTestSetup
    {
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;
        private bool _runInBackground;
        private readonly List<InputDevice> _devices = new List<InputDevice>();

        [OneTimeSetUp]
        public void SetUp()
        {
            BattleController.SkipShowcase = true;
            _editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
            _runInBackground = Application.runInBackground;
            _devices.Clear();
            _devices.AddRange(InputSystem.devices);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            BattleController.SkipShowcase = false;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorBehavior;
            InputSystem.settings.backgroundBehavior = _backgroundBehavior;
            Application.runInBackground = _runInBackground;

            // テストが足して残ったデバイスを外し、始める前にあったデバイスを動かす。
            var now = new List<InputDevice>(InputSystem.devices);
            foreach (var device in now)
            {
                if (!_devices.Contains(device))
                {
                    InputSystem.RemoveDevice(device);
                }
                else if (!device.enabled)
                {
                    InputSystem.EnableDevice(device);
                }
            }

            _devices.Clear();
        }
    }
}
