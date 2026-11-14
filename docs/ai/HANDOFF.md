# Resume state

## 現在の作業

更新: 2026-11-15 Asia/Tokyo / 状態: M3U の T343（ヒーラー → バッファー）を完了・コミット済み。次は T344（スキルの Core）。

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（D-59〜D-74 と【2026-11-14 変更/廃止】の付いた項目）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md) 16章（2026-11-14 変更の正本。2〜8章の旧記述より優先、書き直しは T354）、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md) の M3U。T309 は取りやめ（T355 に置き換え）。
- 変更の要点（詳細は spec）: スロットを作り直す（レバーはクリックか Space、コイン3枚賭け、自動停止、図柄6種〔赤7・青7・緑7・黄7・バー・コイン〕、リール1本6マス、中段1本、難易度ごとの20パターン抽選表）。色7でその色の職のスキルゲージ（5段、満タンでクリック発動）、バー＝戦闘中ずっと攻撃力UP、コイン＝所持金の倍率UP。お金はコインと所持金の2種類。3ステージ制をやめて毎回ランダムな分岐ルート（戦闘・強敵・両替所・ショップ・回復マス・ボス）。ライフは最後まで引き継ぎ。ヒーラー→バッファー。マップは横9〜10×縦4〜5、最初の戦闘は3列一直線。文字あり版をタイトルの隠しボタンで切り替え（起動時は文字なし）。BIG/REG・リールカスタム・目押し・序盤の救済・5本ライン・残りライフのボーナスは廃止。
- 承認: 2026-11-15 ユーザーが条件付きで明示承認（Space キーと3枚賭けの2点を反映済み、spec の承認欄に sha256 9982cd2d8c00）。範囲は spec 全体（「Claude 提案」部分を含む）。plan/tasks/実装はこの承認に基づいて進めてよい。spec を変えたら再承認。
- ブランチ: `feature/001-t402-result`（main に直接コミットしない）。直下の未追跡ファイル（`change.txt`・デザイナー制作依頼書 pptx・メモ txt）はユーザーのもの。触らない。
- T340・T341 の要点（詳細は tasks.md の結果欄）: `Core/Run/RunState`（コイン・所持金・ライフ等）。スロットは `SlotPatterns`（設定ごと20、ハズレが先頭）・`SlotReels`（3本×6、`Problems()`）・`SlotMachine`（`TryLever` → `LeverResult`、回数固定、`Settle` → `Aligned`）。掛け金は `game_config` の `bet`。CSV は旧ファイルを .meta ごと改名して GUID を保持。Runtime のつなぎ: `SlotCabinet` はレバー→自動停止→`Settle`、カスタム画面は削除済み（`IsCustomizing` は常に false）、`WaveRun.ApplySlot` はバー＝旧の攻撃力アップ、早見表は割合だけ。Game シーンは未変更で、リールの絵の子が旧図柄の順のまま（T348 で組み立てメニューを流し直すまで絵が合わない）、カスタム画面はスクリプト欠落のオブジェクトとして残る。
- T342・T343 の要点（詳細は tasks.md の結果欄）: `RunState(config, upgradeTable, slotRules, setting)` が `Gauges`（`SkillGauges`）と `SlotRules` を持つ。`Battlefield.Boosts`（`BattleBoosts`、null なら倍率なし）、`WaveRun.ApplySlot(symbol)`。攻撃力は `Battlefield.EffectiveAtk` = 強化後 × バッファー（`BuffRateOf`）× バー を最後に四捨五入。スキル（T344）はバーの後ろに掛ける。ヒーラー・回復・`heal_rate` は削除済み、`units.csv` は `buffer`（役割 `buff`・`buff_rate` 1.3）。説明画面はバッファーに旧ヒーラーの欄の絵を仮に出す（T351）。操作バーは攻撃力の欄にバーの %（T349 まで）。
- 次の一手: T344（`tdd`。plan 16.4）: `skills.csv`（`color, unit_id, duration, block_add, damage_taken_rate, damage_rate, interval_rate, buff_add`、仮: 赤=剣士・青=弓・緑=バッファー・黄=タンク）、`Battlefield.ActivateSkill(skill)`（発動時に配置中のその職全員へ効果時間、戦闘ステップで減る）、発動の可否（満タン・配置1体以上・一時停止中は不可）でゲージを0に。拒否なら何も変えない、発動後に置いた個体には付けない（AC-023 の Core 部分）。Core（T341〜T347）は EditMode 全件 green が完了条件。plan 16章の仮値（抽選表・リールの並び・両替レート 2・回数固定 first=6・ドロップ量・バー/コインの刻み・バッファー倍率 1.3）は Claude の仮置きで、ユーザーがまだ見ていない。
- 検証（T343 後、2026-11-15）: EditMode 410/410、PlayMode 30/30、Console エラー0、ハーネス 7/8（失敗はユーザーの未追跡 `change.txt` の行末空白だけ。触らない）。テスト実行スクリプトは毎回 scratchpad に作り直す（前セッションの `run-tests.ps1` を写すと早い。refresh→待つ→`error CS` 確認→tests --async→poll-test）。`code execute` は Runtime の型を名前で参照できない（codedom）ので `GetType().FullName` で探す。名前空間は `UnitySlotTower.Battle` 等（`.Runtime.` は付かない）。アクティブシーンが SampleScene のことがある。
- テスト実行: refresh → 25秒待つ → Console の `error CS` 確認 → `editor tests --async` を1回 → 同じ job_id で `poll-test --failed-only` をループ、を1スクリプトにまとめる。CLI の出力は先頭に `[Unity MCP] ...` の行が付くので、最初の `{` から JSON として読む。結果は `result.data.status`・`result.data.progress.failures_so_far`（失敗時は `result.data.result` が null）と、成功時の `result.data.result.summary`。PlayMode でリールの停止を待つときは実時間で待つ（自動停止は約1.8秒）。
- テストの要点（旧実装の知見。作り直し後も使える部分）: Game シーンの PlayMode テストは結果を固定するならシーン読み込み前に `SlotCabinet.RandomOverride`（TearDown で null）。説明を出すなら `GuideOverlay.DisabledForTests = false`（TearDown で true）。実時間で待つ（フレーム数で待たない）。Play 中の撮影は code execute で `Application.runInBackground = true`。
- 既知の問題: プレイヤービルドで戦闘マスがマゼンタ（シェーダー欠落、未調査）。2026-11-04 に Editor が d3d12 Device removed で落ちた（繰り返すならドライバー更新か `-force-d3d11`）。

