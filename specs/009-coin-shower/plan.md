# plan: コインの噴水の演出（spec 009）

要求は [`spec.md`](spec.md) の FR-001〜FR-008。作り方は spec 007（バー）にそろえる。

## 1. Core（FR-003・FR-004）
- `slot_rules.csv` に `coin_delay_seconds,1.5` を足す。`SlotRules.CoinDelaySeconds`、`GameDataValidator` で 0 以上を確かめる。
- `SlotEffects`: `CoinDelaySteps`、待っている分のリスト `_coinDelays`（揃うたびに1つ。緑7の `_greenHeals` と同じ形）、`PendingCoins`。
  - `QueueCoin()`: 待ちが 0 ならその場で `AddCoin()` して true、そうでなければ待ちを足して false。
  - `StepCoinDelay()`: 待ちを1ステップずつ減らし、0 になった分を `AddCoin()` してその数を返す。`Step()` の後に呼ぶ。
  - `IsActive(Coin)`: `min(1 + coin_step × (Coins + PendingCoins), coin_cap) >= coin_cap`。
  - SlotEffects はステージごとに作り直すので、待ちの分はステージが終わると捨てられる（そのまま FR-003 を満たす）。
- `Battlefield`: `ApplySlot(Coin)` は `QueueCoin()`、毎ステップで `StepCoinDelay()` を呼び、上がった回数だけ `CoinGained` を通知する。
  `RemoveDefeatedEnemies` で倒した敵ごとに `EnemyDefeated(BattleEnemy)` を通知する（全体ダメージ・毒で倒した敵も同じ所を通る）。
- `BattleController`: `CoinGained`・`EnemyDefeated` を中継する（`BarBurst` と同じ付け外し）。

## 2. 表示（FR-001・FR-002・FR-005・FR-006）: `Runtime/Battle/CoinFountainEffect.cs`
金貨は筐体（下）→戦闘モニター（上）→所持金の表示（上端の帯）をまたぐので、3D のパーティクルではなく `SlotAlignEffect` と同じ UI Canvas いっぱいの層に UI の Image で描く。部品はこの層の子に作って使い回す（プール）。時間は戦闘の時間（`Time.deltaTime`）。
- 噴き出し: 揃ったら中段の真ん中のマス（`cabinet.ReelCenterCell(1)`）から金貨（初期値 36 枚）を上向きに扇状に打ち上げる。重力で戦闘モニターの中ほどまで上がって落ち始め、横幅を |cos| で縮めて回って見せる。同時に光の閃光を1つ出し、戦闘カメラを弱く揺らす（`BattleCameraShake`、0.2秒・0.06、spec 007 と同じ）。
- 吸い込み: 待ち時間の 55% ほどから金貨を順に所持金の表示（`OperationBarStatus.MoneyRect`、所持金の数字）へ曲線で飛ばし、待ち時間ちょうどに最後の金貨が着く。着くたびに小さいきらめき。
- 山場: `CoinGained` で所持金の表示を弾ませ（大きく→戻る）、金色の光の輪を広げる。`CoinGained` が来なければ（ステージが先に終わった）金貨は着いたところで消えるだけ。
- 倒したとき: 倍率が 1 より上の間、`EnemyDefeated` の敵の位置（`battleCamera.WorldToScreenPoint`）から小さい金貨を `round((倍率−1)×10)` 枚（1〜5）飛び出させ、少し跳ねてから所持金の表示へ飛ばす（約0.6秒）。同時に出ている金貨は上限（初期値 48）を超えたら出さない。
- きらめき: 倍率が 1 より上の間、所持金の表示の周りに小さい星を数個（初期値 4）ゆっくり点滅させる。倍率が 1 に戻ったら消す。
- 絵: 金貨と星は自作の PNG を Setup で作る（`Editor/Setup/CoinFountainTextures.cs`、`Art/VFX/Coin.png`・`Art/VFX/Sparkle.png`）。光は既存の丸い光の絵を使う。外部の素材は使わない。
- `SlotBoardEffect` からコインを外す（spec 007 でバーを外したのと同じ）。`SlotAlignEffect` の倍率への玉はそのまま。

## 3. Setup
- `Editor/Setup/CoinFountainEffectSetup.cs`、メニュー `Build Coin Fountain Effect (spec 009)`: PNG を作り、UI Canvas に層を作って参照を入れ、Game シーンに適用して保存する。
- `OperationBarStatus` に `MoneyRect`（`moneyText` の RectTransform）を足す（シリアライズの変更なし）。

## 4. 影響するファイル
Core: `SlotRules.cs`・`SlotEffects.cs`・`Battlefield.cs`・`GameDataValidator.cs`、`Data/slot_rules.csv`。Runtime: `BattleController.cs`・`OperationBarStatus.cs`・`SlotBoardEffect.cs`・新規 `CoinFountainEffect.cs`。Editor: 新規 Setup 2つ、`SlotBoardEffectSetup.cs`（コイン関連があれば）。Tests: `SlotTestRules.cs`・`SlotEffectsTests.cs`・PlayMode `SlotCabinetEffectTests.cs`。シーン `Game.unity`。

## 5. リスク
- 所持金の表示の弾みと、ほかの所から所持金の数字の大きさを変える処理がぶつかる → `moneyText` の大きさを触っている所がないことを確かめ、元の大きさを覚えて戻す。
- 倒した敵が多いと金貨が増えて重い → 同時の上限とプール。毎フレームの確保をしない。
- 噴水が UI の上に重なってボタンを押せなくなる → 層と Image の `raycastTarget` をオフ。
- Setup の再実行で Inspector の値は初期値に戻る（ほかの出目と同じ）。

## 6. 検証
AC-002 は EditMode（SlotEffectsTests にバーと同じ形で追加）。AC-003・AC-004 は PlayMode（`SlotCabinetEffectTests` に噴水の部品の後始末・同時の上限・きらめきの消え方を追加）。全テストとハーネス。AC-001 はユーザーの Play。

## 7. 変更1（2026-11-26、spec D-06〜D-08・FR-009）
- 噴水の行き先と弾み・きらめきはコインの倍率の表示。弾ませるのは倍率の文字（`moneyRateText`）にして、`SlotAlignEffect` が大きさを変える倍率の枠（`moneyRateGroup`）とぶつけない。`OperationBarStatus.MoneyRateTextRect`。
- Core: `Battlefield.EnemyDefeated` を `(BattleEnemy, int money, int coins)` にして、その敵が落とした所持金（倍率込み）とスロットコインを渡す。
- 倒したとき: 所持金 > 0 ならお札を `1 + round((倍率−1)×10)` 枚（所持金を枚数で分けて持たせる）、コイン > 0 なら金色のコインを枚数分。行き先はお札＝`OperationBarStatus.MoneyRect`、コイン＝`SlotCabinet.CreditRect`（台のコインの数）。同時の上限（48）はお札とコインの合計。
- 表示の遅れ（FR-009）: `OperationBarStatus.MoneyHeldBack`・`SlotCabinet.CreditHeldBack` を足し、表示は `max(0, 実際 − 持ち越し)`。飛ばすときに持たせた分を足し、着いたら引いて数字を小さく弾ませる。上限で飛ばさない分は足さない。`NodeBegan` と無効化のときに 0 に戻す。
- お札の絵は自作 PNG（`Art/VFX/Coin/Bill.png`、`MoneyIconArt` と同じ緑）。お札は縦に裏返るように、コインは横に回る。
