# Resume state

## 現在の作業

更新: 2026-11-23 Asia/Tokyo / 状態: **spec 005（緑7の回復の演出）は T901〜T904 完了。T905（ユーザーが Editor で Play して確認・調整）待ち。**

- spec 005: [`spec.md`](../../specs/005-green7-heal/spec.md)（2026-11-23 承認、FR-001〜FR-006・AC-001〜AC-005）・[`plan.md`](../../specs/005-green7-heal/plan.md)・[`tasks.md`](../../specs/005-green7-heal/tasks.md)。流れ: 上端が緑に光る→光の粒が降る→約1秒後に味方ごとに光の柱が立ち、その瞬間に回復→粒が上がって消える。揺れ・ヒットストップなし（D-04 は仮。ユーザーが入れたいと言えば変える）。
- spec 005 で作ったもの: Core の `green_delay_seconds`=1（`SlotRules`・`SlotEffects.GreenDelaySteps`・`GameDataValidator`）、`Battlefield` の回復の待ち（`PendingGreenHeals`、`GreenHealed`、赤7と同じく複数並ぶ）、`BattleController.GreenHealed`、`Runtime/Battle/Green7HealEffect.cs`、`Editor/Setup/Green7HealEffectSetup.cs`（メニュー `Build Green7 Heal Effect (spec 005)`、Game シーンに適用・保存済み、材質 `Art/VFX/Green7 *.mat` 3個）、`SlotBoardEffect` から緑7を外した。素材の追加なし（`docs/ASSET_LICENSES.md` の用途に追記）。
- 検証（2026-11-23）: EditMode 559/559（新規5件）、PlayMode 成功68/失敗0/Explicit 1（緑7を2回続けて揃える確認を含む）、ハーネス 8/8。見た目（AC-001）は未確認。
- 次の操作: ユーザーに Play で緑7を確認してもらい、調整（秒数・柱の太さ高さ・色は `Green7HealEffect` の Inspector と Setup の初期値）。確認後 T905 を完了にし、次の出目（黄7・バー・コイン）をユーザーが選ぶ。
- 完了済み: spec 004（青7、T801〜T805）、spec 003（赤7、T701〜T706）、spec 002（T601〜T617）。赤・青・緑とも Setup を再実行すると Inspector の調整値は初期値に戻る。
- Setup をかけると Game.unity の差分が大きく見える（行の並べ替え）。2026-11-23 に行の集合で比べ、消えた行がないことを確認した。
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
