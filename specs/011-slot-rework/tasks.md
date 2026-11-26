# tasks: スロットの作り直し（spec 011）

- [x] T1501 (FR-003, FR-010, FR-011) `SlotMachine` の賭けとライン抽選、3×3 の見える図柄、決め打ち。`slot_rules.csv` の新しい行と検証 — 検証: EditMode `SlotMachineTests`（19ケース）・`SlotSimulationTests`（2万回で1ライン60%、3ラインの独立、図柄の重み）。2026-11-26 成功
- [x] T1502 (FR-020, FR-021) 効果の強さ（枚数・最大HPの割合・ボス）。`ApplySlot(symbol, power)` — 検証: EditMode `SlotPowerTests`（新規）と `SlotEffectsTests`（期待値を割合に更新）。2026-11-26 成功
- [x] T1503 (FR-030, FR-031) `slot_showcase` 列、戦闘の一時保留と解除、ステージ1のウェーブ5（近接10＋遠距離4、仮） — 検証: EditMode `SlotShowcaseTests`・`TableValidationTests`・`StageDataTests`。2026-11-26 成功
- [x] T1504 (FR-001, FR-002, FR-011, FR-012) リールの上書き停止、BET ボタン、ライン表示、揃いのずらし通知（`line_gap_seconds`）、揃った行の光（2本目以降は大きく）。Setup `Update Slot Bet (spec 011)` 実行済み — 検証: PlayMode `SlotBetShowcaseTests.Bet_Button_Cycles_And_Lever_Spends_The_Bet`・`SlotDuringBattleTests`（全ラインの見た目）。2026-11-26 成功
- [x] T1505 (FR-031, FR-032, FR-033) 見せ場の Runtime（コイン補充・賭け3枚・レバーの点滅と矢印・決め打ち・最初の揃いで再開・ステージ開始で後片付け） — 検証: PlayMode `SlotBetShowcaseTests.Showcase_Holds_Until_The_Lever_Then_Triple_Hit_Strikes`。2026-11-26 成功
- [x] T1506 (FR-022) 早見表の確率（1ライン）と効果（1枚の値）、賭けとラインの説明（Setup `Update Help Bet (spec 011)` 実行済み） — 検証: PlayMode `HelpTests`（40%・9%・12%・-20%・-4%）。見た目はユーザー確認（T1508）
- [x] T1507 (AC-006) 全件 — 2026-11-26: EditMode 601/601、PlayMode 成功72/失敗0/スキップ1（以前からの Explicit）、ハーネス 8/8
- [ ] T1508 (AC-003〜AC-005) ユーザーが Editor で確認: BET ボタンとライン印の位置・見え方、二重・三重の演出、早見表の説明、ステージ1の見せ場の通し、文字あり版の配置
