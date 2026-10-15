# Build the Android player through the open Unity Editor, then put the project back as it was.
#
#   npm run unity:build:android                      -- development APK (Development + ConnectWithProfiler)
#   npm run unity:build:android:release              -- release APK, debug-signed, for performance measurements
#   npm run unity:build:android -- -Install          -- build, then adb install -r and launch it
#   npm run unity:build:android -- -DryRun           -- validate only; builds nothing
#   npm run unity:build:android -- -Scenes Assets/Scenes/Gameplay/MatchPVE.unity
#   npm run unity:build:android -- -Scenes Assets/Scenes/A.unity,Assets/Scenes/B.unity
#
# Output: Builds/Android/GooGalaxy-<development|release>.apk (git-ignored).
#
# Development is the default for local builds; release builds belong to CI
# (unity-build-release-validation-android.yml and unity-build-release-android.yml). -Release exists for a
# one-off performance measurement on a device.
#
# -Scenes takes scene asset paths (an array, or one comma-separated value, because npm on Windows cannot pass
# JSON quoting through) and replaces the Build Settings list for this build only. The boot scene
# (Assets/Scenes/Bootstrap/Main.unity) holds only a camera and a light, so a device build needs a scene that
# plays -- tracked in GOOM-42.
#
# A build dirties the project whatever the mode (Rule 19 of unity-editor-automation.md), so this script
# refuses a dirty tree and any open scene with unsaved changes (save first), and afterwards restores the known
# build side effects, switches the Editor back to the target (and Build Profile) it started on, refreshes the
# AssetDatabase and confirms the tree is clean again. Any other change is reported and left untouched.
# Do not touch the Editor while the script builds: the allowlisted files are reverted when it finishes.
# When the outcome is unknown (poll timeout, Ctrl+C) it changes nothing, because a build may still be running,
# and prints the recovery steps instead.
#
# -DryRun validates the build and stops: it accepts a dirty tree, never restores or cleans working-tree files,
# and ignores -Install. It still puts buildAppBundle and the build target back if it changed them.
#
# Whether the Editor reloads ProjectSettings from disk after the restore is verified only by a real run.
#
# Exit 0 when the build succeeded, the project was restored and, with -Install, the APK installed and launched;
# 1 otherwise.

[CmdletBinding()]
param(
    [switch]$Release,
    [string[]]$Scenes = @(),
    [switch]$Install,
    [switch]$DryRun
)

. "$PSScriptRoot/_unity-lib.ps1"

$PollSeconds = 20
$MaxPolls = 90          # ~30 minutes
$SwitchPollSeconds = 10
$SwitchMaxPolls = 90    # ~15 minutes: a target switch is a full reimport
$SettlePollSeconds = 5
$SettleMaxPolls = 60    # ~5 minutes for an import to finish
$ActivityClass = 'com.unity3d.player.UnityPlayerGameActivity'

# Paths a build is known to rewrite. Anything else that turns up dirty is reported and left alone.
$ModifiedAllowlist = @(
    'Assets/Settings/Rendering/Pipeline/URP-*.asset'
    'Assets/Settings/Rendering/Pipeline/UniversalRP.asset'
    'Assets/Settings/Rendering/Pipeline/UniversalRenderPipelineGlobalSettings.asset'
    'ProjectSettings/ProjectSettings.asset'
    'ProjectSettings/UnityConnectSettings.asset'
)
$UntrackedAllowlist = @(
    'Assets/DefaultVolumeProfile.asset'
    'Assets/DefaultVolumeProfile.asset.meta'
    'Assets/Resources/PerformanceTestRun*'
)
# Folders the build may create. Each is removed, with its .meta, only when it did not exist at the start and is empty.
$BuildCreatedFolders = @('Assets/Resources', 'Assets/StreamingAssets')

$flavour = if ($Release) { 'release' } else { 'development' }
$outputPath = "Builds/Android/GooGalaxy-$flavour.apk"

