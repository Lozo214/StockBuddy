$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$stockBuddyDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($stockBuddyDotnet) {
    & $stockBuddyDotnet.Source run --project src/StockBuddy.Web -- --urls http://localhost:5265
} else {
    $localSdk = Join-Path $PSScriptRoot '..\.tools\dotnet\dotnet.exe'
    if (!(Test-Path $localSdk)) { throw 'Install the .NET 10 SDK, then run this script again.' }
    & $localSdk run --project src/StockBuddy.Web -- --urls http://localhost:5265
}
