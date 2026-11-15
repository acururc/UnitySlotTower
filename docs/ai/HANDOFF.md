# Resume state

## 現在の作業

更新: 2026-11-15 Asia/Tokyo / 状態: D-76 承認済み。plan 16.13 の T359a〜T359g のうち **T359a〜T359e を完了・コミット**。次は T359f（早見表）。

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（D-76・FR-006・AC-005）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md) 16.13、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md) の T359a〜T359g（各結果欄に作った物の詳細）。
- 承認: 2026-11-15 ユーザーが spec 全体を明示承認（sha256 2b639e0feca9）。plan 16.13 の画面ごとの方針と T359b〜T359e の配置・文言（設定の名前を含む）は Claude の仮置き（T355 でユーザーに見てもらう）。T357 のカメラ 55° はユーザー確定。
- ブランチ: `feature/001-t402-result`（main に直接コミットしない）。直下の未追跡ファイル（`change.txt`・デザイナー制作依頼書 pptx・メモ txt）と `ProjectSettings/URPProjectSettings.asset` の1行（Unity が自動で追加）はコミットしない。
- 仕組み: `Runtime/Ui/TextModeLayout`（部品ごとに文字なし版・文字あり版の値）。値は `Editor/Setup/TextModeLayoutSetup`（`BuildGame` → `BuildBattleHud`・`BuildCabinetLabels`・`BuildRoute`・`BuildShop`・`BuildRest`・`BuildResult` 済み、`BuildTitle` 済み）。実行中に作る部品は `ShopScreen.TextModeRect` の形でコードから付ける。数字入りの文は `TextModeLabel.SetFormat`。新しいラベル・凡例は `TextModeSetup`（T352）で作り、文字あり版の位置で置く。手順: 文言を足したら `CreateFont` → 各シーン（Title・Game）を開いて `AddLabels` → `TextModeLayoutSetup.Build` を code execute の reflection で順に呼ぶ（scratchpad の `build.cs` の形）。実行中に文言を入れるラベル（id 空）は `TextModeAssetTests.DynamicLabels` に足す。
- 撮影: メニュー `Tools/UnitySlotTower/Automation/Capture Screens (T359)` → `Artifacts/Automation/Editor/t359/<日時>/`（16枚）。Editor が非フォーカスだと数分かかる（終わるまで MCP が scene access 待ちで失敗する）。title の直後に NullReference で落ちることがある（3回中2回、原因未特定。メニューは失敗時スタックを出すようにした。再実行で成功）。`<Raw Input>` の未処理ログで落ちることもある。T359e 後は `t359/20261115-222349/`。Setup の直後に撮影したとき「Scene(s) Have Been Modified (Game)」のダイアログが出て作業が止まった（過去にも数回。ユーザーが Save を押した。保存後の EditMode のシーン値テストは成功、増えた物は想定のラベルだけ。原因未特定。その後の PlayMode 実行では出ず）。出たらユーザーに押してもらうしかないので、撮影の前にシーンが dirty でないか確かめる。画像の比較は scratchpad の `cmp.ps1`（System.Drawing で差の範囲を出す）。
- 文言: `ui_texts.csv` は引用符付きの複数行セル可。Noto Sans JP は行が高い（36 で約 52 px）ので狭い所は1行に収まる文言にする（T359d の `node_*_desc` は7字以内）。
- 次の一手（T359f）: 早見表（`HelpScreen`、`TextModeSetup.AddHelpLabels`）の見出し・列の名前・スキルと職業の説明を 36 以上に。早見表の「?」のラベル（22）が札4の職名に少し掛かる件もここで直す。その後 T359g（字 36 以上・はみ出し・重なりの PlayMode テスト、全画面の画像比較）。
- 検証（T359e 後）: EditMode 554/554、PlayMode 59 成功＋撮影1件スキップ（Explicit）、ハーネス 7/8（`change.txt` の行末空白だけ）。文字なし版の画像は T359d 後と比べ、違いはアニメの範囲だけ。
- 未確認・未対応: plan 16章の仮値・T352/T359 の文言・columns 7・ショップの矢印は Claude の仮置き。T503 の手動確認と T355（ユーザーの通しプレイ）は T359 の後。
- Unity 接続: 停止済み（8080 閉を確認）。Editor は開いたまま（Game シーン、保存済み）。
- 手順の注意: テストは scratchpad のスクリプトで refresh→25秒→`code execute -f` でコンパイル確認→tests --async→poll（[`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節）。scratchpad は会話ごとに変わるので作り直す。Bash の `python` は動かない。PowerShell ツールの作業フォルダが Assets 下に移ることがあるので絶対パスを使う。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
