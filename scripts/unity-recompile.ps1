# Compile through the open Unity Editor and wait for it to settle.
# No parameters. Exit 0 when the Editor is settled and the compile succeeded, 1 otherwise.
#
# Run this before `npm run format` / `npm run check` and before every commit:
# `dotnet format` reads the untracked .csproj / goo-galaxy.slnx, and those are
# refreshed only as a side effect of a compile actually running (Rule 3a).
#
# The test scripts run this same gate themselves, so there is no need to chain them.

. "$PSScriptRoot/_unity-lib.ps1"

if (Invoke-UnityRecompileGate) { exit 0 }
exit 1
