param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin'))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4 compiler is required.' }
& $compiler /nologo /target:winexe /optimize+ /out:"$OutputDirectory\Lattice.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll /reference:System.Core.dll "$PSScriptRoot\src\Lattice.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output "Built $OutputDirectory\Lattice.exe"
