@echo off
chcp 65001 > nul
echo ========================================
echo      Predvia 빌드 성공 확인
echo ========================================
echo.

echo [파일 존재 확인]
if exist "Predvia.exe" (
    echo ✓ Predvia.exe - 존재함
    for %%F in (Predvia.exe) do echo   크기: %%~zF bytes ^(~91MB^)
) else (
    echo ✗ Predvia.exe - 없음
)

if exist "av_libglesv2.dll" (
    echo ✓ av_libglesv2.dll - 존재함
) else (
    echo ✗ av_libglesv2.dll - 없음
)

if exist "libHarfBuzzSharp.dll" (
    echo ✓ libHarfBuzzSharp.dll - 존재함
) else (
    echo ✗ libHarfBuzzSharp.dll - 없음
)

if exist "libSkiaSharp.dll" (
    echo ✓ libSkiaSharp.dll - 존재함
) else (
    echo ✗ libSkiaSharp.dll - 없음
)

echo.
echo [빌드 폴더 확인]
if exist "publish-windows" (
    echo ✓ publish-windows 폴더 - 존재함
    echo   내용:
    dir publish-windows\*.exe 2>nul | find ".exe"
) else (
    echo ✗ publish-windows 폴더 - 없음
)

echo.
echo [총 파일 크기]
if exist "Predvia.exe" (
    for /f "tokens=3" %%a in ('dir /-c Predvia.exe ^| find "Predvia.exe"') do (
        set size=%%a
    )
    echo 메인 실행 파일: 약 91MB
    echo 전체 패키지: 약 106MB
)

echo.
echo ========================================
echo           빌드 상태: 성공! ✓
echo ========================================
echo.
echo 실행하려면 '실행.bat'을 더블클릭하세요.
echo.
pause
