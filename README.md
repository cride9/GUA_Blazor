# GUA (General Usage Agent)

An intelligent, autonomous AI assistant built on **Blazor WebAssembly** with full support for file system operations, terminal commands, web scraping, audio/video processing, and multi-turn agent workflows.

---

## 🚀 Features

### Core Capabilities

- **AI-Powered Assistant**: Chat interface with support for both basic and agent modes
- **File System Tools**: Create, read, edit, delete, move, rename, search, and zip files within a secure sandbox
- **Terminal Execution**: Run shell commands with persistent sessions and real-time output monitoring
- **Web Tools**: Google search and URL scraping (HTML/PDF support)
- **Audio/Video Processing**: 
  - Extract audio from videos
  - Transcribe audio using Whisper
  - Burn subtitles into videos with custom styling
  - Text-to-speech with multiple voice options
- **PDF Generation**: Create PDFs from markdown content
- **Git Repository Ingestion**: Analyze entire GitHub repositories instantly
- **Session Management**: Multiple independent chat sessions with isolated sandboxes

### Agent Modes

**Basic Mode**: Standard conversational AI for questions and simple tasks

**Agent Mode**: Autonomous multi-step task execution with:
- Tool usage for complex operations
- Planning and verification of each step
- Automatic parallel tool calls
- Support for up to 50 turns per session
- Graceful handling of errors and edge cases

---

## 🛠️ Tools Reference

### File System Tools

| Tool | Description |
|------|-------------|
| `create_file` | Create new text-based files |
| `read_file` | Read files with line number support |
| `edit_file` | Replace text blocks with diff preview and backup options |
| `delete_file` | Delete files or directories (dry-run supported) |
| `move_file` | Move files/directories with overwrite option |
| `rename_file` | Rename files/directories in place |
| `search_in_files` | Search text or regex patterns across files |
| `list_directory` | List files and directories with tree view |
| `zip_directory` | Create zip archives of files or directories |

### Terminal Tools

| Tool | Description |
|------|-------------|
| `run_command` | Execute shell commands asynchronously |
| `read_terminal_output` | Get command output from terminal sessions |

### Audio/Video Tools

| Tool | Description |
|------|-------------|
| `extract_audio` | Extract audio from video files (MP4, MKV, AVI, MOV) |
| `transcribe_audio` | Transcribe audio using Whisper (plain/SRT/VTT formats) |
| `burn_subtitles` | Burn SRT subtitles into video with custom styling |
| `text_to_speech` | Convert text to speech using Kokoro TTS |
| `merge_audio` | Merge multiple WAV files using FFmpeg |
| `merge_audio_with_video` | Combine TTS audio with video (loops video if needed) |

### Web Tools

| Tool | Description |
|------|-------------|
| `web_search` | Search Google via Custom Search API |
| `scrape_url` | Extract text from HTML pages and PDFs |
| `git_ingest` | Ingest GitHub repositories for analysis |

### Document Tools

| Tool | Description |
|------|-------------|
| `create_pdf` | Generate PDFs from markdown content |

---

## 📁 Project Structure

```
GUA_Blazor/
├── Components/
│   ├── Layout/          # Blazor layout components and styles
│   └── Pages/           # Main chat interface
├── Helper/
│   └── SessionSandbox.cs    # Sandbox path management
├── Models/
│   ├── ChatMessage.cs     # Message data model
│   ├── ChatSession.cs     # Session management
│   ├── Instructions.cs    # System prompts
│   └── SessionFactory.cs  # Session creation
├── Service/
│   ├── AIService.cs       # Main AI orchestration
│   ├── GitIngest.cs       # GitHub repo ingestion
│   ├── KokoroService.cs   # TTS service wrapper
│   └── WhisperService.cs  # Transcription service wrapper
├── Tools/
│   ├── Filesystem/        # File operations
│   ├── Terminal/          # Command execution
│   ├── TTS/               # Text-to-speech
│   ├── Web/               # Web scraping & search
│   └── WhisperTools/      # Audio/video processing
├── wwwroot/
│   ├── app.css            # Base styles
│   ├── app.js             # Client utilities
│   └── style.css          # Main UI styling
└── run_all_servers.bat    # Windows startup script
```

---

## ⚙️ Setup & Installation

### Prerequisites

- .NET 10.0 SDK
- FFmpeg (for audio/video operations)
- Python 3.x (for Kokoro TTS server - optional)
- Access to local AI model servers (recommended)

### Running the Application

