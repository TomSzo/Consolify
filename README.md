# Consolify

**A console-like experience on any PC — decided for you, not by a wall of settings.**

> **Status: paused.** Development is on hold for now; there is no active work and no release
> schedule. The code, the design and the measuring probe stay here as they are, and the project
> may pick up again later. Issues and discussion are still welcome, but replies may be slow.
>
> The project is pre-alpha: the probe works, but there is no complete Consolify build yet.

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
1% low of steady play, and then caps the frame rate one frame below it. The game finishes almost every frame before
the cap's deadline, so frame times become a flat line — perfectly smooth with VRR. Any headroom goes
into image quality, never below console level.

| Tier | Condition (uncapped steady 1% low) | Frame cap | Example |
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
| 1% low of steady play (99th percentile frame time) | 99% of frames finish within this time | **main condition** |
| Missed refreshes (without VRR) | what the eye sees on a fixed-refresh display | **main condition without VRR** |
| Hitches (frames ≥ 2× slower than the second before) | shader compilation, streaming, loading | reported separately, stutter warning |
| GPU busy share of the frame time | whether the GPU or the CPU holds the frame rate back | tells what can help |
| Change between consecutive frames | frame pacing | pacing warning |
| Average FPS | how many frames arrive in total | informational only |

**Stutter never costs image quality either.** Hitches are the game's fault, and lowering the image
quality does not fix them. So Consolify groups slow frames that come less than a second apart into one
hitch, leaves each hitch (plus half a second around it) out of the decision, and decides the cap from
the steady play that remains. A gradually heavier scene is not a hitch: only sudden jumps against the
second before count. The hitches are still listed with their time and worst frame, and a game that
hitches often is called out as stuttery. If less than 60% of a capture is steady play, Consolify does
not decide and asks for a new capture. These thresholds are starting values, to be tuned with real
measurements.

When the full experience is out of reach, the GPU busy share tells the player what could help: if the
GPU is the limit, lighter graphics settings; if the CPU is, lowering the resolution will not help. Once
the settings are at their lowest, the honest answer is that the hardware is the limit for this game.

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
- **Measurement** — [PresentMon](https://github.com/GameTechDev/PresentMon): frame times, steady-play 1% low, hitches, GPU/CPU bottleneck.
- **Community profiles** — game profiles as YAML files plus opt-in, anonymous measurements.

**Stack:** C# / .NET 8. Third-party tools (OptiScaler, PresentMon) are downloaded from their official
releases with pinned versions and checksums — never bundled.

## Roadmap

1. **System fix-up** — useful in every game on day one: VRR, frame cap, V-Sync, display detection,
   plain-language explanations, one-click restore.
2. **Measurement** — PresentMon integration and evaluation against the rules above. *Working in the probe.*
3. **Unreal Engine 5 module** — one module, many games.
4. **Hardware adapters** — AMD first, then NVIDIA and Intel.
5. **Community profile repository** and measurement submissions.
6. **More engines** — driven by community demand.

First test platform: ROG Xbox Ally X (AMD Ryzen AI Z2 Extreme, 1080p 120 Hz VRR), handheld and docked.

## Try the probe

`consolify-probe` shows what Consolify detects and decides. It reads and measures, and changes a file only
with `--apply`, after a backup. Download `consolify-probe.exe` from the
[latest release](https://github.com/TomSzo/Consolify/releases/latest): one file, no install, no .NET needed.

```
consolify-probe                         display report: VRR range, TV or monitor, pixels per degree
consolify-probe measure --app Game.exe  record 60 s with PresentMon (asks for admin rights) and decide
consolify-probe analyze capture.csv     decide from an existing PresentMon capture
consolify-probe measure --app Game.exe --apply   ...and write the cap to the game's OptiScaler.ini
consolify-probe restore --app Game.exe  put back the file as it was before --apply
consolify-probe display set --size 83   tell the real TV size (TVs often report a wrong one)
```

First results on the ROG Xbox Ally X, docked to an LG TV with a 40–120 Hz VRR window:

| Game | Settings | Steady 1% low | Decision |
|---|---|---|---|
| Control | FSR 4 Performance (via OptiScaler) | 51.9 FPS | full experience, cap at 50 FPS |
| Gears of War: E-Day | everything Low, FSR 4 Ultra Performance | 36.3 FPS | not enough — GPU-bound at the lowest settings |

## Contributing

The code is the easy part — the knowledge is the hard part. You can help even without writing code:
measure games, describe config files, report what works on your hardware.
See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[GPL-3.0](LICENSE). Consolify is free software: you may use, modify and share it, and modified
versions must stay open as well. It comes with no warranty.

Consolify is an independent project. AMD, NVIDIA, Intel, Microsoft, Xbox, PlayStation and all game
names are trademarks of their respective owners and are used only to describe compatibility.
