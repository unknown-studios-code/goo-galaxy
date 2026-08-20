---
description: "Use when committing, formatting, or a hook or CI format check fails. Covers the prerequisites, the npm format and check chain, the Husky pre-commit sequence, and the secret-scan gates."
---

# Build & Tooling

## 1. Overview

Everything between a written file and a commit: the prerequisites the chain needs, the formatters and the order they run in, the Husky `pre-commit` hook, and the secret gates. Prerequisites are Unity 6000.3.18f1, Node.js (`npm` scripts and Husky), the .NET SDK (`dotnet tool restore` pulls CSharpier, wired to `npm install` through the `prepare` script), and **Docker Desktop** — the hook shells out to a container to scan staged changes for secrets and fails the commit outright if the daemon is not reachable.

The editor side of the same loop — compiling, running the suites, reading the console — is [unity-editor-automation.md](unity-editor-automation.md). The two meet at one point: `dotnet format` reads Unity-generated project files, so a format is only ever as current as the last compile.

## 2. Cross-References

- **Editor Automation** → [unity-editor-automation.md](unity-editor-automation.md) (Compiling, tests, console and builds; Rule 3a for refreshing the project files `dotnet format` reads, Rules 16–18 for the `unity:*` scripts)
- **Code Style** → [unity-code-style.md](unity-code-style.md) (What CSharpier and `.editorconfig` actually declare)
- **Testing** → [unity-testing.md](unity-testing.md) (The suites CI runs on every PR)

## 3. Core Rules

- **Rule 1 (Compile Before You Format):** Run `npm run unity:recompile` before any format or check script, and before committing. Unity generates `goo-galaxy.slnx` and the per-assembly `.csproj` as a side effect of a compile actually running, and `dotnet format` reads those rather than the `.asmdef` files. Existing is not the same as current: a csproj stale relative to the `.asmdef` files makes `dotnet format` delete `using` directives it wrongly reads as unused — and inside `lint-staged` that deletion is re-staged straight into the commit. The moment of highest risk is therefore the commit that adds a script or an assembly, and a change that touches no code at all (restoring a plugin, editing an `.asmdef`) needs the forced sync in Rule 3a of [unity-editor-automation.md](unity-editor-automation.md).
- **Rule 2 (Format, Then Check):** `npm run format` fixes what a formatter can; `npm run check` reports what is left and is exactly what the CI Format Check runs. Run them in that order. Per-formatter variants exist as `format:csharpier`, `format:dotnet`, `format:prettier` and the matching `check:*`. Leave the routine pass to `lint-staged` at commit time — a whole-repo `npm run format` is for when you want the repository clean, not for shipping one change.
- **Rule 3 (`dotnet format` Is What Enforces `.editorconfig`, At `--severity info`):** It is the only tool in the chain that reads the file's `dotnet_diagnostic` and naming entries — Unity's own compiler never does, and CSharpier only owns layout. That reaches the UNT analyzer rules too, because the generated `.csproj` reference `Microsoft.Unity.Analyzers.dll` and Roslyn discovers `.editorconfig` by walking the directory tree. Both `format:dotnet` and `check:dotnet` pass `--severity info`, and the flag is load-bearing on the write side: `dotnet format` defaults to `warn` and fixes none of the `:suggestion` rules, which would leave `format` unable to fix what `check` then rejects. Keep the two symmetric.
- **Rule 4 (CSharpier Runs First, And The Two Do Not Fight):** CSharpier precedes `dotnet format` in `npm run format` and in `lint-staged`. `dotnet format whitespace` reports zero divergences against CSharpier's output, so neither undoes the other. `dotnet format` costs roughly five seconds regardless of how many files are staged — the cost is loading the MSBuild workspace, not the file count.
- **Rule 5 (The Hook Is Staged-Only, And Ordered On Purpose):** `pre-commit` checks Docker, then that `goo-galaxy.slnx` exists, then runs `lint-staged`, then the secret gates. Docker and the solution check come first because they are answerable without the index, so a stopped daemon or a closed editor fails in milliseconds instead of after the formatters run; the secret gates run last because `lint-staged` is what finally settles the index, and scanning earlier would examine pre-formatter content or miss a file the formatter restaged. **`lint-staged` works on the staged files only** — CSharpier then `dotnet format` on staged C#, Prettier on staged JSON/Markdown/YAML — re-staging what it rewrote and hiding unstaged hunks while it runs, so a partially staged file keeps the hunks you deliberately left out. Its config lives under `lint-staged` in `package.json`. The trade is that the hook no longer verifies the **whole repository**: a violation in a file you did not stage reaches CI instead of failing locally, which is what the CI Format Check and an on-demand `npm run check` are for.
- **Rule 6 (Let The Hooks Run):** `HUSKY=0` used to be the routine path for agent-authored commits, because the Commitizen `prepare-commit-msg` hook opened an interactive prompt that hung any non-interactive caller. That hook now exits immediately whenever git already has a message — which `git commit -m` always does — so the prompt only appears for a bare `git commit`. Skipping the hooks today buys nothing and costs the formatting and secret gates.
- **Rule 7 (Secret Scanning Fails Closed):** The `pre-commit` hook and `.github/workflows/secret-scan.yml` both run [Betterleaks](https://github.com/betterleaks/betterleaks) as a container, digest-pinned — `ghcr.io/betterleaks/betterleaks@sha256:16f903f0100ce7358ef1f870858777e55bec94cf04c6b65c45d013274ea3311c`, never a tag, never `:latest` — plus a filename-extension gate for secret-shaped files (`.key`, `.pem`, `.p12`, `.pfx`, `.keystore`, `.jks`, `.mobileprovision`, `.cer`, `.p8`), sourced from `.github/rulesets/push-rulesets/01-sensitive-files-protection.json`. A failure on the Docker check means start Docker Desktop and retry — the hook fails the commit rather than skipping the scan, on purpose. A failure on an actual finding is a real leak until proven otherwise: fix the content or rotate the credential. Forcing it green — removing `--redact`, appending `|| true`, adding `continue-on-error` or a `.betterleaksignore`/`.betterleaks.toml` — is off the table; ask first if you believe a finding is a false positive.

## 4. Quick Reference

```powershell
npm install                    # husky + dotnet tool restore, via the prepare script
npm run unity:recompile        # compile through the open editor and wait, before format/check
npm run format                 # csharpier + dotnet + prettier, rewriting in place
npm run check                  # the same three, verify only, matching the CI Format Check
```

| Symptom                                              | Cause                                              | What to do                                                 |
| :--------------------------------------------------- | :------------------------------------------------- | :--------------------------------------------------------- |
| `using` directives vanished from a staged file       | Stale csproj — `dotnet format` read them as unused | `npm run unity:recompile`, restore them, re-stage (Rule 1) |
| Commit fails on `goo-galaxy.slnx is missing`         | The editor has not compiled the project yet        | Open the project in Unity, let it settle, retry            |
| Commit fails on the Docker check                     | Daemon not reachable — the gate fails closed       | Start Docker Desktop and retry (Rule 7)                    |
| `format` leaves violations that `check` then rejects | A `--severity` asymmetry between the two scripts   | Keep both at `--severity info` (Rule 3)                    |
| CI Format Check fails on a file you never staged     | The hook only ever saw the staged set              | `npm run check` locally, then fix what it reports (Rule 5) |
