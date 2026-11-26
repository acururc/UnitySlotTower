# Resume state

## 現在の作業

更新: 2026-11-26 Asia/Tokyo / 状態: **spec 007 完了（T1105 ユーザー確認 OK）。spec 008 は弾の尾・緑7の魔法陣・バーの魔法陣がユーザー確認 OK。当たりの衝撃はユーザー指示で一旦閃光だけ（輪・火花は showRing・showSparks でオフ、[`tasks.md`](../../specs/008-effect-polish/tasks.md) T1207）、検証済みで確認待ち。push はユーザーがアカウントを切り替えて行う（コミットだけする）。OK なら T1207 を閉じ、コインの演出（spec 009）の spec へ。下の spec 007 の記述は経緯として残す。**

- spec 007: [`spec.md`](../../specs/007-bar-power/spec.md)（2026-11-23 承認、FR-001〜FR-007・AC-001〜AC-005）・[`plan.md`](../../specs/007-bar-power/plan.md)・[`tasks.md`](../../specs/007-bar-power/tasks.md)。流れ: 味方の足元に光の輪が縮まる（溜め、粒が吸い込まれる）→約1秒後に味方ごとに炎の闘気が爆発して弱く揺れ（ヒットストップなし）、攻撃力アップが始まる→効果の間は味方ごとに橙のオーラと立ちのぼる炎、攻撃ごとに火花→切れたら消える。
- spec 007 で作ったもの: Core の `bar_delay_seconds`=1（`SlotRules`・`SlotEffects.BarDelaySteps`/`QueueAttack`/`IsAttackPending`・`GameDataValidator`）、`Battlefield.BarBurst`、`BattleController.BarBurst`・`UnitAttacked` の中継、`Runtime/Battle/BarPowerEffect.cs`、`Editor/Setup/BarPowerEffectSetup.cs`（メニュー `Build Bar Power Effect (spec 007)`、Game シーンに適用・保存済み、材質 `Art/VFX/Bar *.mat` 3個）、`SlotBoardEffect` からバーを外した。素材の追加なし。攻撃力の倍率の表示は待ちの間は ×1.0、爆発の瞬間から上がる。
- 検証（2026-11-23）: EditMode 569/569（新規5件）、PlayMode 成功68/失敗0/Explicit 1、ハーネス 8/8。見た目はユーザー「ちょっと地味」→ 調整1で全段階を派手にし、調整2でエフェクト解説記事の考え方（チャージは近く→遠く、竜巻の渦、続く効果は控えめ）を入れた（tasks.md T1105）。再確認待ち。
- 次の操作: ユーザーが Editor で Play してバーを確認（AC-001）。調整があれば `BarPowerEffect` の初期値（輪の大きさ・炎の数・オーラの大きさ・揺れ）か Setup を直し、Setup を再適用。確認が取れたら T1105 を閉じる。spec 008（[`spec.md`](../../specs/008-effect-polish/spec.md) 2026-11-23 承認・[`plan.md`](../../specs/008-effect-polish/plan.md)・[`tasks.md`](../../specs/008-effect-polish/tasks.md)）: `HitImpactEffect`＋Setup `Build Hit Impact Effect (spec 008)`、`BattleActorsView.Impact`・弾の `TrailRenderer`、`Editor/Setup/MagicCircleTextures.cs`（自作の魔法陣 PNG、緑7・バーの Setup から呼ぶ）、`Green7HealEffect` の3層の魔法陣。検証 EditMode 569/569、PlayMode 68/0/1、ハーネス 8/8。次: ユーザーの Play 確認（spec 007 T1105 と spec 008 T1207 をまとめて）→ 両方閉じたらコインの演出（spec 009）。
- 完了済み: spec 006（黄7）、005（緑7）、004（青7）、003（赤7）、002。赤・青・緑・黄・バーとも Setup を再実行すると Inspector の調整値は初期値に戻る。
- Setup をかけると Game.unity の差分が大きく見える（行の並べ替え）。行の集合で比べ、消えた行がないことを確認する（2026-11-23 の黄7・バーでも0）。
- そのほかの候補: 001 の M5（T501〜T505、002・003 に合わせて見直す）、本番の 3D 台モデルの差し替え（`Cabinet Model`）。001 の T399・T429 はユーザーの指示（「テストプレイは終わったものとして」）で完了扱いでよい（tasks.md の更新はまだ）。
- 素材の方針: AI が取るログイン不要の無料素材だけ（有料は使わない）。
- ブランチ: main に直接コミットして origin へ push。main は core.autocrlf で CSV が CRLF になる。
- Unity 接続: 2026-11-26 に停止を確認済み（8080 閉）。PlayMode テストは Editor が非フォーカスだと極端に遅いので、ユーザーに前面に出してもらう。Editor は開いたまま。ユーザーが Play 中のことがあるので、Unity 操作の前に isPlaying を確認する。Editor が重いのは PC のメモリ不足（実メモリ16GB）で、プロジェクトの問題ではない。

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
