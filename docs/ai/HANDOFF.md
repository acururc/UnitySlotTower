# Resume state

## 現在の作業

更新: 2026-11-07 Asia/Tokyo / 状態: M0〜M2・M3（T309 だけ後回し）・M3R・M3S・M4 完了。2026-11-06 の仕様変更（spec D-55〜D-58: キーボードは要件外、説明の撤退の題目中は全額返還、序盤 N 通常ゲームのハズレ率を一律に下げる〔表示しない〕、有効ラインに斜め2本を足して5本）を spec に反映し、2026-11-06 にユーザーが承認。M3T（T330〜T332、plan 15章）を実装済み。次は M5。

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（2026-11-06 承認。変えたら再承認）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md)、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md)（完了タスクは `[x]` と行末に結果・API）。
- 承認: 2026-10-29 にユーザーが実装開始を依頼。spec 変更は毎回別に承認が要る。T403 は 2026-11-06 にユーザーが手動確認（第三者確認はユーザー判断で省略）、T405 はマウスでのプレイ確認で完了。
- ブランチ: `feature/001-t402-result`（main に直接コミットしない）。直下の未追跡ファイル（デザイナー制作依頼書 pptx・メモ txt）はユーザーのもの。触らない。
- M3T の検証（2026-11-07）: EditMode 410/410、PlayMode 33/33、ハーネス 8/8。未確認: 斜めで揃ったときの見た目、説明の撤退の題目を実際に出してからの全額返還（PlayMode はフラグを立てて確認しただけ）、序盤の当たりやすさの体感。手動プレイでユーザーに見てもらう。斜めの有効ラインの印は付けていない（必要なら T502 で描く）。序盤の値は `slot_rules.csv` の `early_games`=20・`early_miss_reduction`=0.2。
- 次の一手: M5（tasks.md の M5 節）。T501（素材の選定とライセンス記録）は素材の好み・出どころをユーザーと決めてから（デザイナーチームに任せるかもしれない）。素材なしで進められるのは T503（CSV だけで挙動が変わる確認）。T309（バランスの通しプレイ）はユーザーが後回しにしている。
- テストの要点: Game シーンの PlayMode テストは SetUp で Run ができた後に `ReelCustomTestUtil.CloseWithDefault(_battle)`。結果を固定するにはシーン読み込み前に `SlotCabinet.RandomOverride`（TearDown で null）。説明を出したいときは `GuideOverlay.DisabledForTests = false`（TearDown で true）。ステージを早く進めるには `sceneLoaded` で `BattleController.wavesTable` を短い表に差し替える。スロットのウェイト（2秒）や tick の進みは、フレーム数ではなく実時間で待つ（フレームが速いと足りない）。ロック配置を変えたら `SlotTableTests` の全埋め方検査を通す。画面の作り直しは Editor メニュー（Build Reel Custom Screen (T322)・Build Help Screen (T404)・Apply 6:4 Layout (T317)）。Play 中の撮影は code execute で `Application.runInBackground = true`（撮ったら戻す）。
- 既知の問題: プレイヤービルドで戦闘マスがマゼンタ（シェーダー欠落、未調査）。2026-11-04 に Editor が d3d12 Device removed で落ちた（コードの問題ではない。繰り返すならドライバー更新か `-force-d3d11`）。
- 決めたこと（plan に記録済み）: ▶ は配置操作中でないときの Enter と操作バーの ▶、準備中は `autoStart`=false で ▶ を待つ。難易度の初期選択は常に設定1。説明は題目ごとに1プレイ1回。

### M1 の成果（旧スロット。T313 で Core と CSV を置き換え済み、以下は経緯のみ）
- Core/Slot: 表（SlotReels・SlotPayouts・SlotRules・SlotProbabilities・SlotPremonition）、SlotLines、SlotReelControl（フラグ＋持ち越しごとに 23^3 状態をメモ化評価）、SlotMachine（持ち越し・ボーナス上限・確変・先バレ）、SlotTheory、SlotWait。Core/Economy/CoinWallet。
- Runtime/Slot: SlotCabinet（CSV を TextAsset で読む。`setting` 既定1）、SlotReelView（`symbolsPerSecond` 16。ユーザー確認で 28 から変更）。Editor: `Setup/SlotCabinetSetup`、`Slot/SlotTheoryReport`。
- 理論機械割（適当打ち、ユーザー決定）: 100.04〜125.09%。ルール値は `slot_rules.csv`（BIG 240・REG 96・確変100G・突入率1・ウェイト2秒・最大4コマ滑り）。
- 最終検証（2026-10-30）: EditMode 207/207、Console エラー0、ハーネス 8/8、ユーザーの手動目押しと先バレの光・音を確認。PlayMode テストはまだ0件。

### Unity 自動化の注意
- Unity 接続: 停止済み（8080 閉、2026-11-07 M3T 後）。Editor の camera screenshot は半透明の UI（暗幕など）が薄く写る。濃さは画像で判断しない。テスト実行スクリプトは refresh 後の Console に `error CS` があれば止める（古いアセンブリのままテストが流れて全件成功に見えるため）。Editor は開いたまま。テスト実行は refresh → 25秒待つ → Console → tests --async → poll-test のループを1スクリプトにまとめる（結果は `progress.failures_so_far` と `result.summary`）。`editor refresh` は成功しても exit 1 を返す。
- テスト: refresh → 約20秒待つ → Console → `editor tests --mode EditMode --async` を1回だけ → 同じ job_id で `poll-test` をループ。1スクリプトにまとめ、開始出力から job_id を取りこぼさない（二重送信の前例あり）。EditMode 全体は約75秒。
- PlayMode テスト: 実機デバイスはゲームパッドも含めて SetUp で全部止める（DualShock4 が接続されていることがある）。仮想デバイスは `InputSystem.AddDevice` で足し、`editorInputBehaviorInPlayMode`・`backgroundBehavior` を一時的に変えて非フォーカスでも入力を通す（TearDown で戻す）。シーンは `LoadSceneAsync` の完了を待ってから探す（`LoadScene` 直後だと前のテストの古いオブジェクトを拾う）。実機のマウス・キーボードは SetUp で無効化し TearDown で戻す（実機のポインター位置の変化が向きに反映されてしまうため）。
- CLI はテスト失敗・poll 中・`editor refresh` 成功時でも exit 1 を返すことがあるので `try {} catch {}` で包み、中身の JSON で判断する。
- `editor console` が0件しか返さない不調があった。ログ確認はファイル出力も併用する。`--stacktrace` を付けてもスタックが返らないことがある（`Logs/` はこのセッションの権限で読めなかった）。
- Editor 非フォーカスだと Play のフレームが進まない。自動で動かすときは code execute で `Application.runInBackground = true`（Play 中だけ）。`code execute` では `UnityEngine.Object` と書く。
- Editor を開いたままシーンファイルを外から書き換えると、再読み込みの確認が出たり固まったりすることがある（2026-10-30 に一度フリーズ、原因は未確定）。シーンの変更はなるべく MCP で行う。
- 留意点（M0 由来）: Shift+数字は SelectUnit と Upgrade の両方に反応しうるので Shift 押下中は SelectUnit を無視。Esc は Cancel と Pause を配置操作中かで振り分ける（plan 3章）。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
