# Resume state

## 現在の作業

更新: 2026-11-23 Asia/Tokyo / 状態: **spec 003（赤7の息吹の演出の試作）の T701〜T704 が完了。T705（ユーザーが Editor で Play して見た目を確認・調整）待ち。**

- 対象: [`spec.md`](../../specs/003-red7-breath/spec.md)（2026-11-23 承認: FR-001〜FR-005・AC-001〜AC-006）・[`plan.md`](../../specs/003-red7-breath/plan.md)・[`tasks.md`](../../specs/003-red7-breath/tasks.md)。spec 002 は完了（T601〜T617）。
- ユーザーの決定: VFX に力を入れる。ドラゴン本体なし・息吹だけ。ダメージは爆発の瞬間（`slot_rules.csv` の `red_delay_seconds`＝1）。爆発で戦闘だけ約0.1秒止める（`BattleController.hitStopSeconds`）。素材は AI が取るログイン不要の無料素材だけ（有料は使わない）。赤7がうまくいけば、残り5種類に広げるかを別に決める。
- 作ったもの: `Runtime/Battle/Red7BreathEffect.cs`（予兆→息吹→`RedStruck` で爆発・戦闘カメラの揺れ）、`Editor/Setup/Red7BreathEffectSetup.cs`（メニュー `Tools/UnitySlotTower/Setup/Build Red7 Breath Effect (spec 003)` が Game シーンに「Red7 Breath Effect」とパーティクル8つ、`Art/VFX/` に材質5つを作る）、`Art/VFX/VfxParticle.shader`、`Art/VFX/Kenney/`（CC0、`docs/ASSET_LICENSES.md`）。`SlotBoardEffect` は赤7で何もしない。パーティクルの値は Inspector で調整できる（Setup を再実行すると初期値に戻る）。
- 検証（2026-11-23）: EditMode 549/549、PlayMode 68 成功/失敗0/Explicit 1、ハーネス 8/8。見た目（AC-001）は未確認。撮影用テスト（`ScreenCaptureTests`、Explicit）は赤7で盤面の演出を呼ぶだけなので、息吹は写らない。
- 2026-11-23 ユーザーの感想: 息吹は良い。地面の火（muzzle_02）は水滴のようでダサい → 息吹と同じ flame_01 を回して低く広げる形に変更（muzzle_02 と Red7 Jet.mat は削除）。PlayMode 68 成功/失敗0、ハーネス 8/8。Editor が Play していなくても重いとの報告（調査ではアイドル時の Unity の CPU は約1%、空きメモリ約2GB/16GB、ほかに Roblox・ブラウザなどが動作中。Editor.log は権限がなく読めなかった）。
- 次: ユーザーの Play の感想に合わせて、秒数（`warningEnd`・`breathStart`・`red_delay_seconds`）・炎の量・色・揺れ（`shakeAmplitude`）を調整 → T706（全テスト・HANDOFF）。
- そのほかの残り: 001 の M5（T501〜T505、002・003 に合わせて見直す）、本番の 3D 台モデルの差し替え（`Cabinet Model`）。001 の T399・T429 はユーザーの指示（「テストプレイは終わったものとして」）で完了扱いでよい（tasks.md の更新はまだ）。
- ブランチ: main に直接コミットして origin へ push。main は core.autocrlf で CSV が CRLF になる。
- Unity 接続: 2026-11-23 に停止を確認済み（ポート 8080 閉）。Editor は開いたまま、Game シーンは保存済み。ユーザーが Play 中のことがあるので、Unity 操作の前に isPlaying を確認する。

### 作業の注意
- テストの手順: refresh → 約25秒 → code execute で `scriptCompilationFailed` と `isCompiling` を確認 → `tests --async` を1回 → 同じ job_id を前景のループで poll（[`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節）。PlayMode 全件は約4分。スクリプトは毎回 `$env:TEMP` などに作り直す。
- PlayMode の実行中に Play を止めて打ち切ると、ジョブが `tests_running` のまま残り、Editor の再起動まで新しいテストを流せない。止めずに最後まで待つ。
- ラッパーが失敗すると出力が捨てられる。原因を見るときは `.tools/unity-mcp/Scripts/python.exe -B scripts/unity/unity_mcp_runner.py ...` を直接呼ぶ。
- 実データを読むテスト（`TablePaths`）は数値の調整で落ちる。CSV を変えたら `Get("melee")` などの期待値を grep で探して一緒に直す。
- Bash で C# の `\n` を含む置換をするとバックスラッシュが崩れる。そういう置換は Edit ツールで行う。`node -e "..."` の中のバッククォートはシェルに展開されるので、node のコードは quoted heredoc でファイルに書いてから実行する。
- Unity 操作中に PC が落ちた前例がある。操作は1回の呼び出しに詰め込まず分ける。
- Codex に任せるのは小さく閉じた作業だけ（T604・T605 の委任は失敗）。HANDOFF・tasks を触らせず、テストは数と中身を確かめる。
- シーンは Editor の Setup メニューで作り直す。Play を止めるとシーンが dirty になるので、Setup の前に保存済みシーンを開き直す。
- 撮影: `Tools/UnitySlotTower/Automation/Capture Screens (T359)` → `Artifacts/Automation/Editor/t359/<日時>/`。Game ビューは 1440×1920。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
