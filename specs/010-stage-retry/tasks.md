# 010 ステージのやり直し — タスク

- [x] T1401 (FR-201) Core: `CoinWallet.Reset`・`UnitUpgrades.Snapshot/Restore`・`RunState.RecordStageStart/RestoreStageStart` と EditMode テスト — 検証: EditMode 579/579 成功（2026-11-26、新規3件を含む）
- [x] T1402 (FR-201・FR-203) `RunController` でステージ開始時に記録し、`RetryStage()` を足す — 検証: コンパイル成功（scriptCompilationFailed=False）、T1404 の PlayMode
- [x] T1403 (FR-202・FR-204) `ResultScreen` の3ボタン（ゲームオーバー）/2ボタン（クリア）、キーボード選択、回転中は受け付けない。Setup `Update Result Screen (spec 010)`、文字あり版のラベルと位置、`ui_texts.csv` — 検証: Setup 実行済み。Game.unity は行の集合で比べて追加だけ（削除0、Stage Retry Button・One Mark・ラベル・参照4つ）。見た目は T1405 でユーザーが確認
- [x] T1404 (AC-202・AC-203) PlayMode `ResultFlowTests` の更新と追加（ステージ1でのやり直し→最初から、ステージ2でのやり直し） — 検証: PlayMode 成功70/失敗0/Explicit 1（2026-11-26）。ハーネス 8/8
- [ ] T1405 (AC-203・AC-204) ユーザーの手動プレイ: 負けてからやり直す流れ、リザルトのボタンの見た目（文字なし・文字あり） — 検証: ユーザー確認
