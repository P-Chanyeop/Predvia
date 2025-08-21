@echo off
chcp 65001 > nul
echo 🚀 Predvia 구매대행 시스템 빌드 (완전 안전 모드)
echo 📂 출력 경로: C:\Users\IRENE_XD\Downloads
echo.

echo [1/4] 프로젝트 정리...
dotnet clean

echo [2/4] 의존성 복원...
dotnet restore

echo [3/4] 프로젝트 파일로 빌드 (트리밍 완전 비활성화)...
dotnet publish Gumaedaehang.csproj -c Release -o "C:\Users\IRENE_XD\Downloads" --self-contained true -p:PublishSingleFile=true -p:AssemblyName="Predvia-구매대행시스템" -p:PublishTrimmed=false -p:TrimMode=none

echo [4/4] 실행 파일 테스트...
if exist "C:\Users\IRENE_XD\Downloads\Predvia-구매대행시스템.exe" (
    echo ✅ 실행 파일 생성 성공!
    echo 📁 위치: C:\Users\IRENE_XD\Downloads\Predvia-구매대행시스템.exe
) else (
    echo ❌ 실행 파일 생성 실패!
)

echo.
echo 🎉 빌드 완료! 이제 실행 파일을 테스트해보세요.
pause
