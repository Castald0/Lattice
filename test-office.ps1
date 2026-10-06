$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\build.ps1"
$bin = Join-Path $PSScriptRoot 'bin'
$wordExe = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\WINWORD.EXE').'(default)'
$excelExe = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\excel.exe').'(default)'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /out:"$bin\OfficeSmoke.exe" /reference:"$bin\Lattice.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "$PSScriptRoot\tests\OfficeSmoke.cs"
if ($LASTEXITCODE -ne 0) { throw 'Office-test build failed' }
$arguments = '"{0}" "{1}"' -f $wordExe, $excelExe
$run = Start-Process -FilePath "$bin\OfficeSmoke.exe" -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $run.WaitForExit(100000)) { $run.Kill(); throw 'Office tests timed out' }
Get-Content -LiteralPath "$bin\office-result.txt"
if ($run.ExitCode -ne 0) { throw 'Office integration tests failed' }
