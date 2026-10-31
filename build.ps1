$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
$fx = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fx 'csc.exe'
$wpf = Join-Path $fx 'WPF'
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }
$out = Join-Path $root 'build\MiniPerfApp.exe'
New-Item -ItemType Directory -Force (Join-Path $root 'build') | Out-Null
$src = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
& $csc /nologo /target:winexe /platform:anycpu /codepage:65001 /optimize+ /out:$out `
  /r:"$wpf\PresentationFramework.dll" /r:"$wpf\PresentationCore.dll" /r:"$wpf\WindowsBase.dll" `
  /r:"$fx\System.Xaml.dll" /r:System.Windows.Forms.dll /r:System.Drawing.dll $src
if ($LASTEXITCODE -ne 0) { throw "build failed: $LASTEXITCODE" } else { Write-Host "build ok -> $out" }

