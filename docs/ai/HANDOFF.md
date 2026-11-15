# Resume state

## 現在の作業

更新: 2026-11-15 Asia/Tokyo / 状態: D-76 をユーザーが承認（spec 承認済み）。plan 16.13 を書き、T359 を T359a〜T359g に分けた。**T359a（土台）を完了・コミット**（`520341c`）。次は T359b（戦闘の HUD）。

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（D-76・FR-006・AC-005）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md) 16.13（方式・文字の最小 36・画面ごとの方針）、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md) の T359a〜T359g（T359a の結果欄に作った物の詳細）。
- 承認: 2026-11-15 ユーザーが spec 全体（D-01〜D-76、D-76 の「Claude 提案」を含む）を明示承認（sha256 2b639e0feca9）。plan 16.13 の画面ごとの方針は Claude の仮置き（T355 でユーザーに見てもらう）。
- T357: 戦闘カメラの角度 55° を 2026-11-15 ユーザーが確定（「角度いいよ」）。
- ブランチ: `feature/001-t402-result`（main に直接コミットしない）。直下の未追跡ファイル（`change.txt`・デザイナー制作依頼書 pptx・メモ txt）と `ProjectSettings/URPProjectSettings.asset` の1行（Unity が自動で追加）はコミットしない。
- T359a の要点: `Runtime/Ui/TextModeLayout`（部品に付け、文字なし版と文字あり版の値を両方シリアライズ、Rect・Font・Color・Visibility の旗。隠すのは CanvasGroup。実行中にコードが位置や色を変える部品には付けない）。値は `Editor/Setup/TextModeLayoutSetup`（メニュー `Setup/Build Text Mode Layout (T359)`、`Set(target, parts, edit)` は再実行で同じ結果。`BuildGame`/`BuildTitle` はまだ空）。撮影はメニュー `Tools/UnitySlotTower/Automation/Capture Screens (T359)`（Explicit の `ScreenCaptureTests` を実行し、`Artifacts/Automation/Editor/t359/<日時>/{notext|text}-<画面>.png` を16枚。待つ目安は約1分）。作り直し前は `t359/before/`。
- 次の一手（T359b）: Game シーンの操作バー・HUD の部品名を確かめ（`Editor/Setup/OperationBarSetup.cs`・`TextModeSetup.cs` の AddGameLabels）、`BuildGame` に文字あり版の配置を書く → Game シーンを開いてメニューを実行 → 撮影 → `TextModeTests.Game_Bar_Cabinet_Guide_And_Help_...` の「切り替えで部品が動かない」確認を新しい方式に直す（文字なし版を変えないことは `Switching_Back_...` と EditMode の `Layouts_In_Scenes_...` が守る）。T359b〜f では文言を足したら先に `Setup/Create Text Mode Font (T352)`。
- 検証（T359a 後）: EditMode 554/554、PlayMode 55 件成功＋撮影の1件スキップ（Explicit）、ハーネス 7/8（`change.txt` の行末空白だけ）。撮影の手順は動作確認済み（16枚）。
- 未確認・未対応（前から）: plan 16章の仮値・T352 の文言・columns 7・ショップの矢印の幅と速さは Claude の仮置きでユーザー未確認。T503 の手動確認と T355（ユーザーの通しプレイ）は T359 の後。
- Unity 接続: 停止済み（8080 閉を確認）。Editor は開いたまま（Game シーン、未変更）。
- 手順の注意: テストは scratchpad のスクリプトで refresh→25秒→`code execute -f` でコンパイル確認→tests --async→poll（[`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節）。Bash の `python` は動かない。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
