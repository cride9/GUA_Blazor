window.voicePlayer = (function () {
    const instances = new WeakMap();

    function formatTime(seconds) {
        if (isNaN(seconds)) return '0:00';
        const m = Math.floor(seconds / 60);
        const s = Math.floor(seconds % 60);
        return `${m}:${s.toString().padStart(2, '0')}`;
    }

    function initSingle(container) {
        if (!container || instances.has(container)) return;

        const audio = container.querySelector('audio');
        const playBtn = container.querySelector('.voice-play-btn');
        const waveform = container.querySelector('.voice-waveform');
        const timeDisplay = container.querySelector('.voice-time');
        const progressContainer = container.querySelector('.voice-progress-container');
        const progressBar = container.querySelector('.voice-progress-bar');
        const volumeBtn = container.querySelector('.voice-volume-btn');

        if (!audio || !playBtn) return;

        let isPlaying = false;
        let isMuted = false;

        const updateUI = () => {
            if (isPlaying) {
                playBtn.classList.add('playing');
                waveform?.classList.remove('paused');
                playBtn.innerHTML = '<svg viewBox="0 0 24 24"><rect x="6" y="4" width="4" height="16"/><rect x="14" y="4" width="4" height="16"/></svg>';
            } else {
                playBtn.classList.remove('playing');
                waveform?.classList.add('paused');
                playBtn.innerHTML = '<svg viewBox="0 0 24 24"><path d="M8 5v14l11-7z"/></svg>';
            }

            if (audio.duration) {
                const pct = (audio.currentTime / audio.duration) * 100;
                progressBar.style.width = `${pct}%`;
                timeDisplay.textContent = `${formatTime(audio.currentTime)} / ${formatTime(audio.duration)}`;
            }
        };

        const handlePlayPause = (e) => {
            e.stopPropagation();
            if (audio.paused) {
                audio.play().catch(() => { });
                isPlaying = true;
            } else {
                audio.pause();
                isPlaying = false;
            }
            updateUI();
        };

        const handleSeek = (e) => {
            e.stopPropagation();
            if (!audio.duration) return;
            const rect = progressContainer.getBoundingClientRect();
            const pos = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
            audio.currentTime = pos * audio.duration;
            updateUI();
        };

        const handleVolume = (e) => {
            e.stopPropagation();
            isMuted = !isMuted;
            audio.muted = isMuted;
            if (volumeBtn) volumeBtn.style.opacity = isMuted ? '0.4' : '1';
        };

        // Attach listeners
        playBtn.addEventListener('click', handlePlayPause);
        progressContainer.addEventListener('click', handleSeek);
        volumeBtn?.addEventListener('click', handleVolume);
        audio.addEventListener('timeupdate', updateUI);
        audio.addEventListener('loadedmetadata', updateUI);
        audio.addEventListener('ended', () => {
            isPlaying = false;
            progressBar.style.width = '0%';
            updateUI();
        });

        // Initial render
        updateUI();
        instances.set(container, { audio, playBtn, waveform, timeDisplay, progressContainer, progressBar, volumeBtn, handlePlayPause, handleSeek, handleVolume, updateUI });
    }

    function disposeSingle(container) {
        const data = instances.get(container);
        if (!data) return;

        data.audio.pause();
        data.audio.src = '';
        data.playBtn.removeEventListener('click', data.handlePlayPause);
        data.progressContainer.removeEventListener('click', data.handleSeek);
        data.volumeBtn?.removeEventListener('click', data.handleVolume);
        data.audio.removeEventListener('timeupdate', data.updateUI);
        data.audio.removeEventListener('loadedmetadata', data.updateUI);
        data.audio.removeEventListener('ended', data.updateUI);

        instances.delete(container);
    }

    // Bulk methods for class-based initialization
    function initAll(selector = '.voice-message-container') {
        document.querySelectorAll(selector).forEach(initSingle);
    }

    function disposeAll(selector = '.voice-message-container') {
        document.querySelectorAll(selector).forEach(disposeSingle);
    }

    return { init: initSingle, dispose: disposeSingle, initAll, disposeAll };
})();