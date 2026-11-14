# Resume state

## 現在の作業

更新: 2026-11-15 Asia/Tokyo / 状態: 2026-11-14 の大きな仕様変更（元: 直下 `change.txt`）を spec に反映し、2026-11-15 にユーザーが承認。次は plan.md と tasks.md の作り直し（まだ未着手、コードも未変更）。

- 対象: [`spec.md`](../../specs/001-slot-tower-defense/spec.md)（D-59〜D-74 と【2026-11-14 変更/廃止】の付いた項目が今回の変更）、[`plan.md`](../../specs/001-slot-tower-defense/plan.md)、[`tasks.md`](../../specs/001-slot-tower-defense/tasks.md)（どちらも旧仕様のまま）。
- 変更の要点（詳細は spec）: スロットを作り直す（レバーはクリックか Space、コイン3枚賭け、自動停止、図柄6種〔赤7・青7・緑7・黄7・バー・コイン〕、リール1本6マス、中段1本、難易度ごとの20パターン抽選表）。色7でその色の職のスキルゲージ（5段、満タンでクリック発動）、バー＝戦闘中ずっと攻撃力UP、コイン＝所持金の倍率UP。お金はコインと所持金の2種類。3ステージ制をやめて毎回ランダムな分岐ルート（戦闘・強敵・両替所・ショップ・回復マス・ボス）。ライフは最後まで引き継ぎ。ヒーラー→バッファー。マップは横9〜10×縦4〜5、最初の戦闘は3列一直線。文字あり版をタイトルの隠しボタンで切り替え（起動時は文字なし）。BIG/REG・リールカスタム・目押し・序盤の救済・5本ライン・残りライフのボーナスは廃止。
- 承認: 2026-11-15 ユーザーが条件付きで明示承認（Space キーと3枚賭けの2点を反映済み、spec の承認欄に sha256 9982cd2d8c00）。範囲は spec 全体（「Claude 提案」部分を含む）。plan/tasks/実装はこの承認に基づいて進めてよい。spec を変えたら再承認。
- ブランチ: `feature/001-t402-result`（main に直接コミットしない）。直下の未追跡ファイル（`change.txt`・デザイナー制作依頼書 pptx・メモ txt）はユーザーのもの。触らない。
- 次の一手: 既存コード（Core/Slot・Runtime/Slot・リールカスタム画面・ヒーラー・ステージ進行・CoinWallet・説明 GuideOverlay）を調べて、消すもの・作り直すもの・残すものを plan.md に新しい章として書き、tasks.md に検証できる小タスクで新しいマイルストーンを足す。旧 M5（T501 素材・T503 CSV 確認）と T309 は新仕様に合わせて見直す。
- 検証: 今回は spec の文章変更だけ（ハーネス・テストは実行していない。旧仕様での最後の結果は 2026-11-07: EditMode 410/410、PlayMode 33/33、ハーネス 8/8）。
- テストの要点（旧実装の知見。作り直し後も使える部分）: Game シーンの PlayMode テストは結果を固定するならシーン読み込み前に `SlotCabinet.RandomOverride`（TearDown で null）。説明を出すなら `GuideOverlay.DisabledForTests = false`（TearDown で true）。実時間で待つ（フレーム数で待たない）。Play 中の撮影は code execute で `Application.runInBackground = true`。
- 既知の問題: プレイヤービルドで戦闘マスがマゼンタ（シェーダー欠落、未調査）。2026-11-04 に Editor が d3d12 Device removed で落ちた（繰り返すならドライバー更新か `-force-d3d11`）。

### Unity 自動化の注意
- Unity 接続: 停止済み（8080 閉、2026-11-07 M3T 後。2026-11-15 の spec 変更では Unity を使っていない）。Editor の camera screenshot は半透明の UI（暗幕など）が薄く写る。濃さは画像で判断しない。テスト実行スクリプトは refresh 後の Console に `error CS` があれば止める（古いアセンブリのままテストが流れて全件成功に見えるため）。Editor は開いたまま。テスト実行は refresh → 25秒待つ → Console → tests --async → poll-test のループを1スクリプトにまとめる（結果は `progress.failures_so_far` と `result.summary`）。`editor refresh` は成功しても exit 1 を返す。
- テスト: refresh → 約20秒待つ → Console → `editor tests --mode EditMode --async` を1回だけ → 同じ job_id で `poll-test` をループ。1スクリプトにまとめ、開始出力から job_id を取りこぼさない（二重送信の前例あり）。EditMode 全体は約75秒。
- PlayMode テスト: 実機デバイスはゲームパッドも含めて SetUp で全部止める（DualShock4 が接続されていることがある）。仮想デバイスは `InputSystem.AddDevice` で足し、`editorInputBehaviorInPlayMode`・`backgroundBehavior` を一時的に変えて非フォーカスでも入力を通す（TearDown で戻す）。シーンは `LoadSceneAsync` の完了を待ってから探す（`LoadScene` 直後だと前のテストの古いオブジェクトを拾う）。実機のマウス・キーボードは SetUp で無効化し TearDown で戻す（実機のポインター位置の変化が向きに反映されてしまうため）。
- CLI はテスト失敗・poll 中・`editor refresh` 成功時でも exit 1 を返すことがあるので `try {} catch {}` で包み、中身の JSON で判断する。
- `editor console` が0件しか返さない不調があった。ログ確認はファイル出力も併用する。`--stacktrace` を付けてもスタックが返らないことがある（`Logs/` はこのセッションの権限で読めなかった）。
- Editor 非フォーカスだと Play のフレームが進まない。自動で動かすときは code execute で `Application.runInBackground = true`（Play 中だけ）。`code execute` では `UnityEngine.Object` と書く。
- Editor を開いたままシーンファイルを外から書き換えると、再読み込みの確認が出たり固まったりすることがある（2026-10-30 に一度フリーズ、原因は未確定）。シーンの変更はなるべく MCP で行う。
- 留意点（M0 由来）: Shift+数字は SelectUnit と Upgrade の両方に反応しうるので Shift 押下中は SelectUnit を無視。Esc は Cancel と Pause を配置操作中かで振り分ける（plan 3章）。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
