@echo off
chcp 65001

:: csproj에서 현재 버전 읽기
for /f "tokens=2 delims=<>" %%a in ('findstr "AssemblyVersion" Gumaedaehang.csproj') do set CURRENT=%%a
for /f "tokens=1-3 delims=." %%a in ("%CURRENT%") do (
    set MAJOR=%%a
    set MINOR=%%b
    set /a PATCH=%%c+1
)
set NEW_VER=%MAJOR%.%MINOR%.%PATCH%
set NEW_VER_FULL=%NEW_VER%.0

echo ========================================
echo   Predvia 패키징
echo   %CURRENT% -^> %NEW_VER_FULL%
echo ========================================

:: csproj 버전 업데이트
powershell -Command "(Get-Content Gumaedaehang.csproj) -replace '<AssemblyVersion>.*</AssemblyVersion>','<AssemblyVersion>%NEW_VER_FULL%</AssemblyVersion>' -replace '<FileVersion>.*</FileVersion>','<FileVersion>%NEW_VER_FULL%</FileVersion>' | Set-Content Gumaedaehang.csproj"

echo.
echo [1/3] 빌드 및 퍼블리시...
dotnet publish Gumaedaehang.csproj -c Release -r win-x64 --self-contained -o publish-squirrel

echo.
echo [2/3] Squirrel 패키징 (v%NEW_VER%)...
squirrel pack --packId Predvia --packVersion %NEW_VER% --packDir publish-squirrel --releaseDir releases

echo.
echo [3/3] 완료!
echo ========================================
echo   v%NEW_VER% 패키징 완료
echo   releases 폴더의 파일을 GitHub Releases에 업로드하세요
echo ========================================
pause
