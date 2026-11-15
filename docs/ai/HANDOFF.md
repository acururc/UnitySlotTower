# Resume state

## 現在の作業

更新: 2026-11-15 Asia/Tokyo / 状態: T351（説明と早見表）を完了・コミット。次は T352（文字あり版）。

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（D-59〜D-75 と【2026-11-14/15 変更/廃止】の項目）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md) 16章（2〜8章の旧記述より優先、書き直しは T354）、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md) の M3U。T340〜T351 の実装の要点は tasks.md の各結果欄にある（必要なタスクの行だけ読む）。
- 承認: 2026-11-15 ユーザーが D-75 を含む spec 全体を明示承認（sha256 fca9a9faaa1d、「Claude 提案」部分を含む）。spec を変えたら再承認。
- ブランチ: `feature/001-t402-result`（main に直接コミットしない）。直下の未追跡ファイル（`change.txt`・デザイナー制作依頼書 pptx・メモ txt）はユーザーのもの。触らない。
- 次の一手: T352（tasks.md の T352 の行、plan 16.9）: 日本語フォント（候補 Noto Sans JP、ライセンス確認と T501 の一覧への記録）、`GameSession.TextMode`、タイトルの隠しボタンと「あ」の印、`TextModeLabel` と `ui_texts.csv`、主な画面へのラベル、`TextAudit` の対象外の決まり。既存の `TextAudit`・`ScreenTextTests`・`NumericText`・タイトル画面のセットアップを先に読む。
- 説明（T351）: `GuideSequence` の題目は Deploy/Slot/Skill/Upgrade/Retreat/Route。ショップ・回復の画面中は出さない。Game シーンはメニュー `Setup/Update Guide And Help (T351)` で更新済み。
- 未確認・未対応: plan 16章の仮値（抽選表・リールの並び・両替レート・回数固定 first=6・ドロップ量・バー/コインの刻み・バッファー倍率・スキルの値・報酬・ルートの重みと大きさ・ショップの値段・T347 のマップと出現）は Claude の仮置きでユーザー未確認。plan 16.10 の「ショップは画面自体の絵（矢印のアニメ）」は未作成。説明の新しい題目（スキル・ルート）の Editor 画像は未撮影（PlayMode で動作確認）。
- 検証（T351 後）: EditMode 528/528、PlayMode 50/50、Editor 画像 `Artifacts/Automation/Editor/t351-help.png`、ハーネス 7/8（失敗はユーザーの未追跡 `change.txt` の行末空白だけ。触らない）。テスト手順・code execute の制約・撮影の注意・PlayMode テストの書き方は [`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節。
- Unity 接続: 停止済み（8080 閉を確認、T351 後）。Editor は開いたまま、Game シーンを開いている。
- 既知の問題: プレイヤービルドで戦闘マスがマゼンタ（シェーダー欠落、未調査）。Shift+数字は SelectUnit と Upgrade の両方に反応しうるので Shift 中は SelectUnit を無視、Esc は配置操作中かで Cancel/Pause を振り分け（plan 3章）。`BattleController.IsCustomizing`（常に false）とテストの `ReelCustomTestUtil` は T353 で消す。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
