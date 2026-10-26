@AGENTS.md

## Claude Code specifics
- Use the native PowerShell tool for project scripts (`pwsh -NoProfile -File ...`). For long Unity scripts, run in the background and wait for the completion notification; never start the same mutation twice.
- Project skills: `unity-verify` (change-specific verification), `unity-editor` (Editor/MCP operations), `spec` (significant features), `handoff` (checkpoint for resuming). Load a skill only when its task applies.
- Agents: `unity-check` (read-only instance/scene/Console check with shutdown), `code-task` (small edits at known paths). Use a normal session for broader work. Do not spawn subagents for routine tasks.
- Unity MCP is not registered as a permanent MCP server on purpose (keeps tool schemas out of context and the Editor disconnected when idle); the CLI wrapper covers Editor operations.
