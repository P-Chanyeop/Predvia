@echo off
chcp 65001 > nul
echo 🚀 Predvia 구매대행 시스템 빌드 시작...
echo 📁 아이콘: Assets/predvia_logo.png
echo 📂 출력 경로: C:\Users\IRENE_XD\Downloads
echo.

dotnet publish -c Release -o "C:\Users\IRENE_XD\Downloads" --self-contained true -p:PublishSingleFile=true -p:AssemblyName="Predvia-구매대행시스템"

echo.
echo ✅ 빌드 완료!
echo 📁 실행파일 위치: C:\Users\IRENE_XD\Downloads\Predvia-구매대행시스템.exe
echo 🎨 아이콘이 포함된 실행 파일이 생성되었습니다!
echo.
echo 💡 다운로드 폴더를 열어보시겠습니까? (Y/N)
set /p open_folder=

if /i "%open_folder%"=="Y" (
    explorer "C:\Users\IRENE_XD\Downloads"
)

echo.
echo 🎉 모든 작업이 완료되었습니다!
pause
