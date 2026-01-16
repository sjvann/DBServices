#!/bin/bash

echo "=============================================="
echo "  DbServices NuGet Package Publish Script"
echo "=============================================="
echo

# Check for NuGet API Key
if [ -z "$NUGET_API_KEY" ]; then
    echo "Error: Please set NUGET_API_KEY environment variable"
    echo "Example: export NUGET_API_KEY=your_api_key_here"
    echo
    echo "How to get API Key:"
    echo "1. Go to https://www.nuget.org/account/apikeys"
    echo "2. Login to your NuGet account"
    echo "3. Create new API Key"
    echo "4. Set environment variable: export NUGET_API_KEY=your_key"
    exit 1
fi

echo "Building Release version..."
dotnet clean
dotnet build --configuration Release
if [ $? -ne 0 ]; then
    echo "Build failed!"
    exit 1
fi

echo
echo "Packing NuGet packages..."
dotnet pack --configuration Release --no-build
if [ $? -ne 0 ]; then
    echo "Pack failed!"
    exit 1
fi

echo
echo "Found packages:"
find . -name "*.nupkg" -type f

echo
echo "Publishing to NuGet.org..."

# Publish core package
echo "Publishing DbServices.Core..."
dotnet nuget push "DbServices.Core/bin/Release/DbServices.Core.2.0.0.nupkg" --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json

# Publish providers
echo "Publishing DbServices.Provider.Sqlite..."
dotnet nuget push "DbServices.Provider.Sqlite/bin/Release/DbServices.Provider.Sqlite.2.0.0.nupkg" --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json

echo "Publishing DbServices.Provider.SqlServer..."
dotnet nuget push "DbServices.Provider.MsSql/bin/Release/DbServices.Provider.SqlServer.2.0.0.nupkg" --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json

echo "Publishing DbServices.Provider.MySQL..."
dotnet nuget push "DbServices.Provider.MySql/bin/Release/DbServices.Provider.MySQL.2.0.0.nupkg" --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json

echo "Publishing DbServices.Provider.Oracle..."
dotnet nuget push "DbServices.Provider.Oracle/bin/Release/DbServices.Provider.Oracle.2.0.0.nupkg" --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json

echo "Publishing DbServices.Provider.PostgreSQL..."
dotnet nuget push "DbServices.Provider.PostgreSQL/bin/Release/DbServices.Provider.PostgreSQL.2.0.0.nupkg" --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json

echo "Publishing DbServices..."
dotnet nuget push "DBService/bin/Release/DbServices.2.0.0.nupkg" --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json

# Publish main package
echo "Publishing DbServices..."
dotnet nuget push "DBService/bin/Release/DbServices.2.0.0.nupkg" --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json

echo
echo "=============================================="
echo "  Publishing Complete!"
echo "=============================================="
echo
echo "Packages will be available on NuGet.org in 5-10 minutes"
echo "View your packages: https://www.nuget.org/profiles/your_username"
echo
