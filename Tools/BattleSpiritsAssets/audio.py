#!/usr/bin/env python3
"""Reproduce the original Battle Spirits sound bank without third-party samples."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import struct
import wave

RATE = 22050
ROOT = Path(__file__).resolve().parents[2]


def fade(t, duration, attack=0.012, release=0.08):
    return min(1.0, t / attack, max(0.0, (duration - t) / release))


def synth(cue, duration):
    randomizer = random.Random("LaoDai-BattleSpirits-" + cue)
    result = []
    phases = [0.0, 0.0, 0.0]
    chord = [220.0, 261.625565, 329.627557, 392.0]
    for index in range(round(duration * RATE)):
        t = index / RATE
        progress = t / duration
        if cue == "music":
            beat = t % 0.5
            note = chord[int(t * 2) % len(chord)]
            pad = sum(math.sin(2 * math.pi * pitch * t) for pitch in (110, 164.813778, 220)) * 0.14
            pluck = math.sin(2 * math.pi * note * beat) * math.exp(-beat * 11) * 0.32
            shimmer = math.sin(2 * math.pi * (note * 2) * beat) * math.exp(-beat * 18) * 0.08
            value = (pad + pluck + shimmer) * fade(t, duration, 0.2, 0.2)
        else:
            if cue == "core":
                frequencies = (1046.5, 1568.0, 2093.0)
                envelope = math.exp(-t * 13)
                noise = 0
            elif cue in ("victory", "defeat"):
                scale = [261.6256, 329.6276, 391.9954, 523.2511] if cue == "victory" else [329.6276, 293.6648, 261.6256, 164.8138]
                note = scale[min(3, int(progress * 4))]
                frequencies = (note, note * 1.5, note * 2)
                envelope = 0.45 + 0.55 * (1 - progress)
                noise = 0
            elif cue in ("summon", "boss_summon"):
                base = 95 if cue == "boss_summon" else 210
                sweep = base + 780 * progress ** 1.3
                frequencies = (sweep, sweep * 1.5, base * 0.5)
                envelope = math.sin(math.pi * progress) ** 0.55
                noise = randomizer.uniform(-1, 1) * 0.11 * (1 - progress)
            elif cue == "attack":
                frequencies = (180 - 130 * progress, 290 - 160 * progress, 65)
                envelope = math.exp(-t * 7)
                noise = randomizer.uniform(-1, 1) * 0.42 * envelope
            else:
                frequencies = (90 - 40 * progress, 145, 47)
                envelope = math.exp(-t * (7 if cue == "damage" else 4))
                noise = randomizer.uniform(-1, 1) * 0.35 * envelope
            for part, frequency in enumerate(frequencies):
                phases[part] += 2 * math.pi * frequency / RATE
            tone = sum(math.sin(phase) * weight for phase, weight in zip(phases, (0.4, 0.2, 0.16)))
            value = (tone * envelope + noise) * fade(t, duration)
        result.append(value)
    # Remove DC and leave headroom. Exact zero boundaries avoid playback clicks.
    mean = sum(result) / len(result)
    result = [value - mean for value in result]
    peak = max(abs(value) for value in result)
    scale = 0.65 / max(1, peak)
    pcm = [max(-32767, min(32767, round(value * scale * 32767))) for value in result]
    pcm[0] = pcm[-1] = 0
    return pcm


def generate(destination):
    destination.mkdir(parents=True, exist_ok=True)
    lengths = {"music": 16.0, "summon": 0.85, "boss_summon": 1.5, "attack": 0.48,
               "core": 0.25, "damage": 0.48, "destroy": 0.8, "victory": 2.4, "defeat": 2.0}
    items = []
    for cue, duration in lengths.items():
        pcm = synth(cue, duration)
        path = destination / (cue + ".wav")
        with wave.open(str(path), "wb") as output:
            output.setnchannels(1)
            output.setsampwidth(2)
            output.setframerate(RATE)
            output.writeframes(struct.pack("<" + "h" * len(pcm), *pcm))
        display_path = str(path.relative_to(ROOT)) if ROOT in path.parents else path.name
        items.append({"cue": cue, "path": display_path, "sampleRate": RATE, "channels": 1,
                      "bits": 16, "seconds": duration, "peak": round(max(abs(x) for x in pcm) / 32768, 6),
                      "dc": round(sum(pcm) / len(pcm) / 32768, 8),
                      "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    manifest = {"version": 1, "creator": "LaoDai060204 project procedural synthesis",
                "source": "Original mathematically synthesized tones and deterministic noise; no third-party recordings.",
                "license": "CC0-1.0", "musicLoop": "16 seconds, soft endpoint fades", "files": items}
    (destination / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    return manifest


if __name__ == "__main__":
    arguments = argparse.ArgumentParser(description=__doc__)
    arguments.add_argument("--output", type=Path, default=ROOT / "Sound/BattleSpirits")
    parsed = arguments.parse_args()
    result = generate(parsed.output.resolve())
    print(json.dumps({"files": len(result["files"]), "output": str(parsed.output)}, indent=2))
