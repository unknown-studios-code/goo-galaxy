# GitHub Actions Workflows for Goo Galaxy

This directory contains the CI workflows for Goo Galaxy. They are aligned with the current project strategy described in the GDD: Unity 6, MonoBehaviour/GameObject architecture, Netcode for GameObjects, and a GitHub Flow model centered on `main`.

## Structure

```text
.github/workflows/
├── pr-check.yml               # PR title validation
├── format-check.yml           # Repository format validation
├── codeql.yml                 # CodeQL SAST security analysis
├── secret-scan.yml            # Betterleaks full-history secret scanning
├── unity-build-android.yml    # Android development build (PR check)
├── unity-build-ios.yml        # iOS development build (PR check)
├── unity-build-release-validation-android.yml  # Weekly release-configuration build, no signing secrets (debug-signed APK)
├── unity-build-release-validation-ios.yml      # Weekly release-configuration build, no signing secrets (Xcode export)
├── unity-build-release-android.yml  # Signed Android .aab (manual; disabled for the MVP)
├── unity-build-release-ios.yml      # Signed iOS .ipa (manual; disabled for the MVP)
├── unity-tests-editmode.yml   # Edit Mode tests
├── unity-tests-playmode.yml   # Play Mode tests
└── README.md
```

```text
.github/actions/
├── ci-preflight/              # Fails naming every required repository variable that is missing or empty
├── lfs-setup/                 # Git LFS file list, cache and pull (run after checkout)
├── library-cache/             # Unity Library cache keys, restore and optional save
├── select-xcode/              # Points xcode-select and DEVELOPER_DIR at XCODE_VERSION
├── skip-if-unchanged/         # Gate for the validation workflows: has main moved since the last success?
├── unity-build/               # Shared Unity build flow used by the Android, iOS and release builds
└── unity-test/                # Shared Unity test flow used by Edit Mode and Play Mode
```