# Every Scenes element may itself be comma-separated (see the header).
$sceneList = @($Scenes | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

function Get-GitChange {
    $raw = (& git status --porcelain=v1 -z --untracked-files=all | Out-String)
    $tokens = @($raw -split "`0" | Where-Object { $_ -ne '' -and $_ -ne "`n" })
    $changes = @()
    for ($i = 0; $i -lt $tokens.Count; $i++) {
        $token = $tokens[$i]
        if ($token.Length -lt 4) { continue }
        $code = $token.Substring(0, 2)
        $entry = [pscustomobject]@{ Code = $code; Path = $token.Substring(3); Original = '' }
        # A rename or copy carries its source path as the next token.
        if ($code -match '[RC]') {
            $i++
            if ($i -lt $tokens.Count) { $entry.Original = $tokens[$i] }
        }
        $changes += $entry
    }
    return $changes
}

# Read a single value back from the Editor, or $null when the bridge cannot answer.
function Invoke-UnityEvalValue {
    param([string]$Code)

    $envelope = Invoke-UnityCommand -Command 'eval' -Arguments @('--code', $Code)
    if (-not (Test-UnityEnvelope -Envelope $envelope -What 'eval')) { return $null }
    return $envelope.data.result.result
}

# Rule 6: a setter's return value is not proof, so the value is read back and compared.
function Invoke-AppBundleWrite {
    param([bool]$Value)

    $text = if ($Value) { 'true' } else { 'false' }
    $null = Invoke-UnityEvalValue -Code "UnityEditor.EditorUserBuildSettings.buildAppBundle = $text; return `"ok`";"
    $actual = Invoke-UnityEvalValue -Code 'return UnityEditor.EditorUserBuildSettings.buildAppBundle ? "true" : "false";'
    return ($actual -eq $text)
}

function Get-ActiveBuildTarget {
    return Invoke-UnityEvalValue -Code 'return UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString();'
}

# Empty string when no Build Profile is active, the asset path otherwise, $null when unreadable.
function Get-ActiveBuildProfilePath {
    return Invoke-UnityEvalValue -Code 'var p = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile(); return p == null ? "" : UnityEditor.AssetDatabase.GetAssetPath(p);'
}

function Invoke-ActiveProfileWrite {
    param([string]$Path)

    if ($Path) {
        $code = "var p = UnityEditor.AssetDatabase.LoadMainAssetAtPath(`"$Path`") as UnityEditor.Build.Profile.BuildProfile; if (p == null) return `"missing`"; UnityEditor.Build.Profile.BuildProfile.SetActiveBuildProfile(p); return `"ok`";"
    }
    else {
        $code = 'UnityEditor.Build.Profile.BuildProfile.SetActiveBuildProfile(null); return "ok";'
    }
    $null = Invoke-UnityEvalValue -Code $code
    return ((Get-ActiveBuildProfilePath) -eq $Path)
}

# Wait until the Editor is neither compiling, importing nor reloading the domain. Tolerates a silent bridge.
function Wait-EditorSettled {
    for ($i = 1; $i -le $SettleMaxPolls; $i++) {
        $envelope = Invoke-UnityCommand -Command 'editor_status' -TimeoutSeconds 10
        $state = Get-UnityStatusResult -Envelope $envelope
        if ($null -ne $state -and $state.PSObject.Properties['status'] -and $state.status -eq 'ready' -and
            -not $state.compiling -and -not $state.domainReloadInProgress) {
            return $true
        }
        Start-Sleep -Seconds $SettlePollSeconds
    }
    Write-Host 'WARN  the Editor did not report ready within the poll ceiling.' -ForegroundColor Yellow
    return $false
}

# Switch the Editor to a build target and wait for the reimport to settle. Returns $true on success.
function Switch-UnityBuildTarget {
    param([string]$Target)

    Write-UnityHeader "unity switch_build_target -- $Target"

    $baseline = Get-UnityStatusRaw -Envelope (Invoke-UnityCommand -Command 'switch_build_target_status' -TimeoutSeconds 10)

    $dispatch = Invoke-UnityCommand -Command 'switch_build_target' -Arguments @('--target', $Target, '--confirm', 'true')
    if (-not (Test-UnityEnvelope -Envelope $dispatch -What 'switch_build_target')) { return $false }

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $observed = $false
    for ($i = 1; $i -le $SwitchMaxPolls; $i++) {
        Start-Sleep -Seconds $SwitchPollSeconds

        $envelope = Invoke-UnityCommand -Command 'switch_build_target_status' -TimeoutSeconds 10
        if ($null -eq $envelope -or -not $envelope.success) {
            Write-Host ("      [{0,6:N1}s] bridge silent (domain reload) -- still waiting" -f $sw.Elapsed.TotalSeconds) -ForegroundColor DarkGray
            continue
        }

        $raw = Get-UnityStatusRaw -Envelope $envelope
        if (-not $observed -and $null -ne $baseline -and $raw -eq $baseline) {
            Write-Host ("      [{0,6:N1}s] status unchanged -- previous switch's result, still waiting" -f $sw.Elapsed.TotalSeconds) -ForegroundColor DarkGray
            # The dispatch may have been a no-op that never changes the retained payload; the target check settles that.
            if ((Get-ActiveBuildTarget) -eq $Target) {
                $null = Wait-EditorSettled
                Write-Host "OK    active build target is $Target." -ForegroundColor Green
                return $true
            }
            continue
        }
        $observed = $true

        $status = Get-UnityStatusResult -Envelope $envelope
        if ($null -eq $status -or -not $status.PSObject.Properties['status']) { continue }

        Write-Host ("      [{0,6:N1}s] {1}" -f $sw.Elapsed.TotalSeconds, $status.status) -ForegroundColor DarkGray
        if ($status.status -ne 'completed') { continue }

        if ($status.PSObject.Properties['success'] -and -not $status.success) {
            Write-Host "FAIL  the switch to $Target reported failure." -ForegroundColor Red
            return $false
        }

        $active = Get-ActiveBuildTarget
        if ($active -ne $Target) {
            Write-Host "FAIL  asked for $Target, the Editor reports '$active'." -ForegroundColor Red
            return $false
        }

        $null = Wait-EditorSettled
        Write-Host "OK    active build target is $Target." -ForegroundColor Green
        return $true
    }

    Write-Host 'FAIL  the target switch did not settle within the poll ceiling.' -ForegroundColor Red
    return $false
}

function Test-Allowed {
    param([string]$Path, [string[]]$Patterns)

    foreach ($pattern in $Patterns) {
        if ($Path -like $pattern) { return $true }
    }
    return $false
}

# Undo the known build side effects and report anything else. Returns the paths it refused to touch.
function Restore-Project {
    param([string[]]$CreatedFolders)

    $unexpected = @()
    $createdMetas = @($CreatedFolders | ForEach-Object { "$_.meta" })

    foreach ($change in (Get-GitChange)) {
        $path = $change.Path
        $untracked = $change.Code -eq '??'

        if ($untracked -and ((Test-Allowed -Path $path -Patterns $UntrackedAllowlist) -or ($path -in $createdMetas))) {
            Write-Host "      removing untracked   $path" -ForegroundColor Yellow
            Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
        }
        elseif (-not $untracked -and $change.Code -in ' M', ' D' -and (Test-Allowed -Path $path -Patterns $ModifiedAllowlist)) {
            Write-Host "      restoring tracked    $path" -ForegroundColor Yellow
            & git restore --worktree -- $path | Out-Null
        }
        else {
            $unexpected += $path
        }
    }

    # Only folders the build created, and only once they are empty.
    foreach ($folder in $CreatedFolders) {
        if ((Test-Path -LiteralPath $folder) -and -not (Get-ChildItem -LiteralPath $folder -Force | Select-Object -First 1)) {
            Write-Host "      removing empty folder $folder" -ForegroundColor Yellow
            Remove-Item -LiteralPath $folder -Force
            Remove-Item -LiteralPath "$folder.meta" -Force -ErrorAction SilentlyContinue
        }
    }

    return $unexpected
}

# Restore, let the Editor re-import what changed on disk (Rule 3a), then judge the tree. Returns $true when clean.
function Restore-ProjectAndVerify {
    param([string[]]$CreatedFolders)

    Write-UnityHeader 'restore project'

    $null = Restore-Project -CreatedFolders $CreatedFolders

    $null = Invoke-UnityEvalValue -Code 'UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceUpdate); return "ok";'
    $null = Wait-EditorSettled

    # The refresh can rewrite files again, so the verdict comes from a second pass.
    $unexpected = @(Restore-Project -CreatedFolders $CreatedFolders)
    foreach ($path in $unexpected) {
        Write-Host "FAIL  unexpected change, left untouched: $path" -ForegroundColor Red
    }

    $left = @(Get-GitChange | Where-Object { $_.Path -notin $unexpected })
    foreach ($change in $left) { Write-Host "FAIL  could not clean: $($change.Path)" -ForegroundColor Red }

    if ($unexpected.Count -gt 0 -or $left.Count -gt 0) { return $false }

    Write-Host 'OK    working tree is clean.' -ForegroundColor Green
    return $true
}

# The open scenes, or $null when the Editor cannot answer. An empty array is a valid answer.
function Get-OpenScene {
    $envelope = Invoke-UnityCommand -Command 'list_open_scenes'
    if ($null -eq $envelope -or -not $envelope.success) { return $null }
    if (-not $envelope.data.result.PSObject.Properties['scenes']) { return $null }
    return , @($envelope.data.result.scenes)
}

# Rule 10: discard dirty scenes by reopening the recorded ones. Only called after a build, which opens scenes.
function Restore-OpenScene {
    param($Recorded)

    $current = Get-OpenScene
    if ($null -eq $current -or $null -eq $Recorded -or @($Recorded).Count -eq 0) { return $true }

    $dirty = @($current | Where-Object { $_.isDirty })
    $currentPaths = @($current | ForEach-Object { $_.path } | Sort-Object)
    $recordedPaths = @($Recorded | ForEach-Object { $_.path } | Sort-Object)
    if ($dirty.Count -eq 0 -and ($currentPaths -join '|') -eq ($recordedPaths -join '|')) { return $true }

    Write-Host '      reopening the scenes that were open before the build' -ForegroundColor Yellow
    $first = $true
    foreach ($scene in $Recorded) {
        $arguments = @('--path', $scene.path)
        if (-not $first) { $arguments += @('--additive', 'true') }
        $envelope = Invoke-UnityCommand -Command 'open_scene' -Arguments $arguments
        if (-not (Test-UnityEnvelope -Envelope $envelope -What "open_scene $($scene.path)")) { return $false }
        $first = $false
    }

    $after = Get-OpenScene
    return ($null -ne $after -and @($after | Where-Object { $_.isDirty }).Count -eq 0)
}

function Find-Adb {
    $candidates = @()
    foreach ($root in @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT, (Join-Path $env:LOCALAPPDATA 'Android\Sdk'))) {
        if ($root) { $candidates += (Join-Path $root 'platform-tools\adb.exe') }
    }

    $unityRoot = Invoke-UnityEvalValue -Code 'return System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(UnityEditor.EditorApplication.applicationPath));'
    if ($unityRoot) {
        $candidates += (Join-Path $unityRoot 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe')
    }

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }

    $onPath = Get-Command adb -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    return $null
}

function Install-Apk {
    param([string]$ApkPath, [string]$PackageId)

    Write-UnityHeader 'adb install'
    $adb = Find-Adb
    if (-not $adb) {
        Write-Host 'FAIL  adb not found. Set ANDROID_HOME or install the Android SDK platform-tools.' -ForegroundColor Red
        return $false
    }

    $device = @()
    if ($env:ANDROID_SERIAL) { $device = @('-s', $env:ANDROID_SERIAL) }

    Write-Host "      adb: $adb" -ForegroundColor DarkGray
    & $adb @device install -r $ApkPath
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'FAIL  adb install failed. Is a device connected and authorised (adb devices)?' -ForegroundColor Red
        return $false
    }

    $launch = (& $adb @device shell am start -n "$PackageId/$ActivityClass" 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0 -or $launch -match 'Error|Exception') {
        Write-Host "FAIL  installed, but launching $PackageId/$ActivityClass failed:" -ForegroundColor Red
        Write-Host "      $($launch.Trim())" -ForegroundColor Red
        return $false
    }

    Write-Host "OK    installed and launched $PackageId." -ForegroundColor Green
    return $true
}

# Run one restore step so a failure in it cannot stop the steps after it. The body runs in this scope.
function Invoke-RestoreStep {
    param([string]$What, [scriptblock]$Body)

    try {
        . $Body
    }
    catch {
        Write-Host "FAIL  $What -- $($_.Exception.Message)" -ForegroundColor Red
        $script:restoreOk = $false
    }
}

Push-Location (Join-Path $PSScriptRoot '..')

$exitCode = 1
$restoreOk = $true
$buildStarted = $false
$buildFinished = $false
$appBundleChanged = $false
$originalTarget = $null
$originalProfile = $null
$originalAppBundle = $null
$recordedScenes = $null
$existedAtStart = @{}
foreach ($folder in $BuildCreatedFolders) { $existedAtStart[$folder] = Test-Path -LiteralPath $folder }
$packageId = $null
$buildOk = $false

try {
    Write-UnityHeader "unity build android -- $flavour"

    $dirtyAtStart = @(Get-GitChange)
    if ($dirtyAtStart.Count -gt 0 -and $DryRun) {
        Write-Host '      -DryRun: the tree is dirty, which is fine because nothing is built or restored.' -ForegroundColor DarkGray
    }
    elseif ($dirtyAtStart.Count -gt 0) {
        Write-Host 'FAIL  the working tree is dirty; a build rewrites project settings and must start from a clean tree.' -ForegroundColor Red
        foreach ($change in $dirtyAtStart) { Write-Host "      $($change.Code) $($change.Path)" -ForegroundColor Red }
        Write-Host '      Commit, stash or discard these first.'
        return
    }

    if (-not (Invoke-UnityRecompileGate)) {
        Write-Host 'FAIL  refusing to build a project that does not compile.' -ForegroundColor Red
        return
    }

    $originalTarget = Get-ActiveBuildTarget
    $originalProfile = Get-ActiveBuildProfilePath
    $originalAppBundle = Invoke-UnityEvalValue -Code 'return UnityEditor.EditorUserBuildSettings.buildAppBundle ? "true" : "false";'
    $packageId = Invoke-UnityEvalValue -Code 'return UnityEditor.PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);'
    $recordedScenes = Get-OpenScene
    if (-not $originalTarget -or $null -eq $originalProfile -or -not $originalAppBundle -or -not $packageId -or $null -eq $recordedScenes) {
        Write-Host 'FAIL  could not read the build target, Build Profile, buildAppBundle, package id and open scenes from the Editor.' -ForegroundColor Red
        return
    }

    # A clean git tree says nothing about unsaved scene edits, and reopening scenes after the build would discard them.
    $unsavedScenes = @($recordedScenes | Where-Object { $_.isDirty })
    if ($unsavedScenes.Count -gt 0 -and -not $DryRun) {
        Write-Host 'FAIL  open scenes have unsaved changes; save them first (the script reopens scenes after the build).' -ForegroundColor Red
        foreach ($scene in $unsavedScenes) { Write-Host "      $($scene.path)" -ForegroundColor Red }
        return
    }

    $profileText = if ($originalProfile) { $originalProfile } else { 'none' }
    Write-Host "      target: $originalTarget; profile: $profileText; buildAppBundle: $originalAppBundle; package: $packageId" -ForegroundColor DarkGray

    $buildArgs = @('--target', 'Android', '--outputPath', $outputPath)
    if (-not $Release) { $buildArgs += @('--options', '["Development","ConnectWithProfiler"]') }
    $scenesJson = ''
    if ($sceneList.Count -gt 0) {
        $scenesJson = $sceneList | ConvertTo-Json -AsArray -Compress
        $buildArgs += @('--scenes', $scenesJson)
    }

    # buildAppBundle is per-user Library state that the build reads; an AAB would not install.
    if ($originalAppBundle -eq 'true') {
        Write-Host '      buildAppBundle is on; switching it off for the APK (restored afterwards).' -ForegroundColor Yellow
        $appBundleChanged = $true
        if (-not (Invoke-AppBundleWrite -Value $false)) {
            Write-Host 'FAIL  buildAppBundle did not switch off; reading it back returned a different value.' -ForegroundColor Red
            return
        }
    }

    Write-UnityHeader 'unity build -- dry run'
    $dry = Invoke-UnityCommand -Command 'build' -Arguments ($buildArgs + @('--dry_run', 'true'))
    if (-not (Test-UnityEnvelope -Envelope $dry -What 'build dry run')) { return }

    if (-not $dry.data.result.valid) {
        Write-Host 'FAIL  the dry run reported the build as invalid:' -ForegroundColor Red
        foreach ($e in $dry.data.result.validationErrors) { Write-Host "      $e" -ForegroundColor Red }
        return
    }

    # Rule 12: data.parameters is what the CLI actually parsed; a dropped argument looks like success.
    $echoed = $dry.data.parameters
    $echoedOptions = if ($echoed.PSObject.Properties['options']) { "$($echoed.options)" } else { '' }
    if ($Release -and $echoedOptions -match 'Development') {
        Write-Host "FAIL  a release build echoed a Development option: $echoedOptions" -ForegroundColor Red
        return
    }
    if (-not $Release -and ($echoedOptions -notmatch 'Development' -or $echoedOptions -notmatch 'ConnectWithProfiler')) {
        Write-Host "FAIL  a development option was dropped; the bridge echoed options '$echoedOptions'." -ForegroundColor Red
        return
    }
    if ($sceneList.Count -gt 0) {
        # The echo is a JSON string or an already parsed array; compare the scene lists, not the spelling.
        $echoedScenes = @()
        if ($echoed.PSObject.Properties['scenes']) {
            $value = $echoed.scenes
            if ($value -is [string]) {
                try { $echoedScenes = @($value | ConvertFrom-Json) } catch { $echoedScenes = @($value) }
            }
            else { $echoedScenes = @($value) }
        }
        if (($echoedScenes -join '|') -ne ($sceneList -join '|')) {
            Write-Host "FAIL  the scenes argument was dropped or altered; the bridge echoed '$($echoedScenes -join ',')'." -ForegroundColor Red
            return
        }
    }
    Write-Host "OK    dry run valid ($flavour, $outputPath)." -ForegroundColor Green
    Write-Host "      echoed: options='$echoedOptions' scenes='$(if ($sceneList.Count -gt 0) { $sceneList -join ',' } else { '<build settings list>' })'" -ForegroundColor DarkGray

    if ($DryRun) {
        Write-Host '      -DryRun: stopping before the build.' -ForegroundColor DarkGray
        $buildOk = $true
        $buildFinished = $true
        return
    }

    Write-UnityHeader 'unity build'

    # build_status keeps the previous build's report until the next one starts, so a poll only counts once it
    # differs from the payload taken now. No previous report at all is an empty baseline, not an error.
    $baselineEnvelope = Invoke-UnityCommand -Command 'build_status' -TimeoutSeconds 20
    $baseline = Get-UnityStatusRaw -Envelope $baselineEnvelope
    if ($null -eq $baseline) { $baseline = '' }

    # Without this a stale APK would satisfy the existence check below.
    Remove-Item -LiteralPath $outputPath -Force -ErrorAction SilentlyContinue

    $buildStarted = $true
    $dispatchedAt = [DateTime]::UtcNow
    $dispatch = Invoke-UnityCommand -Command 'build' -Arguments ($buildArgs + @('--confirm', 'true')) -TimeoutSeconds 60
    if (-not (Test-UnityEnvelope -Envelope $dispatch -What 'build')) {
        # A response that says no means the build never started; silence means it might have.
        if ($null -ne $dispatch) { $buildStarted = $false }
        return
    }

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $sawActivity = $false
    for ($i = 1; $i -le $MaxPolls; $i++) {
        # Spaced, and tolerant of silence: the bridge drops across a domain reload.
        Start-Sleep -Seconds $PollSeconds

        $envelope = Invoke-UnityCommand -Command 'build_status' -TimeoutSeconds 20
        if ($null -eq $envelope -or -not $envelope.success) {
            Write-Host ("      [{0,6:N1}s] bridge silent (domain reload) -- still waiting" -f $sw.Elapsed.TotalSeconds) -ForegroundColor DarkGray
            continue
        }

        $status = Get-UnityStatusResult -Envelope $envelope
        if ($null -eq $status -or -not $status.PSObject.Properties['status']) { continue }

        $raw = Get-UnityStatusRaw -Envelope $envelope
        Write-Host ("      [{0,6:N1}s] {1}" -f $sw.Elapsed.TotalSeconds, $status.status) -ForegroundColor DarkGray

        if ($status.status -in @('queued', 'building')) { $sawActivity = $true; continue }
        if ($status.status -ne 'completed') { continue }

        # A completed report belongs to this build when it differs from the baseline, when this build was seen
        # running, or when the APK deleted before dispatch is back. A report that started before dispatch is stale.
        $isNew = ($raw -ne $baseline) -or $sawActivity -or (Test-Path -LiteralPath $outputPath)
        if ($status.PSObject.Properties['buildStartedAt']) {
            try {
                if (([DateTime]$status.buildStartedAt).ToUniversalTime() -lt $dispatchedAt.AddSeconds(-30)) { $isNew = $false }
            }
            catch { Write-Verbose 'buildStartedAt is not a date; relying on the other signals.' }
        }
        if (-not $isNew) {
            Write-Host '      completed report predates this build -- still waiting' -ForegroundColor DarkGray
            continue
        }

        $buildFinished = $true

        # The report carries the verdict at the top level; a nested summary is accepted as well.
        $result = ''
        if ($status.PSObject.Properties['result']) { $result = "$($status.result)" }
        elseif ($status.PSObject.Properties['summary'] -and $status.summary.PSObject.Properties['result']) { $result = "$($status.summary.result)" }

        if (-not $result) {
            Write-Host 'FAIL  build_status completed without a result; treating it as a failure.' -ForegroundColor Red
        }
        elseif ($result -ne 'Succeeded') {
            Write-Host "FAIL  build result: $result" -ForegroundColor Red
        }

        if ($result -ne 'Succeeded') {
            if ($status.PSObject.Properties['errors']) {
                foreach ($e in $status.errors) {
                    $text = if ($e -is [string]) { $e } elseif ($e.PSObject.Properties['message']) { $e.message } else { ($e | ConvertTo-Json -Compress) }
                    Write-Host "      $text" -ForegroundColor Red
                }
            }
            return
        }

        Write-Host ("OK    build Succeeded in {0:N1}s." -f $sw.Elapsed.TotalSeconds) -ForegroundColor Green
        if (-not (Test-Path -LiteralPath $outputPath)) {
            Write-Host "FAIL  the build reported success but $outputPath does not exist." -ForegroundColor Red
            return
        }

        $size = (Get-Item -LiteralPath $outputPath).Length / 1MB
        Write-Host ("      {0} ({1:N1} MB)" -f $outputPath, $size) -ForegroundColor Cyan
        $buildOk = $true
        return
    }

    Write-Host 'FAIL  the build did not finish within the poll ceiling.' -ForegroundColor Red
}
catch {
    Write-Host "FAIL  $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    # Runs for a normal end, a refusal, an exception and Ctrl+C alike. A build that was dispatched and never
    # seen finishing is the unknown state, whatever interrupted the wait.
    if ($buildStarted -and -not $buildFinished) {
        # A build may still be running or may not have read its settings yet: change nothing.
        Write-UnityHeader 'unknown state'
        Write-Host 'FAIL  the build outcome is unknown. Nothing was changed.' -ForegroundColor Red
        Write-Host 'The project may be dirty and the Editor may be on Android. Recover by hand, in this order:' -ForegroundColor Yellow
        Write-Host '  1. unity cmd build_status --no-banner --json   (wait until it reports completed)'
        Write-Host '  2. git status --short, then revert the known build side effects:'
        foreach ($pattern in $ModifiedAllowlist) { Write-Host "       git restore --worktree -- '$pattern'" }
        foreach ($pattern in $UntrackedAllowlist) { Write-Host "       delete $pattern" }
        foreach ($folder in $BuildCreatedFolders) {
            if (-not $existedAtStart[$folder]) { Write-Host "       delete $folder and $folder.meta if empty (it did not exist before)" }
        }
        Write-Host "  3. unity cmd switch_build_target --target $originalTarget --confirm true"
        if ($originalProfile) { Write-Host "     then set the Build Profile back to $originalProfile in the Build Profiles window" }
        if ($appBundleChanged) { Write-Host "  4. restore buildAppBundle to $originalAppBundle (EditorUserBuildSettings.buildAppBundle)" }
        $exitCode = 1
    }
    else {
        if ($appBundleChanged) {
            Invoke-RestoreStep -What 'buildAppBundle restore' -Body {
                if (Invoke-AppBundleWrite -Value ($originalAppBundle -eq 'true')) { $script:appBundleChanged = $false }
                else { throw "buildAppBundle could not be restored to $originalAppBundle." }
            }
        }

        if ($originalTarget) {
            Invoke-RestoreStep -What 'build target restore' -Body {
                $currentTarget = Get-ActiveBuildTarget
                if ($currentTarget -and $currentTarget -ne $originalTarget) {
                    if (-not (Switch-UnityBuildTarget -Target $originalTarget)) { throw "could not switch back to $originalTarget." }
                }
            }

            Invoke-RestoreStep -What 'Build Profile restore' -Body {
                $currentProfile = Get-ActiveBuildProfilePath
                if ($null -ne $currentProfile -and $currentProfile -ne $originalProfile) {
                    $wanted = if ($originalProfile) { $originalProfile } else { 'none' }
                    if (-not (Invoke-ActiveProfileWrite -Path $originalProfile)) {
                        throw "the active Build Profile could not be restored; it was '$wanted'. Set it back in the Build Profiles window."
                    }
                    Write-Host "OK    active Build Profile restored to '$wanted'." -ForegroundColor Green
                }
            }
        }

        # A dry run touches no files and opens no scenes, so there is nothing to restore.
        if ($buildStarted) {
            Invoke-RestoreStep -What 'project restore' -Body {
                $created = @($BuildCreatedFolders | Where-Object { -not $existedAtStart[$_] })
                if (-not (Restore-ProjectAndVerify -CreatedFolders $created)) { $script:restoreOk = $false }
            }

            Invoke-RestoreStep -What 'open scene restore' -Body {
                if (-not (Restore-OpenScene -Recorded $recordedScenes)) {
                    Write-Host 'WARN  open scenes could not be fully restored; check list_open_scenes and discard dirty ones with open_scene (Rule 10).' -ForegroundColor Yellow
                }
            }
        }

        $exitCode = if ($buildOk -and $restoreOk) { 0 } else { 1 }

        if ($exitCode -eq 0 -and $Install -and -not $DryRun) {
            Invoke-RestoreStep -What 'adb install' -Body {
                if (-not (Install-Apk -ApkPath (Resolve-Path -LiteralPath $outputPath).Path -PackageId $packageId)) { $script:exitCode = 1 }
            }
            if (-not $restoreOk) { $exitCode = 1 }
        }
    }

    Pop-Location
    exit $exitCode
}
