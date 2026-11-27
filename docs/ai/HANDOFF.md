# Resume state

## 現在の作業

更新: 2026-11-27 Asia/Tokyo / 状態: **spec 015（テストプレイの指摘への対応）を実装し、T1801〜T1806、2回目のテストプレイの T1808・T1809（見せ場の前はスロットも回せない）完了（[`spec.md`](../../specs/015-playtest-fixes/spec.md) 承認済み 2026-11-27・[`plan.md`](../../specs/015-playtest-fixes/plan.md)・[`tasks.md`](../../specs/015-playtest-fixes/tasks.md)）。残りは T1807（ユーザーが Editor で通しで確認）。spec 014 T1707 と spec 013 T1602 はこのテストプレイで済み、指摘は spec 015 に入れた。spec 012（味方ごとの強化）はユーザー指示で保留。**

- spec 015 の中身: 見せ場をステージ1のウェーブ1（T1808 で3レーンに近接8体ずつ・計24体）へ移し、決め打ちは 赤7・青7・黄7、終わると賭け1枚。止めている間は `SlotShowcaseGuide`（Portrait Root の最後の子、暗幕4枚・レバーの上の矢印・文字あり版の案内）。当たり率 0.7。音はスロット系とクリアを `scripts/audio/synth_sounds.py` の合成音に（再生成は `.tools/unity-mcp/Scripts/python.exe -I scripts/audio/synth_sounds.py` の後に Setup `Sound (spec 014)`）、音量の初期値 0.5・BGM 0.5。ステージ4は `elite_a` を分かれ道に作り直し、5は飛行を3体ずつに、6は序盤を時間差に。HP ゲージは頭の上で `HpGauge.shader`（深度を見ない）、重装の盾は横の細い板。
- T1808（2回目のテストプレイ、FR-005〜FR-008）: 見せ場の群れはお金を落とさない（`BattleEnemy.FromShowcase`）、見せ場の1回はコインを使わない（`SlotMachine.NextSpinFree`）、群れが全滅するまで配置不可（`UnitSummoner.Lock` → `SummonResult.Locked`）、全滅で青7・黄7を消す（`SlotEffects.ClearStopAndPoison`）、札をマウスで選ぶとカーソルを中央に出さない（`UnitDeployment.SelectAt`・`HasCursor`）。
- Setup（何度でも可）: `Showcase Guide (spec 015)`・`HP Gauge Material (spec 015)`・`Build Text Mode Layout (T359)`・`Create Text Mode Font (T352)`（ui_texts.csv に字を足したら必要）。
- PlayMode テストは `PlayModeTestSetup`（SetUpFixture）が `BattleController.SkipShowcase = true` にして見せ場を飛ばす。見せ場を見るテスト（`SlotBetShowcaseTests` の Showcase、`ScreenCaptureTests`）だけ false。早送りでステージを終わらせるテストは `StageTestUtil.SkipShowcase` も使える（`Battlefield.DisableShowcase`、配置の禁止も外す）。見せ場を飛ばすと正本の24体がそのまま本拠地に入り、ステージ1の序盤でゲームオーバーになる。演出を見る `SlotCabinetEffectTests` は以前の出だし（中央に8体）の表を差し込む。
- 最終検証（2026-11-27、T1808 後）: EditMode 633/633、PlayMode 成功75/失敗0/スキップ1（Explicit）、ハーネス 8/8。T1808 の見た目（3レーンの群れ・置けない間の札・カーソル）は撮影していない（T1807 でユーザーが確認）。前回の撮影 `Artifacts/Automation/Editor/t359/20261127-115936/`。半透明の暗幕は撮影では薄く写るので濃さは Editor で見る。音はまだ誰も聞いていない。
- ユーザーが前に音量スライダーを動かしていれば PlayerPrefs の値が優先され、初期値 0.5 は効かない（伝え済み）。
- 気づいて未対応: 文字あり版で右上の「一時停止」の文字がボタンの下に少しはみ出す（ユーザーに伝え済み、指示待ち）。そのほかの候補: 001 の M5（T501〜T505）、本番の 3D 台モデルの差し替え（`Cabinet Model`）。
- Setup をかけると Game.unity・Title.unity の差分が大きく見える（行の並べ替え）。`PortraitScreenLayout`（ExecuteAlways）がカメラの表示範囲を書き換えるのは無害。Setup を再実行すると Inspector の調整値は初期値に戻る。
- 素材の方針: AI が取るログイン不要の無料素材だけ（有料は使わない）。Kenney などの元素材は `Artifacts/audio-src/`（git 外）。
- ブランチ: main に直接コミットして origin へ push。main は core.autocrlf で CSV が CRLF になる。
- Unity 接続: 2026-11-27 T1808 の検証後に停止を確認済み（8080 閉、Editor は開いたまま）。ユーザーが Play 中のことがあるので、Unity 操作の前に isPlaying を確認する。Editor が重いのは PC のメモリ不足（16GB）。
### 作業の注意
- テストの手順: refresh → 約25秒 → code execute で `scriptCompilationFailed` と `isCompiling` を確認 → `tests --async` を1回 → 同じ job_id を前景のループで poll（[`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節）。PlayMode 全件は約4分。スクリプトは毎回 `$env:TEMP` などに作り直す。
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