### Unity 自動化の注意
- Unity 接続: 停止済み（8080 閉を確認、2026-11-15 T343 後。Editor は開いたまま、Game シーンを開いている）。Editor の camera screenshot は半透明の UI（暗幕など）が薄く写る。濃さは画像で判断しない。テスト実行スクリプトは refresh 後の Console に `error CS` があれば止める（古いアセンブリのままテストが流れて全件成功に見えるため）。Editor は開いたまま。テスト実行は refresh → 25秒待つ → Console → tests --async → poll-test のループを1スクリプトにまとめる（結果は `progress.failures_so_far` と `result.summary`）。`editor refresh` は成功しても exit 1 を返す。
- テスト: refresh → 約20秒待つ → Console → `editor tests --mode EditMode --async` を1回だけ → 同じ job_id で `poll-test` をループ。1スクリプトにまとめ、開始出力から job_id を取りこぼさない（二重送信の前例あり）。EditMode 全体は約75秒。
- PlayMode テスト: 実機デバイスはゲームパッドも含めて SetUp で全部止める（DualShock4 が接続されていることがある）。仮想デバイスは `InputSystem.AddDevice` で足し、`editorInputBehaviorInPlayMode`・`backgroundBehavior` を一時的に変えて非フォーカスでも入力を通す（TearDown で戻す）。シーンは `LoadSceneAsync` の完了を待ってから探す（`LoadScene` 直後だと前のテストの古いオブジェクトを拾う）。実機のマウス・キーボードは SetUp で無効化し TearDown で戻す（実機のポインター位置の変化が向きに反映されてしまうため）。
- CLI はテスト失敗・poll 中・`editor refresh` 成功時でも exit 1 を返すことがあるので `try {} catch {}` で包み、中身の JSON で判断する。
- `editor console` が0件しか返さない不調があった。ログ確認はファイル出力も併用する。`--stacktrace` を付けてもスタックが返らないことがある（`Logs/` はこのセッションの権限で読めなかった）。
- Editor 非フォーカスだと Play のフレームが進まない。自動で動かすときは code execute で `Application.runInBackground = true`（Play 中だけ）。`code execute` では `UnityEngine.Object` と書く。
- Editor を開いたままシーンファイルを外から書き換えると、再読み込みの確認が出たり固まったりすることがある（2026-10-30 に一度フリーズ、原因は未確定）。シーンの変更はなるべく MCP で行う。
- 留意点（M0 由来）: Shift+数字は SelectUnit と Upgrade の両方に反応しうるので Shift 押下中は SelectUnit を無視。Esc は Cancel と Pause を配置操作中かで振り分ける（plan 3章）。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
