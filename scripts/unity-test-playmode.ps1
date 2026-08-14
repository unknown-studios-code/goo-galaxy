# Run the PlayMode suite through the open Unity Editor.
#
#   npm run unity:test:playmode                          -- the whole suite
#   npm run unity:test:playmode -- AbilityControllerTests -- only matching tests
#
# The filter is a case-insensitive partial match on the full test name, and it is
# positional: there is no flag name to misspell. Exit 0 only when the project
# compiled, tests actually ran, and every one of them passed.
#
# PlayMode MUST run asynchronously: entering play mode triggers a domain reload
# that drops a synchronous HTTP request. Asked synchronously, the Editor refuses
# and answers with envelope success:true, inner success:false and a zeroed
# Summary -- which reads as a green suite. This script never asks that way.

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Filter = ''
)

. "$PSScriptRoot/_unity-lib.ps1"

$PollSeconds = 5
$MaxPolls = 120   # ceiling of ~10 minutes

# Compile first, and refuse to run otherwise. A suite launched against a broken
# compile executes the previously built assemblies and reports them as green.
if (-not (Invoke-UnityRecompileGate)) {
    Write-Host 'FAIL  refusing to run PlayMode against a project that does not compile.' -ForegroundColor Red
    exit 1
}

$scope = if ($Filter) { "PlayMode -- filter '$Filter'" } else { 'PlayMode' }
Write-UnityHeader "unity run_tests -- $scope (async)"

# Baseline the retained payload BEFORE dispatching. test_status keeps the previous
# run's result, so an early poll would otherwise read a stale 'completed'.
$baseline = Get-UnityStatusRaw -Envelope (Invoke-UnityCommand -Command 'test_status' -TimeoutSeconds 15)

$commandArgs = @('--mode', 'playmode', '--async_tests', 'true')
if ($Filter) { $commandArgs += @('--filter', $Filter) }

$dispatch = Invoke-UnityCommand -Command 'run_tests' -Arguments $commandArgs
if (-not (Test-UnityEnvelope -Envelope $dispatch -What "run_tests ($scope dispatch)")) { exit 1 }

Write-Host '      dispatched; entering play mode...' -ForegroundColor DarkGray

$sw = [Diagnostics.Stopwatch]::StartNew()
$final = $null

for ($i = 1; $i -le $MaxPolls; $i++) {
    Start-Sleep -Seconds $PollSeconds

    $envelope = Invoke-UnityCommand -Command 'test_status' -TimeoutSeconds 15

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

    if ($state -eq 'completed') { $final = $status; break }
}

if ($null -eq $final) {
    Write-Host 'FAIL  PlayMode did not complete within the poll ceiling.' -ForegroundColor Red
    Write-Host "      Check 'unity status' and whether the Editor is stuck in play mode."
    exit 1
}

if (-not $final.PSObject.Properties['summary']) {
    Write-Host 'FAIL  completed without a summary -- refusing to report a result.' -ForegroundColor Red
    exit 1
}

# test_status uses lowercase keys; run_tests uses PascalCase. This is the lowercase side.
$summary = $final.summary

# A zeroed summary is how both a refused run and an unmatched filter look.
if ($summary.total -le 0) {
    if ($Filter) {
        Write-Host "FAIL  no test matched '$Filter'." -ForegroundColor Red
        Write-Host '      The filter is a partial match on the full test name. List candidates with:'
        Write-Host '        unity cmd list_tests --mode playmode --no-banner --json'
    }
    else {
        Write-Host 'FAIL  the suite reported 0 tests -- it did not run.' -ForegroundColor Red
    }
    exit 1
}

Write-Host ("      total={0} passed={1} failed={2} skipped={3} inconclusive={4} in {5:N1}s" -f `
        $summary.total, $summary.passed, $summary.failed, $summary.skipped, $summary.inconclusive, $sw.Elapsed.TotalSeconds)

if ($summary.failed -gt 0 -or $summary.inconclusive -gt 0) {
    Write-Host ''
    foreach ($t in ($final.results | Where-Object { $_.Status -ne 'Passed' })) {
        Write-Host "FAIL  $($t.FullName)" -ForegroundColor Red
        if ($t.Message) { Write-Host "      $($t.Message)" -ForegroundColor DarkGray }
        if ($t.StackTrace) { Write-Host "      $(($t.StackTrace -split "`n")[0])" -ForegroundColor DarkGray }
    }
    Write-Host ''
    Write-Host ("FAIL  $scope`: {0} of {1} failed." -f $summary.failed, $summary.total) -ForegroundColor Red
    exit 1
}

Write-Host ("OK    $scope`: {0}/{1} passed." -f $summary.passed, $summary.total) -ForegroundColor Green
exit 0
