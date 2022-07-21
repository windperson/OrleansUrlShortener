Set-PSDebug -Trace 1
Remove-Item ./publish -Force -Recurse -ErrorAction SilentlyContinue
dotnet publish ./src/UrlShortener.Frontend -c Release -o ./publish