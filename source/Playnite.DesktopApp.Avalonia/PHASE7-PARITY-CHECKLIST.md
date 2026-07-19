# Phase 7 parity sign-off checklist

Gate for making the Avalonia shells the default product. Two bars:
- **opt-in bar** — must pass before the Avalonia shells are offered behind a flag.
- **default-on bar** — must pass before the flag defaults on (WPF kept as fallback one release).

Legend: [x] verified automated · [~] partially verified · [ ] needs manual/hardware run.

## Automated (re-runnable, CI-able)

- [x] Full solution builds Debug+Release x64 (VS MSBuild), 0 warnings under TreatWarningsAsErrors + NuGetAudit.
- [x] Core test suite 294/294 (`Playnite.Tests`).
- [x] SDK v7 suites green (`Playnite.SDK.V7.Tests`, `Playnite.SDK.V7.Host.Tests`, `Playnite.Avalonia.App.V7.Tests`).
- [x] Fullscreen `--self-test` 20/20.
- [x] Desktop `--self-test` 81/81.
- [x] Desktop `--plugin-compatibility-test` against a cloned real library: SDK v7 Steam reference plugin PASS (load, settings, 21-game import, controllers, library metadata, client detection).
- [ ] Same compatibility run across the full installed extension set on a real profile (needs a profile with the community addons installed).

## Manual — controller / hardware (default-on bar)

- [ ] Gamepad end-to-end on real hardware: navigation, hold-repeat, A/B/X/Y/shoulders/triggers, on-screen keyboard, guide button. (Automated only covers the synthetic bridge.)
- [ ] Controller hotplug (connect/disconnect mid-session) — no stuck buttons.
- [ ] Theme audio on a theme that ships navigation/activation/background assets.

## Manual — visual / theme (default-on bar)

- [ ] Default Desktop + Fullscreen themes render correctly at 100/125/150/200% DPI.
- [ ] Multi-monitor: window placement, maximize, fullscreen span, per-monitor DPI.
- [ ] At least 3 community themes ported to theme API 3 render without fallback.
- [ ] Localization: a RTL language (Arabic/Hebrew) and a CJK language (fonts, layout, input).

## Manual — system integration (default-on bar)

- [ ] `playnite://` URI activation from browser/OS opens the running instance.
- [ ] Single-instance: launching a second time forwards args to the running instance (pipe).
- [ ] Minimize/close to tray; tray menu recent/favorite launch, restore, exit.
- [ ] Mode switch Desktop <-> Fullscreen (process restart path) round-trips settings and library.
- [ ] Backup and restore (settings, library, extensions, themes) round-trip.
- [ ] Safe mode (disable extensions/themes) launches.
- [ ] Portable install layout works (no absolute-path assumptions).
- [ ] HDR toggle enables on launch and restores the original state on exit (incl. overlapping controlled games).

## Config / migration (blocks default-on — see plan 7.2)

- [ ] Avalonia shells read the canonical PlayniteSettings (theme, library path, window state) so users keep one config across the switch.
- [ ] First-run migration of window positions and fullscreen video modes.
- [ ] Crash reporter + Diagnostic package produced correctly from the Avalonia shells (exe names in the MD5 manifest updated).

## Notes

- Prerequisite landed: the WPF compatibility Application is now a process-lifetime singleton (commit 3746a461), required because cutover re-inits the runtime host in-process on mode switch.
- The library-metadata compatibility probe is time-bounded (60s) so a provider needing a live service session cannot stall the run.
