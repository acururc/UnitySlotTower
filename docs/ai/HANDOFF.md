# Resume state

## 現在の作業

更新: 2026-11-15 Asia/Tokyo / 状態: T352（文字あり版）を完了・コミット。次は T353（全画面の文字チェックとマウスだけの通し）。

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（D-59〜D-75 と【2026-11-14/15 変更/廃止】の項目）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md) 16章（2〜8章の旧記述より優先、書き直しは T354）、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md) の M3U。T340〜T352 の実装の要点は tasks.md の各結果欄にある（必要なタスクの行だけ読む）。
- 承認: 2026-11-15 ユーザーが D-75 を含む spec 全体を明示承認（sha256 fca9a9faaa1d、「Claude 提案」部分を含む）。spec を変えたら再承認。T352 で変えたのは plan 16.9 のフォントの1行（動的→静的）だけで spec は未変更。
- ブランチ: `feature/001-t402-result`（main に直接コミットしない）。直下の未追跡ファイル（`change.txt`・デザイナー制作依頼書 pptx・メモ txt）はユーザーのもの。触らない。
- 次の一手: T353（tasks.md の T353 の行）: `ScreenTextTests`・`InputOnlyRunTests` を新しい流れ（ルートのマス選び → 戦闘 → ショップ）で書き直し、`BattleController.IsCustomizing`（常に false）とテストの `ReelCustomTestUtil` を消す。`ScreenTextTests` は T352 で「`TextModeLabel` の子は対象外、文字なし版で描かれないこと」を足し済み。
- 文字あり版（T352）: 文言は `Data/Tables/ui_texts.csv`、ラベルは `Setup/Add Text Mode Labels (T352)`（開いている Title／Game シーンに作り直す）、文言の字を足したら先に `Setup/Create Text Mode Font (T352)`（`TextMode SDF.asset` は csv の字だけの静的アセット）。ライセンスは [`docs/ASSET_LICENSES.md`](../ASSET_LICENSES.md)。
- 未確認・未対応: plan 16章の仮値（抽選表・リールの並び・両替レート・回数固定 first=6・ドロップ量・バー/コインの刻み・バッファー倍率・スキルの値・報酬・ルートの重みと大きさ・ショップの値段・T347 のマップと出現）と T352 の文言（職名・スキルの説明など）は Claude の仮置きでユーザー未確認。plan 16.10 の「ショップは画面自体の絵（矢印のアニメ）」は未作成。説明の新しい題目（スキル・ルート）の文字なし版の Editor 画像は未撮影。
- 検証（T352 後）: EditMode 539/539、PlayMode 53/53、Console エラー0、Editor 画像 `Artifacts/Automation/Editor/t352-*.png`（両方の版）、ハーネス 7/8（失敗はユーザーの未追跡 `change.txt` の行末空白だけ。触らない）。テスト手順・code execute の制約・撮影の注意・PlayMode テストの書き方は [`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節（PowerShell の変数は大文字小文字を区別しないので `$s` と `$S` を併用しない）。
- Unity 接続: 停止済み（8080 閉を確認、T352 後）。Editor は開いたまま、Game シーンを開いている（未保存の変更なし）。
- 既知の問題: プレイヤービルドで戦闘マスがマゼンタ（シェーダー欠落、未調査）。Shift+数字は SelectUnit と Upgrade の両方に反応しうるので Shift 中は SelectUnit を無視、Esc は配置操作中かで Cancel/Pause を振り分け（plan 3章）。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
