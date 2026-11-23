# 赤7の息吹の演出 — タスク

書式: `- [ ] Txxx (要求ID) 作業 — 検証: 方法`。完了時は `[x]` と実際の結果。

- [x] T701 (FR-005) Core: `red_delay_seconds` と赤7の待ち・`RedStruck` — 検証: EditMode（新しいテストと既存の全件） — 2026-11-23 EditMode 549/549（`SlotEffectsTests` に4件追加）
- [x] T702 (FR-002) Runtime: `BattleController` のヒットストップと `RedStruck` の通知 — 検証: PlayMode — 2026-11-23 `SlotCabinetEffectTests` で爆発の直後に戦闘の Tick が止まり、レバーを引け、0.3秒後に戦闘が進むことを確認
- [x] T703 (FR-004) 素材の取り込みとライセンスの記録、パーティクル用シェーダーと材質 — 検証: Editor のコンパイル・文書の確認 — 2026-11-23 Kenney 6枚と `License.txt`（行末の空白だけ削除）、`VfxParticle.shader`、材質6個（Setup が作成）、`docs/ASSET_LICENSES.md` に1行
- [x] T704 (FR-001・FR-002・FR-003) `Red7BreathEffect` と Setup、`SlotBoardEffect` から赤7を外す、シーンの再構築 — 検証: PlayMode（`SlotCabinetEffectTests` の更新） — 2026-11-23 Setup で Game シーンに「Red7 Breath Effect」（パーティクル8つ）を作って保存。PlayMode 68 成功/失敗0/Explicit 1、ハーネス 8/8
- [x] T705 (AC-001) ユーザーが Editor で Play して確認し、秒数・色・揺れを調整 — 検証: ユーザーの確認 — 2026-11-23 ユーザー確認: 息吹は良い。地面の火を muzzle_02 から flame_01（回して低く広げる）に変更し、変更後も良いと確認。秒数・揺れは初期値のまま
- [x] T706 全体の検証（EditMode・PlayMode 全件、ハーネス）と HANDOFF の更新 — 2026-11-23 EditMode 549/549、PlayMode 68 成功/失敗0/Explicit 1（地面の火の変更後）、ハーネス 8/8
