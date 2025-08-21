@echo off
chcp 65001 > nul
echo 🚀 Predvia 구매대행 시스템 빌드 (리소스 안전 모드)
echo 📁 아이콘: Assets/predvia_logo.png
echo 📂 출력 경로: C:\Users\IRENE_XD\Downloads
echo.

echo [1/3] 프로젝트 정리...
dotnet clean

echo [2/3] 의존성 복원...
dotnet restore

echo [3/3] 안전 모드 빌드 (트리밍 비활성화)...
dotnet publish -c Release -o "C:\Users\IRENE_XD\Downloads" --self-contained true -p:PublishSingleFile=true -p:AssemblyName="Predvia-구매대행시스템" -p:PublishTrimmed=false

echo.
echo ✅ 빌드 완료!
echo 📁 실행파일: C:\Users\IRENE_XD\Downloads\Predvia-구매대행시스템.exe
echo 🛡️ 리소스 에러 방지를 위해 트리밍을 비활성화했습니다.
echo 📦 파일 크기가 약간 클 수 있지만 안정성이 향상됩니다.
echo.
pause
