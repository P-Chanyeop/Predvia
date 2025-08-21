@echo off
chcp 65001 > nul

echo Predvia 빌드 시작...

REM 프로세스 종료
taskkill /f /im Gumaedaehang.exe 2>nul
taskkill /f /im dotnet.exe 2>nul

REM 캐시 정리
rmdir /s /q "bin" 2>nul
rmdir /s /q "obj" 2>nul

REM 빌드
dotnet clean Gumaedaehang.csproj
dotnet restore Gumaedaehang.csproj
dotnet build Gumaedaehang.csproj

echo.
echo 빌드 완료! 실행하려면 아래 명령어를 사용하세요:
echo dotnet run --project Gumaedaehang.csproj
echo.
pause
