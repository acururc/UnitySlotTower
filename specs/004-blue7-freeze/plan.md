# 青7の凍結の演出 — 実装計画

要求は [`spec.md`](spec.md)（FR-001〜FR-007）。赤7（spec 003 の [`plan.md`](../003-red7-breath/plan.md)）の仕組みをなぞる。

## 1. Core: 停止を凍結まで待つ（FR-003・FR-005）
- `slot_rules.csv` に `blue_delay_seconds`＝1（0〜10、`GameDataValidator` に追加）。`SlotRules.BlueDelaySeconds`、`SlotEffects.BlueDelaySteps`。
- 待ちは `SlotEffects` が持つ（`StopDelayStepsLeft`、`IsStopPending`）。`IsActive(Blue7)` は `IsStopped || IsStopPending`（FR-005）。
- `Battlefield.ApplySlot(Blue7)` は `Effects.QueueStop()`: 待ちが 0 ならその場で `StartStop`、そうでなければ待ちを入れる（待ち中にもう一度来たら待ちを最初に戻す。抽選で外れるのでふつうは起きない）。
- `Battlefield.Advance` で `Effects.Step()` の後に待ちを1ステップ減らし、0 になったら `StartStop` して `BlueFroze` を通知する。停止の長さは凍結の時点から `StopSteps`（その同じステップの減算はもう済んでいるので丸々残る）。
- ステージが終わると `Field.Advance` が呼ばれず、`SlotEffects` は戦闘ごとに作り直されるので待ちは捨てられる（AC-002）。一時停止・倍速は戦闘のステップで数えるので自然に従う。
- `SlotTestRules.Create` の `blueDelaySeconds` の既定は 0（既存の青7のテストを変えない）。

## 2. Runtime: ヒットストップ（FR-002）
- `BattleController` に `blueHitStopSeconds`（0.1、Inspector）を足し、`Field.BlueFroze` で `_hitStopLeft` に入れる（長いほうを残す）。表示用に `BattleController.BlueFroze` を通知する。購読・解除は `RedStruck` と同じ場所。

## 3. Runtime: 演出（FR-001・FR-002・FR-004）
- 新しい `Blue7FreezeEffect`（`Runtime/Battle`）。`Red7BreathEffect` と同じく 3D のフィールドの上のパーティクル（Shuriken）を戦闘カメラで写す。時間は戦闘の時間。
  - 予兆（待ち時間の 0〜40%）: 盤面の奥の端に青白い光の粒。
  - 冷気（20〜100%）: 霜・雪の粒の帯を奥の端から手前の端へ動かし、通った所に地面の霜を残す。
  - 凍結（`BlueFroze`）: 盤面全体の青白い閃光、盤面に散らばる氷のきらめき、カメラの小さな揺れ（`shakeAmplitude` 0.1・`shakeSeconds` 0.25 を仮置き）。
  - 氷の塊: 停止中（`Effects.IsStopped`）は毎フレーム、生きている敵の位置に氷の塊を置く（プール。停止中に出た敵にも付き、倒された敵の分は消える）。停止が切れた瞬間に、出ていた塊の位置に砕けた破片を出して塊を消す。ステージが変わったら塊を消す。
  - 揺れは赤7と同じ方法（元の位置を覚えて戻す）。赤7と同時に揺れても戻る位置が崩れないよう、揺れの処理を小さな共有部品 `BattleCameraShake` に移し、両方の演出がそれを使う。
- 氷の塊はまず仮の形（半透明の水色の立方体を少し傾けたもの）で置く。見た目の細部はユーザーの確認で詰める。
- パーティクル・塊の材質は Editor の Setup（`Blue7FreezeEffectSetup`、メニュー `Tools/UnitySlotTower/Setup/Build Blue7 Freeze Effect (spec 004)`）が作る。材質は `Art/VFX/`（既存の `UnitySlotTower/VFX Particle` シェーダー）。
- `SlotBoardEffect` は青7では何もしない（盤面の色と敵ごとの青い丸を消す）。

## 4. 素材（FR-007）
- Kenney Particle Pack（CC0、spec 003 と同じ）から、きらめき・霜・破片に使う数枚（star・spark・magic・scratch 系から選ぶ）を `Art/VFX/Kenney/` へ足し、`docs/ASSET_LICENSES.md` に追記する。

## 5. リスク
- 赤7と青7の待ちが重なる: 両方の演出が別々に動き、ヒットストップは長いほう、揺れは共有部品でまとめる。
- 氷の塊の数: 敵の数に比例する。プールに上限（64）を付ける。
- `BattleCameraShake` への移し替えで赤7の揺れが変わらないこと（既存の PlayMode テスト）。

## 6. 検証
- EditMode: 待ち・凍結の時点からの停止の長さ・ステージ終了・倍速・抽選からの除外・CSV の検証（AC-002・AC-003）。
- PlayMode: 青7で予兆→凍結、ヒットストップ中にレバーを引ける（AC-004）。停止中に塊が付き、切れると消え、部品が残らない（AC-005）。既存の演出テスト（AC-006）。
- ハーネス。見た目はユーザーが Editor で Play（AC-001）。
