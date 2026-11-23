# バーの闘気の演出 — タスク

要求は [`spec.md`](spec.md)、設計は [`plan.md`](plan.md)。

- [x] T1101 (FR-003・FR-005) Core: `bar_delay_seconds`、`SlotEffects` の待ち、`Battlefield.BarBurst` — 検証: EditMode テスト（待ち・長さ・通知・倍速・抽選の除外・CSV） → 2026-11-23 EditMode 569/569（新規5件）
- [x] T1102 (FR-002・FR-004) Runtime: `BattleController.BarBurst`・`UnitAttacked` — 検証: コンパイル → 2026-11-23 コンパイル成功（`scriptCompilationFailed`=False）
- [x] T1103 (FR-001・FR-004・FR-007) `BarPowerEffect` と Setup、`SlotBoardEffect` のバーを外す、ライセンスの用途に追記 — 検証: Setup をシーンに適用、コンパイル → 2026-11-23 新しい素材なし（circle_05・flame_01・spark_04 を流用、`docs/ASSET_LICENSES.md` の用途に追記）。Setup で部品8個を Game シーンに作成・保存（行の集合で比べて消えた行0）、材質3個、参照の欠けなし
- [x] T1104 (FR-002・FR-004・FR-006) PlayMode テスト（輪・爆発・揺れ・ヒットストップなし・オーラの付け外し・火花・部品が残らない）と全テスト・ハーネス — 検証: EditMode・PlayMode の件数、ハーネス → 2026-11-23 EditMode 569/569、PlayMode 成功68・失敗0・Explicit 1（1回目は溜め中に引いたレバーがコインで揃い、盤面演出の回数の期待値がずれて失敗→数え方を直して再実行）、ハーネス 8/8
- [ ] T1105 (FR-001) ユーザーが Editor で Play して確認、調整 — 検証: ユーザーの確認
