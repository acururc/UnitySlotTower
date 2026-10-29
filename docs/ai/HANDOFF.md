# Resume state

## 現在の作業

更新: 2026-10-29 Asia/Tokyo / 状態: M0 完了（25cac48）。M1 の T101〜T106 完了。次は T107

### M1 の進み（2026-10-29）
- T106: `SlotMachine` のコンストラクタに `SlotRules` を追加（`control` の次）。抽選は `State` を使う。ボーナス中に始めたゲームの払い出しを `BonusPayout` に累計し、`BigPayoutLimit`/`RegPayoutLimit` 以上で Normal・`ActiveBonus` null・累計0に戻す。上限をまたぐゲームは全額払い（plan 5章に追記）。テストは `BigThenBellMachine`（通常 BIG・ボーナス中ベルが確率1）と、上限20の CSV 文字列で境界を確認。
- 検証（T106）: red→green 2周（ボーナス表で抽選、240枚で終了）、ガード3件（29ゲームで継続、REG 12ゲーム、上限またぎ全額）は追加時点で green。EditMode 162/162、Console エラー0、ハーネス 8/8。Unity 接続は停止済み（8080 閉）。
- 次の一手: T107（BIG 後の確変: `kakuhen_entry_rate` で突入、`kakuhen_games` ゲーム、確変中 BIG でリセット）を `tdd` で。T106 の終了処理で BIG 終了時は Normal 固定なので、そこを確変判定に置き換える。
- T105: `SlotMachine` に `CarriedBonus`（揃わなかった BIG/REG を持ち越し）・`State`（ボーナス役が揃うと Bonus）・`ActiveBonus`。持ち越し中に引いたフラグは `SlotProbabilities.WithoutBonus` でボーナス役を外す（big+watermelon→watermelon、big→ハズレ）。`SlotReelControl` の評価は（ボーナス役の数, ほかの成立役の数）の期待値を辞書式で比べる（ボーナス優先、plan 5章に追記）。`SlotFlag.BigRole/RegRole/IsBonusRole` 追加。
- 検証（T105）: EditMode 157/157、Console エラー0、ハーネス 8/8。
- T104: `Core/Economy/CoinWallet.cs` と `Core/Slot/SlotMachine.cs`（`TryLever()` 掛け金→抽選→制御開始、`Stop` で3本目を止めると揃った役を1回ずつ払い出し、リプレイなら次は掛け金なし）。テストは `SlotMachineTests`（抽選表を「役1つが確率1」の CSV 文字列で渡し、`BonusPresses` で揃う/揃わない押し位置を別の制御で探す）。
- T103: `Core/Slot/SlotLines.cs`（5ライン判定）と `SlotReelControl.cs`（1フラグ＋持ち越しごとに全状態 22^3 を先読み評価してキャッシュ。非成立役は揃えず、リプレイ・ベルは必ず揃える）。
- 注意: `editor refresh` は成功しても CLI が JSON の前に文字を出すため、ラッパーが exit 1 を返す（中身は `success: true`。`try {} catch {}` で包む）。`poll-test --wait` は数秒で戻るので、同じ job_id でループして待つ（再実行しない）。今回は EditMode 全体が約25秒。
- T101: `Core/Slot/`（SlotSymbol・SlotReels・SlotPayouts・SlotRules）と `Tests/EditMode/Slot/SlotTableTests.cs`。`slot_reels.csv`・`slot_payouts.csv` に中身を入れた（`slot_premonition.csv` はまだ見出しのみ）。
- T102: `Core/Slot/SlotProbabilities.cs`（SlotState Normal/Kakuhen/Bonus、SlotFlag、`Draw(IRandom, state, setting)` は乱数1回）と `Tests/EditMode/Slot/SlotLotteryTests.cs`。重複当選は role 列で `big+center_cherry` のようにつなぐ（plan 5章に追記）。各行は排他、残りがハズレ（Flags[0]）。表検証は `TableValidator.ValidateListReference` で `+` を分けて参照確認。`slot_probabilities.csv` の値は仮（T109 で機械割に合わせて調整）。ボーナス中は big/reg なし。
- 検証（T102）: EditMode red 15件（新規14＋表検証）→ green 118/118、Console エラー0、ハーネス 8/8。
- ユーザー決定（2026-10-29）: レア役（スイカ・角チェリー・中段チェリー）は取りこぼしあり。リプレイ・ベル・ハズレは必ず一致。plan 5章「リール制御」「払い出し」に反映済み。
- リール配列の前提（T103 で守る）: 停止位置 s で上段 s-1・中段 s・下段 s+1。押した位置 p から p, p-1, …, p-4 に滑る（下向き回転）。この前提で、全押し順×全押し位置でリプレイ・ベル・ハズレが実現でき、BIG/REG は先読みあり制御で約16%/23%、スイカ/角チェ/中段チェは約47%/86%/71% の押し位置で揃う（使い捨て node スクリプトでの総当たり。プロジェクト内のテストではない）。
- 払い出しは1ゲーム1回。cherry と center_cherry は同じ図柄（cherry,any,any）で、左リールの段で区別する。初期値 replay0/bell8/watermelon6/cherry2/center_cherry4/big0/reg0（T109 で調整）。
- 検証（T101）: EditMode 104/104、Console エラー0、ハーネス 8/8。

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（2026-10-26 承認。変えたら再承認）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md)、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md)（M0 は `[x]` と各行末に結果）。
- 承認: 2026-10-29 にユーザーが「M0から実装を始めて」と依頼。M1 以降も実装依頼の範囲内と解釈してよいが、spec 変更は別途承認が要る。
- 完成物（`Assets/UnitySlotTower/`）: Core（`Data/` CSV パーサー・KeyValueTable・GameConfig・TableValidator・GameDataValidator、`Simulation/` SimulationClock・SeededRandom(SplitMix64)・IRandom、`Layout/PortraitLayout`、`Ui/NumericText`）、Runtime（`PresentationCatalog`、`PortraitScreenLayout`）、Editor（`Setup/GameSceneSetup`・`NumericFontSetup` の一回限りメニュー、`Ui/TextAudit`）、`Data/Tables/` CSV 雛形17本（game_config・slot_rules は plan の値、他は見出しのみ、maps/w1〜w5 は同じ仮グリッド）、`Scenes/Game.unity`、`Art/Fonts/NumericOnly SDF.asset`。ほかに `Assets/TextMesh Pro/`（TMP Essentials）、`InputSystem_Actions` に SlotTower マップ、プレイヤー設定 720×1280 可変ウィンドウ、ビルド設定は Game のみ有効（SampleScene は無効化して残置）。
- 補助スクリプト: `Test-UnityPlayer.ps1` に `-Width/-Height`（既定 1280×720）、`Test-ProjectHarness.ps1` の空白検査から `Assets/TextMesh Pro/` を除外（ベンダーのシェーダーに行末空白）。
- URP 資産・GraphicsSettings の差分はビルド時に Unity が自動で書いたもの（意図した変更ではない）。
- 検証（実行済み）: EditMode 95/95 green（各 Core 機能は red 確認後に green）、Console エラー0、Windows 開発ビルド成功、プレイヤー画像 720×1080/1920×1080/2560×1080 で 9:16 中央・黒帯・上38%戦闘を目視確認、ハーネス 8/8。未実行: PlayMode テスト（まだ0件）、真の 1440p 表示（モニターが 1080p。計算は単体テストで確認）。
- 留意点: Shift+数字は SelectUnit と Upgrade の両方に反応しうるので、Runtime 側で Shift 押下中は SelectUnit を無視する。Esc は Cancel と Pause の両方で、配置操作中かどうかで振り分ける（plan 3章）。マップのマス記号は G/H/X/E/S1〜S4、経路は上下左右の連続のみ検証（範囲・通行可否の検証は T201）。
- Unity 接続: 停止済み（8080 閉）。Editor は開いたまま。
- テスト手順:  refresh → Console 確認 → `editor tests --async` を1回だけ → `poll-test` の順で、テスト開始を二重に送らない。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
