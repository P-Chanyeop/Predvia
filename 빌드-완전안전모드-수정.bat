@echo off
chcp 65001 > nul
echo ========================================
echo    Predvia 완전 안전 빌드 시스템
echo ========================================
echo.

REM 1단계: 실행 중인 .NET 프로세스 종료
echo [1/6] 실행중인 .NET 프로세스 종료...
taskkill /f /im Gumaedaehang.exe 2>nul
taskkill /f /im dotnet.exe 2>nul
timeout /t 2 /nobreak > nul

REM 2단계: bin, obj 폴더 완전 삭제
echo [2/6] 빌드 캐시 완전 정리...
if exist "bin" (
    echo    - bin 폴더 삭제 중...
    rmdir /s /q "bin" 2>nul
)
if exist "obj" (
    echo    - obj 폴더 삭제 중...
    rmdir /s /q "obj" 2>nul
)

REM 3단계: NuGet 캐시 정리
echo [3/6] NuGet 캐시 정리...
dotnet nuget locals all --clear > nul 2>&1

REM 4단계: 프로젝트 복원
echo [4/6] 프로젝트 의존성 복원...
dotnet restore Gumaedaehang.csproj
if %errorlevel% neq 0 (
    echo    ERROR: 의존성 복원 실패!
    pause
    exit /b 1
)

REM 5단계: 프로젝트 빌드
echo [5/6] 프로젝트 빌드...
dotnet build Gumaedaehang.csproj --configuration Debug --verbosity minimal
if %errorlevel% neq 0 (
    echo    ERROR: 빌드 실패!
    pause
    exit /b 1
)

REM 6단계: 실행 파일 확인 및 실행
echo [6/6] 실행 파일 확인...
if exist "bin\Debug\net8.0\Gumaedaehang.exe" (
    echo    SUCCESS: 빌드 완료!
    echo    실행 파일: bin\Debug\net8.0\Gumaedaehang.exe
    echo.
    echo 프로그램을 실행하시겠습니까? (Y/N)
    choice /c YN /n /m "선택: "
    if !errorlevel!==1 (
        echo    프로그램 실행 중...
        start "" "bin\Debug\net8.0\Gumaedaehang.exe"
    )
) else (
    echo    ERROR: 실행 파일을 찾을 수 없습니다!
    echo    예상 위치: bin\Debug\net8.0\Gumaedaehang.exe
    dir bin\Debug\net8.0\ 2>nul
)

echo.
echo ========================================
echo           빌드 프로세스 완료
echo ========================================
pause
