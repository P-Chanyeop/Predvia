@echo off
setlocal enabledelayedexpansion
chcp 65001 > nul

echo ========================================
echo    Predvia 최종 안전 빌드 시스템
echo ========================================
echo.

REM 현재 디렉토리 확인
echo 현재 디렉토리: %CD%
echo.

REM 1단계: 모든 .NET 프로세스 강제 종료
echo [1/7] 모든 .NET 프로세스 강제 종료...
taskkill /f /im Gumaedaehang.exe 2>nul
taskkill /f /im dotnet.exe 2>nul
wmic process where "name='dotnet.exe'" delete 2>nul
timeout /t 3 /nobreak > nul

REM 2단계: 파일 핸들 해제 대기
echo [2/7] 파일 핸들 해제 대기...
timeout /t 5 /nobreak > nul

REM 3단계: 빌드 폴더 강제 삭제 (여러 시도)
echo [3/7] 빌드 캐시 강제 정리...
for /l %%i in (1,1,3) do (
    if exist "bin" (
        echo    시도 %%i: bin 폴더 삭제...
        rmdir /s /q "bin" 2>nul
        timeout /t 1 /nobreak > nul
    )
)
for /l %%i in (1,1,3) do (
    if exist "obj" (
        echo    시도 %%i: obj 폴더 삭제...
        rmdir /s /q "obj" 2>nul
        timeout /t 1 /nobreak > nul
    )
)

REM 4단계: .NET 캐시 완전 정리
echo [4/7] .NET 캐시 완전 정리...
dotnet nuget locals all --clear > nul 2>&1
dotnet clean Gumaedaehang.csproj > nul 2>&1

REM 5단계: 프로젝트 복원
echo [5/7] 프로젝트 의존성 복원...
dotnet restore Gumaedaehang.csproj --force --no-cache
if !errorlevel! neq 0 (
    echo    ERROR: 의존성 복원 실패!
    echo    다시 시도하시겠습니까? (Y/N)
    choice /c YN /n
    if !errorlevel!==1 (
        dotnet restore Gumaedaehang.csproj --force --no-cache --verbosity detailed
    ) else (
        pause
        exit /b 1
    )
)

REM 6단계: 프로젝트 빌드
echo [6/7] 프로젝트 빌드...
dotnet build Gumaedaehang.csproj --configuration Debug --no-restore --verbosity minimal
if !errorlevel! neq 0 (
    echo    ERROR: 빌드 실패!
    echo    상세 로그를 보시겠습니까? (Y/N)
    choice /c YN /n
    if !errorlevel!==1 (
        dotnet build Gumaedaehang.csproj --configuration Debug --no-restore --verbosity detailed
    )
    pause
    exit /b 1
)

REM 7단계: 실행 파일 확인
echo [7/7] 빌드 결과 확인...
set "exe_path=bin\Debug\net8.0\Gumaedaehang.exe"
if exist "!exe_path!" (
    echo    ✓ SUCCESS: 빌드 완료!
    echo    실행 파일: !exe_path!
    echo    파일 크기: 
    dir "!exe_path!" | find "Gumaedaehang.exe"
    echo.
    echo 프로그램을 실행하시겠습니까? (Y/N)
    choice /c YN /n
    if !errorlevel!==1 (
        echo    프로그램 실행 중...
        start "" "!exe_path!"
    )
) else (
    echo    ✗ ERROR: 실행 파일을 찾을 수 없습니다!
    echo    예상 위치: !exe_path!
    echo.
    echo    bin 폴더 내용:
    if exist "bin" (
        dir /s bin\*.exe 2>nul
    ) else (
        echo    bin 폴더가 존재하지 않습니다.
    )
)

echo.
echo ========================================
echo           빌드 프로세스 완료
echo ========================================
pause
