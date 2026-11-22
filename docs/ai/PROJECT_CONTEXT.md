# Confirmed project context

Unity project at repository root (created 2026-10-26). This is an index of confirmed facts; verify current values in the authoritative files.

| Topic | Confirmed fact / source |
| --- | --- |
| Name | UnitySlotTower (folder, productName in `ProjectSettings/ProjectSettings.asset`); official title unconfirmed |
| Repository | https://github.com/acururc/UnitySlotTower.git (`main`) |
| Gameplay / targets | 縦長 PC 向けのタワーディフェンス主体のスロット × TD。戦闘の領域は上半分（上端の帯を除いてカメラ）、下半分は味方の札とスロット台（外枠と LED、3D の仮置き台 `Cabinet Model` は本番モデルに差し替え予定）。全8ステージを順に進み、ステージ1はチュートリアル（全滅で次のウェーブ）、2以降は時間でも次のウェーブが来る。ライフ・コイン・所持金・強化は持ち越し、無料の1体はステージ1だけ。ルート・ショップ・回復・難易度・スキル・説明はない。スロットは1枚賭けで6つの効果（全体ダメージ・停止・回復・毒・攻撃力・コイン倍率）、発動中の出目は抽選から外す。味方は円。倍速は1〜3倍、配置中は `placement_time_scale`（初期値0.2）。マウスのみ、文字なし UI と隠し日本語モード。正本は `specs/002-td-focus/spec.md`（001 の spec が土台で、食い違いは 002 が優先）、設計は 002 の `plan.md`（変えない部分は 001 の `plan.md`）、進捗は各 `tasks.md`、現在状態は `HANDOFF.md` |
| Unity | 6000.6.7f1 — `ProjectSettings/ProjectVersion.txt`; created from the bundled `com.unity.template.urp-blank-17.2.6` template |
| Rendering | URP 17.6.0 (3D) — package files; `Assets/Settings/PC_RPAsset.asset` / `Mobile_RPAsset.asset` |
| Input | Input System 1.20.1, `activeInputHandler: 1` (new only); `Assets/InputSystem_Actions.inputactions` |
| UI | uGUI 2.6.0。共通の色は `Runtime/Ui/UiStyle`、角を切った地と縁は `UiPanel`、既存ボタンの見た目は `UiSkin`。上端の帯にステージの残りの敵・発動中の効果と残り時間・倍率・倍速・一時停止、札に強化と無料の1回の「¥0」、選んだ味方の `UnitDetailPanel` に撤退。配置中は置けるマスを緑で示す。ブロック数は壁と赤い三角（0 は赤い ×）。UI Toolkit モジュールも導入済み。数字専用 TMP と文字あり版の Noto Sans JP（SIL OFL）静的 TMP を使用。ライセンスは `docs/ASSET_LICENSES.md` |
| Scene | Build Settings: `Assets/UnitySlotTower/Scenes/Title.unity`, `Game.unity` enabled; template `Assets/Scenes/SampleScene.unity` listed but disabled |
| Tests | Unity Test Framework 1.8.0 (builtin); EditMode and PlayMode suites under `Assets/UnitySlotTower/Tests/` (latest counts in `tasks.md`) |
| Code | `Assets/UnitySlotTower/`: `Core` (game logic), `Runtime`, `Editor` (scene setup menus), `Tests/EditMode`, `Tests/PlayMode` asmdefs; `Automation/` helpers (Editor connection, dev-player screenshot) |
| Unity automation | MCP for Unity 10.2.0 package + pinned Python CLI/server in `.tools/unity-mcp` (Python 3.12); 127.0.0.1:8080; `UNITY_AUTOMATION.md`. Read-only connection check passed on 6000.6.7f1 (2026-10-26) |
| Build | `scripts/unity/Build-UnityPlayer.ps1` (Windows 64-bit Development) → `Builds/Windows/`; player built and run in this project. Distribution target undecided |
| Spec workflow | Lightweight: `SPEC_WORKFLOW.md`, `specs/NNN-name/` |
| 戦闘の表示と操作 | 開始時は全経路、群れの出始めはその道だけを矢印で示す。本拠地到達時は画面の縁が赤く光る。弓と遠距離の敵の弾は表示専用で使い回す。スロットが揃うと出目ごとの盤面全体の演出（仮置き、本番の絵は M5）。一時停止中の帯から早見表（スロット・味方の2ページ）とタイトルに戻る確認を開く（001 plan 19章、002 plan 3章） |
| Game data | 数値の正本は `Assets/UnitySlotTower/Data/Tables/` の CSV（ステージは `stages.csv`・`stage_waves.csv`・`waves.csv`、効果は `slot_rules.csv`）。`Core/Data/GameDataValidator` と EditMode `TableValidationTests` で検証。数値は小さい桁（最低ダメージ1、攻撃0の職は攻撃しない）。ステージとマップ: 1 `first`、2〜3 `battle_a/b`、4 `elite_a`（中ボス）、5 `battle_c`、6 `battle_d`（2方向）、7 `battle_e`（本拠地2つで共通ライフ）、8 `boss_a`。002 plan 4章の値は仮置きで、通しプレイ（T617）で調整 |
| Harness | `scripts/harness/Test-ProjectHarness.ps1` (required files, JSON, Unity version, .meta pairs, whitespace, secret patterns) |

## Adopted decisions
- Agent rules: `AGENTS.md` is canonical; `CLAUDE.md` imports it. Skills canonical in `.agents/skills/` with thin `.claude/skills/` entries.
- Unity automation ported from CookingShot (verified there on 6000.3): on-demand start, bounded waits, explicit Stop; no permanently registered MCP server. Shares port 8080 with CookingShot, so one project at a time.
- Blender tooling is intentionally not included.
- Personal, non-commercial project (user, 2026-11-17): assets need not be free or commercially licensed; paid or non-commercial assets are fine. Still record source and license in `docs/ASSET_LICENSES.md`.

## Maintenance
Keep confirmed facts/decisions here and continuation state in `HANDOFF.md`; replace obsolete state rather than append history. Update version facts when authoritative settings change.
