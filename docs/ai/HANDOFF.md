# Resume state

## 現在の作業

更新: 2026-11-22 Asia/Tokyo / 状態: **spec 002 の実装中。T608 まで完了（EditMode 666/666・PlayMode 72 成功/失敗0/Explicit 1・ハーネス 8/8）。** 次: T609（ルート・ショップ・回復・両替の削除）。T608 で後のタスクへ残したもの（ショップの強化の品だけ・旧ルートのゲージの点・ガイドのスキルの題目・色7の演出の行き先 null・早見表の色7の「+1」と `slot_red7` 等の文言）は tasks.md の T608 行、T607 で残したものは T607 行。T604 の仮置き: タンク・バッファー・ボスの攻撃間隔は据え置き、最終ボスの範囲攻撃3、`def_mult`=`hp_mult`。Codex 委任は T604・T605 とも失敗したので T606〜T608 は Claude が書いた。Codex に任せるのは小さく閉じた作業だけにする（`git stash` の「T605 codex attempt (rejected)」は不要なら drop してよい）。
- 対象: [`spec.md`](../../specs/002-td-focus/spec.md)（2026-11-21 承認、sha256 `24ce711235c4`、E-01〜E-30・FR-101〜FR-117・AC-101〜AC-113）、[`plan.md`](../../specs/002-td-focus/plan.md)、[`tasks.md`](../../specs/002-td-focus/tasks.md)（T601〜T617）。元は 2026-11-21 の第三者プレイ後のメモ `変更.txt`（リポジトリ直下、未追跡）。001 の spec は土台で、食い違いは 002 が優先。
- 承認の範囲: spec 002 全体（Claude 提案の E-23〜E-30 を含む）。plan の 4章の数値・マップの割り当て・`stages.csv`/`stage_waves.csv` の形・最低ダメージ1・buff_rate 2.0・強化倍率は Claude の仮置きで承認の対象外。
- ユーザーの主な決定: ルート・ショップ・回復・無料ストック・説明・ガイドヒント・難易度・スキルを削除。全8ステージを順に。ステージ1はチュートリアル（全滅で次のウェーブ）、2以降は全滅を待たない。持ち越しはライフ・コイン・強化・所持金。無料の1体はステージ1だけ。召喚費用は今のまま。ウェーブ数は出さない。ハズレなし（一旦）。
- 001 の残り: T399・T429（ユーザーの通しプレイ）と M5（T501〜T505）は 002 で前提が変わる。T501 のデザインの方向（サブモニター案など）は未決定のまま。
- ブランチ: `feature/002-td-focus`（main より約118コミット先）。spec・plan・tasks・HANDOFF はコミット済み。
- Unity 接続: 2026-11-22 T608 の検証後に停止を確認済み（ポート 8080 閉）。Editor は開いたまま。PlayMode 全件は Editor が非フォーカスだと約5〜10分かかる。`poll-test --wait` はすぐ返るので、前景で回数を区切ったループで同じ job_id を poll する（バックグラウンドのポーラーはメモリ不足で止められた。止められても Unity 側のジョブは続く）。CLI にテスト名の絞り込みはない。

### 作業の注意（001 から引き継ぐもの）
- テストの手順: scratchpad のスクリプトで refresh→25秒→code execute でコンパイル確認→`tests --async`→poll（[`UNITY_AUTOMATION.md`](UNITY_AUTOMATION.md) の Tests 節）。PlayMode 全件は約5分。poll が切れたら同じ job_id で poll し直す（バックグラウンドの poll はメモリ不足で止まった前例があり、前景のループで待つ）。「tests did not start within timeout」でも走っていることがあるので、`isPlaying` が false に戻るのを待ってから再送する。
- Unity 操作中に PC が3回落ちた（原因未特定）。操作は1回の呼び出しに詰め込まず分ける。
- Codex に委任するとき: HANDOFF・tasks を触らせない。テストは数と中身を必ず確認する（T602 では2件しか書かず、失敗するテストを `[Ignore]` にしていた）。Editor コードで `GetComponent<T>() ?? Add` を使わせない。行番号や既存の長さなどを正確に指示する。
- シーンは Editor の Setup メニューで作り直す。Play を止めるとシーンが dirty になるので、Setup の前に保存済みシーンを開き直す。テストの後はアクティブシーンが無題になる。
- 文字あり版: 配置は `TextModeLayoutSetup`、ラベルは `TextModeSetup`、読みやすさは `TextModeTests`。表に字を足したら `Create Text Mode Font (T352)`。
- 撮影: `Tools/UnitySlotTower/Automation/Capture Screens (T359)` → `Artifacts/Automation/Editor/t359/<日時>/`。Game ビューは 1440×1920。
- Bash の `python` は動かない（Windows のスタブ）。編集は Edit ツールか `node`。`code execute` のコードは位置引数。PowerShell で関数名 `R` は使わない。
- Bash ツールで node スクリプトを書くとき、C# 文字列の `
`（バックスラッシュ2つ）が1つに崩れて一致しない。エスケープを含む置換は Edit ツールで行う。

## 再開のしかた

この節の「現在の作業」だけを読み、記載されたspec・ソース・`git status`・テスト結果と照合する。段階移行・中断・AI切り替え時は `handoff` スキルで上の節を置き換える（追記しない）。
