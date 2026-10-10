# Consolify

**A console-like experience on any PC — decided for you, not by a wall of settings.**

> **Status: pre-alpha.** There is no usable build yet. This repository holds the project's
> manifesto and design, and is open for discussion and contributors.

---

## The problem

Unless you own a top-of-the-line GPU, PC gaming means acronyms, technologies and sliders.
Everyone struggles with something different:

- The desk player has too few frames. Push the settings up and the game suddenly looks awful.
  The screen tears, and nobody ever explained what VRR is.
- The couch player has plenty of frames, yet the game looks worse than on a console.
  They just wanted to play — they skipped the console because their PC is powerful.

A console never asks these questions. The developer decides for the player, and the result is
**both good-looking and stable**. The console knows the screen, and there is nothing to break.

## The goal

Make playing a game on any PC a pleasure, knowing that the machine in front of you delivers
**the best console-like experience it possibly can**. Then only the people who truly need new
hardware will upgrade.

## What Consolify does

Consolify detects your hardware, your display and your game, and then configures the game the way
a good console developer would. It explains every change in plain language, and every change can be
undone with one click.

The knowledge — which settings work for which game, on which hardware, on which display — is built
**by the community, from measurements, not opinions**.

## Principles

1. **Never uglier than on a console, never stuttering.** This one rule decides everything else.
2. **Image quality floor = console level.** Consolify never picks settings below the game's
   PS5 / Xbox Series X equivalent, where that equivalent is known.
3. **Stability matched to the display.** Inside the VRR window with VRR, perfectly paced without it.
4. **Decide, don't ask.** No "do you prefer stutter or blur?" questions. A console avoids both — so do we.
5. **Detect everything that can be detected.** The only question: how far do you sit from the screen?
6. **Plain language.** Jargon only lives behind a "Details" button.
7. **Honest hardware verdict.** If a game can't hold even a locked 40 FPS at console quality on your PC,
   Consolify says so and names the bottleneck. No false hope, no upselling.
8. **Everything is reversible.** Every change is backed up first.
9. **Measurements, not opinions.** A profile only enters the community database with measured data.

## How it decides

Consolify tunes the image so the game averages about 60 FPS **without** a frame cap, measures the
1% low, and then caps the frame rate one frame below it. The game finishes almost every frame before
the cap's deadline, so frame times become a flat line — perfectly smooth with VRR. Any headroom goes
into image quality, never below console level.

| Tier | Condition (uncapped 1% low) | Frame cap | Example |
|---|---|---|---|
| Full experience, with VRR | ≥ VRR minimum + 1, and at least 41 | 1% low − 1, at most 60 | 48–120 Hz panel: 49 · 40–120 Hz TV: 41 |
| Full experience, without VRR | ≥ 61 | 60 | 60 or 120 Hz display |
| Locked 40 (a stated compromise) | ≥ 41 | 40 | only at 120 Hz, or with VRR that shows 40 directly or through frame doubling (LFC) |
| Below that | – | – | honest hardware verdict — there is no 30 FPS mode |

The VRR minimum + 1 floor keeps the picture inside the display's VRR window. "At least 41" matters for
displays whose window reaches down to 1 Hz (G-Sync modules), where the rule would otherwise accept
anything. A fluctuating frame rate below the VRR window means judder, tearing or flicker, so there is no
experience there. A locked 40 is even and feels closer to 60 than to 30, so it is the only fallback —
and Consolify says it is a compromise.

Frame generation is only offered **after** the full experience is reached, and only with the player's
consent — together with what it adds and how much latency it costs.

**Frame times matter, not the average.** With a cap, the average FPS always sits near the cap — even
while the game stutters — so the average is informational only. Decisions are made on frame times:

| Metric | What it shows | Role |
|---|---|---|
| 1% low (99th percentile frame time) | 99% of frames finish within this time | **main condition** |
| Missed refreshes (without VRR) | what the eye sees on a fixed-refresh display | **main condition without VRR** |
| 0.1% low and spike count (≥ 2× the median) | one-off hitches: shader compilation, loading | stutter warning |
| Change between consecutive frames | frame pacing | pacing warning |
| Average FPS | how many frames arrive in total | informational only |

If a game is full of spikes, Consolify does not lower the image quality — it tells the player the game
itself stutters. Spike and pacing thresholds are starting values, to be tuned with real measurements.

**Latency never costs image quality.** A desk player with a mouse feels latency more than a couch player
with a controller — but a faster, uglier picture would break the experience Consolify exists for. The
monitor is also closer, so flaws show more. Image quality and frame cap therefore follow the same rules on
a monitor and on a TV. Latency is reduced only with tools that cost nothing in the image: Anti-Lag /
Reflex and a good (preferably in-game) frame limiter. With a mouse, techniques that add a frame of latency
(Auto SR, frame generation) are only enabled with the player's explicit consent.

**Viewing distance matters.** Consolify uses *pixels per degree* (PPD) — how many pixels fall into one
degree of your field of view. A 65" 4K TV at 3 m is ~140 PPD: aggressive upscaling is invisible there.
A 24" 1080p monitor at 60 cm is ~40 PPD: it needs a much higher internal resolution.

## What it is — and isn't

| Yes | No |
|---|---|
| AA and AAA single-player and co-op games | Esports and competitive multiplayer |
| Desktops, laptops, handhelds | Chasing benchmark numbers for their own sake |
| Monitors, TVs, built-in screens | Overclocking or voltage tuning |
| Windows 11 first | Injecting anything into games with anti-cheat |

## Architecture (planned)

- **Detection** — hardware, display (EDID: VRR range, TV or monitor), active sync, input device, running game, anti-cheat.
- **Decision engine** — console quality floor, frame cap from the measured 1% low, locked 40 fallback, sync rules, PPD.
- **Appliers** — each one backs up and can roll back:
  - **System:** VRR, frame cap, V-Sync, Windows game settings
  - **Hardware adapters:** AMD (ADLX), NVIDIA (NVAPI), Intel (IGCL), handheld TDP
  - **Upscaler layer:** in-game upscalers, OptiScaler, Windows Auto SR, frame generation
  - **Engine modules:** Unreal Engine 5 first, then UE4 and others
- **Measurement** — [PresentMon](https://github.com/GameTechDev/PresentMon): average, 1% low, 0.1% low, missed vblanks.
- **Community profiles** — game profiles as YAML files plus opt-in, anonymous measurements.

**Stack:** C# / .NET 8. Third-party tools (OptiScaler, PresentMon) are downloaded from their official
releases with pinned versions and checksums — never bundled.

## Roadmap

1. **System fix-up** — useful in every game on day one: VRR, frame cap, V-Sync, display detection,
   plain-language explanations, one-click restore.
2. **Measurement** — PresentMon integration and evaluation against the rules above.
3. **Unreal Engine 5 module** — one module, many games.
4. **Hardware adapters** — AMD first, then NVIDIA and Intel.
5. **Community profile repository** and measurement submissions.
6. **More engines** — driven by community demand.

First test platform: ROG Xbox Ally X (AMD Ryzen AI Z2 Extreme, 1080p 120 Hz VRR), handheld and docked.

## Contributing

The code is the easy part — the knowledge is the hard part. You can help even without writing code:
measure games, describe config files, report what works on your hardware.
See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[GPL-3.0](LICENSE). Consolify is free software: you may use, modify and share it, and modified
versions must stay open as well. It comes with no warranty.

Consolify is an independent project. AMD, NVIDIA, Intel, Microsoft, Xbox, PlayStation and all game
names are trademarks of their respective owners and are used only to describe compatibility.
