@echo off
echo ========================================
echo    Predvia 구매대행 시스템 빌드 스크립트
echo    실행파일을 다운로드 폴더로 복사
echo ========================================
echo.

echo [1/4] 프로젝트 정리 중...
dotnet clean

echo [2/4] 의존성 복원 중...
dotnet restore

echo [3/4] Release 빌드 중...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true

echo [4/4] 다운로드 폴더로 복사 중...
set "downloads_folder=%USERPROFILE%\Downloads"
set "exe_file=bin\Release\net6.0\win-x64\publish\Gumaedaehang.exe"

if exist "%exe_file%" (
    copy "%exe_file%" "%downloads_folder%\Predvia-구매대행시스템.exe"
    echo.
    echo ========================================
    echo 빌드 완료!
    echo 실행 파일이 다운로드 폴더에 복사되었습니다:
    echo %downloads_folder%\Predvia-구매대행시스템.exe
    echo ========================================
) else (
    echo.
    echo ========================================
    echo 오류: 빌드된 실행 파일을 찾을 수 없습니다.
    echo ========================================
)

pause
