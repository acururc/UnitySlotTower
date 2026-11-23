# 黄7の毒の演出 — 実装計画

要求は [`spec.md`](spec.md)（FR-001〜FR-007）。青7（spec 004 の [`plan.md`](../004-blue7-freeze/plan.md)）の仕組みをなぞる。

## 1. Core: 毒を沼が弾けるまで待つ（FR-003・FR-005）
- `slot_rules.csv` に `yellow_delay_seconds`＝1（0〜10、`GameDataValidator` に追加）。`SlotRules.YellowDelaySeconds`、`SlotEffects.YellowDelaySteps`。
- 待ちは `SlotEffects` が持つ（`PoisonDelayStepsLeft`、`IsPoisonPending`、`QueuePoison`、`StepPoisonDelay`）。`IsActive(Yellow7)` は `IsPoisoned || IsPoisonPending`（FR-005）。
- `Battlefield.ApplySlot(Yellow7)` は `QueuePoison()`、待ちが 0 ならその場で毒を始めて `YellowBurst` を通知する。
- `Battlefield.Advance` で `Effects.Step()`（毒のダメージ）の後に待ちを減らし、0 で毒を始めて `YellowBurst`。毒の長さは弾けた時点から丸々 `PoisonSteps`。毒のダメージを入れたら `PoisonTicked` を通知する（オーラの点滅、FR-004）。
- ステージが終わると `Advance` が呼ばれず、`SlotEffects` は戦闘ごとに作り直されるので待ちは捨てられる。一時停止・倍速は戦闘のステップに従う。
- `SlotTestRules.Create` の `yellowDelaySeconds` の既定は 0（既存の黄7のテストを変えない）。

## 2. Runtime: 通知（FR-002）
- `BattleController.YellowBurst`・`PoisonTicked` を中継する（ヒットストップなし）。購読・解除は `GreenHealed` と同じ場所。

## 3. Runtime: 演出（FR-001・FR-002・FR-004）
- 新しい `Yellow7PoisonEffect`（`Runtime/Battle`）。3D のフィールドの上のパーティクルを戦闘カメラで写す。時間は戦闘の時間。
  - 沼（待ち時間の 0〜60%）: 盤面のランダムな位置に平らな濃い紫の丸（10個）を順に湧かせ、泡を立てる（弾ける直前ほど多い）。
  - 弾ける（`YellowBurst`）: 沼を消し、沼の位置から光の輪・飛沫・毒の霧を出す。カメラを弱く揺らす（`shakeAmplitude` 0.06・`shakeSeconds` 0.2、`BattleCameraShake`）。
  - オーラ: 毒の間（`Effects.IsPoisoned`）は毎フレーム、生きている敵の位置に紫の光の粒を置く（上限64）。`PoisonTicked` で明るく大きくして0.2秒で戻し、敵ごとに泡を出す。毒が切れたら霧を出してオーラを消す。ステージが変わったらオーラを消す。
- 材質とパーティクルは Setup（`Yellow7PoisonEffectSetup`、メニュー `Tools/UnitySlotTower/Setup/Build Yellow7 Poison Effect (spec 006)`）が作る。材質 `Art/VFX/Yellow7 *.mat` 3個。
- `SlotBoardEffect` は黄7では何もしない。

## 4. 素材（FR-007）
- 新しい素材は使わない（Kenney の circle_05・smoke_04 を流用）。`docs/ASSET_LICENSES.md` の用途に追記。

## 5. リスク
- 毒のアイコン（`OperationBarStatus`）は待ちの間は出ず、弾けた瞬間から出る（青7と同じ）。
- 黄7と青7・赤7の揺れが重なる: `BattleCameraShake` がまとめる。

## 6. 検証
- EditMode: 待ち・弾けた時点からの毒の長さ・ダメージの通知・ステージ終了・倍速・抽選からの除外・CSV（AC-002）。
- PlayMode: 黄7で沼→弾ける（揺れ・ヒットストップなし）、オーラが敵に付き点滅し、切れると消え、部品が残らない（AC-003）。既存の演出テスト（AC-004）。
- ハーネス。見た目はユーザーが Editor で Play（AC-001）。
