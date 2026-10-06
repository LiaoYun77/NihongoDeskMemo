# Development and Release Rules

- Work on a short-lived feature/fix branch; do not upload directly to main.
- Every user-facing iteration updates src/VersionInfo.cs and CHANGELOG.md. Use a minor bump for features and a patch bump for fixes; document incompatible data changes explicitly.
- Preserve existing words.xml/config.xml and review history. Exclude private textbooks, generated full vocabularies, secrets, and user backups from Git and releases.
- Run build.bat -RunTests and tools/package.ps1 before opening a PR. UI changes require a rendered settings check; native global-key tests require an interactive desktop and must not send keys in unattended CI. These tests send F10-F12 and Ctrl+Shift+Q; close running copies first.
- Open a PR describing the user-visible changes, compatibility, and actual test results. Wait for CI success before merging. Do not weaken or skip checks to make a release pass.
- Merge only when release/iteration work is authorized. A main-branch push runs the release workflow, which publishes a new version once and never overwrites an existing release.
- Verify the release tag, assets, and remote commit before reporting it as published. Distinguish local builds, open PRs, merged changes, and published releases.
- Treat docs/RELEASING.md as the workflow reference. Do not rewrite existing release tags or force-push shared branches.
