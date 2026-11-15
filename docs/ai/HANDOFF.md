# Resume state

## 現在の作業

更新: 2026-11-15 Asia/Tokyo / 状態: T357・T358 を完了・コミット。T359 は spec の下書き（D-76）まで済み、**ユーザーの再承認待ち**。承認待ちの間（ユーザー離席中）にプレイヤーのマゼンタ不具合を修正・コミットし、続けて T503 の EditMode 部分（`DataDrivenBehaviorTests`、CSV の1セル変更で6種の挙動が変わる）を完了・コミット。T503 の手動確認は残り。さらに plan 16.10 のショップの絵による説明（矢印のアニメ）を追加・コミット。

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（D-59〜D-76）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md)（2〜8章・10章が現行、16章は詳細）、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md) の M3U。各タスクの要点は tasks.md の結果欄（必要な行だけ読む）。
- 承認: 前回 2026-11-15 に spec 全体を明示承認（sha256 fca9a9faaa1d）。その後 D-76（文字あり版の作り直し）を追加し D-74・FR-006・AC-005 を書き換えたので**現在は未承認**（状態・承認行も「承認待ち／未承認」に変更済み、下書き後の sha256 先頭 2b639e0feca9 は状態行の修正前の値なので承認時に取り直す）。「Claude 提案」部分（文字なし版は変えない・数字に名前・アイコンに名前と短い説明・文字の最小サイズは plan で決める）の確認を含めて、ユーザーに D-76・FR-006・AC-005 を見せて承認を得る。承認前に T359 を実装しない。
- ブランチ: `feature/001-t402-result`（main に直接コミットしない）。直下の未追跡ファイル（`change.txt`・デザイナー制作依頼書 pptx・メモ txt）はユーザーのもの。触らない。
- 今回の結果: T357 = 戦闘カメラの俯角 65→55°（`BattleCameraFit.DefaultPitchDegrees`）。比較画像 `Artifacts/Automation/Editor/t357-pitch{65,55,50,45}.png` から、ユーザー離席中に Claude が仮に 55° を選んだ（変えるなら定数とテスト1行）。T358 = `route_rules.csv` の columns 4→7、次の列は斜め2:まっすぐ1、`RouteNode.DisplayOffsetX/Y`（±0.2）で `RouteScreen` のマスをずらす。画像 `t358-route-3/4.png`。
- 次の一手: ① ユーザーに T357 の角度（55° でよいか）と D-76 の承認を確認 → ② 承認後 T359 の plan（文字あり版の画面構成・切り替え方式・文字の最小サイズ）とタスク分け → 実装 → ③ T355（ユーザーの通しプレイ。T503 の手動確認も一緒に）。
- 文字あり版（T352 の現状）: 文言は `Data/Tables/ui_texts.csv`、ラベルは `Setup/Add Text Mode Labels (T352)`、字を足したら先に `Setup/Create Text Mode Font (T352)`（静的 `TextMode SDF.asset`）。ライセンスは [`docs/ASSET_LICENSES.md`](../ASSET_LICENSES.md)。
- 未確認・未対応: plan 16章の仮値と T352 の文言は Claude の仮置きでユーザー未確認（columns 7 も仮値）。ショップの矢印のアニメ（`Ui/NudgeArrow`、押せる札・両替の矢印だけ動く）の幅・速さも仮置きで、ユーザー未確認（画像 `Artifacts/Automation/Editor/shop-arrows-a/b.png`。品の矢印は動きが小さめ）。説明の新しい題目（スキル・ルート）の文字なし版の Editor 画像は未撮影。Game.unity に保存されたカメラは 65° のまま（実行時に Fit が置き直すので影響なし）。
- マゼンタ修正: 原因は実行時の `CreatePrimitive` が URP の既定マテリアル（Editor 専用）に頼っていたこと。3つの戦闘表示に `primitiveMaterial`（`Art/Materials/Greybox.mat`、URP Lit）を足して Game.unity で割り当て、テスト `PrimitiveMaterialTests` を追加。`Test-UnityPlayer.ps1 -Scene Game`（開発ビルド限定の `-automationScene`）で戦闘画面を撮れるようにした。画像 `Artifacts/Automation/Player/20261115-193205-332/player.png` で正しい色を確認。Unity が自動で足した `ProjectSettings/URPProjectSettings.asset` の1行（`m_ProjectSettingFolderPath`）は未コミット。
- 検証（ショップの矢印の後、最新）: EditMode 553/553、PlayMode 54/54（`NodeScreenTests` に矢印の確認を追加）、ハーネス 7/8（`change.txt` だけ）。文字あり版で品の札のラベルと矢印が重ならないかは未確認（T359 で作り直す予定）。検証（マゼンタ修正後）: EditMode 546/546、PlayMode 54/54、プレイヤービルド成功・起動撮影 exit 0、ハーネス 7/8（失敗はユーザーの未追跡 `change.txt` の行末空白だけ）。テスト手順・code execute の制約・撮影の注意は [`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節。code execute は引用符の都合で `-f ファイル` で渡すと確実。
- Unity 接続: 停止済み（8080 閉を確認）。Editor は開いたまま（Game シーン、未変更）。
- 既知の問題: なし（マゼンタは解決）。Shift+数字と Esc の振り分けは plan 3章。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
