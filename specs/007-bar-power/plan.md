# バーの闘気の演出 — 実装計画

要求は [`spec.md`](spec.md)（FR-001〜FR-007）。黄7（spec 006 の [`plan.md`](../006-yellow7-poison/plan.md)）の仕組みをなぞる。

## 1. Core: 攻撃力アップを闘気の爆発まで待つ（FR-003・FR-005）
- `slot_rules.csv` に `bar_delay_seconds`＝1（0〜10、`GameDataValidator` に追加）。`SlotRules.BarDelaySeconds`、`SlotEffects.BarDelaySteps`。
- 待ちは `SlotEffects` が持つ（`AttackDelayStepsLeft`、`IsAttackPending`、`QueueAttack`、`StepAttackDelay`）。`IsActive(Bar)` は `IsAttackUp || IsAttackPending`（FR-005）。
- `Battlefield.ApplySlot(Bar)` は `QueueAttack()`、待ちが 0 ならその場で始めて `BarBurst` を通知する。`Battlefield.Advance` で `Effects.Step()` の後に待ちを減らし、0 で攻撃力アップを始めて `BarBurst`。長さは爆発の時点から丸々 `AttackSteps`。
- ステージが終わると `Advance` が呼ばれず、`SlotEffects` は戦闘ごとに作り直されるので待ちは捨てられる。一時停止・倍速は戦闘のステップに従う。
- `SlotTestRules.Create` の `barDelaySeconds` の既定は 0（既存のバーのテストを変えない）。

## 2. Runtime: 通知（FR-002・FR-004）
- `BattleController.BarBurst`・`UnitAttacked`（`Battlefield.UnitAttacked` の中継）。ヒットストップなし。購読・解除は `YellowBurst` と同じ場所。

## 3. Runtime: 演出（FR-001・FR-002・FR-004）
- 新しい `BarPowerEffect`（`Runtime/Battle`）。3D のフィールドの上のパーティクルを戦闘カメラで写す。時間は戦闘の時間。
  - 溜め: 生きている味方の足元（`CellTopToWorld`）に光の輪を毎フレーム置き、待ち時間で 3.2→0.6 マスに縮める。外から足元へ光の粒が吸い込まれる（爆発の直前ほど多い）。
  - 爆発（`BarBurst`）: 輪を消し、味方ごとに炎（flame_01）が噴き上がり、地面に光の輪が広がり、火花が飛ぶ。カメラを弱く揺らす（`shakeAmplitude` 0.06・`shakeSeconds` 0.2）。
  - オーラ: 攻撃力アップの間（`Effects.IsAttackUp`）は毎フレーム、生きている味方に橙の光を置き（上限64）、炎を立ちのぼらせる。`UnitAttacked` のたびに（攻撃力アップの間だけ）その味方から火花。切れたら炎を一度出してオーラを消す。ステージが変わったらオーラと輪を消す。
- 材質とパーティクルは Setup（`BarPowerEffectSetup`、メニュー `Tools/UnitySlotTower/Setup/Build Bar Power Effect (spec 007)`）が作る。材質 `Art/VFX/Bar *.mat` 3個（Glow=circle_05、Flame=flame_01、Spark=spark_04）。
- `SlotBoardEffect` はバーでは何もしない（矢印のコードは残す、黄7と同じ）。

## 4. 素材（FR-007）
- 新しい素材は使わない（Kenney の circle_05・flame_01・spark_04 を流用）。`docs/ASSET_LICENSES.md` の用途に追記。

## 5. リスク
- 攻撃力の倍率の表示（`OperationBarStatus`）は待ちの間は ×1.0 のままで、爆発の瞬間から出る（青7・黄7と同じ）。
- 味方の数が多いと炎の粒が増える: 上限（600）で頭打ち。

## 6. 検証
- EditMode: 待ち・爆発の時点からの長さ・通知・倍速・抽選からの除外・CSV（AC-002）。
- PlayMode: バーで輪→爆発（揺れ・ヒットストップなし）、オーラが味方に付き、攻撃ごとに火花、切れると消え、部品が残らない（AC-003）。既存の演出テスト（AC-004）。
- ハーネス。見た目はユーザーが Editor で Play（AC-001）。
