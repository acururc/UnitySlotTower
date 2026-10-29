# Resume state

## 現在の作業

更新: 2026-10-30 Asia/Tokyo / 状態: M0 完了（25cac48）、M1（T101〜T110）完了、M2 の T201〜T205 完了。次は T206

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（2026-10-26 承認。変えたら再承認）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md)、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md)（完了タスクは `[x]` と行末に結果）。
- 承認: 2026-10-29 にユーザーが「M0から実装を始めて」と依頼。M2 以降も実装依頼の範囲内と解釈してよいが、spec 変更は別途承認が要る（T108 で先バレ対象を BIG・REG に変えたときは承認済み）。
- ブランチ: `feature/001-m0-foundation`（main に直接コミットしない）。
- 次の一手: T206（味方の攻撃: 範囲内でゴールに最も近い敵、剣士・タンクはブロック中の敵を優先、弓だけ空中可、ダメージ式。ヒーラー・バッファー、HP0 での消滅。EditMode テスト、AC-022 の一部。plan 5章）。`Battlefield` に足す。味方が消えるときは、その味方がブロックしていた敵の `BlockedBy` を null に戻す（`BlockUsed` も）。敵の HP0 での消滅もここで扱う（ドロップは T207）。攻撃範囲は `UnitRanges.CellsFrom`。Core 側は `tdd` スキルで red→green。
- T201 の成果: `Core/Battle/BattleMap`（GridPoint は x=列・y=CSV の上の行。`MapTile`・`SpawnNumberAt`・`Paths`）、`Runtime/Battle/BattleFieldView`（グリッド中心が原点、上の行ほど +z、`CellToWorld`。マスの箱はコライダーあり＝T208 の配置で使える）、Editor `Setup/BattleFieldSetup`。経路 ID は全マップで一意にする（例 `w1_g1`）。
- T202 の成果: `Core/Battle/UnitTable`（`UnitDefinition`・`UnitTile`）、`Core/Battle/UnitSummoner`（`TrySummon`→`SummonResult`、`CooldownRemaining`、`Advance(steps)`、`DeployedCount`）。撤退・死亡時に配置数を減らす API はまだない（T302 で追加）。`units.csv` の HP・攻撃などは仮値。EditMode 235/235、ハーネス 8/8。
- T203 の成果: `Core/Battle/UnitPlacement.CanPlace`（マス種別だけ判定。占有は T208）、`Core/Battle/UnitRanges`（`Facing`、`CellsFrom(unitId, origin, facing)`。dx=前方・dy=右手側、時計回り回転、マップ外も返す）。`unit_ranges.csv` は仮値（M3 で調整）。EditMode 247/247（Console は 0 件取得＝既知の不調で未確認）、ハーネス 8/8。
- T204 の成果: `Core/Battle/EnemyTable`・`WaveSchedule`（`Load(table, wave, map, enemies)`）・`Battlefield`（`AddUnit`・`Advance(steps)`・`Enemies`・`Units`・`ArrivedCount`）・`BattleEnemy`（`X`/`Y` はマス単位、`Distance`）・`BattleUnit`・`Damage`。テスト用の小さな地図と敵は `Tests/EditMode/Battle/BattleFixtures`。EditMode 279/279、ハーネス 8/8。
- T205 の成果: `Battlefield` にブロック（マスの手前の境界で停止、出現順に優先、空きが消費以上のときだけ、空中は不可）と近距離敵のブロック相手への攻撃。`BattleEnemy.BlockedBy`・`BattleUnit.BlockUsed`。フィクスチャに `sword`（ブロック1）・`heavy`（消費2）を追加。EditMode 288/288、Console エラー0、ハーネス 8/8。

### M1 の成果（詳細は tasks.md 各行と plan 5章）
- Core/Slot: 表（SlotReels・SlotPayouts・SlotRules・SlotProbabilities・SlotPremonition）、SlotLines、SlotReelControl（フラグ＋持ち越しごとに 23^3 状態をメモ化評価）、SlotMachine（持ち越し・ボーナス上限・確変・先バレ）、SlotTheory、SlotWait。Core/Economy/CoinWallet。
- Runtime/Slot: SlotCabinet（CSV を TextAsset で読む。`setting` 既定1）、SlotReelView（`symbolsPerSecond` 16。ユーザー確認で 28 から変更）。Editor: `Setup/SlotCabinetSetup`、`Slot/SlotTheoryReport`。
- 理論機械割（適当打ち、ユーザー決定）: 100.04〜125.09%。ルール値は `slot_rules.csv`（BIG 240・REG 96・確変100G・突入率1・ウェイト2秒・最大4コマ滑り）。
- 最終検証（2026-10-30）: EditMode 207/207、Console エラー0、ハーネス 8/8、ユーザーの手動目押しと先バレの光・音を確認。PlayMode テストはまだ0件。

### Unity 自動化の注意
- Unity 接続: 停止済み（8080 閉）。Editor は開いたまま。
- テスト: refresh → 約20秒待つ → Console → `editor tests --mode EditMode --async` を1回だけ → 同じ job_id で `poll-test` をループ。1スクリプトにまとめ、開始出力から job_id を取りこぼさない（二重送信の前例あり）。EditMode 全体は約75秒。
- CLI はテスト失敗・poll 中・`editor refresh` 成功時でも exit 1 を返すことがあるので `try {} catch {}` で包み、中身の JSON で判断する。
- `editor console` が0件しか返さない不調があった。ログ確認はファイル出力も併用する。
- Editor 非フォーカスだと Play のフレームが進まない。自動で動かすときは code execute で `Application.runInBackground = true`（Play 中だけ）。`code execute` では `UnityEngine.Object` と書く。
- Editor を開いたままシーンファイルを外から書き換えると、再読み込みの確認が出たり固まったりすることがある（2026-10-30 に一度フリーズ、原因は未確定）。シーンの変更はなるべく MCP で行う。
- 留意点（M0 由来）: Shift+数字は SelectUnit と Upgrade の両方に反応しうるので Shift 押下中は SelectUnit を無視。Esc は Cancel と Pause を配置操作中かで振り分ける（plan 3章）。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
