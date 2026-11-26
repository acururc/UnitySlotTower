# 010 ステージのやり直し — 計画

spec: [`spec.md`](spec.md)（FR-201〜FR-204）

## 方針
ステージを進めるときの処理（`RunController.BeginStage` → `BattleController.BeginStage`）は、新しい `BattleNodeRun` を作り、盤面の表示（`BattleActorsView` は Field の差し替えを見る）と効果の演出（各 Effect が `NodeBegan` で片付ける）、縁の光・倍速・ヒットストップを初期化する。やり直しはこの道をそのまま使い、その前に `RunState` をステージ開始時の値へ戻すだけにする（シーンは読み込み直さない: G-03）。

## Core（FR-201）
- `CoinWallet.Reset(int coins)`: 残高を指定値にする（負なら例外）。
- `UnitUpgrades`: `Snapshot()` が職ごとのレベルの写しを返し、`Restore(snapshot)` がそれに戻す（写しにない職はレベル1）。
- `RunState.RecordStageStart()`: ライフ・所持金・コイン・強化レベルを記録する。`RunState.RestoreStageStart()`: 記録した値に戻す（記録がなければ例外）。`StageIndex` は変えない。記録は何度戻しても同じ値（写しを共有しない）。
- 記録するのは `RunController.BeginStage` の中、`battle.BeginStage` の前（ゲーム開始時のステージ1も含む）。

## Runtime（FR-201〜FR-204）
- `RunController.RetryStage()`: `State.RestoreStageStart()` → `BeginStage()`。`StageBegan` も普段どおり出る。
- `ResultScreen`:
  - 新しいボタン `stageRetryButton`（シリアライズ。旧シーンでは null を許し、そのときは今の2ボタンのまま動く）。`RunController` への参照 `run` を足す。
  - ゲームオーバーで開くと、ボタンは [このステージから・最初から・タイトルへ] の3つ、初期選択は「このステージから」。クリアでは「このステージから」を隠し、[最初から・タイトルへ] の2つ（初期選択は最初から）。キーボードの左右は見えているボタンの中で動く。`selectionFrames` は stageRetry・retry・title の順の3つ。
  - 3つのときと2つのときでボタンの横位置を変える（開くときにアンカーを入れ替える）。
  - 「このステージから」: スロットが回転中（`SlotCabinet.IsSpinning`）なら受け付けない（止まったあとに揃った効果が新しいステージに入らないようにする）。受け付けたらパネルを閉じて `run.RetryStage()`。
  - 既存の `Retry()`（最初から）と `BackToTitle()` は変えない（FR-204）。`Selected` の番号は見えているボタンの並びでの位置。
- アイコン（文字なし UI）: このステージから＝緑の地に ▶ と旗（到達した階と同じ旗）、最初から＝今の ▶ のボタンに小さな「1」、タイトルへ＝今の ◀。文字あり版のラベルは `ui_texts.csv` に `result_stage_retry`（「このステージから」）を足し、`result_retry` を「最初から」に変える。

## Editor
- `ResultScreenSetup` に `Update Result Screen (spec 010)` を足す: 「Stage Retry Button」（▶＋旗、下線）を作り、最初から のボタンに「1」を足し、`stageRetryButton`・`run`・`selectionFrames`（3つ）を結ぶ。既に `stageRetryButton` があれば何もしない。文字あり版のラベルと位置（`TextModeSetup` / `TextModeLayoutSetup` の Result 部分）にも新しいボタンを足す。シーンは Editor のメニューで更新し、差分は ResultScreen 周りだけであることを確かめる。

## テスト
- EditMode `RunStateTests`: 記録 → ライフ・所持金・コイン・強化を変える → 戻すと開始時の値、2回戻しても同じ、記録前に戻すと例外。`CoinWallet.Reset`・`UnitUpgrades.Snapshot/Restore`。
- PlayMode `ResultFlowTests`:
  - 既存の「ゲームオーバー → Enter でステージ1から」を、Enter（初期選択）＝このステージから（ステージ1、同じシーン・同じ RunState）に直し、「最初から」のボタンでシーンが読み込み直されステージ1に戻る確認を分ける。
  - ステージ2でゲームオーバーにして「このステージから」→ ステージ2、盤上の味方・敵0、残りの敵の数がステージの総数、所持金・コイン・ライフ・強化がステージ2開始時の値、コインの倍率が初期値（AC-202）。
  - クリアのリザルトは2つのボタンのまま（既存の通しテストで確かめる）。

## 影響するファイル
`Core/Economy/CoinWallet.cs`、`Core/Battle/UnitUpgrades.cs`、`Core/Run/RunState.cs`、`Runtime/Flow/RunController.cs`、`Runtime/Flow/ResultScreen.cs`、`Editor/Setup/ResultScreenSetup.cs`、`Editor/Setup/TextModeSetup.cs`・`TextModeLayoutSetup.cs`（Result 部分）、`Data/Tables/ui_texts.csv`、`Scenes/Game.unity`（Editor のメニューで更新）、テスト2ファイル。

## リスク
- ゲームオーバーの瞬間に飛んでいる弾・演出が新しいステージに残る → 各 Effect の `NodeBegan` と `BattleActorsView` の Field 差し替えで片付く想定。PlayMode とユーザーのプレイで確かめる。
- 表示の遅れ（`OperationBarStatus.MoneyHeldBack`・`SlotCabinet.CreditHeldBack`）が残ると所持金の表示がずれる → `CoinFountainEffect` が `NodeBegan` で Clear するので戻る想定。テストで表示の値も見る。
- 文字あり版のレイアウトの仕組み（`TextModeLayout`）に新しいボタンを足す手順は、実装時に既存の Result 部分に合わせる。
