# Shared helpers for the unity-* scripts in this folder.
# Dot-sourced, never run directly. Everything here encodes a trap verified against
# a live Editor — see .claude/rules/unity-editor-automation.md for the why.

Set-StrictMode -Version Latest

# Invoke an Editor command and return the parsed envelope, or $null when the
# bridge could not be reached. Rule 12: every argument must carry its dashes,
# because a bare key=value is silently dropped and the command runs with defaults.
function Invoke-UnityCommand {
    param(
        [Parameter(Mandatory)][string]$Command,
        [string[]]$Arguments = @(),
        [int]$TimeoutSeconds = 20
    )

    $argv = @('cmd', $Command) + $Arguments + @('--no-banner', '--json', '--timeout', "$TimeoutSeconds")
    $raw = & unity @argv 2>&1 | Out-String

    try {
        return $raw | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

# Rule 15: `success` has two layers and the outer one only means "the CLI reached
# the Editor". The command's own verdict is data.result.success, and reading the
# outer one alone turns a refusal into a false green.
function Test-UnityEnvelope {
    param($Envelope, [string]$What)

    if ($null -eq $Envelope) {
        Write-Host "FAIL  $What -- no parseable response from the bridge." -ForegroundColor Red
        Write-Host "      Run 'unity status' to check the Editor is alive; do not relaunch it on a failed call."
        return $false
    }

    if (-not $Envelope.success) {
        $msg = if ($Envelope.PSObject.Properties['errors'] -and $Envelope.errors) { $Envelope.errors[0].message } else { 'unknown bridge error' }
        Write-Host "FAIL  $What -- bridge: $msg" -ForegroundColor Red
        return $false
    }

    $result = $Envelope.data.result
    if ($null -ne $result -and $result.PSObject.Properties['success'] -and -not $result.success) {
        $err = if ($result.PSObject.Properties['error'] -and $result.error) { $result.error } else { 'command reported failure' }
        Write-Host "FAIL  $What -- command: $err" -ForegroundColor Red
        return $false
    }

    return $true
}

# Rule 13: every *_status command except editor_status returns data.result as a
# JSON *string*, so it needs a second parse before any field is readable.
function Get-UnityStatusResult {
    param($Envelope)

    if ($null -eq $Envelope) { return $null }
    $result = $Envelope.data.result
    if ($null -eq $result) { return $null }

    if ($result -is [string]) {
        try { return $result | ConvertFrom-Json } catch { return $null }
    }

    return $result
}

# The raw, unparsed payload — used as a staleness baseline. Status commands retain
# the previous run's result, so a poll issued too early reads a stale 'completed'
# from the run before this one.
function Get-UnityStatusRaw {
    param($Envelope)

    if ($null -eq $Envelope) { return $null }
    $result = $Envelope.data.result
    if ($null -eq $result) { return $null }
    if ($result -is [string]) { return $result }
    return ($result | ConvertTo-Json -Depth 20 -Compress)
}

function Write-UnityHeader {
    param([string]$Text)
    Write-Host ""
    Write-Host "== $Text" -ForegroundColor Cyan
}

# Is the project currently broken? Returns $true when compilation is failing.
#
# `recompile_status.failed` is NOT usable for this: it describes only the last
# recompile *attempt*, and a later dispatch that finds nothing to do overwrites it
# with a clean {status: up_to_date, failed: false, errors: []} while the project is
# still broken. EditorUtility.scriptCompilationFailed is the durable state, and it
# stayed True across exactly that sequence.
function Test-UnityCompilationFailed {
    $envelope = Invoke-UnityCommand -Command 'eval' `
        -Arguments @('--code', 'return UnityEditor.EditorUtility.scriptCompilationFailed ? "FAILED" : "OK";')

    if (-not (Test-UnityEnvelope -Envelope $envelope -What 'compile-state probe')) {
        # Cannot tell -- treat as broken rather than let a false green through.
        return $true
    }

    return ($envelope.data.result.result -eq 'FAILED')
}

# Print whatever compile errors the console still holds. The buffer is not
# guaranteed to retain them, so this is a courtesy, never the verdict.
function Write-UnityCompileErrors {
    $envelope = Invoke-UnityCommand -Command 'get_console_logs' -Arguments @('--severity', 'error', '--limit', '25')
    if ($null -eq $envelope -or -not $envelope.success) { return }

    # Set-StrictMode turns a missing property into a terminating error, so probe first.
    $result = $envelope.data.result
    $entries = $null
    if ($null -ne $result -and $result.PSObject.Properties['entries']) { $entries = $result.entries }

    if (-not $entries) {
        Write-Host "      (the console buffer holds no errors -- open the Editor's Console for detail)" -ForegroundColor DarkGray
        return
    }

    foreach ($e in $entries) { Write-Host "      $($e.message.Split("`n")[0])" -ForegroundColor Red }
}

# Compile through the Editor and wait for it to settle. Returns $true when the
# Editor is settled and the compile succeeded, $false otherwise.
#
# Every test run must pass this gate first. A run started against a broken or
# unfinished compile silently executes the PREVIOUSLY compiled assemblies and
# reports their results -- measured here as a green 519/519 while the project
# did not compile at all.
function Invoke-UnityRecompileGate {
    $PollSeconds = 3
    $MaxPolls = 60   # ceiling of ~3 minutes; a bounded loop cannot hang the session

    Write-UnityHeader 'unity recompile'

    # The dispatch answers one of two ways, both verified: `up_to_date` synchronously
    # when nothing needs compiling, or a null result when a compile is now in flight.
    $dispatch = Invoke-UnityCommand -Command 'recompile'
    if (-not (Test-UnityEnvelope -Envelope $dispatch -What 'recompile')) { return $false }

    $immediate = $dispatch.data.result
    if ($null -ne $immediate -and $immediate.PSObject.Properties['status'] -and $immediate.status -eq 'up_to_date') {
        if (Test-UnityCompilationFailed) {
            Write-Host 'FAIL  the project does not compile, and nothing has changed since the last attempt.' -ForegroundColor Red
            Write-UnityCompileErrors
            return $false
        }
        Write-Host 'OK    up_to_date -- no scripts needed recompilation.' -ForegroundColor Green
        return $true
    }

    # Baseline the retained payload so a stale 'completed' from the previous compile
    # is not mistaken for this one finishing.
    $baseline = Get-UnityStatusRaw -Envelope (Invoke-UnityCommand -Command 'recompile_status' -TimeoutSeconds 10)

    $sw = [Diagnostics.Stopwatch]::StartNew()
    for ($i = 1; $i -le $MaxPolls; $i++) {
        # Sleep before the first poll: the bridge goes silent across the domain reload,
        # and a tight loop just burns each call's timeout against a dead socket.
        Start-Sleep -Seconds $PollSeconds

        $envelope = Invoke-UnityCommand -Command 'recompile_status' -TimeoutSeconds 10

        if ($null -eq $envelope -or -not $envelope.success) {
            Write-Host ("      [{0,6:N1}s] bridge silent (domain reload) -- still waiting" -f $sw.Elapsed.TotalSeconds) -ForegroundColor DarkGray
            continue
        }

        $raw = Get-UnityStatusRaw -Envelope $envelope
        if ($null -ne $baseline -and $raw -eq $baseline) {
            Write-Host ("      [{0,6:N1}s] status unchanged -- previous run's result, still waiting" -f $sw.Elapsed.TotalSeconds) -ForegroundColor DarkGray
            continue
        }

        $status = Get-UnityStatusResult -Envelope $envelope
        if ($null -eq $status) { continue }

        $state = if ($status.PSObject.Properties['status']) { $status.status } else { '<none>' }
        Write-Host ("      [{0,6:N1}s] {1}" -f $sw.Elapsed.TotalSeconds, $state) -ForegroundColor DarkGray

        if ($state -in @('completed', 'up_to_date')) {
            # Prefer the errors this run reported; fall back to the durable state,
            # which stays true even after the status payload is overwritten.
            if ($status.PSObject.Properties['failed'] -and $status.failed) {
                Write-Host 'FAIL  compilation failed.' -ForegroundColor Red
                foreach ($e in $status.errors) { Write-Host "      $e" -ForegroundColor Red }
                return $false
            }
            if (Test-UnityCompilationFailed) {
                Write-Host 'FAIL  compilation is still failing after this run.' -ForegroundColor Red
                Write-UnityCompileErrors
                return $false
            }
            Write-Host ("OK    {0} in {1:N1}s -- .csproj / .slnx regenerated." -f $state, $sw.Elapsed.TotalSeconds) -ForegroundColor Green
            return $true
        }
    }

    Write-Host 'FAIL  recompile did not settle within the poll ceiling.' -ForegroundColor Red
    Write-Host "      Check 'unity status' and the Editor's console before retrying."
    return $false
}