```text
.github/ios/
├── ExportOptions.plist        # xcodebuild -exportArchive template, filled at run time
└── configure_signing.py       # Pins manual signing per target: profile on Unity-iPhone, none on UnityFramework
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
- Build Profile: `Assets/Settings/Build Profiles/Android-Development.asset` (development build; no profiler autoconnect, so the artifact does not bake in the runner address)
- Output: `Android-Development-APK`, an installable development APK under `build/Android`
- PR builds are development builds on purpose. `android-export-type: androidPackage` only routes the call to the Android step of the `unity-build` action; with a profile active the profile decides the output, and `Android-Development` sets `m_BuildAppBundle: 0`, so the artifact is an `.apk` and not an `.aab`. Release configuration is checked by Release Validation below.

### iOS Build

- File: `unity-build-ios.yml`
- Trigger: `pull_request` to `main`, plus `workflow_dispatch`
- Status check: `Build iOS Player`
- Runner: `macos-26` (arm64), Xcode from `XCODE_VERSION` selected by the `select-xcode` action
- Build Profile: `Assets/Settings/Build Profiles/iOS-Development.asset` (development build; no profiler autoconnect, so the artifact does not bake in the runner address)
- Output: `iOS-Development-XcodeProject`, the development Xcode project export under `build/iOS`
- A development `.ipa` needs Apple signing, which does not exist yet, so the PR build stops at the Xcode export.

### Android Release Validation

- File: `unity-build-release-validation-android.yml`
- Trigger: weekly schedule (Mondays 06:00 UTC) plus `workflow_dispatch`; not a PR check
- Jobs: `Detect Changes` (`ubuntu-latest`) and `Release Validation (Android)` (`ubuntu-latest`) — not required status checks
- Secrets: only the `UNITY_*` ones; no `environment`, no release signing material
- Output: `Android-Release-APK`, an installable release APK, debug-signed because no keystore is supplied. Use it for performance measurements. `Android-Release-Validation-Logs` holds the Unity build logs, uploaded only on failure.
- **Builds without a Build Profile, mirroring the signed release job.** The release configuration comes from Player Settings, `android-export-type: androidPackage` makes the default build script emit an APK, and `android-symbol-type: public` packages symbols. The signed job ships an AAB, so bundle packaging is not exercised here. A change to how `Signed Android Build` is configured should be mirrored here, or the weekly check stops validating what ships.
- **Skipped when nothing changed.** `Detect Changes` runs the `skip-if-unchanged` action: if `github.sha` equals the head SHA of the last successful run of this workflow on `main`, the build job is skipped. A manual dispatch always builds, and a failed lookup builds too. The job needs `actions: read`. The gate compares commits only, so a change to just the `UNITY_VERSION` or `XCODE_VERSION` variable or to the runner image does not trigger a build; dispatch the workflow to force one.
- **Concurrency does not cancel in progress,** so a manual dispatch never kills a scheduled run that is already building.
- **These jobs save the Library cache** (no secrets are in these builds), so they warm the same keys the PR builds restore.

### iOS Release Validation

- File: `unity-build-release-validation-ios.yml`
- Trigger: weekly schedule (Mondays 06:00 UTC) plus `workflow_dispatch`; not a PR check
- Jobs: `Detect Changes` and `Release Validation (iOS)` (`macos-26`, Xcode from `XCODE_VERSION`) — not required status checks
- Secrets: only the `UNITY_*` ones; no `environment`, no release signing material
- Output: `iOS-Release-XcodeProject`, the Xcode export from `iOS-Release.asset` (no `.ipa`: signing is absent). `iOS-Release-Validation-Logs` holds the Unity build logs, uploaded only on failure.
- Skip-when-unchanged, concurrency and cache behaviour match the Android validation workflow.

### Android Release Build

- File: `unity-build-release-android.yml`
- Trigger: `workflow_dispatch` only
- Status: **disabled for the MVP, but a new workflow starts enabled.** After merge, run `gh workflow disable "Unity Build (Android Release)"`; until then a manual dispatch would run it. Re-enable it with `gh workflow enable` and add a `push` to `main` trigger when store releases start. `run_number` restarts at 1 in this file, which is harmless because nothing has been uploaded yet.
- Job: `Signed Android Build` (`ubuntu-latest`) — not a required status check
- Environment: `release`, so its secrets, variables and branch restrictions apply (see Required Secrets)
- Build Profile: none (see below)
- Output: `Android-Release`, a signed `.aab` with versionCode = build number; `Android-Release-Logs` holds the Unity build logs, uploaded only on failure. The marketing version is `bundleVersion` in Player Settings; the workflow does not override it.
- **Build number = `github.run_number * 10 + github.run_attempt`**, computed once per job and used for both versionCode and `CFBundleVersion`. `run_number` alone repeats when a run is re-run, which both stores reject; the attempt digit keeps each upload unique, and a tenth attempt fails rather than collide with the next run. **`run_number` restarts at 1 if the workflow file is renamed or recreated** — the stores then reject every build as a duplicate or downgrade. If that ever happens, add a fixed offset above the last uploaded build number to the computation.
- **Concurrency does not cancel in progress.** A running release is never interrupted mid-signing; a newer dispatch waits, and GitHub keeps only the newest pending run.
- **The job fails before checkout if a signing secret is missing.** The check passes only `secrets.X != ''` into the shell, never the value.
- **The Android job builds without a Build Profile, on purpose.** The `Validate build inputs` step of the `unity-build` action explains why and rejects the combination. Player Settings, scenes and defines match `Android-Release`; the profile's own platform build settings do not apply on this path, and debug symbols come from `android-symbol-type` (`public` here, for Play Console crash symbolication).
- **The keystore is decoded into the workspace as `ci-upload.keystore`** by the CLI (it needs a file path for Unity's `PlayerSettings.Android.keystoreName`) and deleted by the composite action in an `always()` step. It is outside `build/Android`, so it never reaches an artifact.
- **The signed job never saves the Library cache.** It passes `save-library-cache: "false"`, which switches `library-cache` to `actions/cache/restore`. Unity can write the keystore passwords in plain text into the generated Gradle project under `Library/Bee/Android`, and a cache saved on `main` is readable from every branch. As a second line, the action deletes `Library/Bee/Android` in an `always()` step whenever a keystore was supplied. Caches saved from a pull request ref are not readable from `main`, so the signed job restores the `main`-scoped caches, seeded by the weekly validation runs and by Format Check on push.
- **The Android release AAB uses default compression.** `unity-builder` v6.0.0 exposes no compression input, and no profile is applied on the signed path. The `LZ4HC` setting in `Android-Release.asset` currently reaches no CI build: PR builds use `Android-Development`, and release validation and the signed Android build run without a profile.

### iOS Release Build

- File: `unity-build-release-ios.yml`
- Trigger: `workflow_dispatch` only
- Status: **disabled for the MVP, but a new workflow starts enabled.** After merge, run `gh workflow disable "Unity Build (iOS Release)"`; until then a manual dispatch would run it. Add a `push` to `main` trigger when store releases start. `run_number` restarts at 1 in this file, which is harmless because nothing has been uploaded yet.
- Job: `Signed iOS Build` (`macos-26`, Xcode from `XCODE_VERSION`) — not a required status check
- Environment: `release`
- Build Profile: `Assets/Settings/Build Profiles/iOS-Release.asset`
- Output: `iOS-Release-IPA`, the App Store Connect `.ipa` with `CFBundleVersion` = build number; `iOS-Release-XcodeProject`, the Unity Xcode export the `.ipa` was archived from; `iOS-Release-Logs`, uploaded only on failure.
- **Build number = `github.run_number * 10 + github.run_attempt`** as in the Android release workflow. Each workflow has its own `run_number` sequence, so the Android and iOS build numbers no longer move together; the stores only compare numbers within their own platform.
- **Concurrency does not cancel in progress.** A running release is never interrupted mid-signing; a newer dispatch waits, and GitHub keeps only the newest pending run.
- **The job fails before checkout if a signing secret is missing.** The check passes only `secrets.X != ''` into the shell, never the value.
- **Xcode is pinned, not inherited.** Apple only accepts App Store uploads built with Xcode 26 and the iOS 26 SDK. The three iOS workflows (`unity-build-ios.yml`, `unity-build-release-ios.yml`, `unity-build-release-validation-ios.yml`) run on `macos-26` and call the `select-xcode` action with the `XCODE_VERSION` repository variable (`26.6`), which points `xcode-select` and `DEVELOPER_DIR` at `/Applications/Xcode_${XCODE_VERSION}.app` and fails with the list of installed versions if the image no longer ships it. Changing the variable moves all three at once. Unity 6000.3.8f1 or later is the documented baseline for the iOS 26 SDK UIScene requirement, and the project is on 6000.3.18f1.
- **iOS signing uses a throwaway keychain** under `$RUNNER_TEMP` with a random, masked password; the `.p12` is deleted right after import, and the keychain and installed profile are removed in an `always()` step.
- **Manual signing is written per target, not on the `xcodebuild` command line.** Command-line build settings reach every target, and Xcode rejects a provisioning profile on `UnityFramework`. `configure_signing.py` edits only the build settings of `Unity-iPhone` (profile, team, identity) and `UnityFramework` (team, identity, no profile). `CODE_SIGN_IDENTITY` is the imported certificate's SHA-1; if archiving ever fails on identity resolution, `Apple Distribution` is the fallback value.
- **`ExportOptions.plist` is a template.** `teamID`, `signingCertificate` and the `provisioningProfiles` entry are filled with PlistBuddy from `APPLE_TEAM_ID`, the imported certificate's SHA-1 and the decoded profile's UUID, so nothing account-specific is committed. The bundle ID is read from the profile rather than committed, and `configure_signing.py` fails unless it matches the project's `PRODUCT_BUNDLE_IDENTIFIER` from Player Settings.

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
- `XCODE_VERSION`: the Xcode selected on the iOS runners, currently `26.6`; it must exist on the pinned `macos-26` image. Read by all three iOS workflows.
- `APPLE_TEAM_ID`: the 10-character Apple Developer Team ID, read only by `Signed iOS Build` — set it on the `release` environment rather than the repository

Every Unity workflow, plus `codeql.yml` and `format-check.yml`, maps the variables it reads from `vars` in its top-level `env` block and checks them with the `ci-preflight` action, which names every missing one. The check runs right after checkout, because a local action needs the repository on disk; `CI_FETCH_DEPTH` is therefore not yet validated when checkout runs, and an empty value falls back to a depth of 1.

## Required Secrets

All Unity workflows use these secrets:

- `UNITY_LICENSE`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`

