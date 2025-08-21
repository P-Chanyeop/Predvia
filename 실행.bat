@echo off
chcp 65001 > nul
echo ========================================
echo        Predvia 구매대행 시스템
echo ========================================
echo.

REM 현재 디렉토리 확인
if exist "Predvia.exe" (
    echo ✓ 실행 파일 발견: Predvia.exe
    echo   파일 크기: 
    dir Predvia.exe | find "Predvia.exe"
    echo.
    echo 프로그램을 시작합니다...
    echo.
    start "" "Predvia.exe"
    echo 프로그램이 실행되었습니다!
) else (
    echo ✗ 오류: Predvia.exe 파일을 찾을 수 없습니다.
    echo.
    echo 다음 파일들을 확인하세요:
    dir *.exe 2>nul
    echo.
    echo publish-windows 폴더에서 파일을 복사해보세요:
    echo copy publish-windows\Gumaedaehang.exe Predvia.exe
)

echo.
echo ========================================
pause
