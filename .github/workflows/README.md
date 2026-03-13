# GitHub Actions Workflows for Goo Galaxy

This directory contains the CI workflows for Goo Galaxy. They are aligned with the current project strategy described in the GDD: Unity 6, MonoBehaviour/GameObject architecture, Netcode for GameObjects, and a GitHub Flow model centered on `main`.

## Structure

```text
.github/workflows/
├── pr-check.yml               # PR title validation
├── format-check.yml           # Repository format validation
├── unity-build-android.yml    # Android build validation
├── unity-build-ios.yml        # iOS build validation
├── unity-tests-editmode.yml   # Edit Mode tests
├── unity-tests-playmode.yml   # Play Mode tests
└── README.md
```

```text
.github/actions/
├── unity-build/               # Shared Unity build flow used by Android and iOS
└── unity-test/                # Shared Unity test flow used by Edit Mode and Play Mode
```

## Workflows

### PR Check

- File: `pr-check.yml`
- Trigger: `pull_request_target`
- Status check: `PR Check`
- Purpose: enforce `type(scope): subject` PR titles for small, readable GitHub Flow PRs.

### Format Check

- File: `format-check.yml`
- Trigger: `pull_request` on repository content and `push` to `main`
- Status check: `Format Check`
- Purpose: run the repository formatting checks from `package.json`, respecting `.formatterignore`.

### Android Build

- File: `unity-build-android.yml`
- Trigger: `pull_request` to `main`, plus `workflow_dispatch`
- Status check: `Build Android Player`
- Runner: `ubuntu-latest`
- Output: Android App Bundle artifact under `build/Android`

### iOS Build

- File: `unity-build-ios.yml`
- Trigger: `pull_request` to `main`, plus `workflow_dispatch`
- Status check: `Build iOS Player`
- Runner: `macos-latest`
- Output: exported iOS player project artifact under `build/iOS`

### Edit Mode Tests

- File: `unity-tests-editmode.yml`
- Trigger: `pull_request` to `main`, plus `workflow_dispatch`
- Status check: `Unity Edit Mode Tests`
- Purpose: validate deterministic board, card, and match logic plus authored data checks.

### Play Mode Tests

- File: `unity-tests-playmode.yml`
- Trigger: `pull_request` to `main`, plus `workflow_dispatch`
- Status check: `Unity Play Mode Tests`
- Purpose: validate scene-level integration and runtime flows.

## Shared CI Variables

These workflows require repository-level GitHub Variables. If any required variable is missing or empty, the workflow fails before running the main CI steps.

Required variables:

- `UNITY_VERSION`: expected value `6000.3.11f1`
- `UNITY_CACHE_VERSION`: expected invalidation token such as `v1`
- `UNITY_COVERAGE_OPTIONS`: current runtime coverage filters and reports
- `NODE_VERSION`: current Node.js version used by formatting validation
- `CI_FETCH_DEPTH`: fetch depth for repository checkout
- `ARTIFACT_RETENTION_DAYS`: artifact retention period in days

The Unity build and test workflows validate their required variables explicitly, and the format workflow does the same for `NODE_VERSION`.

## Required Secrets

All Unity workflows use these secrets:

- `UNITY_LICENSE`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`

The iOS workflow currently exports the iOS project only. If you later want signed `.ipa` distribution, add a second signing/archive stage with Apple credentials and provisioning assets.

## Ruleset Integration

The `main` branch ruleset is designed to require these exact job names:

- `PR Check`
- `Format Check`
- `Build Android Player`
- `Build iOS Player`
- `Unity Edit Mode Tests`
- `Unity Play Mode Tests`

Use the job names above in GitHub Rulesets, not the combined `Workflow / Job` labels shown in the UI.

## Notes

- Unity version is pinned to `6000.3.11f1`, matching `ProjectSettings/ProjectVersion.txt`.
- Android and iOS builds remain split so the required checks and platform-specific runners stay explicit.
- The duplicated Unity build and test steps live in shared composite actions under `.github/actions/`, while checkout and Git LFS setup stay inline in each Unity workflow because local actions cannot run before repository checkout.
- The iOS build uses a macOS runner because Unity iOS export depends on Apple tooling.
- Build and test jobs now include explicit concurrency and timeout controls to reduce stale runs and hung jobs.