The release workflows additionally need these, each stored base64-encoded where it is a file. Store them, with `APPLE_TEAM_ID`, as **`release` environment** secrets and variables — not repository-level — and limit the environment's deployment branches to `main`, so no other branch or manual run elsewhere can reach the signing keys. The files themselves never enter the repository — their extensions are blocked by the secret scan and the push ruleset:

- `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS`: the Play upload key
- `IOS_DIST_CERT_P12_BASE64`, `IOS_DIST_CERT_PASSWORD`: the Apple Distribution certificate exported as `.p12`
- `IOS_PROVISIONING_PROFILE_BASE64`: an App Store distribution profile for an explicit App ID matching the iOS bundle identifier

The PR iOS workflow exports the development Xcode project only; signing happens in `unity-build-release-ios.yml`.

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
- Local Android builds go through `npm run unity:build:android` (development) and `npm run unity:build:android:release`; the script restores the project afterwards.
- Android and iOS builds remain split so the required checks and platform-specific runners stay explicit.
- The duplicated steps live in composite actions under `.github/actions/` (see Structure). Checkout stays inline in every workflow because a local action cannot run before the repository exists. The PR build and test jobs are deliberately not reusable workflows: a `workflow_call` caller renames its checks to `caller / callee`, which would break the required status checks in the main ruleset.
- `library-cache` builds the Library keys as `<prefix>-<os>-<unity>-<cache version>-<source hash>` with restore keys that drop one segment at a time, then any extra keys, then `Library-`. The `Library-Format` cache also falls back to the Edit Mode test cache, since both import the same project.
- The iOS build uses a macOS runner because Unity iOS export depends on Apple tooling.
- Build and test jobs now include explicit concurrency and timeout controls to reduce stale runs and hung jobs.
