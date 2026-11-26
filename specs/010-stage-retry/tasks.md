# 010 ステージのやり直し — タスク

- [x] T1401 (FR-201) Core: `CoinWallet.Reset`・`UnitUpgrades.Snapshot/Restore`・`RunState.RecordStageStart/RestoreStageStart` と EditMode テスト — 検証: EditMode 579/579 成功（2026-11-26、新規3件を含む）
- [x] T1402 (FR-201・FR-203) `RunController` でステージ開始時に記録し、`RetryStage()` を足す — 検証: コンパイル成功（scriptCompilationFailed=False）、T1404 の PlayMode
- [x] T1403 (FR-202・FR-204) `ResultScreen`: ゲームオーバーは やり直し（▶＋旗）・タイトル、クリアは もう一度・タイトル（どちらも2つ。やり直しはクリアの ▶ と同じ位置）。回転中は やり直し を受け付けない。Setup `Update Result Screen (spec 010)`、文字あり版のラベルと位置、`ui_texts.csv`（`result_stage_retry`） — 検証: Setup 実行済み。Game.unity は行の集合で比べて追加だけ（2026-11-26 の再承認後に「最初から」の「1」の印を Editor で削除）
- [x] T1404 (AC-202・AC-203) PlayMode `ResultFlowTests`・`InputOnlyRunTests` の更新と追加（ステージ1・2でのやり直し、ゲームオーバーは2ボタン、クリアは もう一度・タイトル） — 検証: 2ボタン版で PlayMode 成功70/失敗0/Explicit 1（2026-11-26）。ハーネス 8/8
- [x] T1406 (別件、2026-11-26 ユーザー指摘) 文字あり版で札の味方の絵が枠の上にはみ出す: `TextModeLayoutSetup.BuildUnitCard` が実際の操作バーの高さ（外枠で 211.2）で割っていたのを設計値 240 に直し、`Build Text Mode Layout (T359)` を再実行 — 検証: シーンの差分は札の絵と強化の板のアンカー8つだけ。Play 中の文字あり版の画像で枠内に収まることを確認
- [ ] T1405 (AC-203・AC-204) ユーザーの手動プレイ: 負けてからやり直す流れ、リザルトのボタンの見た目（文字なし・文字あり） — 検証: ユーザー確認
