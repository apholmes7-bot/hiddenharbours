# Unity MCP — the editor as a tool

**What:** Unity's official MCP bridge (`com.unity.ai.assistant`, pre-release) lets a Claude Code
session read and drive the *running* editor: scene hierarchy, assets, console, script edits, test
runs. Project config lives in `.mcp.json` (checked in; the relay path expands `${USERPROFILE}`).

**Why:** cheaper, more accurate editor facts than grepping YAML — and a way for the coordinator to
render proofs and read the Console without asking the owner to.

## One-time setup (owner's dev machine)
1. Pull this branch; open the project in Unity 6.5. Package Manager resolves
   `com.unity.ai.assistant@2.16.0-pre.1` and the editor drops the relay at
   `%USERPROFILE%\.unity\relay\relay_win.exe`. Commit the regenerated `Packages/packages-lock.json`.
2. **Edit → Project Settings → AI → Unity MCP Server** — the Unity Bridge should show *Running*.
3. Start Claude Code in the repo; `/mcp` (or `claude mcp list`) should show `unity-mcp`.
4. Back in Unity, the settings page shows **Pending Connection** → **Allow**. Approved clients
   reconnect without re-approval.

## Rules
- The bridge only exists while the editor is **open and focused on this project**. Worktree lanes
  and cloud lanes have no editor: they keep using the no-Unity verification paths (compiled-DLL
  runs, Cecil IL, the V8 rig harness). MCP is for the **owner's checkout and coordinator
  last-miles**, not a substitute for tests.
- Tool calls that mutate the project (script edits, asset creation) are repo changes — same
  branch/PR discipline as any edit. Prefer reading (hierarchy, console, test results) over writing.
- Pre-release package: if it fails to resolve or the bridge will not start, the fallback is
  [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) (needs `uv`); pin one, not both.
