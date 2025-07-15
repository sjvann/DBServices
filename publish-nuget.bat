@echo off
echo ==============================================
echo  DbServices NuGet 套件發布腳本
echo ==============================================
echo.

REM 檢查是否有 NuGet API Key
if "%NUGET_API_KEY%"=="" (
    echo 錯誤: 請設定 NUGET_API_KEY 環境變數
    echo 範例: set NUGET_API_KEY=your_api_key_here
    echo.
    echo 如何取得 API Key:
    echo 1. 前往 https://www.nuget.org/account/apikeys
    echo 2. 登入您的 NuGet 帳戶
    echo 3. 建立新的 API Key
    echo 4. 設定環境變數: set NUGET_API_KEY=your_key
    pause
    exit /b 1
)

echo 正在建置 Release 版本...
dotnet clean
dotnet build --configuration Release
if errorlevel 1 (
    echo 建置失敗!
    pause
    exit /b 1
)

echo.
echo 正在打包 NuGet 套件...
dotnet pack --configuration Release --no-build
if errorlevel 1 (
    echo 打包失敗!
    pause
    exit /b 1
)

echo.
echo 找到以下套件:
dir /s *.nupkg

echo.
echo 正在發布到 NuGet.org...

REM 發布核心套件
echo 發布 DbServices.Core...
dotnet nuget push "DbServices.Core\bin\Release\DbServices.Core.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

REM 發布資料庫提供者
echo 發布 DbServices.Provider.Sqlite...
dotnet nuget push "DbServices.Provider.Sqlite\bin\Release\DbServices.Provider.Sqlite.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

echo 發布 DbServices.Provider.SqlServer...
dotnet nuget push "DbServices.Provider.MsSql\bin\Release\DbServices.Provider.SqlServer.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

echo 發布 DbServices.Provider.MySQL...
dotnet nuget push "DbServices.Provider.MySql\bin\Release\DbServices.Provider.MySQL.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

echo 發布 DbServices.Provider.Oracle...
dotnet nuget push "DbServices.Provider.Oracle\bin\Release\DbServices.Provider.Oracle.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

REM 發布主套件
echo 發布 DbServices...
dotnet nuget push "DBService\bin\Release\DbServices.2.0.0.nupkg" --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json

echo.
echo ==============================================
echo  發布完成!
echo ==============================================
echo.
echo 套件將在 5-10 分鐘內在 NuGet.org 上可用
echo 查看您的套件: https://www.nuget.org/profiles/your_username
echo.
pause
