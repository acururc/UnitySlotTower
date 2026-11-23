# Resume state

## 現在の作業

更新: 2026-11-23 Asia/Tokyo / 状態: **spec 006（黄7の毒の演出）完了（T1001〜T1005、ユーザー確認済み）。次の出目（バー・コイン）をユーザーが選ぶところ。**

- spec 006: [`spec.md`](../../specs/006-yellow7-poison/spec.md)（2026-11-23 承認、FR-001〜FR-007・AC-001〜AC-005）・[`plan.md`](../../specs/006-yellow7-poison/plan.md)・[`tasks.md`](../../specs/006-yellow7-poison/tasks.md)。流れ: 盤面の地面に毒の沼が湧き泡が立つ→約1秒後に沼が弾けて弱く揺れ（ヒットストップなし）、毒が始まる→毒の間は敵ごとに紫のオーラ、ダメージごとに点滅→毒が切れたら消える。
- spec 006 で作ったもの: Core の `yellow_delay_seconds`=1（`SlotRules`・`SlotEffects.YellowDelaySteps`/`QueuePoison`/`IsPoisonPending`・`GameDataValidator`）、`Battlefield.YellowBurst`・`PoisonTicked`、`BattleController` の中継、`Runtime/Battle/Yellow7PoisonEffect.cs`、`Editor/Setup/Yellow7PoisonEffectSetup.cs`（メニュー `Build Yellow7 Poison Effect (spec 006)`、Game シーンに適用・保存済み、材質 `Art/VFX/Yellow7 *.mat` 3個）、`SlotBoardEffect` から黄7を外した。素材の追加なし。毒のアイコンは待ちの間は出ず、弾けた瞬間から出る（青7と同じ）。
- 同じコミットで: 初期コインを 3→10（`game_config.csv`、`GameConfigTests` の期待値、ユーザー指示 2026-11-23）。spec 005 T905 完了（ユーザー確認）。
- 検証（2026-11-23）: EditMode 564/564（新規5件）、PlayMode 成功68/失敗0/Explicit 1、ハーネス 8/8。見た目はユーザーが確認。
- 調整（T1005）: 敵の足元にも沼（spec D-06）、沼を大きく（盤面 2.2〜3.4 マス、足元 5.0 マス。足元はユーザーが Inspector で決めた値）。
- 次の操作: 次の出目（バー・コイン）をユーザーが選び、spec 007 を `spec` スキルで始める（003〜006 と同じ進め方: 流れ・揺れを聞く→承認→Core の待ち→演出と Setup→テスト→ユーザーの Play 確認）。
- 完了済み: spec 006（黄7）、005（緑7）、004（青7）、003（赤7）、002。赤・青・緑・黄とも Setup を再実行すると Inspector の調整値は初期値に戻る。
- Setup をかけると Game.unity の差分が大きく見える（行の並べ替え）。行の集合で比べ、消えた行がないことを確認する（2026-11-23 の黄7でも0）。
- そのほかの候補: 001 の M5（T501〜T505、002・003 に合わせて見直す）、本番の 3D 台モデルの差し替え（`Cabinet Model`）。001 の T399・T429 はユーザーの指示（「テストプレイは終わったものとして」）で完了扱いでよい（tasks.md の更新はまだ）。
- 素材の方針: AI が取るログイン不要の無料素材だけ（有料は使わない）。
- ブランチ: main に直接コミットして origin へ push。main は core.autocrlf で CSV が CRLF になる。
- Unity 接続: 2026-11-23 に停止を確認済み。Editor は開いたまま。ユーザーが Play 中のことがあるので、Unity 操作の前に isPlaying を確認する。Editor が重いのは PC のメモリ不足（実メモリ16GB）で、プロジェクトの問題ではない。

### 作業の注意
- テストの手順: refresh → 約25秒 → code execute で `scriptCompilationFailed` と `isCompiling` を確認 → `tests --async` を1回 → 同じ job_id を前景のループで poll（[`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節）。PlayMode 全件は約4分。スクリプトは毎回 `$env:TEMP` などに作り直す。
- PlayMode の実行中に Play を止めて打ち切ると、ジョブが `tests_running` のまま残り、Editor の再起動まで新しいテストを流せない。止めずに最後まで待つ。
- ラッパーが失敗すると出力が捨てられる。原因を見るときは `.tools/unity-mcp/Scripts/python.exe -B scripts/unity/unity_mcp_runner.py ...` を直接呼ぶ。
- 実データを読むテスト（`TablePaths`）は数値の調整で落ちる。CSV を変えたら `Get("melee")` などの期待値を grep で探して一緒に直す。
- Bash で C# の `\n` を含む置換をするとバックスラッシュが崩れる。そういう置換は Edit ツールで行う。`node -e "..."` の中のバッククォートはシェルに展開されるので、node のコードは quoted heredoc でファイルに書いてから実行する。Git Bash の `sed -i` は CRLF を LF にするので `sed -i -b` を使う。
- Unity 操作中に PC が落ちた前例がある。操作は1回の呼び出しに詰め込まず分ける。
- Codex に任せるのは小さく閉じた作業だけ（T604・T605 の委任は失敗）。HANDOFF・tasks を触らせず、テストは数と中身を確かめる。
- シーンは Editor の Setup メニューで作り直す。Play を止めるとシーンが dirty になるので、Setup の前に保存済みシーンを開き直す。
- 撮影: `Tools/UnitySlotTower/Automation/Capture Screens (T359)` → `Artifacts/Automation/Editor/t359/<日時>/`。Game ビューは 1440×1920。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
