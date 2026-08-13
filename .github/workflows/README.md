# GitHub Actions Workflows for Goo Galaxy

This directory contains the CI workflows for Goo Galaxy. They are aligned with the current project strategy described in the GDD: Unity 6, MonoBehaviour/GameObject architecture, Netcode for GameObjects, and a GitHub Flow model centered on `main`.

## Structure

```text
.github/workflows/
├── pr-check.yml               # PR title validation
├── format-check.yml           # Repository format validation
├── codeql.yml                 # CodeQL SAST security analysis
├── secret-scan.yml            # Betterleaks full-history secret scanning
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
- Status checks: `Format Check` and `EditorConfig Check` — two jobs in one workflow, running in parallel
- Purpose: `Format Check` runs CSharpier and Prettier from `package.json` on the runner, respecting `.formatterignore`. `EditorConfig Check` validates everything `.editorconfig` declares — the naming matrix, the IDE severities, and the 42 Unity analyzer rules in section 6.
- **`EditorConfig Check` needs a Unity Editor, which is why it costs minutes rather than seconds.** `.editorconfig` reaches a compiler only through `dotnet format`, and `dotnet format` reads the `.csproj` that Unity generates and never commits. Those files also carry absolute paths into the Unity installation — a single assembly's `.csproj` holds roughly 300 references, the overwhelming majority pointing inside the Editor install — so they resolve nowhere else. That rules out generating them in one job and consuming them in another: any consumer needs the same Editor at the same path, which is the cost the split was meant to avoid.
- **`unity-builder` is used without building anything.** `buildMethod` replaces the build entirely with `GooGalaxy.Editor.Automation.SolutionSync.Sync`, which refreshes the AssetDatabase, writes the project files, and quits. The action only inspects the Editor's exit code, so no player is produced and none is expected. It is there for the licensing and container plumbing, not for a build.
- **The `dotnet format` step runs inside the same editor image, mounted at the same `/github/workspace` path.** Both halves of that sentence are load-bearing: a different image has no Unity installation to resolve against, and a different mount path invalidates the absolute paths Unity just wrote. The image tag is read back from the Docker daemon rather than reconstructed, because the revision suffix is game-ci's to change.
- **The SDK version is pinned in `global.json`, and both jobs install it explicitly** — `actions/setup-dotnet` on the runner, `dotnet-install.sh` inside the container. Which style rules exist at all depends on the SDK, so a check that inherits whatever the runner image happens to ship is not reproducible: the same commit passes one week and fails the next. `global.json` also governs every `dotnet` command in the repository, which is why `Format Check` needs the setup step even though it only runs CSharpier — without it, `dotnet tool restore` fails against a runner that has no matching SDK.
- **The generated solution is not named `goo-galaxy.slnx` in CI.** Unity names it after the project folder, and game-ci mounts the repository at `/github/workspace`, so the file is `workspace.slnx`. The step resolves it by glob rather than assuming a name.
- **`LangVersion` is rewritten by `Assets/Editor/Automation/SolutionPostprocessor.cs`.** The Visual Studio package derives it from the installed IDE and falls back to `latest` when none resolves — which is every container. Under `latest` the analyzers report rules for language features Unity rejects, so the gate would demand changes that break the Editor build.
- The Library cache falls back to the Edit Mode test job's keys, since both jobs import the same project.

### CodeQL Analysis

- File: `codeql.yml` (config: `.github/codeql/codeql-config.yml`)
- Trigger: `push` to `main`, `pull_request` to `main`, weekly schedule, plus `workflow_dispatch`
- Status check: `CodeQL Analysis (csharp)`, `CodeQL Analysis (actions)`
- Purpose: perform static application security testing (SAST) on C# codebase and GitHub Actions workflows (`security-and-quality` suite with `build-mode: none`).

### Secret Scan

- File: `secret-scan.yml`
- Trigger: `push` to any branch, plus a weekly schedule
- Status check: `Secret Scan`
- Purpose: run [Betterleaks](https://github.com/betterleaks/betterleaks) against the full repository history (digest-pinned container, `--redact --verbose`) and block staged/changed files whose extension is secret-shaped (mirrors the `file_extension_restriction` rule in `.github/rulesets/push-rulesets/01-sensitive-files-protection.json`, which the push ruleset itself cannot enforce — see `.github/rulesets/README.md`).
- **Deliberately does not read `CI_FETCH_DEPTH`.** It hardcodes `fetch-depth: 0` instead. `CI_FETCH_DEPTH` is `1`, and a depth-1 clone only scans the current tree — a credential that was committed and later deleted (the most common way a secret leaks) would never be seen. Every other workflow reads the shared variable; this is the one intentional exception, so do not "fix" it back during a consistency pass.
- **No `pull_request` trigger, on purpose.** It would overlap with `push` on every branch that has a pull request open and scan each commit twice. The `push` run attaches its check to the head commit, which is what the required status check on `main` evaluates, so the pull request is still gated. The trade: a pull request **from a fork** produces no run here, because the push happens in the fork. The required check then never reports and the merge stays blocked — visible and fail-closed, but it needs a manual look rather than a re-run.

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

- `UNITY_VERSION`: expected value `6000.3.18f1`
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
- `EditorConfig Check`
- `Secret Scan`
- `Build Android Player`
- `Build iOS Player`
- `Unity Edit Mode Tests`
- `Unity Play Mode Tests`

Use the job names above in GitHub Rulesets, not the combined `Workflow / Job` labels shown in the UI.

## Notes

- Unity version is pinned to `6000.3.18f1`, matching `ProjectSettings/ProjectVersion.txt`.
- Android and iOS builds remain split so the required checks and platform-specific runners stay explicit.
- The duplicated Unity build and test steps live in shared composite actions under `.github/actions/`, while checkout and Git LFS setup stay inline in each Unity workflow because local actions cannot run before repository checkout.
- The iOS build uses a macOS runner because Unity iOS export depends on Apple tooling.
- Build and test jobs now include explicit concurrency and timeout controls to reduce stale runs and hung jobs.
