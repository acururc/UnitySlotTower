# 黄7の毒の演出 — タスク

要求は [`spec.md`](spec.md)、設計は [`plan.md`](plan.md)。

- [x] T1001 (FR-003・FR-005) Core: `yellow_delay_seconds`、`SlotEffects` の待ち、`Battlefield.YellowBurst`・`PoisonTicked` — 検証: EditMode テスト（待ち・毒の長さ・通知・倍速・抽選の除外・CSV） → 2026-11-23 EditMode 564/564（新規5件）
- [x] T1002 (FR-002) Runtime: `BattleController.YellowBurst`・`PoisonTicked` — 検証: コンパイル → 2026-11-23 コンパイル成功（`scriptCompilationFailed`=False）
- [x] T1003 (FR-001・FR-004・FR-007) `Yellow7PoisonEffect` と Setup、`SlotBoardEffect` の黄7を外す、ライセンスの用途に追記 — 検証: Setup をシーンに適用、コンパイル → 2026-11-23 新しい素材なし（circle_05・smoke_04 を流用、`docs/ASSET_LICENSES.md` の用途に追記）。Setup で部品7個を Game シーンに作成・保存（行の集合で比べて消えた行0）、材質3個、参照の欠けなし
- [x] T1004 (FR-002・FR-004・FR-006) PlayMode テスト（沼・弾ける・揺れ・ヒットストップなし・オーラの付け外しと点滅・部品が残らない）と全テスト・ハーネス — 検証: EditMode・PlayMode の件数、ハーネス → 2026-11-23 EditMode 564/564、PlayMode 成功68・失敗0・Explicit 1、ハーネス 8/8
- [ ] T1005 (FR-001) ユーザーが Editor で Play して確認、調整 — 検証: ユーザーの確認
