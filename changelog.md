# 📜 Changelog

All notable changes to the **GUA (General Usage Agent)** project are documented below.

## [1.4.0] - 2026-04-04

### ✨ Added
* **Voice Messages**: Added TTS audio playback in chat with a custom interactive player (play/pause, progress, volume, waveform) (`<commit>`).
* **AI Tooling**: Introduced `send_voice_message` tool for delivering generated audio to the UI (`<commit>`).

### 🔧 Improvements & Fixes
* **UI Reactivity**: Implemented dynamic re-initialization of voice players for newly rendered messages (`<commit>`).
* **Performance**: Added Base64 audio caching to reduce repeated file reads (`<commit>`).
* **Cleanup**: Ensured proper disposal of audio instances via JS interop (`<commit>`).

---

## [1.3.5] - 2026-04-03
### 🔧 Improvements & Fixes
- Revamp agent instructions and README, enhance browser automation tooling, and add changelog.
- Models/Instructions.cs: Rewrote and condensed system prompts to clearer core directives (autonomy, verification, file/terminal/browser workflows, media workflows and failure handling). Added guidance about ignoring automated "agent_helper" prompts.
- Tools/Web/BrowserUseTool.cs: Added "click_coordinates" action and X/Y args; improved interactive-elements JS to surface better labels, coordinates, sizes and context; increased viewport to 1920x1080; stronger click fallback (force click + helpful error suggesting coordinates); added ClickCoordinatesAsync mouse-based click implementation; adjusted screenshot quality and full-page settings; limited interactive list to 50 items.
- README.md: Major rewrite to reflect Blazor Server (.NET 10), reorganized sections (features, install, tools, limitations, structure), added instructions for Playwright/llama.cpp/whisper/Kokoro setup and clarified tools.
- Added changelog.md documenting recent releases and notable changes.

These changes improve robustness of browser interactions, make agent behavior and tooling expectations clearer, and document the project history and installation steps.

## [1.3.0] - 2026-04-02
### ✨ Added
- **Browser Automation**: Integrated **Playwright** via the `browser_use` tool for real-time web interaction (`4c6f2d4`).
- **Standardization**: Unified tool return formats to improve agent reasoning consistency (`4c6f2d4`).

---

## [1.2.5] - 2026-04-01
### ✨ Added
- **Repository Ingestion**: Added `git_ingest` tool to analyze entire GitHub repositories instantly (`43b53bd`).
- **Archive Support**: Added `unzip_file` tool for managing compressed workspaces (`2cfeccb`).

### 🔧 Improvements & Fixes
- **Parallel Execution**: Enabled **Parallel Tool Calling**, allowing the agent to execute multiple tools in a single turn (`b88e0a2`).
- **UX**: Improved file and terminal output interactions for a smoother user experience (`2cfeccb`).
- **Architecture**: Switched internal session management to use string-based IDs (`b88e0a2`).
- **Maintenance**: General stability patches ("work lol") (`14834b1`).

---

## [1.2.0] - 2026-03-26
### 🛡️ Security & Sandboxing
- **Per-Session Sandbox**: Implemented strict directory isolation for every chat session (`8c44aac`).
- **Refactor**: Migrated all existing tools to respect the new sandboxing logic and session ID tracking (`8c44aac`, `af740a6`).
- **Session Logic**: Overhauled `ChatSession` to handle isolated file work paths (`af740a6`).

---

## [1.1.0] - 2026-03-25
### 🎙️ Multimedia & Speech
- **Whisper Integration**: Added full support for local Whisper transcription and media sandboxing (`066b66d`).
- **Kokoro TTS**: Integrated Kokoro service and added the official fork as a submodule for high-quality voice cloning (`114890e`, `b57cdd3`).
- **Automation**: Created `run_all_servers.bat` to launch the local AI stack simultaneously (`a1382ac`).
- **Hardening**: Improved FFmpeg extraction reliability for video-to-audio workflows (`a1382ac`).
- **Agent Tuning**: Refactored the `AIService` loop and `StopTool` arguments for better exit logic (`de5cfa0`).

---

## [1.0.0] - 2026-03-23
### 🤖 Tool Suite Expansion
- **Core Tools**: Added the initial set of filesystem and terminal execution tools (`49306af`).
- **PDF Generation**: Integrated PDF export support for generated reports (`49306af`).

---

## [0.2.0] - 2026-03-18
### 🧠 Agent Logic
- **Agent Mode**: Introduced the autonomous multi-step reasoning loop (`6ef3c94`).
- **Framework**: Developed the base AI tool framework and the initial `CreateFile` tool (`9e58fad`).
- **Control**: Implemented the `stop_loop` tool for manual and automatic agent termination (`6ef3c94`).

---

## [0.1.0] - 2026-03-17
### 🏗️ Foundation
- **Initial Scaffold**: Basic GUA Blazor Server application structure (`6e64a10`).
- **UI Essentials**: Added multi-file uploads, image previews, and auto-resizing textareas (`f088d7e`).
- **System Config**: Established the `Instructions` model and initial `AIService` configuration (`882a0d1`).

***

> **Note**: Dates and hashes reflect the project repository history as of April 2026.