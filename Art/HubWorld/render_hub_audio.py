"""Render the plaza's seamless stereo ambience and mono fire bed."""
import wave
from pathlib import Path

import numpy as np

RATE = 24_000
DURATION = 32
SIZE = RATE * DURATION
TIME = np.arange(SIZE) / RATE
RNG = np.random.default_rng(228)
DESTINATION = Path(__file__).resolve().parents[2] / "Assets/Lumon/HubWorld/Audio"


def band(low, high):
    frequency = np.fft.rfftfreq(SIZE, 1 / RATE)
    shape = (frequency / (frequency + low))**3 / (1 + (frequency / high)**4)
    samples = np.fft.irfft(np.fft.rfft(RNG.normal(size=SIZE)) * shape, n=SIZE)
    return samples / np.std(samples)


def save(name, samples, channels):
    DESTINATION.mkdir(parents=True, exist_ok=True)
    pcm = np.rint(np.clip(samples, -1, 1) * 32767).astype("<i2")
    with wave.open(str(DESTINATION / f"{name}.wav"), "wb") as audio:
        audio.setparams((channels, 2, RATE, len(samples), "NONE", "not compressed"))
        audio.writeframes(pcm.tobytes())


def main():
    gust = 0.63 + 0.18 * np.sin(2*np.pi*TIME/DURATION * 3) + 0.1 * np.cos(2*np.pi*TIME/DURATION * 5)
    breeze = 0.041 * band(65, 460) + 0.011 * band(400, 2050)
    environment = [(breeze + 0.009 * band(98, 1030)) * gust for _ in range(2)]
    for beginning, pan, root in [(3.7, 0.31, 2300), (13.1, 0.66, 2950), (21.8, 0.43, 2600), (29.2, 0.74, 2900)]:
        for offset, ratio in [(0, 1), (0.17, 1.12), (0.38, 0.89)]:
            elapsed = np.arange(int(0.115 * RATE)) / RATE
            progress = elapsed / 0.115
            phase = 2*np.pi * root * ratio * (elapsed + 0.032 * np.sin(np.pi * progress))
            call = 0.016 * np.sin(phase) * np.sin(np.pi * progress)**1.8
            start = round((beginning + offset) * RATE)
            environment[0][start:start+len(call)] += (1-pan) * call
            environment[1][start:start+len(call)] += pan * call
    save("Observatory-Breeze", np.column_stack(environment), 2)

    bed = 0.031 * band(55, 350) + 0.024 * band(150, 3300)
    for _ in range(228):
        length = RNG.integers(90, 750)
        t = np.arange(length) / RATE
        envelope = np.minimum(t / 0.00075, 1) * np.exp(-t * RNG.uniform(210, 940))
        envelope *= np.linspace(1, 0, length)
        crackle = RNG.uniform(0.019, 0.25) * envelope * (
            0.7 * RNG.uniform(-1, 1, length) + 0.3 * np.sin(t * RNG.uniform(3800, 19600))
        )
        start = RNG.integers(0, SIZE)
        bed[(start + np.arange(length)) % SIZE] += crackle
    save("Observatory-Campfire", bed, 1)


if __name__ == "__main__":
    main()
