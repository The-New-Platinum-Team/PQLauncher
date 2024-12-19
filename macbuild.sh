dotnet publish -r osx-arm64 --configuration Release -f net8.0 -p:UseAppHost=true --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
mkdir -p mac-dist/PQLauncher.app/Contents/MacOS
mv bin/Release/net8.0/osx-arm64/publish/* "mac-dist/PQLauncher.app/Contents/MacOS/"