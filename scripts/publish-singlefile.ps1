# Mermaid completo (WebView2 + JS embedded). Consigliato: profilo Fx = un solo .exe leggero, senza estrarre il runtime .NET.
# Prerequisito sul PC destinazione (profilo Fx): .NET 8 Desktop Runtime + WebView2 (di solito gia' su Windows 11).
param(
    [ValidateSet('Fx', 'SelfContained')]
    [string]$Mode = 'Fx'
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot\..
$proj = 'MermaidViewer.csproj'
$profile = if ($Mode -eq 'Fx') { 'SingleFile-Fx' } else { 'SingleFile-SelfContained' }

# GenerateBundle non puo' sovrascrivere MermaidViewer.exe se e' in esecuzione (Access denied).
Get-Process -Name 'MermaidViewer' -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Chiusura MermaidViewer (PID $($_.Id)) per pubblicare..."
    Stop-Process -Id $_.Id -Force
}
Start-Sleep -Milliseconds 500

dotnet publish $proj -c Release -p:PublishProfile=$profile
if ($Mode -eq 'Fx') {
    Write-Host "Exe: bin\Release\net8.0-windows\win-x64\publish-fx\MermaidViewer.exe"
} else {
    Write-Host "Exe: bin\Release\net8.0-windows\win-x64\publish-selfcontained\MermaidViewer.exe"
}
