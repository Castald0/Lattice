param([switch]$IncludeWindowTests, [switch]$RequireDragInput)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\build.ps1"
$bin = Join-Path $PSScriptRoot 'bin'
$test = Start-Process -FilePath "$bin\Lattice.exe" -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
Get-Content -LiteralPath "$bin\self-test-result.txt"
if ($test.ExitCode -ne 0) { throw 'Core tests failed' }
if ($IncludeWindowTests -or $RequireDragInput) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /target:winexe /out:"$bin\SmokeTest.exe" /reference:"$bin\Lattice.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\tests\SmokeTest.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Window-test build failed' }
    $start = @{ FilePath = "$bin\SmokeTest.exe"; WindowStyle = 'Hidden'; PassThru = $true; Wait = $true }
    if ($RequireDragInput) { $start.ArgumentList = '--require-input' }
    $smoke = Start-Process @start
    Get-Content -LiteralPath "$bin\smoke-result.txt"
    if ($smoke.ExitCode -ne 0) { throw 'Window tests failed' }
}
