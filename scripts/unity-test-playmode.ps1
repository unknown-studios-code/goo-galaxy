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

function Write-NoTestMatched {
    Write-Host "FAIL  no test matched '$Filter'." -ForegroundColor Red
    Write-Host '      The filter is a partial match on the full test name, e.g. PointerInputViewTests.'
    Write-Host '      List candidates with:'
    Write-Host '        unity cmd list_tests --mode playmode --no-banner --json'
}

# Compile first, and refuse to run otherwise. A suite launched against a broken
# compile executes the previously built assemblies and reports them as green.
if (-not (Invoke-UnityRecompileGate)) {
    Write-Host 'FAIL  refusing to run PlayMode against a project that does not compile.' -ForegroundColor Red
    exit 1
}

$scope = if ($Filter) { "PlayMode -- filter '$Filter'" } else { 'PlayMode' }
Write-UnityHeader "unity run_tests -- $scope"

# A filter is checked against the listing before anything is dispatched. A run that matches nothing
# completes with a payload carrying no run identity -- zero duration, no results -- so two in a row
# are byte-identical, and the baseline check below would sit out the whole poll ceiling waiting for
# a result that was ready in under a second. Checking first also answers a typo without entering
# play mode. The match mirrors run_tests: case-insensitive, partial, on the full test name.
if ($Filter) {
    $listing = Invoke-UnityCommand -Command 'list_tests' -Arguments @('--mode', 'playmode') -TimeoutSeconds 60
    if (-not (Test-UnityEnvelope -Envelope $listing -What "list_tests ($scope)")) { exit 1 }

    $matched = @($listing.data.result.Tests | Where-Object { $_.FullName.IndexOf($Filter, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
    if ($matched.Count -eq 0) {
        Write-NoTestMatched
        exit 1
    }
}

# Baseline the retained payload BEFORE dispatching. test_status keeps the previous
# run's result, so an early poll would otherwise read a stale 'completed'.
$baseline = Get-UnityStatusRaw -Envelope (Invoke-UnityCommand -Command 'test_status' -TimeoutSeconds 15)

# A whole-suite run is scoped to this project's own test assembly. Packages listed under
# "testables" in Packages/manifest.json (com.unity.inputsystem, for InputTestFixture) put their
# own tests in the runner too, and one of them rewrites the active input backend in Player
# Settings. A name filter can still reach those tests if it happens to match one.
$commandArgs = @('--mode', 'playmode', '--async_tests', 'true')
if ($Filter) { $commandArgs += @('--filter', $Filter) }
else { $commandArgs += @('--filter', 'GooGalaxy.Tests.PlayMode', '--filter_type', 'assembly') }

$dispatch = Invoke-UnityCommand -Command 'run_tests' -Arguments $commandArgs
if (-not (Test-UnityEnvelope -Envelope $dispatch -What "run_tests ($scope)")) { exit 1 }

Write-Host '      dispatched; entering play mode...' -ForegroundColor DarkGray

$sw = [Diagnostics.Stopwatch]::StartNew()
$final = $null

# Only until this run shows up. Once any poll differs from the baseline the run is under way, and
# its own completed result may match the previous one exactly, so it must not be compared again.
$runObserved = $false

for ($i = 1; $i -le $MaxPolls; $i++) {
    Start-Sleep -Seconds $PollSeconds

    $envelope = Invoke-UnityCommand -Command 'test_status' -TimeoutSeconds 15

    if ($null -eq $envelope -or -not $envelope.success) {
        Write-Host ("      [{0,6:N1}s] bridge silent (domain reload) -- still waiting" -f $sw.Elapsed.TotalSeconds) -ForegroundColor DarkGray
        continue
    }

    $raw = Get-UnityStatusRaw -Envelope $envelope
    if (-not $runObserved -and $null -ne $baseline -and $raw -eq $baseline) {
        Write-Host ("      [{0,6:N1}s] status unchanged -- previous run's result, still waiting" -f $sw.Elapsed.TotalSeconds) -ForegroundColor DarkGray
        continue
    }

    $runObserved = $true

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
    Write-Host 'FAIL  the result carried no summary -- refusing to report a result.' -ForegroundColor Red
    exit 1
}

# test_status uses lowercase keys; run_tests uses PascalCase. This is the lowercase side.
$summary = $final.summary

# A zeroed summary is how both a refused run and an unmatched filter look.
if ($summary.total -le 0) {
    if ($Filter) {
        Write-NoTestMatched
    }
    else {
        Write-Host 'FAIL  the suite reported 0 tests -- it did not run.' -ForegroundColor Red
        if ($final.PSObject.Properties['error'] -and $final.error) { Write-Host "      $($final.error)" -ForegroundColor Red }
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
