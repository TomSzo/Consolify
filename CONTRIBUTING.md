# Contributing to Consolify

Thank you for wanting to help. Consolify is in its **pre-alpha** stage: the design is being discussed
and the first code is being written. Right now, ideas and knowledge are worth as much as code.

## Ways to help

### 1. Share knowledge (no coding needed)

- **Game configuration:** where a game stores its settings, which `.ini` values it actually respects,
  and which settings match the console version.
- **Display behavior:** VRR range, whether VRR survives a dock or an adapter, TV game-mode quirks.
- **What breaks:** games that ignore config overrides, anti-cheat that reacts to changes,
  upscaler combinations that conflict.

Open an issue and describe what you know and how you know it (tested yourself, official docs,
a measurement, a trusted source).

### 2. Measure (once measurement tooling exists)

Profiles are accepted on **measurements, not opinions**. When the measurement mode is ready, you will
be able to submit anonymous results: hardware, display, settings and the PresentMon summary.

### 3. Write code

The stack is **C# / .NET 8** on Windows. Areas that will need help:

- hardware adapters (AMD ADLX, NVIDIA NVAPI, Intel IGCL)
- engine modules (Unreal Engine 5 first)
- display detection (EDID parsing, VRR state)
- the plain-language user interface

Before starting a larger change, please open an issue so we can agree on the approach.

## Ground rules

1. **Never uglier than on a console, never stuttering.** Every proposal is judged against this rule.
2. **No injection into anti-cheat games.** Accounts must never be put at risk.
3. **Everything reversible.** Any change to a user's system or game files needs a backup and a restore path.
4. **No bundled third-party binaries.** Download official releases with pinned versions and checksums.
5. **Respect licenses.** Code taken from another project must be license-compatible with GPL-3.0
   and credited in the file.
6. **Plain language for players.** Technical detail belongs behind "Details", never in the main message.

## Language

Everything in this repository — code, comments, issues, pull requests — is in **English**,
so that anyone can take part.

## License

By contributing, you agree that your contributions are licensed under the [GPL-3.0](LICENSE).
