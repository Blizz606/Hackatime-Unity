# Changelog

## [1.0.2] - 2026-11-18

- Track editor activity from selection, Inspector/Undo changes, Project window, Hierarchy, Scene view, and scene changes.
- Queue an activity heartbeat at the next allowed 30-second interval instead of discarding it.
- Keep the documented Hackatime 30-second heartbeat rate limit.

## [1.0.1] - 2026-11-14

- Open a one-time setup screen after installation when no API key is configured.
- Add a direct link to Hackatime from the setup screen.
- Add Package Manager links to documentation, changelog, and license.

## [1.0.0] - 2026-09-16

- Initial release.
- Hackatime heartbeat tracking.
- Unity activity tracking with throttling and queueing.
- Git branch and `.wakatime-project` detection.
- Editor settings window and connection test.
