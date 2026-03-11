@echo off
chcp 65001 > nul
echo ============================================================
echo  GenSpecData — SpecData 코드 자동 생성
echo  [주의] GenProto.bat 를 먼저 실행하여 Protocol.cs를 최신화하세요.
echo ============================================================
echo.

REM ── 이 배치 파일이 있는 디렉터리(Common/SpecData/)로 이동
cd /d "%~dp0"

REM ── Generator 실행
REM   --id    : Google Spreadsheet ID
REM   --key   : Google API Key (시트 목록 자동 탐색)
REM   --proto : Protocol.cs 경로 (Common/SpecData/ 기준 상대 경로)
REM   --sheets 없이 실행 → API로 시트 목록 자동 탐색
echo [1/1] SpecData 코드 생성 중...
dotnet run --project SpecDataGenerator\SpecDataGenerator.csproj -- ^
    --id    1I3ClbfFILySaBlP5TddE2I7xaxMrN7s-5sxXAocGoPE ^
    --key   AIzaSyAICVBdeEPZWrweaPRoZJlgGoXlYZkybWI ^
    --proto ..\protoc-3.12.3-win64\bin\Protocol.cs

IF ERRORLEVEL 1 (
    echo.
    echo [ERROR] 코드 생성 실패. 위 오류 메시지를 확인하세요.
    PAUSE
    EXIT /B 1
)

echo.
echo ============================================================
echo  완료! 생성된 파일:
echo    Client/Assets/Scripts/Data/SheetEnums.cs
echo    Client/Assets/Scripts/Data/MetaData.cs
echo    Client/Assets/Scripts/Data/GoogleSheetConfig.cs
echo    Client/Assets/Scripts/Managers/Core/SpecDataManager.cs
echo    Client/Assets/Scripts/Managers/Core/ConfigManager.cs
echo    Server/Server/Data/SheetEnums.cs
echo    Server/Server/Data/MetaData.cs
echo    Server/Server/Data/GoogleSheetConfig.cs
echo    Server/Server/Data/SpecDataManager.cs
echo    Server/Server/Data/ConfigManager.cs
echo ============================================================
PAUSE
