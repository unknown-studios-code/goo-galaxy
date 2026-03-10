# GitHub Rulesets for Goo Galaxy

This directory contains the repository rulesets aligned with Goo Galaxy's current branch strategy and CI pipeline.

## Structure

```text
.github/rulesets/
├── branch-rulesets/
│   └── 01-main-protection.json
├── push-rulesets/
│   └── 01-sensitive-files-protection.json
├── tag-rulesets/
│   └── 01-release-tags-protection.json
└── README.md
```

## Branch Strategy Alignment

The GDD defines a lightweight GitHub Flow process:

- `main` is the only long-lived protected branch.
- `feature/*`, `fix/*`, `chore/*`, and `spike/*` are short-lived topic branches.
- All production-ready changes merge back into `main` through pull requests.

Because of that, the ruleset set is intentionally small. The copied `master`, `develop`, and story/task branch protections were removed.

## Configured Rulesets

### Main Branch Protection

- File: `branch-rulesets/01-main-protection.json`
- Target: `refs/heads/main`
- Required status checks:
  - `PR Check`
  - `Format Check`
  - `Build Android Player`
  - `Build iOS Player`
  - `Unity Edit Mode Tests`
  - `Unity Play Mode Tests`
- Additional protections:
  - block force-push
  - require linear history
  - block deletion
  - block direct updates

### Sensitive Files Protection

- File: `push-rulesets/01-sensitive-files-protection.json`
- Scope: entire repository
- Blocks generic secret files plus mobile-signing assets such as `.keystore`, `.jks`, `.mobileprovision`, `.p8`, `.p12`, and `.pfx`.
- Enforces `100 MB` max file size and `255` max path length.

### Release Tag Protection

- File: `tag-rulesets/01-release-tags-protection.json`
- Target: `refs/tags/v*`
- Protects creation, deletion, updates, and requires signed tags.

## Importing Rulesets

### GitHub UI

1. Open the repository settings.
2. Go to `Settings > Rules > Rulesets`.
3. Create or import each JSON file from this directory.

### GitHub CLI

```bash
gh api repos/<owner>/<repo>/rulesets \
  --method POST \
  --input .github/rulesets/branch-rulesets/01-main-protection.json

gh api repos/<owner>/<repo>/rulesets \
  --method POST \
  --input .github/rulesets/push-rulesets/01-sensitive-files-protection.json

gh api repos/<owner>/<repo>/rulesets \
  --method POST \
  --input .github/rulesets/tag-rulesets/01-release-tags-protection.json
```

## Notes

- Rulesets must use the workflow job names, not the combined `Workflow / Job` label shown in the GitHub UI.
- The push ruleset requires a GitHub plan that supports push rulesets.
- If you later introduce a release branch or a dedicated hotfix lane, add a separate ruleset instead of reintroducing `develop` by default.
