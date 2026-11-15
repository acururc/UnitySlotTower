using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace UnitySlotTower.Editor.Automation
{
    /// <summary>
    /// 撮影用の PlayMode 手順（<c>ScreenCaptureTests</c>、Explicit）だけを実行する（T359、plan 16.13）。
    /// 画像は <c>Artifacts/Automation/Editor/t359/&lt;日時&gt;/</c>。終わると Console に <c>[ScreenCapture] done</c> と結果を出す。
    /// </summary>
    internal static class ScreenCaptureMenu
    {
        private const string TestName = "UnitySlotTower.Tests.PlayMode.Flow.ScreenCaptureTests.Capture_All_Screens_In_Both_Modes";

        [MenuItem("Tools/UnitySlotTower/Automation/Capture Screens (T359)")]
        private static void Run()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callbacks = new Callbacks(api);
            api.RegisterCallbacks(callbacks);
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = new[] { TestName } }));
        }

        private sealed class Callbacks : ICallbacks
        {
            private readonly TestRunnerApi _api;

            public Callbacks(TestRunnerApi api) => _api = api;

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                _api.UnregisterCallbacks(this);
                Debug.Log($"[ScreenCapture] done: {result.TestStatus} passed={result.PassCount} failed={result.FailCount} {result.Message}");
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed)
                {
                    Debug.LogError($"[ScreenCapture] {result.FullName}: {result.Message}");
                }
            }
        }
    }
}
