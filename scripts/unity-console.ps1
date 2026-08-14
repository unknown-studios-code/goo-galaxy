# Read the open Unity Editor's console.
#
#   npm run unity:console:mark     -- remember where the console is right now
#   npm run unity:console          -- show only what was logged since that mark
#   npm run unity:console -- error -- errors only
#   npm run unity:console -- log   -- everything, including plain logs
#   npm run unity:console -- error 200
#
# Both positional arguments are optional: level (log|warn|error, default warn)
# then tail count (default 25). There is no flag name to misspell.
#
# Exit 1 when the window being read holds any error, exception or assert.
# Warnings print but never fail the run.
#
# WHY THE MARK EXISTS. The buffer is not scoped to your change: it holds
# everything the Editor has logged, and the PlayMode suite deliberately logs
# errors it asserts on with LogAssert.Expect. Read straight after a green run it
# showed 39 errors here, every one intentional. So the exit code is only
# trustworthy over a window you defined:
#
#     npm run unity:console:mark   ->   do the thing   ->   npm run unity:console
#
# `clear_console` is NOT that mechanism. It answers {"cleared": true} and leaves
# this buffer untouched -- 200 entries and the same max seq before and after,
# measured. It also reads a different store from `get_console_logs`, which was
# empty while `console` held 200 entries. The cursor is the only thing that
# actually bounds a read, which is what --since takes.

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('log', 'warn', 'error')]
    [string]$Level = 'warn',

    [Parameter(Position = 1)]
    [ValidateRange(1, 1000)]
    [int]$Tail = 25,

    [switch]$Mark
)

. "$PSScriptRoot/_unity-lib.ps1"

$CursorFile = Join-Path $env:TEMP 'goo-galaxy-unity-console.cursor'

function Get-UnityConsole {
    param([string]$AtLeast, [int]$Count, [long]$Since = -1)

    $commandArgs = @('--level', $AtLeast, '--tail', "$Count")
    if ($Since -ge 0) { $commandArgs += @('--since', "$Since") }

    $envelope = Invoke-UnityCommand -Command 'console' -Arguments $commandArgs
    if (-not (Test-UnityEnvelope -Envelope $envelope -What 'console')) { return $null }
    return $envelope.data.result
}

# -- Mark: remember the current position and stop. -----------------------------
if ($Mark) {
    Write-UnityHeader 'unity console -- mark'

    $snapshot = Get-UnityConsole -AtLeast 'log' -Count 1
    if ($null -eq $snapshot) { exit 1 }

    Set-Content -Path $CursorFile -Value "$($snapshot.cursor)" -Encoding ascii
    Write-Host "OK    marked at seq $($snapshot.cursor)." -ForegroundColor Green
    Write-Host '      Everything logged from here on is what `npm run unity:console` will show.'
    exit 0
}

# -- Read ----------------------------------------------------------------------
$since = -1
if (Test-Path $CursorFile) {
    $stored = (Get-Content $CursorFile -Raw).Trim()
    if ($stored -match '^\d+$') { $since = [long]$stored }
}

$scope = if ($since -ge 0) { "since seq $since" } else { "last $Tail entries" }
Write-UnityHeader "unity console -- level '$Level', $scope"

$result = Get-UnityConsole -AtLeast $Level -Count $Tail -Since $since
if ($null -eq $result) { exit 1 }

if ($since -lt 0) {
    Write-Host '      No mark set, so this is the whole buffer, not your change.' -ForegroundColor DarkGray
    Write-Host '      Run `npm run unity:console:mark` first for an exit code you can trust.' -ForegroundColor DarkGray
}

$entries = $null
if ($result.PSObject.Properties['entries']) { $entries = $result.entries }

if (-not $entries) {
    Write-Host "OK    nothing at level '$Level' $scope." -ForegroundColor Green
    exit 0
}

if ($result.PSObject.Properties['dropped'] -and $result.dropped -gt 0) {
    Write-Host "      note: $($result.dropped) entries dropped from the buffer before this read." -ForegroundColor DarkGray
}

$errorCount = 0
$warnCount = 0

foreach ($e in $entries) {
    $lvl = if ($e.PSObject.Properties['level'] -and $e.level) { "$($e.level)".ToLowerInvariant() } else { 'log' }
    $head = ($e.message -split "`n")[0]

    if ($lvl -match 'error|exception|assert') {
        $errorCount++
        Write-Host "ERROR $head" -ForegroundColor Red
        # One stack frame is usually enough to locate it; the rest is noise.
        if ($e.PSObject.Properties['stackTrace'] -and $e.stackTrace) {
            $frame = (($e.stackTrace -split "`n") | Where-Object { $_.Trim() } | Select-Object -First 1)
            if ($frame) { Write-Host "      $($frame.Trim())" -ForegroundColor DarkGray }
        }
    }
    elseif ($lvl -match 'warn') {
        $warnCount++
        Write-Host "WARN  $head" -ForegroundColor Yellow
    }
    else {
        Write-Host "      $head" -ForegroundColor DarkGray
    }
}

Write-Host ''

if ($errorCount -gt 0) {
    Write-Host ("FAIL  {0} error(s), {1} warning(s) {2}." -f $errorCount, $warnCount, $scope) -ForegroundColor Red
    if ($since -lt 0) {
        Write-Host '      Most of these may predate your change -- set a mark and re-read.' -ForegroundColor DarkGray
    }
    exit 1
}

Write-Host ("OK    no errors; {0} warning(s) {1}." -f $warnCount, $scope) -ForegroundColor Green
exit 0
