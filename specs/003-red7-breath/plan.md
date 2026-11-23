# 赤7の息吹の演出 — 実装計画

要求は [`spec.md`](spec.md)（FR-001〜FR-005）。

## 1. Core: ダメージを爆発まで待つ（FR-005）
- `slot_rules.csv` に `red_delay_seconds`＝1（0〜10、`GameDataValidator` に追加）。`SlotRules.RedDelaySeconds`、`SlotEffects.RedDelaySteps`（ステップに直す）。
- `Battlefield.ApplySlot(Red7)` は待ちの残りステップを列に足す（0 ならその場で当てる）。`Advance` の1ステップごとに減らし、0 になったら全体ダメージ → `RedStruck` を通知 → 倒した敵の消滅（ドロップ）。
- ステージが終わると `BattleNodeRun` が `Field.Advance` を呼ばなくなるので、待ちは捨てられる（AC-005）。開始の演出中・一時停止中は進まず、倍速では速く進む（戦闘のステップで数えるため）。
- テスト用の `SlotTestRules.Create` は `redDelaySeconds` の既定を 0 にして既存の赤7のテストを変えない。

## 2. Runtime: ヒットストップ（FR-002・D-05）
- `BattleController` が `Field.RedStruck` を受けて `hitStopSeconds`（0.1、Inspector）の間、`Run.Advance` を呼ばない。減らすのは実時間（`unscaledDeltaTime`）で、一時停止中（timeScale 0）は減らさない。`Time.timeScale` は触らないのでスロットと操作は止まらない。
- 表示用に `BattleController.RedStruck` を通知する。

## 3. Runtime: 演出（FR-001・FR-002）
- 新しい `Red7BreathEffect`（`Runtime/Battle`）。3D のフィールドの上に置いたパーティクル（Shuriken）を戦闘カメラで写すので、戦闘モニターの枠で自然に切れる。
  - 予兆（揃ってから待ち時間の 0〜35%）: 盤面の奥の端（画面の上端）に赤い光の粒。
  - 息吹（25〜100%）: 盤面の奥の上空から炎の流れを吹き下ろし、狙いを奥から手前へ動かして盤面をなめる。地面の炎も同じ線で追う。
  - 爆発（`RedStruck`）: 盤面全体の閃光、盤面に散らばる火の玉、敵ごとの爆発、火の粉、煙。戦闘カメラを短く揺らす（終わったら元の位置に戻す）。
  - 時間は戦闘の時間（`Time.deltaTime`）。ステージが先に終わって爆発が来なければ、息吹を止めて終える。
- パーティクルは Editor の Setup（`Red7BreathEffectSetup`、メニュー Build Red7 Breath Effect）がシーンに作るので Inspector で調整できる。材質は `Art/VFX/` に置く（加算と半透明の2種類の小さな自作シェーダー `UnitySlotTower/VFX Particle`）。
- `SlotBoardEffect` は赤7では何もしない（赤い帯と敵ごとの丸は消す）。

## 4. 素材（FR-004）
- Kenney Particle Pack 1.1（CC0、https://kenney.nl/assets/particle-pack、2026-11-23 取得）から5枚を `Assets/UnitySlotTower/Art/VFX/Kenney/` へ: flame_01・fire_01・smoke_04・circle_05・scorch_02（透過版。muzzle_02 は地面の炎が水滴に見えたので外した）。`License.txt` も同じ場所に置き、`docs/ASSET_LICENSES.md` に記録する。

## 5. リスク
- 戦闘カメラの位置は `BattleFieldView.FitCamera` がマップを読んだときだけ書くので、揺れは元の位置を覚えて戻す。揺れの最中にマップが変わったら、戻す位置は新しいカメラの位置にする。
- パーティクルの数が多すぎると重い。最大数を決めて上限を付ける。

## 6. 検証
- EditMode: 待ち・ステージ終了・倍速（ステップ数）・ドロップ・CSV の検証（AC-005）。
- PlayMode: 赤7で息吹が始まって爆発し、部品が消える。ヒットストップ中は戦闘の Tick が止まり、レバーを引ける（AC-002・AC-006）。既存の演出テスト（AC-003）。
- ハーネス。見た目はユーザーが Editor で Play（AC-001）。
