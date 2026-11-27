# plan: テストプレイの指摘への対応（spec 015）

## 方針

- 見せ場（FR-001〜FR-003）: `waves.csv` のステージ1を「ウェーブ1 = 3レーンに近接8体」＋元の1〜4 にし、`stage_waves.csv` の `slot_showcase` をウェーブ1へ移す。`BattleController.ShowcaseHits` を 中段 赤7・上段 青7・下段 黄7 にし、見せ場のレバーの後の `SlotCabinet.Settled` で `SetBet(1)`。
- 強調（FR-004）: Runtime に `SlotShowcaseGuide`（新規、`Runtime/Slot/`）。`SlotCabinet.LeverHighlighted` の間だけ、筐体の矩形を除いた4枚の暗幕（入力も止める）・レバーを指す大きな `NudgeArrow`・`TextModeLabel`（`ui_texts.csv` の `showcase_lever`）の案内を出す。Portrait Root の最後の子に置く。Setup メニュー `Tools/UnitySlotTower/Setup/Showcase Guide (spec 015)`（`SlotCabinetSetup.cs` に追加）。旧の小さな三角（`leverHint`）は残す。
- 当たり率（FR-010）: `slot_rules.csv` の `line_hit_rate` 0.7。早見表は値から出る。
- 音（FR-020〜FR-024）: `SoundSettings` の初期値 0.5。電子音は `scripts/audio/synth_sounds.py`（標準ライブラリだけ、矩形波・三角波の合成）で `Assets/UnitySlotTower/Audio/Se/synth_*.wav` を作る（賭け・レバー・リール回転ループ・リール停止・揃い・揃いの上昇音・ハズレ・クリアのファンファーレ）。`SoundId` の末尾に `LineFanfare` を足し（既存の番号は変えない）、`GameAudio.PlaySe(id, pitch)` を足して揃いの n 本目ほど高くする。`SoundSetup.cs` の表を差し替え、BGM 0.5・ウェーブ開始 0.35。
- ステージ（FR-030〜FR-032）: `maps/elite_a.csv` を中央の高台の島を挟む分かれ道に作り直し、`map_paths.csv` の elite_a の経路（g1 上回り・g2 下回り・a1 直進、すべて S1）とステージ4の `waves.csv` を書き直す。ステージ5・6は `waves.csv`（6 は `stage_waves.csv` のウェーブ2の `start_after` も）。
- 表示（FR-040〜FR-043）: 文字あり版のレバーの札と BET ボタンの重なりは `TextModeLayoutSetup` の札の位置で直す。札の灰色の長方形は Editor で原因を見て直す。HP ゲージは `BattleActorsView` で頭の上に置き、深度を無視して最前面に描く小さなシェーダー（`Art/Materials/HpGauge.shader`＋マテリアル）を使う。重装の盾は小さく・体の横に寄せる。

## 影響

- データ: `waves.csv`・`stage_waves.csv`・`slot_rules.csv`・`maps/elite_a.csv`・`map_paths.csv`・`ui_texts.csv`
- Runtime: `BattleController`・`SlotShowcaseGuide`（新）・`GameAudio`・`BattleSound`・`BattleActorsView`。Core: `SoundSettings`・`SoundId`
- Editor: `SlotCabinetSetup`・`SoundSetup`・`TextModeLayoutSetup`・`BattleActorsSetup`（ゲージのマテリアル）
- シーン: Game.unity（Setup で再生成）
- テスト: `SlotShowcaseTests`・`StageDataTests`・`StageThemeTests`・`SlotSimulationTests`・`SlotTestRules`・`HelpTests`・`SlotBetShowcaseTests`・音のテスト

## リスク

- Setup の再実行で Inspector の調整値が初期値に戻る（既知）。
- 合成音は狙いの形（上がる和音・ループ）は保証できるが、好みはユーザーの試聴で決まる。
- 見せ場の群れが3つの効果で全滅しない数値だと、配置前に本拠地まで来る。PlayMode テストで全滅を確かめる。
