using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnitySlotTower.Battle;
using UnitySlotTower.Core.Slot;
using UnitySlotTower.Core.Ui;
using UnitySlotTower.Flow;

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
            GameSession.StartGame();
            BattleController battle = null;
            for (var i = 0; i < 300 && (battle == null || battle.Run == null); i++)
            {
                yield return null;
                battle = SceneManager.GetActiveScene().name == GameSession.GameScene
                    ? Object.FindAnyObjectByType<BattleController>()
                    : null;
            }

            Assert.That(battle != null && battle.Run != null, Is.True, "Game シーンの進行が作られない");
            yield return null;
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
            // spec 016 FR-004: 数値は 青7・黄7・バーの秒数（slot_rules.csv: 3・5・10）と、味方のブロック・コスト・待ち秒数（units.csv）だけ。
            var seconds = (TMP_Text[])Field(help, "secondsTexts");
            Assert.That((seconds[(int)SlotSymbol.Blue7].text, seconds[(int)SlotSymbol.Yellow7].text, seconds[(int)SlotSymbol.Bar].text),
                Is.EqualTo(("3", "5", "10")), "青7・黄7・バーの秒数");
            Assert.That(seconds[(int)SlotSymbol.Red7] == null && seconds[(int)SlotSymbol.Green7] == null && seconds[(int)SlotSymbol.Coin] == null,
                Is.True, "秒数のない図柄に数字がある");
            var rows = (HelpScreen.UnitRow[])Field(help, "unitRows");
            Assert.That((rows[0].blockText.text, rows[0].costText.text, rows[0].cooldownText.text), Is.EqualTo(("3", "¥130", "20")), "タンク");
            Assert.That((rows[3].blockText.text, rows[3].costText.text, rows[3].cooldownText.text), Is.EqualTo(("×", "¥150", "25")), "バッファー");
            foreach (var name in new[] { "missText", "chanceTexts", "effectTexts", "capTexts" })
            {
                Assert.That(typeof(HelpScreen).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance), Is.Null, $"{name} が残っている");
            }

            foreach (var text in help.GetComponentsInChildren<TMP_Text>(true))
            {
                // 文字あり版のラベル（T352）は対象外。文字なし版では描かれないことだけ確かめる。
                if (text.GetComponentInParent<UnitySlotTower.Ui.TextModeLabel>(true) != null)
                {
                    Assert.That(text.isActiveAndEnabled, Is.False, $"{text.name}: 文字なし版でラベルが出ている");
                    continue;
                }

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

        [UnityTest]
        public IEnumerator Help_Pages_Switch_With_Tabs_And_Arrows()
        {
            GameSession.StartGame();
            BattleController battle = null;
            for (var i = 0; i < 300 && (battle == null || battle.Run == null); i++)
            {
                yield return null;
                if (SceneManager.GetActiveScene().name == GameSession.GameScene)
                    battle = Object.FindAnyObjectByType<BattleController>();
            }
            Assert.That(battle != null && battle.Run != null, Is.True);
            var help = Object.FindAnyObjectByType<HelpScreen>();
            battle.SetPaused(true);
            yield return null;

            void Check(HelpScreen.Page page)
            {
                Assert.That(help.CurrentPage, Is.EqualTo(page));
                for (var p = 0; p < HelpScreen.PageCount; p++)
                {
                    var shown = help.PageRoot((HelpScreen.Page)p).activeInHierarchy;
                    Assert.That(shown, Is.EqualTo(p == (int)page), $"{(HelpScreen.Page)p} の表示");
                }

                Assert.That(help.PreviousButton.gameObject.activeInHierarchy && help.NextButton.gameObject.activeInHierarchy, Is.True);
                Assert.That(battle.IsPaused, Is.True);
                Assert.That(Time.timeScale, Is.Zero);
            }

            // spec 016 FR-001: 5ページ。最初は遊び方、タブで直接、◀ ▶ は端で反対の端へ回る。
            help.OpenButton.onClick.Invoke();
            Check(HelpScreen.Page.Guide);
            for (var p = 0; p < HelpScreen.PageCount; p++)
            {
                help.Tab((HelpScreen.Page)p).onClick.Invoke();
                Check((HelpScreen.Page)p);
            }

            help.NextButton.onClick.Invoke();
            Check(HelpScreen.Page.Guide);
            help.PreviousButton.onClick.Invoke();
            Check(HelpScreen.Page.Enemies);
            help.PreviousButton.onClick.Invoke();
            Check(HelpScreen.Page.Units);
            help.CloseButton.onClick.Invoke();
            Assert.That(help.IsOpen, Is.False);
            Assert.That(Time.timeScale, Is.Zero);
            yield return null;
            // 開き直すと前のページのまま。
            help.OpenButton.onClick.Invoke();
            Check(HelpScreen.Page.Units);
            help.Tab(HelpScreen.Page.Bet).onClick.Invoke();
            Check(HelpScreen.Page.Bet);
        }

        [UnityTest]
        [Description("spec 016 FR-002: 各ページに動く図があり、一時停止中も実時間で動く。見えていないページの図は動かない")]
        public IEnumerator Help_Demos_Move_While_Paused()
        {
            GameSession.StartGame();
            BattleController battle = null;
            for (var i = 0; i < 300 && (battle == null || battle.Run == null); i++)
            {
                yield return null;
                if (SceneManager.GetActiveScene().name == GameSession.GameScene)
                    battle = Object.FindAnyObjectByType<BattleController>();
            }

            Assert.That(battle?.Run, Is.Not.Null);
            var help = Object.FindAnyObjectByType<HelpScreen>();
            battle.SetPaused(true);
            yield return null;
            help.OpenButton.onClick.Invoke();
            for (var p = 0; p < HelpScreen.PageCount; p++)
            {
                var page = (HelpScreen.Page)p;
                help.Tab(page).onClick.Invoke();
                var demos = help.PageRoot(page).GetComponentsInChildren<HelpDemo>();
                Assert.That(demos.Length, Is.GreaterThan(0), $"{page} に動く図がない");
                var before = Snapshot(demos);
                var hidden = help.PageRoot((HelpScreen.Page)((p + 1) % HelpScreen.PageCount)).GetComponentsInChildren<HelpDemo>(true);
                var hiddenBefore = Snapshot(hidden);
                var start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < 0.6f)
                {
                    yield return null;
                }

                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(Snapshot(demos), Is.Not.EqualTo(before), $"{page} の図が一時停止中に動かない");
                Assert.That(Snapshot(hidden), Is.EqualTo(hiddenBefore), "見えていないページの図が動いている");
            }
        }

        // 図の中の動く部品の位置と色をまとめた値。
        private static List<string> Snapshot(HelpDemo[] demos)
        {
            var values = new List<string>();
            foreach (var demo in demos)
            {
                foreach (var track in demo.Tracks)
                {
                    if (track.target == null) continue;
                    var alpha = track.graphic != null ? track.graphic.color.a : track.group != null ? track.group.alpha : 1f;
                    values.Add($"{track.target.anchorMin}{track.target.anchorMax}{alpha:0.000}");
                }
            }

            return values;
        }

        [UnityTest]
        public IEnumerator Pause_Band_And_Quit_Confirmation()
        {
            GameSession.StartGame();
            BattleController battle = null;
            for (var i = 0; i < 300 && (battle == null || battle.Run == null); i++)
            {
                yield return null;
                if (SceneManager.GetActiveScene().name == GameSession.GameScene)
                    battle = Object.FindAnyObjectByType<BattleController>();
            }
            Assert.That(battle?.Run, Is.Not.Null);
            var help = Object.FindAnyObjectByType<HelpScreen>();
            var quit = Object.FindAnyObjectByType<QuitToTitle>();
            Assert.That(quit, Is.Not.Null);
            var status = Object.FindAnyObjectByType<OperationBarStatus>().transform;
            var band = (RectTransform)status.parent;
            var speed = status.Find("Speed Button");
            Assert.That(band.name, Is.EqualTo("Top Band"));
            Assert.That(speed.gameObject.activeInHierarchy, Is.True);
            Assert.That(help.OpenButton.gameObject.activeInHierarchy || quit.HomeButton.gameObject.activeInHierarchy, Is.False);

            battle.SetPaused(true);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(speed.gameObject.activeInHierarchy, Is.False);
            foreach (var button in new[] { help.OpenButton, quit.HomeButton })
            {
                Assert.That(button.gameObject.activeInHierarchy, Is.True);
                Assert.That(button.transform.parent, Is.EqualTo(status));
                var corners = new Vector3[4];
                ((RectTransform)button.transform).GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var point = band.InverseTransformPoint(corner);
                    Assert.That(point.x, Is.InRange(band.rect.xMin - 0.1f, band.rect.xMax + 0.1f));
                    Assert.That(point.y, Is.InRange(band.rect.yMin - 0.1f, band.rect.yMax + 0.1f));
                }
            }

            help.OpenButton.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(quit.HomeButton.gameObject.activeInHierarchy || help.OpenButton.gameObject.activeInHierarchy, Is.False);
            help.Close();
            yield return null;
            yield return null;
            quit.HomeButton.onClick.Invoke();
            Assert.That(quit.IsConfirmOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            // 出したばかりの Graphic は描かれるまで depth が -1 で、GraphicRaycaster の対象にならない。
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var pause = (RectTransform)status.Find("Pause Button");
            var canvas = pause.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, pause.TransformPoint(pause.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            var overlay = quit.ConfirmButton.transform.parent.parent;
            Assert.That(hits[0].gameObject.transform.IsChildOf(overlay), Is.True, "確認中に帯を押せる");
            help.OpenButton.onClick.Invoke();
            yield return Press(Key.Slash, () => true);
            Assert.That(help.IsOpen, Is.False);
            quit.CancelButton.onClick.Invoke();
            Assert.That(quit.IsConfirmOpen, Is.False);
            Assert.That(battle.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);

            quit.HomeButton.onClick.Invoke();
            yield return Press(Key.Escape, () => !quit.IsConfirmOpen);
            Assert.That(quit.IsConfirmOpen, Is.False);
            Assert.That(battle.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);

            quit.HomeButton.onClick.Invoke();
            yield return Press(Key.P, () => !battle.IsPaused);
            Assert.That(quit.IsConfirmOpen || battle.IsPaused, Is.False);
            Assert.That(speed.gameObject.activeInHierarchy, Is.True);
            battle.SetPaused(true);
            yield return null;
            yield return null;
            quit.HomeButton.onClick.Invoke();
            quit.ConfirmButton.onClick.Invoke();
            for (var i = 0; i < 300 && SceneManager.GetActiveScene().name != GameSession.TitleScene; i++)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameSession.TitleScene));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
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
