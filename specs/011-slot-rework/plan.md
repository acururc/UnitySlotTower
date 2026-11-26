# plan: スロットの作り直し（spec 011）

要求は [`spec.md`](spec.md) の FR/AC を参照する。

## 方針

### Core: 抽選（FR-003, FR-010, FR-011）
- `SlotMachine` に賭けを持たせる: `SelectedBet`（1〜3、`CycleBet()`、回転中は変えない）、`EffectiveBet = clamp(min(SelectedBet, coins), 1, 3)`、レバーで払った枚数 `SpinBet`。
- 抽選は有効ライン（中段→上段→下段の順で賭け枚数ぶん）ごとに `line_hit_rate` で揃うかを決め、揃うなら `canAppear` を満たし同じ回で未使用の図柄から `slot_patterns.csv` の個数を重みに選ぶ（miss の行は 0 にする）。結果は `Hits`（行と図柄の列、中→上→下の順）。
- 3×3 の見える図柄 `Visible(reel,row)` は Core が直接決める: 揃う行は3本とも同じ図柄、揃わない行は3本がそろわない、各リールの縦3マスは別の図柄。固定のリール帯では二重・三重の組み合わせを作れないため（今の3本の並びは隣り合う2図柄の組が共通しない）、帯は回転中の見た目だけに使う。
- 決め打ち（FR-032）: `TryLever(filter, forced)` に揃える行と図柄を渡せる。
- `Settle` で `Hits` を1つずつ通知する（`Aligned(SlotSymbol)` は残し、行と賭けは `SlotLineHit` で渡す新イベントを足す）。

### Runtime: リールの表示（FR-002, FR-011, FR-012）
- `SlotReelView.StopAt(int[] window)`: 押し位置から4コマ以上先に目標を取り、目標の上・中・下のマスだけ指定の図柄で上書きして止める（位置は循環させず、上書きのキーがずれないようにする）。
- `SlotCabinet`: BET ボタン（コインの絵の数で枚数、押すと 1→2→3→1）、有効ラインの光（窓の上・中・下の帯）、揃いの通知を `line_gap_seconds`（CSV）ずつずらす。通知が終わるまで `IsSpinning`。通知中の行は `LastAlignedRow`、賭けは `LastAlignedPower`、何本目かは `LastAlignedIndex`。
- `SlotAlignEffect` は揃った行のマス（`ReelCell(reel,row)`）を光らせ、2本目・3本目は光を大きくする（仮の二重・三重の演出）。
- Editor の `SlotCabinetSetup` に BET ボタンとライン表示を足し、Setup を実行する。

### Core: 効果の強さ（FR-020, FR-021）
- `Battlefield.ApplySlot(symbol, power)`、`BattleNodeRun.ApplySlot(symbol, power)`。
- `slot_rules.csv`: `red_damage`→`red_hp_rate`(0.2)、`yellow_poison_damage`→`yellow_hp_rate`(0.04)、`boss_damage_rate`(0.5)、`bar_attack_rate`→`bar_attack_step`(1、倍率 1＋step×枚数)、`line_hit_rate`(0.6)、`line_gap_seconds`(0.4)、`showcase_coins`(3)。
- `BattleEnemy.IsBoss`（出現の is_boss から）。赤7・毒のダメージ = max(1, round(最大HP×率×枚数×(ボスなら boss_damage_rate)))。
- `SlotEffects`: 停止の長さ×枚数（表示の分母は今回の長さ）、毒の率×枚数、攻撃倍率、回復率×枚数、コインは +枚数。待ちの赤7・緑7は枚数も持つ。

### 見せ場（FR-030〜FR-033）
- `stage_waves.csv` に `slot_showcase` 列（0/1）。`WaveDefinition.SlotShowcase`。
- `Battlefield`: 見せ場のウェーブを出し終わったら `IsShowcaseHolding`（Advance で戦闘を進めない）。`ReleaseShowcase()` で解除。1戦闘に1回。
- `BattleController`: 止まったらコインを `showcase_coins` まで補い、賭けを3枚にし、レバーを点滅＋矢印（仮置き）。レバーは決め打ちの三重当たり、回し始めで解除して効果が入る。やり直しは戦闘を作り直すので再発生する。
- `waves.csv` にステージ1のウェーブ5（近接10＋遠距離4、仮）。

### 早見表（FR-022）
- `HelpScreen` の確率を「1ライン 60% × 重み」に、効果の値を1枚の値に。賭けの説明（コイン1〜3枚の絵とライン）を足す。

## 影響ファイル
Core: `Slot/SlotMachine.cs`・`SlotRules.cs`・`SlotPatterns.cs`、`Battle/SlotEffects.cs`・`Battlefield.cs`・`BattleEnemy.cs`・`BattleNodeRun.cs`・`WaveSchedule.cs`・`WaveProgress.cs`、`Data/GameDataValidator.cs`。
Runtime: `Slot/SlotCabinet.cs`・`SlotReelView.cs`、`Battle/BattleController.cs`・`SlotAlignEffect.cs`、`Flow/HelpScreen.cs`。Editor: `Setup/SlotCabinetSetup.cs`（ほか早見表の Setup）。
データ: `slot_rules.csv`・`slot_patterns.csv`・`stage_waves.csv`・`waves.csv`。テスト: EditMode `Slot/`・`Battle/SlotEffectsTests.cs`・`Data/`、PlayMode `Battle/SlotCabinetEffectTests.cs`。

## リスク
- 効果の演出（003〜009）は `Aligned(SlotSymbol)` を聞いている。通知を残して、ずらして出すことで壊さない。
- PlayMode テストは今の「毎回当たり」を前提にしているものがある。固定乱数と決め打ちで書き直す。
- Setup をやり直すと Inspector の調整値が戻る（HANDOFF の注意）。

## 検証
spec の AC-001〜AC-006。EditMode → PlayMode → ハーネス → ユーザーの Editor 確認の順。
