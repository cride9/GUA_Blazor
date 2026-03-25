@echo off
REM --- Kokoro server ---
cd /d "C:\Users\cride\Documents\Github\GUA_Blazor\Kokoro"
call venv\Scripts\activate.bat
start "" python kokoro_server.py

REM --- Llama server ---
start "" "C:\Users\cride\Documents\Github\llamacpp\llama-server.exe" -m "C:\Users\cride\Documents\Tools\AIModels\Qwen3.5-35B-A3B-Q4_K_M.gguf" -t 6 -ot ".ffn_.*_exps.=CPU" -c 65536 -b 2048 --mlock --no-mmap --cache-type-k q8_0 --cache-type-v q8_0 --temp 0.6 --top-p 0.95 --top-k 20 --min-p 0.0 --presence-penalty 0.0 --repeat-penalty 1.0 --chat-template-kwargs "{\"enable_thinking\": false}" --mmproj "C:\Users\cride\Documents\Tools\AIModels\mmproj-BF16-35b.gguf"

REM --- Whisper server ---
start "" "C:\Users\cride\Documents\Github\Whisper\whisper-server.exe" -m "C:\Users\cride\Documents\Github\Whisper\ggml-large-v3-q5_0.bin" --port 8081