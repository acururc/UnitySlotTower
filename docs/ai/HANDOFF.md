# Resume state

## 現在の作業

更新: 2026-11-15 Asia/Tokyo / 状態: T350d（リザルト・リトライ・AC-004 の通し）を完了し T350 全体が完了・コミット。次は T351。

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（D-59〜D-75 と【2026-11-14/15 変更/廃止】の項目）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md) 16章（2〜8章の旧記述より優先、書き直しは T354）、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md) の M3U。T340〜T350d の実装の要点は tasks.md の各結果欄にある（必要なタスクの行だけ読む）。
- 承認: 2026-11-15 ユーザーが D-75 を含む spec 全体を明示承認（sha256 fca9a9faaa1d、「Claude 提案」部分を含む）。T350 の4分割（a〜d）も了承済み。spec を変えたら再承認。
- ブランチ: `feature/001-t402-result`（main に直接コミットしない）。直下の未追跡ファイル（`change.txt`・デザイナー制作依頼書 pptx・メモ txt）はユーザーのもの。触らない。
- 次の一手: T351（tasks.md の T351 の行）: 説明（`GuideSequence` の題目: スロット＝レバーだけ・スキル・ルートを追加、カスタム・ボーナスを削除）と早見表（`HelpScreen` の上半分を20パターンの内訳と新しい効果に、味方にバッファーとスキル。plan 16.10）。既存の `GuideSequence`・`GuideOverlay`・`HelpScreen` と Guide/Help のテストを先に読む。
- 流れの全体（T350 で完成）: `Runtime/Flow/RunController`（`State`・`Rules`・`Visited`・`SeedOverride`・`OpenScreen`）。戦闘 → `BattleController.NodeCompleted` → `RouteScreen`（リールで `NextOnReel`）→ 非戦闘マスは `NodeScreen`（`ShopScreen`・`RestScreen`）→ ◀ → ルート。ボス撃破かライフ0で `ResultScreen`（到達階・所持金・コイン）、リトライは Game の再読み込みでルートも新しいシード。画面中は `SlotCabinet.HeldByScreen`。
- 未確認: plan 16章の仮値（抽選表・リールの並び・両替レート・回数固定 first=6・ドロップ量・バー/コインの刻み・バッファー倍率・スキルの値・報酬・ルートの重みと大きさ・ショップの値段・T347 のマップと出現）は Claude の仮置きでユーザー未確認。説明（`Guide`）の途中でショップに入ると画面に重なる（T351 で確認）。
- 検証（T350d 後）: EditMode 525/525、PlayMode 48/48、Editor 画像 `Artifacts/Automation/Editor/t350d-result-play-2.png`、ハーネス 7/8（失敗はユーザーの未追跡 `change.txt` の行末空白だけ。触らない）。テスト手順・code execute の制約・撮影の注意・PlayMode テストの書き方は [`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節。
- Unity 接続: 停止済み（8080 閉を確認、T350d 後）。Editor は開いたまま、Game シーンを開いている。
- 既知の問題: プレイヤービルドで戦闘マスがマゼンタ（シェーダー欠落、未調査）。Shift+数字は SelectUnit と Upgrade の両方に反応しうるので Shift 中は SelectUnit を無視、Esc は配置操作中かで Cancel/Pause を振り分け（plan 3章）。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
