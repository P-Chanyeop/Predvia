$ErrorActionPreference = "Stop"

$csprojPath = "Gumaedaehang.csproj"
$csproj = [System.IO.File]::ReadAllText($csprojPath, [System.Text.Encoding]::UTF8)
$match = [regex]::Match($csproj, '<AssemblyVersion>(\d+)\.(\d+)\.(\d+)\.(\d+)</AssemblyVersion>')
$major = $match.Groups[1].Value
$minor = $match.Groups[2].Value
$patch = [int]$match.Groups[3].Value + 1
$newVer = "$major.$minor.$patch"
$newVerFull = "$newVer.0"

Write-Host "========================================"
Write-Host "  Predvia v$newVer packaging"
Write-Host "========================================"

# .env 파일에서 환경변수 로드
if (Test-Path ".env") {
    Get-Content ".env" | ForEach-Object {
        if ($_ -match '^([^=]+)=(.*)$') {
            [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2], "Process")
        }
    }
    Write-Host "  .env loaded"
} else {
    Write-Host "  WARNING: .env not found!"
}

$csproj = $csproj -replace '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$newVerFull</AssemblyVersion>"
$csproj = $csproj -replace '<FileVersion>.*?</FileVersion>', "<FileVersion>$newVerFull</FileVersion>"
[System.IO.File]::WriteAllText($csprojPath, $csproj, [System.Text.Encoding]::UTF8)

Write-Host "[1/3] Build..."
dotnet publish $csprojPath -c Release -r win-x64 --self-contained -o publish-squirrel
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed!"; pause; exit 1 }

# .env를 배포 폴더에 복사 (실행 시 환경변수 로드용)
if (Test-Path ".env") {
    Copy-Item ".env" "publish-squirrel\.env" -Force
    Write-Host "  .env copied to publish-squirrel"
}

Write-Host "[2/3] Squirrel pack v$newVer..."
$squirrelExe = Join-Path $env:USERPROFILE ".nuget\packages\clowd.squirrel\2.11.1\tools\Squirrel.exe"
& $squirrelExe pack --packId Predvia --packVersion $newVer --packDir publish-squirrel --releaseDir releases --allowUnaware
if ($LASTEXITCODE -ne 0) { Write-Host "Pack failed!"; pause; exit 1 }

Write-Host "[3/3] Done! v$newVer"
Write-Host "Upload releases folder to GitHub Releases"
pause
