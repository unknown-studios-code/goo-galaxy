# Run the EditMode suite through the open Unity Editor.
#
#   npm run unity:test:editmode                     -- the whole suite
#   npm run unity:test:editmode -- Board.HexTests   -- only matching tests
#
# The filter is a case-insensitive partial match on the full test name, and it is
# positional: there is no flag name to misspell. Exit 0 only when the project
# compiled, tests actually ran, and every one of them passed.
#
# EditMode runs synchronously and returns its results inline, so there is nothing
# to poll here. PlayMode cannot -- use unity-test-playmode.ps1 for that.

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Filter = ''
)

. "$PSScriptRoot/_unity-lib.ps1"

# Compile first, and refuse to run otherwise. A suite launched against a broken
# compile executes the previously built assemblies and reports them as green.
if (-not (Invoke-UnityRecompileGate)) {
    Write-Host 'FAIL  refusing to run EditMode against a project that does not compile.' -ForegroundColor Red
    exit 1
}

$scope = if ($Filter) { "EditMode -- filter '$Filter'" } else { 'EditMode' }
Write-UnityHeader "unity run_tests -- $scope"

$commandArgs = @('--mode', 'editor')
if ($Filter) { $commandArgs += @('--filter', $Filter) }
else { Write-Host '      running (about 12s for 519 tests; the Editor must stay open)...' -ForegroundColor DarkGray }

$sw = [Diagnostics.Stopwatch]::StartNew()
$envelope = Invoke-UnityCommand -Command 'run_tests' -Arguments $commandArgs -TimeoutSeconds 900
$sw.Stop()

if (-not (Test-UnityEnvelope -Envelope $envelope -What "run_tests ($scope)")) { exit 1 }

$result = $envelope.data.result
if ($null -eq $result -or -not $result.PSObject.Properties['Summary']) {
    Write-Host 'FAIL  response carried no Summary -- refusing to report a result.' -ForegroundColor Red
    exit 1
}

$summary = $result.Summary

# A zeroed Summary is how both a refused run and an unmatched filter look.
# Neither is a pass, but they need different advice.
if ($summary.Total -le 0) {
    if ($Filter) {
        Write-Host "FAIL  no test matched '$Filter'." -ForegroundColor Red
        Write-Host '      The filter is a partial match on the full test name, e.g. Board.HexTests'
        Write-Host '      or HexTests.Neighbours_AtEdge_ReturnsThree. List candidates with:'
        Write-Host '        unity cmd list_tests --mode editor --no-banner --json'
    }
    else {
        Write-Host 'FAIL  the suite reported 0 tests -- it did not run.' -ForegroundColor Red
        if ($result.PSObject.Properties['error'] -and $result.error) { Write-Host "      $($result.error)" -ForegroundColor Red }
    }
    exit 1
}

Write-Host ("      total={0} passed={1} failed={2} skipped={3} inconclusive={4} in {5:N1}s" -f `
        $summary.Total, $summary.Passed, $summary.Failed, $summary.Skipped, $summary.Inconclusive, $sw.Elapsed.TotalSeconds)

if ($summary.Failed -gt 0 -or $summary.Inconclusive -gt 0) {
    Write-Host ''
    foreach ($t in ($result.Results | Where-Object { $_.Status -ne 'Passed' })) {
        Write-Host "FAIL  $($t.FullName)" -ForegroundColor Red
        if ($t.Message) { Write-Host "      $($t.Message)" -ForegroundColor DarkGray }
        if ($t.StackTrace) { Write-Host "      $(($t.StackTrace -split "`n")[0])" -ForegroundColor DarkGray }
    }
    Write-Host ''
    Write-Host ("FAIL  $scope`: {0} of {1} failed." -f $summary.Failed, $summary.Total) -ForegroundColor Red
    exit 1
}

Write-Host ("OK    $scope`: {0}/{1} passed." -f $summary.Passed, $summary.Total) -ForegroundColor Green
exit 0
