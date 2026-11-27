"""Synthesize the electronic slot and stage-clear sounds (spec 015 FR-021 to FR-023).

Standard library only. Writes 44.1 kHz 16-bit mono WAV files into
Assets/UnitySlotTower/Audio/Se/ (the folder SoundSetup.cs reads).
Usage: python -I scripts/audio/synth_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "UnitySlotTower", "Audio", "Se")


def note(name):
    """Frequency of a note such as 'C5' or 'F#4'."""
    names = {"C": -9, "C#": -8, "D": -7, "D#": -6, "E": -5, "F": -4, "F#": -3, "G": -2, "G#": -1, "A": 0, "A#": 1, "B": 2}
    pitch, octave = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((names[pitch] + (octave - 4) * 12) / 12)


def square(phase, duty=0.5):
    return 1.0 if (phase % 1.0) < duty else -1.0


def triangle(phase):
    p = phase % 1.0
    return 4 * p - 1 if p < 0.5 else 3 - 4 * p


class Track:
    def __init__(self, seconds):
        self.data = [0.0] * int(seconds * RATE)

    def tone(self, start, length, freq, wave_fn, volume=0.5, attack=0.004, decay=None, sweep_to=None, vibrato=0.0, duty=None):
        """Add one tone. decay: time constant of an exponential fade (None = flat with a short release)."""
        first = int(start * RATE)
        count = int(length * RATE)
        phase = 0.0
        release = min(0.02, length * 0.3)
        for i in range(count):
            t = i / RATE
            k = i / max(1, count - 1)
            f = freq if sweep_to is None else freq * (sweep_to / freq) ** k
            if vibrato:
                f *= 1 + vibrato * math.sin(2 * math.pi * 6 * t)
            phase += f / RATE
            env = min(1.0, t / attack) if attack > 0 else 1.0
            if decay is not None:
                env *= math.exp(-t / decay)
            left = length - t
            if left < release:
                env *= left / release
            sample = wave_fn(phase, duty) if duty is not None else wave_fn(phase)
            j = first + i
            if 0 <= j < len(self.data):
                self.data[j] += sample * volume * env

    def noise(self, start, length, volume=0.3, decay=0.02):
        rng = random.Random(7)
        first = int(start * RATE)
        for i in range(int(length * RATE)):
            j = first + i
            if j < len(self.data):
                self.data[j] += (rng.random() * 2 - 1) * volume * math.exp(-(i / RATE) / decay)

    def write(self, name, peak=0.8):
        top = max(1e-9, max(abs(s) for s in self.data))
        scale = peak / top
        path = os.path.normpath(os.path.join(OUT, name + ".wav"))
        with wave.open(path, "wb") as out:
            out.setnchannels(1)
            out.setsampwidth(2)
            out.setframerate(RATE)
            out.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * scale)) * 32767)) for s in self.data))
        print(path)


def bet():
    t = Track(0.14)
    t.tone(0.0, 0.045, note("B5"), square, 0.5, duty=0.25, decay=0.05)
    t.tone(0.05, 0.08, note("E6"), square, 0.5, duty=0.25, decay=0.05)
    t.write("synth_bet")


def lever():
    t = Track(0.26)
    t.noise(0.0, 0.03, 0.4, 0.008)
    t.tone(0.0, 0.14, 900, square, 0.6, duty=0.5, sweep_to=180, decay=0.08)
    t.tone(0.15, 0.1, note("C6"), square, 0.35, duty=0.25, sweep_to=note("G6"), decay=0.06)
    t.write("synth_lever")


def reel_loop():
    # 0.48 s = 6 ticks of 80 ms; each tick fades to silence so the loop is seamless.
    t = Track(0.48)
    pitches = ["C6", "G5", "E6", "G5", "D6", "G5"]
    for i, p in enumerate(pitches):
        t.tone(i * 0.08, 0.06, note(p), square, 0.5, attack=0.002, duty=0.25, decay=0.018)
    t.write("synth_reel_loop", peak=0.9)


def reel_stop():
    t = Track(0.12)
    t.noise(0.0, 0.02, 0.5, 0.004)
    t.tone(0.0, 0.1, note("A4"), square, 0.6, attack=0.001, duty=0.5, sweep_to=note("A3"), decay=0.035)
    t.write("synth_reel_stop")


def line_hit():
    t = Track(0.62)
    seq = ["C6", "E6", "G6"]
    for i, p in enumerate(seq):
        t.tone(i * 0.055, 0.07, note(p), square, 0.45, duty=0.25, decay=0.08)
        t.tone(i * 0.055, 0.07, note(p) / 2, triangle, 0.35)
    t.tone(0.165, 0.42, note("C7"), square, 0.45, duty=0.25, decay=0.25, vibrato=0.01)
    t.tone(0.165, 0.42, note("C6"), triangle, 0.4, decay=0.3)
    t.tone(0.165, 0.42, note("G6"), square, 0.18, duty=0.125, decay=0.2)
    t.write("synth_line_hit")


def line_fanfare():
    # Rising shimmer layered on the line hit.
    t = Track(0.6)
    t.tone(0.0, 0.38, 400, triangle, 0.5, attack=0.01, sweep_to=1800, vibrato=0.02)
    t.tone(0.0, 0.38, 800, square, 0.15, attack=0.01, duty=0.125, sweep_to=3600)
    for i, p in enumerate(["G6", "C7", "E7", "G7"]):
        t.tone(0.3 + i * 0.05, 0.12, note(p), square, 0.22, duty=0.25, decay=0.05)
    t.write("synth_line_fanfare")


def miss():
    t = Track(0.3)
    t.tone(0.0, 0.11, note("G4"), triangle, 0.6, decay=0.12)
    t.tone(0.12, 0.16, note("D4"), triangle, 0.6, decay=0.12, sweep_to=note("C#4"))
    t.write("synth_miss")


def victory():
    t = Track(1.9)
    lead = [("C5", 0.0, 0.1), ("E5", 0.11, 0.1), ("G5", 0.22, 0.1), ("C6", 0.33, 0.16),
            ("G5", 0.55, 0.1), ("C6", 0.66, 1.1)]
    for p, start, length in lead:
        long = length > 0.5
        t.tone(start, length, note(p), square, 0.42, duty=0.25, decay=0.6 if long else None, vibrato=0.012 if long else 0.0)
    # Harmony on the final chord (C major) and a bass line.
    t.tone(0.66, 1.1, note("E5"), square, 0.22, duty=0.125, decay=0.6)
    t.tone(0.66, 1.1, note("G5"), square, 0.18, duty=0.125, decay=0.6)
    for p, start, length in [("C3", 0.0, 0.32), ("G3", 0.33, 0.2), ("C4", 0.55, 0.1), ("C3", 0.66, 1.1)]:
        t.tone(start, length, note(p), triangle, 0.55, decay=0.8)
    t.noise(0.66, 0.05, 0.25, 0.015)
    t.write("synth_victory")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for make in (bet, lever, reel_loop, reel_stop, line_hit, line_fanfare, miss, victory):
        make()
