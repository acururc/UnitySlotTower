# Confirmed project context

Unity project at repository root (created 2026-10-26). This is an index of confirmed facts; verify current values in the authoritative files.

| Topic | Confirmed fact / source |
| --- | --- |
| Name | UnitySlotTower (folder, productName in `ProjectSettings/ProjectSettings.asset`); official title unconfirmed |
| Repository | https://github.com/acururc/UnitySlotTower.git (`main`) |
| Gameplay / targets | Portrait PC slot × tower-defense game (battle top 60%, slot cabinet bottom 40%; 3 stages × 5 waves, difficulty = slot setting 1–6). Requirements: `specs/001-slot-tower-defense/spec.md` (approved 2026-11-01), design `plan.md`, progress `tasks.md`; current state in `HANDOFF.md` |
| Unity | 6000.6.7f1 — `ProjectSettings/ProjectVersion.txt`; created from the bundled `com.unity.template.urp-blank-17.2.6` template |
| Rendering | URP 17.6.0 (3D) — package files; `Assets/Settings/PC_RPAsset.asset` / `Mobile_RPAsset.asset` |
| Input | Input System 1.20.1, `activeInputHandler: 1` (new only); `Assets/InputSystem_Actions.inputactions` |
| UI | uGUI 2.6.0 (game UI); UI Toolkit module installed |
| Scene | Build Settings: `Assets/UnitySlotTower/Scenes/Title.unity`, `Game.unity` enabled; template `Assets/Scenes/SampleScene.unity` listed but disabled |
| Tests | Unity Test Framework 1.8.0 (builtin); EditMode and PlayMode suites under `Assets/UnitySlotTower/Tests/` (latest counts in `tasks.md`) |
| Code | `Assets/UnitySlotTower/`: `Core` (game logic), `Runtime`, `Editor` (scene setup menus), `Tests/EditMode`, `Tests/PlayMode` asmdefs; `Automation/` helpers (Editor connection, dev-player screenshot) |
| Unity automation | MCP for Unity 10.2.0 package + pinned Python CLI/server in `.tools/unity-mcp` (Python 3.12); 127.0.0.1:8080; `UNITY_AUTOMATION.md`. Read-only connection check passed on 6000.6.7f1 (2026-10-26) |
| Build | `scripts/unity/Build-UnityPlayer.ps1` (Windows 64-bit Development) → `Builds/Windows/`; player built and run in this project. Distribution target undecided |
| Spec workflow | Lightweight: `SPEC_WORKFLOW.md`, `specs/NNN-name/` |
| Game data | CSV tables in `Assets/UnitySlotTower/Data/Tables/` (list in plan 6章); all validated by `Core/Data/GameDataValidator` (EditMode `TableValidationTests`) |
| Harness | `scripts/harness/Test-ProjectHarness.ps1` (required files, JSON, Unity version, .meta pairs, whitespace, secret patterns) |

## Adopted decisions
- Agent rules: `AGENTS.md` is canonical; `CLAUDE.md` imports it. Skills canonical in `.agents/skills/` with thin `.claude/skills/` entries.
- Unity automation ported from CookingShot (verified there on 6000.3): on-demand start, bounded waits, explicit Stop; no permanently registered MCP server. Shares port 8080 with CookingShot, so one project at a time.
- Blender tooling is intentionally not included.

## Maintenance
Keep confirmed facts/decisions here and continuation state in `HANDOFF.md`; replace obsolete state rather than append history. Update version facts when authoritative settings change.
