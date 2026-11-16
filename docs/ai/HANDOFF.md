# Resume state

## 現在の作業

更新: 2026-11-16 Asia/Tokyo / 状態: **T359（文字あり版の作り直し、T359a〜T359g）を完了・コミット**。残りは T355（ユーザーの通しプレイ）と T5xx。

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（D-76・FR-006・AC-005）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md) 16.13、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md) の T359a〜T359g（各結果欄に作った物の詳細）。
- 承認: 2026-11-15 ユーザーが spec 全体を明示承認（sha256 2b639e0feca9）。plan 16.13 の画面ごとの方針と T359b〜T359f の配置・文言（設定の名前を含む）は Claude の仮置き（T355 でユーザーに見てもらう）。T357 のカメラ 55° はユーザー確定。
- ブランチ: `feature/001-t402-result`（main に直接コミットしない）。直下の未追跡ファイル（`change.txt`・デザイナー制作依頼書 pptx・メモ txt・スクリーンショット png）と `ProjectSettings/URPProjectSettings.asset` の1行（Unity が自動で追加）はコミットしない。
- 文字あり版の仕組み: `Runtime/Ui/TextModeLayout`（部品ごとに両方の版の値）、値は `Editor/Setup/TextModeLayoutSetup`、ラベルは `TextModeSetup`（T352）。読みやすさの判定は `Tests/PlayMode/Flow/TextReadability.cs`（36 以上・1440×1920 内・省略なし・同じ親の中で重ならない）を `TextModeTests.Every_Screen_Shows_Japanese_Text_Large_Inside_And_Apart` が全9画面で使う。文言や配置を変えたらこのテストと撮影で確かめる。
- 撮影: メニュー `Tools/UnitySlotTower/Automation/Capture Screens (T359)` → `Artifacts/Automation/Editor/t359/<日時>/`（16枚）。最新は `t359/20261115-235209/`、文字なし版の基準は `t359/before/`。撮影の前にシーンが dirty でないか確かめる（「Scene(s) Have Been Modified」ダイアログで止まった前例）。title 直後の NullReference で落ちることがある（原因未特定、再実行で成功）。完了待ちでフォルダを数えるときは `before` を除く。
- 検証（T359g 後）: EditMode 554/554、PlayMode 61 件で 60 成功＋撮影1件スキップ（Explicit）、ハーネス 7/8（`change.txt` の行末空白だけ）。文字なし版は `before/` と比べて6画面差 0、game・route はアニメの範囲だけ。
- 未確認・未対応: plan 16章の仮値・T352/T359 の文言・columns 7・ショップの矢印は Claude の仮置き。
- 次の一手: T355（ユーザーが通しで手動プレイし、仮置きの値・文言・配置を見る）を依頼するか、ユーザーの指示で T503（CSV だけで挙動が変わる確認）などに進む。どれに進むかはユーザーに確かめる。
- Unity 接続: 停止済み（8080 閉を確認）。Editor は開いたまま（Game シーン、保存済み）。
- 手順の注意: テストは scratchpad のスクリプトで refresh→25秒→code execute でコンパイル確認→tests --async→poll（[`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節）。scratchpad は会話ごとに変わるので作り直す。Bash の `python` は動かない。ツールの作業フォルダが Assets 下に移ることがあるので絶対パスを使う。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
