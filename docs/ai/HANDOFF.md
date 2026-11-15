# Resume state

## 現在の作業

更新: 2026-11-15 Asia/Tokyo / 状態: D-76 承認済み。plan 16.13 の T359a〜T359g のうち **T359a〜T359d を完了・コミット**。次は T359e（ショップ・回復・リザルト・難易度選択）。

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（D-76・FR-006・AC-005）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md) 16.13、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md) の T359a〜T359g（各結果欄に作った物の詳細）。
- 承認: 2026-11-15 ユーザーが spec 全体を明示承認（sha256 2b639e0feca9）。plan 16.13 の画面ごとの方針と T359b〜T359d の配置・文言は Claude の仮置き（T355 でユーザーに見てもらう）。T357 のカメラ 55° はユーザー確定。
- ブランチ: `feature/001-t402-result`（main に直接コミットしない）。直下の未追跡ファイル（`change.txt`・デザイナー制作依頼書 pptx・メモ txt）と `ProjectSettings/URPProjectSettings.asset` の1行（Unity が自動で追加）はコミットしない。
- 仕組み: `Runtime/Ui/TextModeLayout`（部品ごとに文字なし版・文字あり版の値）。値は `Editor/Setup/TextModeLayoutSetup`（`BuildGame` → `BuildBattleHud`・`BuildCabinetLabels`・`BuildRoute` 済み、`BuildTitle` は空）。新しいラベル・凡例は `TextModeSetup`（T352）で作り、文字あり版の位置で置く。手順: 文言を足したら `CreateFont` → `AddLabels` → `TextModeLayoutSetup.Build` を code execute の reflection で順に呼ぶ（scratchpad の `build.cs` の形。Game シーンを開いた状態）。実行中に文言を入れるラベル（id 空）は `TextModeAssetTests.DynamicLabels` に足す。
- 撮影: メニュー `Tools/UnitySlotTower/Automation/Capture Screens (T359)` → `Artifacts/Automation/Editor/t359/<日時>/`（16枚）。Editor が非フォーカスだと数分かかる（終わるまで MCP が scene access 待ちで失敗する）。title の直後に NullReference で落ちることがある（3回中2回、原因未特定。メニューは失敗時スタックを出すようにした。再実行で成功）。`<Raw Input>` の未処理ログで落ちることもある。T359d 後は `t359/20261115-220330/`。画像の比較は scratchpad の `cmp.ps1`（System.Drawing で差の範囲を出す）。
- 文言: `ui_texts.csv` は引用符付きの複数行セル可。Noto Sans JP は行が高い（36 で約 52 px）ので狭い所は1行に収まる文言にする（T359d の `node_*_desc` は7字以内）。
- 次の一手（T359e）: ショップ（`ShopScreen`、品の札は `offerLabelTemplate` の複製）・回復・リザルト（`TextModeSetup.AddNodeLabels`・`AddResultLabels`）と難易度選択（Title シーン、`BuildTitle`）。品の名前と説明、両替の文「所持金 6 → コイン 3」、回復量、リザルトの名前、設定の名前。両替や回復量の文は数字が変わるので実行時に組み立てが要るか先に確かめる。
- 検証（T359d 後）: EditMode 554/554、PlayMode 57 成功＋撮影1件スキップ（Explicit）、ハーネス 7/8（`change.txt` の行末空白だけ）。文字なし版の画像は T359c 後と比べ、違いはアニメの範囲だけ。
- 未確認・未対応: plan 16章の仮値・T352/T359 の文言・columns 7・ショップの矢印は Claude の仮置き。早見表の「?」のラベル（22）が札4の職名に少し掛かる → T359f。T503 の手動確認と T355（ユーザーの通しプレイ）は T359 の後。
- Unity 接続: 停止済み（8080 閉を確認）。Editor は開いたまま（Game シーン、保存済み）。
- 手順の注意: テストは scratchpad のスクリプトで refresh→25秒→`code execute -f` でコンパイル確認→tests --async→poll（[`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節）。scratchpad は会話ごとに変わるので作り直す。Bash の `python` は動かない。PowerShell ツールの作業フォルダが Assets 下に移ることがあるので絶対パスを使う。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
