# 016 早見表の作り直し — 計画

要求は [`spec.md`](spec.md) の FR/AC で参照する。

## 方針

- 早見表の中身（タブ・ページ・行・文字ありのラベルと配置）を、新しい Setup `Tools/UnitySlotTower/Setup/Build Help Pages (spec 016)`（`HelpRebuildSetup`）ひとつで作り直す。外枠（`Help`・`Panel`・閉じる・見出しの ?・一時停止の ? ボタン）は今のまま残し、`Panel` の子のうち外枠以外（`Page Tabs`・`Slot Page`・`Unit Page`・古い行・古いラベル）を消して作る。何度実行しても同じ結果。
- 文字なし版と文字あり版は同じ部品を使う。行ごとに「絵 | 動く図 | 数値」を置き、文字ありでは `TextModeLayout`（Rect）で動く図を縮めて空いた所に文（`TextModeLabel`）を出す。文字なし版の配置はシーンの値と同じ（既存の EditMode テストの決まり）。
- 今の重ねた Setup（`HelpPagesSetup`・`SlotBetHelpSetup`、`HelpScreenSetup` の行づくり、`TextModeSetup.AddHelpLabels` の中身、`TextModeLayoutSetup.BuildHelp`、`TextModeRestyleSetup` の `RestyleUnits`）は早見表の中身に触れないようにする。`AddHelpLabels` と `BuildHelp` は新しい Setup の文字ありの部分を呼ぶだけにして、Build Text Mode Layout (T359) などを再実行しても壊れないようにする。使われなくなったメニューと関数は消す。

## 動く図（FR-002・D3）

- Runtime に `HelpDemo`（MonoBehaviour）。`period`（秒）と `Track[]` を持つ。Track は 対象の RectTransform・Graphic・キーの列（時刻 0〜1、中心の位置〔親の矩形の割合〕、大きさ〔親の割合〕、色、回転）。`Update` で `Time.unscaledTime` からループ位置を出し、前後のキーを補間して anchorMin/Max・色を置く。親の矩形の割合で置くので、文字ありで図の枠が縮んでも中の絵は崩れない。
- ページが非表示なら Update が走らないので、開いているページの図だけ動く。`OnEnable` で時刻を0から始める。毎フレームの割り当てはしない（キーは配列で事前に持つ）。
- 図の中身（キーの並び）は Setup のコードで作る。絵は今ある図形（`TriangleGraphic`・Knob・四角、`SlotSymbolArt`・`UnitLooks`）を使う仮の絵。

## ページと数値（FR-001・FR-004・FR-010〜FR-014）

- `HelpScreen` を5ページに直す: `Page { Guide, Bet, Symbols, Units, Enemies }`、`pages[]`・`tabs[]`、◀ ▶ は端で回る。`ShowPage`・`CurrentPage`・`NextPage`・`PreviousPage`、`OpenButton`・`CloseButton` などの公開 API は残す（`SlotTab`・`UnitTab` は `Tab(Page)` に置き換え、テストを直す）。
- `Refresh` で書く数値は FR-004 だけ: 青7・黄7・バーの秒数（`slot_rules.csv`）、味方ごとのブロック（`BlockText.Show`）・コスト ¥・待ち秒数（`units.csv`）。範囲図は今の `BuildGridsOnce` をそのまま使う。確率・倍率・HP などのフィールドと計算は消す。秒は「3秒」のように数字の後に「秒」の代わりの時計アイコン（文字なし）／「秒」（文字あり）。¥ はコストの前。
- 敵の行の絵は盤面の敵の見た目に合わせた2D の図形（色・形は `UnitLooks` と同じように Setup の定数）。

## 文字あり（FR-003・AC-002）

- 文は `ui_texts.csv` に `help2_*` の id で足す（古い `help_col_*`・`help_slot_heading`・`help_units_heading`・`stat_*` のうち早見表だけで使うものは消す。ほかで使う id は残す）。足した字は Setup `Create Text Mode Font (T352)` で文字ありフォントに入れる。
- 文の枠は行の中の決まった矩形。字の大きさは 36（`TextModeLayoutSetup.MinFontSize`）〜40、自動縮小はしない。

## テスト・検証

- PlayMode `HelpTests` を5ページ・◀ ▶ の回り・数値（FR-004 の項目だけ、CSV の値どおり）・図が一時停止中に動く（`HelpDemo` の対象の位置が実時間で変わる）に直す。
- PlayMode に AC-002 のテスト: 両方の版で全ページを開き、文字ありは `TMP_Text.isTextOverflowing` がなく、ページ内の文・数字・アイコン（`HelpDemo` 以外の Graphic）の画面上の矩形が重ならない。文字なしでは文字の TMP（数字以外）が出ていない（今の TextAudit を使う）。
- `TextModeTests`・`ScreenCaptureTests`・`ScreenTextTests`・`InputOnlyRunTests` の早見表の部分を新しい API に直す。撮影は5ページ×2版。
- ハーネス、EditMode・PlayMode 全件、撮影を見る、ユーザーの Editor 確認（AC-003）。

## リスク

- Setup の作り直しで Game.unity の差分が大きくなる（いつものこと）。シーンは保存済みのものを開いてから Setup を実行する。
- Setup を再実行すると Inspector の調整値が初期値に戻る（いつものこと）。
- 文字ありフォントに字が足りないと豆腐になる → フォント Setup を必ず再実行し、TextAudit／撮影で見る。
