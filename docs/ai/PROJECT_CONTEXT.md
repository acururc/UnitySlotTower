# Confirmed project context

Unity project at repository root (created 2026-10-26). This is an index of confirmed facts; verify current values in the authoritative files.

| Topic | Confirmed fact / source |
| --- | --- |
| Name | UnitySlotTower (folder, productName in `ProjectSettings/ProjectSettings.asset`); official title unconfirmed |
| Repository | https://github.com/acururc/UnitySlotTower.git (`main`) |
| Gameplay / targets | 縦長 PC 向けのスロット × タワーディフェンス。戦闘の領域は上半分（上端の帯96 pxを除いてカメラ）、下半分は味方の札と筐体。3列・8階のルートをリールで選ぶ。戦闘マスは1ウェーブで群れが出る。ショップ・回復あり。スロットはコイン2枚、味方は円。倍速は1〜3倍で戦闘マスごとに1倍へ戻り、配置中は `placement_time_scale`（初期値0.2）。難易度は設定1〜6、マウスのみ、文字なし UI と隠し日本語モード。正本は `specs/001-slot-tower-defense/spec.md`（D-92〜D-108 を含む）、設計は `plan.md`（18・19章と通常章へ反映）、進捗は `tasks.md`、現在状態は `HANDOFF.md` |
| Unity | 6000.6.7f1 — `ProjectSettings/ProjectVersion.txt`; created from the bundled `com.unity.template.urp-blank-17.2.6` template |
| Rendering | URP 17.6.0 (3D) — package files; `Assets/Settings/PC_RPAsset.asset` / `Mobile_RPAsset.asset` |
| Input | Input System 1.20.1, `activeInputHandler: 1` (new only); `Assets/InputSystem_Actions.inputactions` |
| UI | uGUI 2.6.0。共通の色は `Runtime/Ui/UiStyle`、角を切った地と縁は `UiPanel`、既存ボタンの見た目は `UiSkin`。上端の帯に状態と倍率・倍速・一時停止、札に強化、選んだ味方の `UnitDetailPanel` に撤退。ブロック数は壁と赤い三角（0 は赤い ×）、スキルの絵は `SkillPictures`、一時停止中の説明は `SkillHelpPanel`。UI Toolkit モジュールも導入済み。数字専用 TMP と文字あり版の Noto Sans JP（SIL OFL）静的 TMP を使用。ライセンスは `docs/ASSET_LICENSES.md` |
| Scene | Build Settings: `Assets/UnitySlotTower/Scenes/Title.unity`, `Game.unity` enabled; template `Assets/Scenes/SampleScene.unity` listed but disabled |
| Tests | Unity Test Framework 1.8.0 (builtin); EditMode and PlayMode suites under `Assets/UnitySlotTower/Tests/` (latest counts in `tasks.md`) |
| Code | `Assets/UnitySlotTower/`: `Core` (game logic), `Runtime`, `Editor` (scene setup menus), `Tests/EditMode`, `Tests/PlayMode` asmdefs; `Automation/` helpers (Editor connection, dev-player screenshot) |
| Unity automation | MCP for Unity 10.2.0 package + pinned Python CLI/server in `.tools/unity-mcp` (Python 3.12); 127.0.0.1:8080; `UNITY_AUTOMATION.md`. Read-only connection check passed on 6000.6.7f1 (2026-10-26) |
| Build | `scripts/unity/Build-UnityPlayer.ps1` (Windows 64-bit Development) → `Builds/Windows/`; player built and run in this project. Distribution target undecided |
| Spec workflow | Lightweight: `SPEC_WORKFLOW.md`, `specs/NNN-name/` |
| 戦闘の表示と操作 | 開始時は全経路、群れの出始めはその道だけを矢印で示す。本拠地到達時は画面の縁が赤く光る。弓と遠距離の敵の弾は表示専用で使い回す。一時停止中の帯から早見表（スロット・味方の2ページ）とタイトルに戻る確認を開く。説明の暗幕はクリックを通し、対象を問わず同種の操作で進み、× でも閉じられる（plan 19章） |
| Game data | 数値の正本は `Assets/UnitySlotTower/Data/Tables/` の CSV（一覧は plan 6章）。`Core/Data/GameDataValidator` と EditMode `TableValidationTests` で検証。重装 `heavy` は遠距離倍率 `ranged_damage_rate` 0.4、最初の戦闘には出さない。通常マップは `battle_a〜e`（`battle_d` は2方向、`battle_e` は本拠地2つで共通ライフ）。別の候補があれば直前のマップを除いて重み付き抽選（通常は a〜c 各7、d・e 各26） |
| Harness | `scripts/harness/Test-ProjectHarness.ps1` (required files, JSON, Unity version, .meta pairs, whitespace, secret patterns) |

## Adopted decisions
- Agent rules: `AGENTS.md` is canonical; `CLAUDE.md` imports it. Skills canonical in `.agents/skills/` with thin `.claude/skills/` entries.
- Unity automation ported from CookingShot (verified there on 6000.3): on-demand start, bounded waits, explicit Stop; no permanently registered MCP server. Shares port 8080 with CookingShot, so one project at a time.
- Blender tooling is intentionally not included.
- Personal, non-commercial project (user, 2026-11-17): assets need not be free or commercially licensed; paid or non-commercial assets are fine. Still record source and license in `docs/ASSET_LICENSES.md`.

## Maintenance
Keep confirmed facts/decisions here and continuation state in `HANDOFF.md`; replace obsolete state rather than append history. Update version facts when authoritative settings change.