1. **Build and run the Blazor app**:
   ```bash
   dotnet build
   dotnet run
   ```

2. **Optional: Run supporting services** (Windows):
   ```batch
   run_all_servers.bat
   ```

This script starts:
- Kokoro TTS server (Python-based)
- Llama inference server (ggml-based)
- Whisper transcription server

### Environment Configuration

Set the following environment variables for web search:

```bash
export google_api="YOUR_GOOGLE_API_KEY"
export google_engine="YOUR_SEARCH_ENGINE_ID"
```

---

## 🔧 Configuration

### Model Settings

The application uses LlmTornado for model communication. Default server:
- URL: `http://26.86.240.240:8080`
- Update in `AIService.cs` if using local models

### System Instructions

Customize agent behavior in `Instructions.cs`:

- **BasicInstruction**: For standard chat mode
- **AgentInstruction**: For autonomous agent mode with tool usage

### Session Storage

Sessions are stored in the `sessions/` directory with isolated workspaces:
- Each session gets its own sandbox folder
- Uploaded files are stored in `sessions/{sessionId}/work/`
- Maximum file size: 500MB
- Maximum concurrent uploads: 10 files

---

## 🎨 UI Features

### Dark Theme
- Modern dark interface with accent color (#d4a853)
- Responsive design (mobile, tablet, desktop)
- Smooth animations and transitions

### Chat Interface
- Sidebar with session history
- Markdown rendering with syntax highlighting
- Tool call visualization with collapsible JSON
- File attachment support with image previews
- Agent mode toggle
- Auto-resizing text input

### Message Display
- User and AI avatars
- Typing indicators
- Tool call dropdowns
- File preview chips
- Image lightbox viewer

---

## 🔒 Security Features

- **Sandbox Isolation**: Each chat session operates in an isolated directory
- **Path Validation**: Prevents directory traversal attacks
- **Command Blocking**: Blocks dangerous shell commands
- **File Size Limits**: Prevents resource exhaustion
- **Circuit Breaker**: Automatic reconnection with retry logic

---

## 🤝 Using the Agent

### Basic Chat
1. Type your question or request
2. Press Enter or click send
3. Get instant response with markdown formatting

### Agent Mode Tasks
1. Toggle "Agent Mode" on
2. Describe a multi-step task (e.g., "Create a Python script that analyzes logs and generates a report")
3. The agent will:
   - Plan the approach
   - Execute tools sequentially
   - Show tool calls for transparency
   - Handle errors gracefully

### Example Workflows

**File Analysis**:
```
"Search for all TODO comments in *.cs files and list them"
```

**Code Generation**:
```
"Create a README.md for a new C# project with setup instructions"
```

**Media Processing**:
```
"Extract audio from video.mp4, transcribe it, and burn subtitles"
```

**Repository Analysis**:
```
"Analyze https://github.com/dotnet/runtime to find recent changes"
```

---

## 📦 Dependencies

### NuGet Packages
- `LlmTornado` (v3.8.54) - AI model communication
- `Markdig` (v1.1.1) - Markdown parsing
- `AngleSharp` (v1.4.0) - HTML parsing
- `PdfPig` (v0.1.14) - PDF text extraction
- `QuestPDF` (v2026.2.4) - PDF generation
- `QuestPDF.Markdown` (v1.49.0) - Markdown to PDF

---

## 🐛 Troubleshooting

### Common Issues

**FFmpeg not found**:
- Install FFmpeg and ensure it's in your PATH
- Windows: Download from https://ffmpeg.org/download.html

**TTS/Transcription fails**:
- Ensure Kokoro (port 8082) and Whisper (port 8081) servers are running
- Check `KokoroService.cs` and `WhisperService.cs` for correct URLs

**Web search fails**:
- Set `google_api` and `google_engine` environment variables
- Verify API key has Custom Search API enabled

**Session data not persisting**:
- Check write permissions on `sessions/` directory
- Ensure no antivirus is blocking file operations

---

## 📝 License

This project is provided as-is for educational and commercial use.

---

## 🙏 Acknowledgments

- **Blazor** - Web framework by Microsoft
- **LlmTornado** - AI model client library
- **Whisper** - Speech recognition by OpenAI
- **Kokoro** - Text-to-speech engine
- **QuestPDF** - Modern PDF generation library

---

## 📞 Support

For issues or questions:
1. Check the troubleshooting section above
2. Review the code comments in `Tools/` directory
3. Examine `AIService.cs` for agent workflow details

---

**Built with ❤️ using Blazor and modern AI technologies**
