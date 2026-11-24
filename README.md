# In The Loop

A Windows desktop app that puts you in the loop while an AI builds something visual.

The AI posts a screenshot through an MCP server. You look at it in the app and either approve it or
write what should change. The AI gets your answer immediately, fixes things, and posts the next
version. Both sides can also just talk: you can ask a question without giving a verdict, and the AI
can reply or ask one back.

## How it works

```
AI client ──stdio/HTTP──▶ intheloop-mcp.exe ──▶ %LOCALAPPDATA%\InTheLoop ◀── InTheLoop.exe (you)
```

The MCP server and the app share one folder on disk. Each review is a folder with a `review.json`
and its screenshots; each project is a small JSON file. Both processes watch that folder, so a
verdict you give reaches a waiting AI within a fraction of a second, and there are no ports or
background services. If the app is closed when a screenshot arrives, the MCP server starts it.

## Getting started

Requirements: Windows 10 1809 or later, the .NET 10 SDK and the MAUI workload
(`dotnet workload install maui-windows`).

```powershell
.\scripts\publish.ps1
```

That produces `dist\InTheLoop\InTheLoop.exe` (the app) and `dist\InTheLoop\mcp\intheloop-mcp.exe`
(the MCP server). Start the app once and open **Connect an AI** in the sidebar: it shows the exact
snippet for your client, with a copy button.

| Client | How |
| --- | --- |
| Claude Code | `claude mcp add --scope user intheloop -- "<path>\intheloop-mcp.exe"` |
| Claude Desktop, Cursor, other MCP clients | Add `{"mcpServers": {"intheloop": {"command": "<path>\\intheloop-mcp.exe"}}}` to the client's MCP config |
| ChatGPT Codex | Add `[mcp_servers.intheloop]` with `command = "<path>\\intheloop-mcp.exe"` to `~/.codex/config.toml` |
| Clients that need a URL | Run `intheloop-mcp.exe --http 5179` and use `http://127.0.0.1:5179/mcp` |

The ChatGPT desktop and web apps only accept MCP servers at a public HTTPS address. For those, run
the HTTP mode behind a tunnel of your choice; the AI then has to send images as base64 because it
cannot read your disk.

Then tell your AI something like: *"Take a screenshot of the page you built and post it to In The
Loop for my review."*

## What the AI sees

| Tool | Purpose |
| --- | --- |
| `submit_screenshot` | Post a screenshot (file path or base64) as a new review, or as the next revision with `review_id`. Waits briefly for your verdict. |
| `wait_for_feedback` | Keep waiting. Returns `approved`, `changes_requested`, `comment` or `pending`. |
| `reply_to_human` | Send you a message without a new screenshot. |
| `create_project` | Create a project to group reviews. Returns the existing one if the name is taken. |
| `list_projects` | List projects with their review counts. |
| `get_review` | Status and full conversation of one review. |
| `list_reviews` | Find reviews again, optionally by status or project. |

Every result ends with a "Next:" line telling the model what to do, and errors say how to recover
(for example, an unknown project lists the ones that exist).

## Using the app

- **Sidebar**: your projects, each with the number of reviews waiting for you. The AI creates
  projects itself; you can delete one from the list header (its reviews are kept).
- **Review list**: the three counters (Your turn, With the AI, Approved) are also filters.
- **Screenshot**: double-click to open it at full size. Earlier versions appear as a strip below.
- **Conversation**: write feedback and press **Request changes**, press **Approve** (feedback
  optional), or use the send button to comment without a verdict.

Reviews live in `%LOCALAPPDATA%\InTheLoop`. Set `INTHELOOP_DATA_DIR` for both the app and the MCP
server to use another folder.

## Code layout

```
src/InTheLoop.Core   Models, the file-based stores and every use case
src/InTheLoop.Mcp    MCP server (stdio and HTTP), a thin layer over Core
src/InTheLoop.App    .NET MAUI app (Windows)
tests/               xUnit tests for the Core use cases
```

- **Handler pattern**: every use case is a request with one handler
  (`SubmitScreenshot` → `SubmitScreenshotHandler`, `JudgeReview`, `WaitForFeedback`,
  `CreateProject`, …) in `Core/Features`. View models and MCP tools only talk to
  `IRequestDispatcher`, so the app and the server run exactly the same logic. On the MAUI side,
  `Handlers/InputHandlers.cs` customises the platform handlers for `Entry` and `Editor`.
- **MVVM**: views are XAML with compiled bindings; view models use CommunityToolkit.Mvvm and
  contain no UI types. Platform concerns sit behind small interfaces in `App/Services`.
- **DI**: everything is registered in `MauiProgram.cs` and `Core/ServiceCollectionExtensions.cs`;
  handlers are discovered by scanning the Core assembly.

```powershell
dotnet build InTheLoop.sln
dotnet test tests/InTheLoop.Core.Tests
```
