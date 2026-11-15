/* Review-only Canvas2D boss reveal. No asset loading or game-state changes. */
(function (global) {
  'use strict';

  const TAU = Math.PI * 2;
  const clamp = (v, lo = 0, hi = 1) => Math.min(hi, Math.max(lo, v));
  const smooth = (v, a, b) => {
    const t = clamp((v - a) / (b - a));
    return t * t * (3 - 2 * t);
  };
  const random = (seed) => {
    const n = Math.sin(seed * 127.1 + 311.7) * 43758.5453;
    return n - Math.floor(n);
  };

  class BossCutscene {
    constructor(canvas, { onComplete } = {}) {
      if (!canvas || typeof canvas.getContext !== 'function') {
        throw new TypeError('BossCutscene requires a canvas element.');
      }
      const context = canvas.getContext('2d');
      if (!context) throw new Error('Canvas2D is unavailable.');
      this.canvas = canvas;
      this._ctx = context;
      this._onComplete = typeof onComplete === 'function' ? onComplete : null;
      this._playing = false;
      this._disposed = false;
      this._elapsed = 0;
      this._duration = 4200;
      this._frame = null;
      this._token = 0;
      this._options = null;
      this._error = '';
      this._audio = null;
      this._width = 1;
      this._height = 1;
      this._dpr = 1;
      this._resize = this._resize.bind(this);
      this._resize();
      this._observer = typeof global.ResizeObserver === 'function'
        ? new global.ResizeObserver(this._resize) : null;
      if (this._observer) this._observer.observe(canvas);
      global.addEventListener('resize', this._resize, { passive: true });
    }

    /**
     * Call from a user click when enabling sound. Returns false and paints an
     * error if image is not loaded. The host supplies all name/BP metadata.
     * The canvas should have explicit CSS width and height (e.g. 100% / 100%).
     */
    play({ image, name = '', bpText = '', color = '#ed896c', duration = 4200,
      sound = false, reducedMotion = false } = {}) {
      if (this._disposed) throw new Error('BossCutscene has been disposed.');
      this._cancel();
      this._elapsed = 0;
      this._duration = Number.isFinite(Number(duration)) && Number(duration) > 0
        ? clamp(Number(duration), 250, 30000) : 4200;
      this._error = '';
      this._options = null;
      this._resize();
      if (!image || image.tagName !== 'IMG' || !image.complete ||
          !image.naturalWidth || !image.naturalHeight) {
        this._error = 'The character image is not ready.';
        this._drawError();
        return false;
      }
      this._ctx.save();
      this._ctx.fillStyle = '#ed896c';
      this._ctx.fillStyle = String(color);
      const accent = this._ctx.fillStyle;
      this._ctx.restore();
      const systemReduce = typeof global.matchMedia === 'function' &&
        global.matchMedia('(prefers-reduced-motion: reduce)').matches;
      this._options = {
        image, name: String(name || 'Spirit summon'), bpText: String(bpText || ''),
        color: accent, reducedMotion: Boolean(reducedMotion || systemReduce)
      };
      this._playing = true;
      const token = this._token;
      if (this._options.reducedMotion) {
        this._complete('complete', token);
        return true;
      }
      this._started = global.performance.now();
      this._draw(0);
      if (sound === true) this._startSound();
      this._schedule(token);
      return true;
    }

    skip() {
      if (!this._disposed && this._playing) this._complete('skipped', this._token);
    }

    getState() {
      return { playing: this._playing, elapsed: this._elapsed, duration: this._duration };
    }

    dispose() {
      if (this._disposed) return;
      this._cancel();
      this._disposed = true;
      if (this._observer) this._observer.disconnect();
      global.removeEventListener('resize', this._resize);
      this._observer = null;
      this._options = null;
      this._onComplete = null;
      this._ctx = null;
    }

    _cancel() {
      this._token += 1;
      this._playing = false;
      if (this._frame !== null) global.cancelAnimationFrame(this._frame);
      this._frame = null;
      this._cancelSound();
    }

    _schedule(token) {
      if (!this._playing || this._disposed || token !== this._token) return;
      this._frame = global.requestAnimationFrame((time) => {
        if (!this._playing || this._disposed || token !== this._token) return;
        this._frame = null;
        this._elapsed = clamp(time - this._started, 0, this._duration);
        if (this._elapsed >= this._duration) {
          this._complete('complete', token);
          return;
        }
        this._draw(this._elapsed / this._duration);
        this._schedule(token);
      });
    }

    _complete(reason, token) {
      if (!this._playing || this._disposed || token !== this._token) return;
      if (this._frame !== null) global.cancelAnimationFrame(this._frame);
      this._frame = null;
      this._playing = false;
      this._elapsed = this._duration;
      this._cancelSound();
      this._draw(1);
      // State is settled before calling the host; a callback may safely replay.
      if (this._onComplete) this._onComplete({ reason, ...this.getState() });
    }

    _resize() {
      if (this._disposed) return;
      const rect = this.canvas.getBoundingClientRect();
      const w = Math.max(1, rect.width || this.canvas.clientWidth || this._width);
      const h = Math.max(1, rect.height || this.canvas.clientHeight || this._height);
      const dpr = clamp(Number(global.devicePixelRatio) || 1, 1, 2);
      const backingW = Math.max(1, Math.round(w * dpr));
      const backingH = Math.max(1, Math.round(h * dpr));
      this._width = w;
      this._height = h;
      this._dpr = dpr;
      if (this.canvas.width !== backingW) this.canvas.width = backingW;
      if (this.canvas.height !== backingH) this.canvas.height = backingH;
      this._ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      if (this._error) this._drawError();
      else if (this._options) this._draw(this._playing ? this._elapsed / this._duration : 1);
      else this._ctx.clearRect(0, 0, w, h);
    }

    _begin() {
      const x = this._ctx;
      x.setTransform(this._dpr, 0, 0, this._dpr, 0, 0);
      x.globalAlpha = 1;
      x.globalCompositeOperation = 'source-over';
      x.shadowBlur = 0;
      x.shadowColor = 'transparent';
      x.setLineDash([]);
      x.clearRect(0, 0, this._width, this._height);
      return x;
    }

    _draw(progress) {
      if (!this._options || !this._ctx) return;
      const x = this._begin();
      const { image, color, reducedMotion } = this._options;
      const w = this._width, h = this._height, cx = w / 2;
      const pad = clamp(Math.min(w, h) * .045, 10, 42);
      const plateTop = h * .765;
      const artTop = Math.max(pad, h * .045);
      const artBottom = plateTop - pad * .65;
      const cy = (artTop + artBottom) / 2;
      const radius = Math.min(w * .43, h * .41);
      const p = reducedMotion ? 1 : clamp(progress);
      const reveal = smooth(p, .025, .20);
      const arrival = 1 - Math.pow(1 - smooth(p, .025, .22), 3);
      const burst = (1 - smooth(p, .22, .65)) * smooth(p, 0, .09);
      const idleTime = p * 4.2;

      x.fillStyle = '#050a12';
      x.fillRect(0, 0, w, h);
      let gradient = x.createRadialGradient(cx, cy, 0, cx, cy, Math.max(w, h) * .8);
      gradient.addColorStop(0, '#182433');
      gradient.addColorStop(.55, '#0b1321');
      gradient.addColorStop(1, '#03060d');
      x.fillStyle = gradient;
      x.fillRect(0, 0, w, h);
      x.save();
      x.globalAlpha = .22;
      gradient = x.createRadialGradient(cx, cy, 0, cx, cy, radius * 1.7);
      gradient.addColorStop(0, color);
      gradient.addColorStop(1, 'transparent');
      x.fillStyle = gradient;
      x.fillRect(0, 0, w, h);
      x.restore();
      for (const side of [0, w]) {
        gradient = x.createRadialGradient(side, h * .48, 0, side, h * .48, h * .65);
        gradient.addColorStop(0, 'rgba(38,108,182,.23)');
        gradient.addColorStop(1, 'rgba(38,108,182,0)');
        x.fillStyle = gradient;
        x.fillRect(0, 0, w, h);
      }

      // Source-inspired concentric summoning rings, with a calm final state.
      x.save();
      x.globalCompositeOperation = 'lighter';
      x.strokeStyle = color;
      for (let i = 0; i < 6; i += 1) {
        const phase = reducedMotion || p >= .72 ? (i + .55) / 6
          : ((idleTime * .27 + i / 6) % 1);
        x.globalAlpha = (.045 + Math.sin(phase * Math.PI) * .10) * (.6 + burst * .5);
        x.lineWidth = Math.max(1, radius * (.006 + phase * .018));
        x.beginPath();
        x.arc(cx, cy, Math.max(1, radius * (.30 + phase * 1.12)), 0, TAU);
        x.stroke();
      }
      x.globalAlpha = .36;
      x.lineWidth = 1;
      x.setLineDash([2, Math.max(7, radius * .045)]);
      x.beginPath();
      x.arc(cx, cy, radius * .86, 0, TAU);
      x.stroke();
      x.setLineDash([]);
      x.globalAlpha = .17;
      for (let i = 0; i < 36; i += 1) {
        const angle = i / 36 * TAU;
        const inner = radius * .925;
        const outer = radius * (i % 3 === 0 ? .97 : .945);
        x.beginPath();
        x.moveTo(cx + Math.cos(angle) * inner, cy + Math.sin(angle) * inner);
        x.lineTo(cx + Math.cos(angle) * outer, cy + Math.sin(angle) * outer);
        x.stroke();
      }
      // Broad beams stay behind the original character pixels.
      x.fillStyle = color;
      for (let i = 0; i < 22; i += 1) {
        const angle = i / 22 * TAU + random(i + 41) * .16 + idleTime * .012;
        const length = radius * (1.1 + random(i + 9) * .8);
        const spread = .008 + random(i + 63) * .018;
        x.globalAlpha = (.012 + burst * .045) * (.5 + random(i + 92));
        x.beginPath();
        x.moveTo(cx, cy);
        x.lineTo(cx + Math.cos(angle - spread) * length, cy + Math.sin(angle - spread) * length);
        x.lineTo(cx + Math.cos(angle + spread) * length, cy + Math.sin(angle + spread) * length);
        x.closePath();
        x.fill();
      }
      // A flat ring anchors the cutout to a summoning plane.
      for (let i = 0; i < 3; i += 1) {
        x.globalAlpha = (.16 - i * .035) * reveal;
        x.lineWidth = i === 0 ? 1.7 : 1;
        x.beginPath();
        x.ellipse(cx, artBottom - pad * .3, radius * (.70 + i * .10),
          Math.max(3, radius * (.075 + i * .013)), 0, 0, TAU);
        x.stroke();
      }
      x.restore();

      const fit = Math.min((w - pad * 2.4) / image.naturalWidth,
        (artBottom - artTop - pad * .7) / image.naturalHeight);
      const scale = Math.max(.0001, fit * (.91 + arrival * .09));
      const drawW = image.naturalWidth * scale;
      const drawH = image.naturalHeight * scale;
      const lift = reducedMotion ? 0 : (1 - arrival) * Math.min(22, h * .04);
      x.save();
      // The source image is drawn directly, without filters or pixel rewriting.
      x.globalAlpha = reveal;
      x.shadowColor = color;
      x.shadowBlur = Math.min(26, radius * .08) * reveal;
      x.drawImage(image, cx - drawW / 2, cy - drawH / 2 + lift, drawW, drawH);
      x.restore();

      // Sparse motes replace the reference's unrelated six-star level row.
      x.save();
      x.globalCompositeOperation = 'lighter';
      x.fillStyle = '#e5f4ff';
      for (let i = 0; i < 24; i += 1) {
        const px = pad + random(i + 110) * Math.max(1, w - pad * 2);
        const baseY = artTop + random(i + 195) * (artBottom - artTop);
        const py = baseY - (reducedMotion || p >= .72 ? 0 : idleTime * (2 + random(i) * 3));
        const pulse = reducedMotion || p >= .72 ? .35 : .3 + Math.sin(idleTime * 2 + i) * .17;
        x.globalAlpha = pulse * reveal;
        x.beginPath();
        x.arc(px, py, .7 + random(i + 265) * 1.4, 0, TAU);
        x.fill();
      }
      x.restore();

      gradient = x.createLinearGradient(0, h * .63, 0, h);
      gradient.addColorStop(0, 'rgba(3,7,14,0)');
      gradient.addColorStop(.5, 'rgba(3,7,14,.55)');
      gradient.addColorStop(1, 'rgba(3,7,14,.97)');
      x.fillStyle = gradient;
      x.fillRect(0, h * .63, w, h * .37);
      this._drawNameplate(plateTop, pad, smooth(p, .10, .25));

      // One short, low-opacity reveal wash; reduced motion never flashes.
      if (!reducedMotion && p > .095 && p < .175) {
        const flash = Math.sin((p - .095) / .08 * Math.PI) * .13;
        x.globalAlpha = flash;
        x.fillStyle = '#eaf4ff';
        x.fillRect(0, 0, w, h);
        x.globalAlpha = 1;
      }
    }

    _drawNameplate(top, pad, alpha) {
      const x = this._ctx, w = this._width, h = this._height;
      const width = Math.max(1, Math.min(w - pad * 2, 880));
      const left = (w - width) / 2;
      const height = Math.max(1, h - top - pad * .6);
      const color = this._options.color;
      x.save();
      x.globalAlpha = alpha;
      let gradient = x.createLinearGradient(left, 0, left + width, 0);
      gradient.addColorStop(0, 'rgba(14,26,45,0)');
      gradient.addColorStop(.12, 'rgba(17,33,56,.92)');
      gradient.addColorStop(.88, 'rgba(17,33,56,.92)');
      gradient.addColorStop(1, 'rgba(14,26,45,0)');
      x.fillStyle = gradient;
      x.fillRect(left, top, width, height);
      x.fillStyle = color;
      x.globalAlpha = alpha * .65;
      x.fillRect(left + width * .06, top, width * .88, 1);
      x.globalAlpha = alpha;
      x.textAlign = 'center';
      x.textBaseline = 'middle';
      const labelSize = clamp(height * .105, 8, 12);
      x.font = `600 ${labelSize}px system-ui, sans-serif`;
      x.fillStyle = '#c6d6e5';
      x.fillText('BOSS / SPIRIT', w / 2, top + height * .17, width * .86);
      const hasBP = Boolean(this._options.bpText);
      const titleSize = clamp(Math.min(w * .049, height * .245), 11, 38);
      let title = this._fitText(this._options.name, width * .87, titleSize, 11,
        '600', 'Georgia, "Times New Roman", serif', 2);
      if (hasBP && title.lines.length > 1 && title.size > height * .19) {
        title = this._fitText(this._options.name, width * .87,
          Math.max(11, height * .19), 11, '600', 'Georgia, "Times New Roman", serif', 2);
      }
      const titleCenter = top + height * (hasBP ? .46 : .56);
      x.font = `600 ${title.size}px Georgia, "Times New Roman", serif`;
      x.fillStyle = '#f5f8fc';
      title.lines.forEach((line, i) => x.fillText(line, w / 2,
        titleCenter + (i - (title.lines.length - 1) / 2) * title.size * 1.18));
      if (hasBP) {
        const bp = this._fitText(this._options.bpText, width * .79,
          clamp(height * .13, 9, 18), 8, '600', 'system-ui, sans-serif', 1);
        x.font = `600 ${bp.size}px system-ui, sans-serif`;
        const badgeW = Math.min(width * .88, Math.max(116, x.measureText(bp.lines[0]).width + 30));
        const badgeH = Math.max(14, height * .20);
        const badgeY = top + height * .79;
        x.fillStyle = 'rgba(23,57,91,.9)';
        x.fillRect(w / 2 - badgeW / 2, badgeY - badgeH / 2, badgeW, badgeH);
        x.strokeStyle = 'rgba(133,180,218,.55)';
        x.lineWidth = 1;
        x.strokeRect(w / 2 - badgeW / 2, badgeY - badgeH / 2, badgeW, badgeH);
        x.fillStyle = '#deecf7';
        x.fillText(bp.lines[0], w / 2, badgeY);
      }
      x.restore();
    }

    _fitText(text, maxWidth, initialSize, minSize, weight, family, maxLines) {
      const x = this._ctx;
      const value = String(text).replace(/\s+/g, ' ').trim();
      let size = initialSize, lines = [value];
      const wrap = () => {
        x.font = `${weight} ${size}px ${family}`;
        lines = [''];
        for (const character of Array.from(value)) {
          const last = lines.length - 1;
          if (lines[last] && x.measureText(lines[last] + character).width > maxWidth) {
            lines.push(character.trimStart());
          } else lines[last] += character;
        }
      };
      do { wrap(); if (lines.length <= maxLines) break; size -= 1; }
      while (size >= minSize);
      size = Math.max(size, minSize);
      if (lines.length > maxLines) {
        x.font = `${weight} ${size}px ${family}`;
        lines = lines.slice(0, maxLines);
        let tail = lines[maxLines - 1];
        while (tail && x.measureText(tail + '…').width > maxWidth) {
          tail = Array.from(tail).slice(0, -1).join('');
        }
        lines[maxLines - 1] = tail.trimEnd() + '…';
      }
      return { size, lines };
    }

    _drawError() {
      if (!this._ctx) return;
      const x = this._begin(), w = this._width, h = this._height;
      x.fillStyle = '#080f1b';
      x.fillRect(0, 0, w, h);
      x.fillStyle = '#edb4a0';
      x.textAlign = 'center';
      x.textBaseline = 'middle';
      x.font = `600 ${clamp(w * .035, 12, 24)}px system-ui, sans-serif`;
      x.fillText('Summon unavailable', w / 2, h * .46, w * .86);
      x.fillStyle = '#b6c4d5';
      x.font = `${clamp(w * .025, 10, 16)}px system-ui, sans-serif`;
      x.fillText(this._error, w / 2, h * .57, w * .86);
    }

    _startSound() {
      // A true flag alone must not bypass browser gesture restrictions.
      if (global.navigator.userActivation && !global.navigator.userActivation.isActive) return;
      const AudioContext = global.AudioContext || global.webkitAudioContext;
      if (!AudioContext) return;
      let audio, context;
      try {
        context = new AudioContext();
        const master = context.createGain();
        master.gain.value = .065;
        master.connect(context.destination);
        audio = { context, master, nodes: [master], sources: [] };
        this._audio = audio;
        const now = context.currentTime;
        const pace = Math.min(1, this._duration / 1800);
        const tone = (from, to, delay, length, level, type) => {
          const oscillator = context.createOscillator();
          const gain = context.createGain();
          oscillator.type = type;
          oscillator.frequency.setValueAtTime(from, now + delay * pace);
          oscillator.frequency.exponentialRampToValueAtTime(to, now + (delay + length) * pace);
          gain.gain.setValueAtTime(.0001, now + delay * pace);
          gain.gain.linearRampToValueAtTime(level, now + (delay + .025) * pace);
          gain.gain.exponentialRampToValueAtTime(.0001, now + (delay + length) * pace);
          oscillator.connect(gain);
          gain.connect(master);
          audio.sources.push(oscillator);
          audio.nodes.push(oscillator, gain);
          oscillator.start(now + delay * pace);
          oscillator.stop(now + (delay + length + .03) * pace);
        };
        tone(110, 42, 0, .65, .45, 'sine');
        tone(390, 620, .12, .56, .17, 'triangle');
        tone(930, 930, .28, .90, .07, 'sine');
        tone(1395, 1395, .28, .72, .035, 'sine');
        const resumed = context.resume();
        if (resumed && typeof resumed.catch === 'function') {
          resumed.catch(() => { if (this._audio === audio) this._cancelSound(); });
        }
      } catch (_) {
        if (audio && this._audio === audio) this._cancelSound();
        else if (context) {
          try {
            const closed = context.close();
            if (closed && typeof closed.catch === 'function') closed.catch(() => {});
          } catch (_) { /* Context construction can fail partway through. */ }
        }
      }
    }

    _cancelSound() {
      const audio = this._audio;
      if (!audio) return;
      this._audio = null;
      try {
        audio.master.gain.cancelScheduledValues(0);
        audio.master.gain.value = 0;
      } catch (_) { /* Context may already be closed. */ }
      for (const source of audio.sources) {
        try { source.stop(); } catch (_) { /* Already ended. */ }
      }
      for (const node of audio.nodes) {
        try { node.disconnect(); } catch (_) { /* Already disconnected. */ }
      }
      try {
        const closed = audio.context.close();
        if (closed && typeof closed.catch === 'function') closed.catch(() => {});
      } catch (_) { /* Audio support is optional. */ }
    }
  }

  global.BossCutscene = BossCutscene;
})(window);
