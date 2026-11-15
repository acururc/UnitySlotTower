# Resume state

## 現在の作業

更新: 2026-11-15 Asia/Tokyo / 状態: D-76 承認済み。plan 16.13 の T359a〜T359g のうち **T359a〜T359c を完了・コミット**。次は T359d（ルート）。

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（D-76・FR-006・AC-005）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md) 16.13、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md) の T359a〜T359g（各結果欄に作った物の詳細）。
- 承認: 2026-11-15 ユーザーが spec 全体を明示承認（sha256 2b639e0feca9）。plan 16.13 の画面ごとの方針と T359b・T359c の配置は Claude の仮置き（T355 でユーザーに見てもらう）。T357 のカメラ 55° はユーザー確定。
- ブランチ: `feature/001-t402-result`（main に直接コミットしない）。直下の未追跡ファイル（`change.txt`・デザイナー制作依頼書 pptx・メモ txt）と `ProjectSettings/URPProjectSettings.asset` の1行（Unity が自動で追加）はコミットしない。
- 仕組み: `Runtime/Ui/TextModeLayout`（部品ごとに文字なし版・文字あり版の値。旗 Rect・Font・Color・Visibility）。値は `Editor/Setup/TextModeLayoutSetup`（メニュー `Setup/Build Text Mode Layout (T359)`、再実行で同じ結果。`BuildGame` → `BuildBattleHud`・`BuildCabinetLabels` 済み、`BuildTitle` は空）。ラベルを `Add Text Mode Labels (T352)` で作り直したら、その後に Build Text Mode Layout も実行する。撮影はメニュー `Tools/UnitySlotTower/Automation/Capture Screens (T359)` → `Artifacts/Automation/Editor/t359/<日時>/`（約1分、16枚）。作り直し前は `t359/before/`、T359c 後は `t359/20261115-211747/`。
- 文言の改行: `ui_texts.csv` は引用符付きの複数行セルが使える（`Characters()` は改行を数えない）。TMP の折り返しが不自然なら改行を入れる（T359c で `cabinet_credit`）。
- 次の一手（T359d）: ルート画面（`Runtime/Flow/RouteScreen.cs`、T352 のラベルは `TextModeSetup.AddRouteLabels`）に、上の帯の名前付きの数字・マスの種類の凡例・行き先の札（方向・名前・短い説明）。`BuildGame` に `BuildRoute` を足す → refresh → コンパイル確認 → `TextModeLayoutSetup.Build` を code execute で reflection 呼び出し（メニュー実行は終わる前に状態を読むことがある）→ 撮影 → テスト。文言を足したら先に `Create Text Mode Font (T352)`。
- 検証（T359c 後）: EditMode 554/554、PlayMode 56 成功＋撮影1件スキップ（Explicit）、ハーネス 7/8（`change.txt` の行末空白だけ）。文字なし版の画像は T359b 後と比べ、違いはアニメの範囲だけ。
- 未確認・未対応: plan 16章の仮値・T352 の文言・columns 7・ショップの矢印は Claude の仮置き。早見表の「?」のラベル（22）が札4の職名に少し掛かる → T359f で直す。T503 の手動確認と T355（ユーザーの通しプレイ）は T359 の後。撮影で `<Raw Input> Failed to get raw input data` が SetUp の未処理ログになり1回失敗した（再実行で成功、環境由来）。
- Unity 接続: 停止済み（8080 閉を確認）。Editor は開いたまま（Game シーン、保存済み）。
- 手順の注意: テストは scratchpad のスクリプトで refresh→25秒→`code execute -f` でコンパイル確認→tests --async→poll（[`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節）。Bash の `python` は動かない。PowerShell ツールの作業フォルダが Assets 下に移ることがあるので絶対パスを使う。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
