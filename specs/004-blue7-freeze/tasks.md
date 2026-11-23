# 青7の凍結の演出 — タスク

要求は [`spec.md`](spec.md)、設計は [`plan.md`](plan.md)。

- [x] T801 (FR-003・FR-005) Core: `blue_delay_seconds`、`SlotEffects` の待ち、`Battlefield.BlueFroze` — 検証: EditMode テスト（待ち・停止の長さ・倍速・抽選の除外・CSV）→ 2026-11-23 EditMode 554/554（新規5件）
- [x] T802 (FR-002) Runtime: `BattleController` の青7のヒットストップと `BlueFroze`、揺れを `BattleCameraShake` に移す — 検証: コンパイル、既存の赤7の PlayMode テスト → 2026-11-23 コンパイル成功、赤7の PlayMode テスト（揺れ・ヒットストップ）を含め T804 の全件が通過
- [x] T803 (FR-001・FR-004・FR-007) 素材の取得とライセンス記録、`Blue7FreezeEffect` と Setup、`SlotBoardEffect` の青7を外す — 検証: Setup をシーンに適用、コンパイル → 2026-11-23 Kenney 3枚（star_06・star_08・spark_04）を追加し `docs/ASSET_LICENSES.md` を更新、Setup で部品10個を Game シーンに作成（シーンの差分は追加だけ）、材質6個
- [x] T804 (FR-002・FR-004・FR-006) PlayMode テスト（凍結・ヒットストップ中のレバー・塊の付け外し・部品が残らない）と全テスト・ハーネス — 検証: EditMode・PlayMode の件数、ハーネス → 2026-11-23 EditMode 554/554、PlayMode 成功68・失敗0・Explicit 1、ハーネス 8/8
- [x] T805 (FR-001) ユーザーが Editor で Play して確認、調整 — 検証: ユーザーの確認 → 2026-11-23 ユーザーが確認「良かった」。調整なし（初期値のまま）
