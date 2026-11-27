# Resume state

## 現在の作業

更新: 2026-11-27 Asia/Tokyo / 状態: **spec 016 早見表の作り直し（[`spec.md`](../../specs/016-help-rework/spec.md)・[`tasks.md`](../../specs/016-help-rework/tasks.md)）は全タスク完了（T1906 はユーザー判断で終了。文字なし版は伝わりにくいとの指摘あり、今は直さない）。進行中の作業なし。spec 012 はユーザー指示で保留。**
- **ユーザーのメモ（2026-11-27）: spec 016 の次に直すのは「キャラの配置キャンセル」。中身はまだ聞いていない（着手時に聞き取る）。**
- spec 016 の中身: 早見表は5ページ（遊び方・回し方・図柄・味方・敵）。各行に動く図（`Runtime/Flow/HelpDemo.cs`、実時間で動くキー補間、舞台は RectMask2D で切り抜く）。数値は 青7・黄7・バーの秒数と味方のブロック・コスト・待ち秒数だけ。文字ありは右の列を上に縮めて下に文（6行のページは1行24字まで、ほかは2行）。作るのは Setup `Build Help Pages (spec 016)`（`Editor/Setup/HelpRebuildSetup.cs`、何度でも可、外枠は残す）。文言は ui_texts.csv の `help2_*`、変えたら `Create Text Mode Font (T352)` も。動く図の絵は仮（盤面の見た目に合わせた図形、味方は青・敵は赤の台）。
- 検証（2026-11-27）: EditMode 633/633、ハーネス 8/8、PlayMode 78件で失敗1・スキップ1（Explicit）。失敗は `TextModeTests.Every_Screen_Shows_Japanese_Text_Large_Inside_And_Apart` で、帯のボタン（一時停止・早見表・タイトル）の字 30 が下限 36 未満。前のコミット 9809404（36→30）から出ていた既存の失敗で、早見表とは無関係（ユーザーに報告済み、直し方は未回答: 帯のボタンだけ下限を30にするか、36に戻して幅で収めるか）。撮影 `Artifacts/Automation/Editor/t359/20261127-190313/*-help-*.png`。
- Setup（何度でも可）: `Build Help Pages (spec 016)`・`Showcase Guide (spec 015)`・`HP Gauge Material (spec 015)`・`Build Text Mode Layout (T359)`（早見表には触れなくなった）・`Create Text Mode Font (T352)`（ui_texts.csv に字を足したら必要）。
- PlayMode テストは `PlayModeTestSetup`（SetUpFixture）が `BattleController.SkipShowcase = true` にして見せ場を飛ばす。見せ場を見るテスト（`SlotBetShowcaseTests` の Showcase、`ScreenCaptureTests`）だけ false。早送りでステージを終わらせるテストは `StageTestUtil.SkipShowcase` も使える。
- ユーザーが前に音量スライダーを動かしていれば PlayerPrefs の値が優先され、初期値 0.5 は効かない（伝え済み）。音はまだ誰も聞いていない。
- 次の候補（spec 016 の後）: 配置キャンセル（上のメモ）、001 の M5（T501〜T505）、本番の 3D 台モデルの差し替え（`Cabinet Model`）。
- 撮影メニュー（Capture Screens）は PlayMode テストとして走り、シーンが dirty だと保存ダイアログで止まる（「Don't Save」で続行）。撮影の前に保存済みシーンを開き直す。
- Setup をかけると Game.unity・Title.unity の差分が大きく見える（行の並べ替え）。`PortraitScreenLayout`（ExecuteAlways）がカメラの表示範囲を書き換えるのは無害。Setup を再実行すると Inspector の調整値は初期値に戻る。
- 素材の方針: AI が取るログイン不要の無料素材だけ（有料は使わない）。Kenney などの元素材は `Artifacts/audio-src/`（git 外）。
- ブランチ: main に直接コミットして origin へ push。main は core.autocrlf で CSV が CRLF になる。
- Unity 接続: 2026-11-27 spec 016 の検証後に停止を確認済み（8080 閉、Editor は開いたまま）。ユーザーが Play 中のことがあるので、Unity 操作の前に isPlaying を確認する。Editor が重いのは PC のメモリ不足（16GB）。
### 作業の注意
- テストの手順: refresh → 約25秒 → code execute で `scriptCompilationFailed` と `isCompiling` を確認 → `tests --async` を1回 → 同じ job_id を前景のループで poll（[`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節）。PlayMode 全件は約5分。`poll-test --wait` はすぐ返ることがあるので、ループに Start-Sleep を入れる。スクリプトは毎回 `$env:TEMP` などに作り直す。
- 入力のテストは SetUp で実機のマウス・キーボードを止める。SetUp が失敗すると TearDown が走らず Editor の Game ビューを触れなくなる（2026-11-27 に発生）。`PlayModeTestSetup` の OneTimeTearDown で戻すようにした。それでも触れなければ code execute で `InputSystem.devices` を見て `EnableDevice`・`Test Keyboard*` を `RemoveDevice`。
- PlayMode の実行中に Play を止めて打ち切ると、ジョブが `tests_running` のまま残り、Editor の再起動まで新しいテストを流せない。止めずに最後まで待つ。
- `editor refresh` はラッパーが「invalid JSON」で失敗扱いにするが、refresh 自体は効いている（CLI が JSON の前に文字を出すため）。ラッパーが失敗すると出力が捨てられる。原因を見るときは `.tools/unity-mcp/Scripts/python.exe -B scripts/unity/unity_mcp_runner.py ...` を直接呼ぶ。
- 実データを読むテスト（`TablePaths`）は数値の調整で落ちる。CSV を変えたら `Get("melee")` などの期待値を grep で探して一緒に直す。
- Bash で C# の `\n` を含む置換をするとバックスラッシュが崩れる。そういう置換は Edit ツールで行う。`node -e "..."` の中のバッククォートはシェルに展開されるので、node のコードは quoted heredoc でファイルに書いてから実行する。Git Bash の `sed -i` は CRLF を LF にするので `sed -i -b` を使う。
- Unity 操作中に PC が落ちた前例がある。操作は1回の呼び出しに詰め込まず分ける。
- Codex に任せるのは小さく閉じた作業だけ（T604・T605 の委任は失敗）。HANDOFF・tasks を触らせず、テストは数と中身を確かめる。
- シーンは Editor の Setup メニューで作り直す。Play を止めるとシーンが dirty になるので、Setup の前に保存済みシーンを開き直す。
- 撮影: `Tools/UnitySlotTower/Automation/Capture Screens (T359)` → `Artifacts/Automation/Editor/t359/<日時>/`。Game ビューは 1440×1920。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
