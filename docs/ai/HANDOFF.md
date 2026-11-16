# Resume state

## 現在の作業

更新: 2026-11-16 Asia/Tokyo / 状態: **M3V（spec D-77〜D-91、2026-11-16 承認 sha256 `eb605bccac38`）の実装中。T360 完了 `7fc3a92`、T361（1ウェーブ・準備フェーズ廃止・開始の演出・ストックは待ち時間なし）完了（tasks.md に詳細）。** 次は plan 17.11 の順で T361 の次のタスク（T362 で waves.csv の群れを作り直す。T361 では W2 を W1 の最後の 8 秒後に続けただけ）。
- T361 の Claude の仮置き: 演出の残りステップは戦闘に持ち越す、撤退の説明は2つ目の戦闘マスでレバーか次の召喚で終わる、光の粒の色と大きさ。`Battlefield.BeginWave` と ui_texts.csv の `bar_wave`・`bar_start` は未使用のまま残っている（T370・T371 で片付ける）。

- T355 の記録: [`playtest-t355.md`](../../specs/001-slot-tower-defense/playtest-t355.md)。T355 は完了扱い（設定6はユーザーが不要と判断）。文字あり版の確認は T375（文字なし版ではロゴ右下の隅の見えないボタンで切り替え）。
- 承認の範囲: spec 全体（D-01〜D-91、FR-001〜FR-064、AC-001〜AC-051、D-77〜D-91 の Claude 提案を含む）。plan 17章の数値（円は約8倍、bet 2、両替 10、1戦闘12回以上、intro 2秒など）は Claude の仮置きで承認の対象外。T368 で戦闘とスロットの割合を変えるかは画像を見てユーザーに確かめる。
- 次の一手: tasks.md の M3V を T361 から順に（順序は plan 17.11）。Core のタスクは `tdd` スキル、終わるごとに `unity-verify` で検証して tasks.md に結果を書く。
- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)、[`plan.md`](../../specs/001-slot-tower-defense/plan.md)、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md)。
- T357 のカメラ 55° はユーザー確定（D-87 で寄せるのは距離・余白）。
- ブランチ: `feature/001-t402-result`（main に直接コミットしない）。直下の未追跡ファイル（デザイナー制作依頼書 pptx・メモ txt・スクリーンショット png）と `ProjectSettings/URPProjectSettings.asset` の1行（Unity が自動で追加）はコミットしない。
- 文字あり版の仕組み: `Runtime/Ui/TextModeLayout`（部品ごとに両方の版の値）、値は `Editor/Setup/TextModeLayoutSetup`、ラベルは `TextModeSetup`（T352）。読みやすさの判定は `Tests/PlayMode/Flow/TextReadability.cs`（36 以上・1440×1920 内・省略なし・同じ親の中で重ならない）を `TextModeTests.Every_Screen_Shows_Japanese_Text_Large_Inside_And_Apart` が全9画面で使う。文言や配置を変えたらこのテストと撮影で確かめる。
- 撮影: メニュー `Tools/UnitySlotTower/Automation/Capture Screens (T359)` → `Artifacts/Automation/Editor/t359/<日時>/`（16枚）。最新は `t359/20261116-172457/`、文字なし版の基準は `t359/before/`。撮影の前にシーンが dirty でないか確かめる（「Scene(s) Have Been Modified」ダイアログで止まった前例）。title 直後の NullReference で落ちることがある（原因未特定、再実行で成功）。完了待ちでフォルダを数えるときは `before` を除く。
- 検証（T361 時点）: EditMode 561/561、PlayMode 63 件で 62 成功＋撮影1件スキップ（Explicit）、ハーネス 8/8、開始の演出の画像 `Artifacts/Automation/Editor/t361/t361-intro-2.png`。`CoinIncomeTests` は通っているが T362 で群れを変えたら再確認。PlayMode でゲーム開始直後に操作するテストは `RouteTestUtil.WaitForIntro` で演出の終わりを待つ。
- 未確認・未対応: plan 16章の仮値・T352/T359 の文言・columns 7・ショップの矢印は Claude の仮置き。
- Unity 接続: 停止済み（8080 閉を確認）。Editor は開いたまま（Game シーン、保存済み）。Unity 操作用の scratchpad スクリプト（compile.ps1・tests.ps1）は会話ごとに作り直す。
- 手順の注意: テストは scratchpad のスクリプトで refresh→25秒→code execute でコンパイル確認→tests --async→poll（[`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節）。scratchpad は会話ごとに変わるので作り直す。`tests --async` が「tests did not start within timeout」で失敗しても Play が遅れて始まりテストが走っていることがある（2026-11-16）。`isPlaying` が false に戻るのを待ってから再送する（二重実行しない）。`code execute` のコードは位置引数（`--code` はない）。Bash の `python` と heredoc は動かないので、編集スクリプトは Write で scratchpad に置いて `node` で実行する。ツールの作業フォルダが Assets 下に移ることがあるので絶対パスを使う。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
