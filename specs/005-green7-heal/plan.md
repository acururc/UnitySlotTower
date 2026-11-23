# 緑7の回復の演出 — 実装計画

要求は [`spec.md`](spec.md)（FR-001〜FR-006）。待ちが複数並んでよい点は赤7（spec 003 の [`plan.md`](../003-red7-breath/plan.md)）と同じなので、赤7の仕組みをなぞる。

## 1. Core: 回復を光の柱まで待つ（FR-002・FR-004）
- `slot_rules.csv` に `green_delay_seconds`＝1（0〜10、`GameDataValidator` に追加）。`SlotRules.GreenDelaySeconds`、`SlotEffects.GreenDelaySteps`。
- `Battlefield.ApplySlot(Green7)`: 待ちが 0 ならその場で回復、そうでなければ `_greenHeals` に待ちを足す（赤7の `_redStrikes` と同じ。2回揃えば2つ並ぶ）。`PendingGreenHeals`。
- `Battlefield.Advance` で `StepRedStrikes` の隣で待ちを1ステップ減らし、0 になったら生きている味方全員を回復して `GreenHealed` を通知する（回復した後に呼ぶ）。
- ステージが終わると `Field.Advance` が呼ばれず、`Battlefield` は戦闘ごとに作り直されるので待ちは捨てられる。一時停止・倍速は戦闘のステップで数えるので自然に従う。
- `SlotTestRules.Create` の `greenDelaySeconds` の既定は 0（既存の緑7のテストを変えない）。

## 2. Runtime: 通知（FR-003）
- `BattleController` に `GreenHealed` を足し、`Field.GreenHealed` をそのまま流す（購読・解除は `RedStruck` と同じ場所）。ヒットストップは入れない。

## 3. Runtime: 演出（FR-001）
- 新しい `Green7HealEffect`（`Runtime/Battle`）。`Red7BreathEffect` と同じく 3D のフィールドの上のパーティクル（Shuriken）を戦闘カメラで写す。時間は戦闘の時間。揺れは使わない。
  - 予兆（待ち時間の 0〜40%）: 盤面の奥の端に緑の光の粒。
  - 降り注ぐ光（20〜100%）: 盤面の上空から緑・白の光の粒がゆっくり降る（盤面全体に広く）。
  - 光の柱（`GreenHealed`）: その時点で生きている味方ごとに、上へ伸びる光の柱（縦に引き伸ばした粒）と足元の光の輪、盤面全体のやわらかい緑の閃光。
  - 余韻: 味方ごとに光の粒がふわっと上がって消える。
- 柱が来ないまま待ち時間＋0.5秒を過ぎたら（ステージが先に終わった）、降る光を止める（赤7の `StrikeGrace` と同じ）。
- パーティクルの材質は Editor の Setup（`Green7HealEffectSetup`、メニュー `Tools/UnitySlotTower/Setup/Build Green7 Heal Effect (spec 005)`）が作る。材質は `Art/VFX/`（既存の `UnitySlotTower/VFX Particle` シェーダー、加算）。
- `SlotBoardEffect` は緑7では何もしない（盤面の色と味方ごとの緑の粒を消す）。

## 4. 素材（FR-006）
- まず手元の Kenney Particle Pack の素材（circle_05・star_06・star_08・spark_04）で組む。柱や光線に足りなければ、同じパック（CC0）から光の素材（light・magic 系）を数枚 `Art/VFX/Kenney/` へ足し、`docs/ASSET_LICENSES.md` に追記する。

## 5. リスク
- 赤7・青7・緑7の待ちが重なる: それぞれの演出が別々に動く。緑7は揺れ・ヒットストップを使わないので干渉しない。
- 倒れた味方: 柱の瞬間に `IsAlive` の味方だけに柱を出す（Core と同じ条件）。
- 粒の数: 味方の数に比例する。1回の柱で出す数に上限を付ける。

## 6. 検証
- EditMode: 待ち・柱の瞬間の回復・待ちの間に倒れた味方・2回揃う・倍速・CSV の検証（AC-002）。
- PlayMode: 緑7で予兆→柱が出て、揺れずに部品がすべて消える、続けて揃っても崩れない（AC-003）。既存の演出テスト（AC-004）。
- ハーネス。見た目はユーザーが Editor で Play（AC-001）。
