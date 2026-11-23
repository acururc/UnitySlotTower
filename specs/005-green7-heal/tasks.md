# 緑7の回復の演出 — タスク

要求は [`spec.md`](spec.md)、設計は [`plan.md`](plan.md)。

- [x] T901 (FR-002・FR-004) Core: `green_delay_seconds`、`Battlefield` の回復の待ちと `GreenHealed` — 検証: EditMode テスト（待ち・柱の瞬間の回復・倒れた味方・2回・倍速・CSV） → 2026-11-23 EditMode 559/559（新規5件。剣士が brute の2回目で倒れるため、テストの待ちは0.5秒）
- [x] T902 (FR-003) Runtime: `BattleController.GreenHealed` — 検証: コンパイル → 2026-11-23 コンパイル成功（EditMode の実行で確認）
- [x] T903 (FR-001・FR-006) 素材（必要なら追加とライセンス記録）、`Green7HealEffect` と Setup、`SlotBoardEffect` の緑7を外す — 検証: Setup をシーンに適用、コンパイル → 2026-11-23 新しい素材なし（circle_05・star_06・star_08 を流用、`docs/ASSET_LICENSES.md` の用途に追記）。Setup で部品6個を Game シーンに作成（シーンの差分は追加だけ、行の並べ替えあり）、材質3個
- [x] T904 (FR-001・FR-005) PlayMode テスト（柱・揺れない・部品が残らない・続けて揃う）と全テスト・ハーネス — 検証: EditMode・PlayMode の件数、ハーネス → 2026-11-23 EditMode 559/559、PlayMode 成功68・失敗0・Explicit 1（緑7を2回続けて揃える確認を含む）、ハーネス 8/8
- [x] T905 (FR-001) ユーザーが Editor で Play して確認、調整 — 検証: ユーザーの確認 → 2026-11-23 ユーザー確認「イイ感じにできてる」。調整なし
