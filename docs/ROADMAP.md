# Iteration Roadmap

User-approved order, 2026-10-22. Each version is implemented and verified separately.
Release publication is confirmed only after the GitHub release and assets exist.

## v0.4.0 - Desktop Workflow (Released)

- [x] Fixed question, reading, meaning, rating and navigation regions.
- [x] Tray and context-menu access to settings, library, pause and recovery.
- [x] Browse mode without review updates; due-review mode with explicit ratings.
- [x] Pause while hovering, interacting with menus/dialogs, hidden, or awaiting a rating.
- [x] Previous word, next word, one-round repeat avoidance and undo last rating.
- [x] Atomic XML saves, rolling backups, daily snapshots and corruption recovery.
- [x] Configuration, storage, WPF workflow and real global-key regression tests.
- [x] PR, CI and release verification (PR #3, v0.4.0).

## v0.5.0 - Library and Learning Records (In Development)

- [x] Named multi-unit practice presets: create, update, rename, delete, apply.
- [x] Draft-only settings edits, legacy config and missing-unit compatibility.
- [x] Import preview with missing-field and duplicate checks before confirmation.
- [x] Stable word identities and individual review events.
- [x] Separate progress for recognition, recall and reading directions.
- [x] Full-version compatibility, backups, native desktop and 2142-entry source integrity checks.
- [ ] Final PR CI, review and release verification.

All feature modules are implemented; full-version verification and publication
remain distinct steps. See V0.5-SPEC.md.

## v0.6.0 - Japanese Learning Content (In Development)

- [x] Japanese pronunciation with manual playback by default and speech rate control.
- [x] Editable/importable examples and source information; schema 2 backups.
- [x] Daily rated-card counts, due counts and weak-word views.
- [ ] Full desktop verification, PR CI and authorized release publication.
- Evaluate an established FSRS implementation after review history is available;
  keep the current scheduler if compatibility or evidence is insufficient.

## References

- [ToastFish](https://github.com/Uahh/ToastFish)
- [Desktop Floating Vocabulary Card](https://github.com/whymewhynot05/desktop-floating-vocabulary-card)
- [Anki](https://github.com/ankitects/anki)
- [Yomitan](https://github.com/yomidevs/yomitan)

The project remains C# / WPF. References inform interaction and data design.
