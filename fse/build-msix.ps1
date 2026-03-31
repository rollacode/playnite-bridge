$ErrorActionPreference = "Stop"

$FSEDir = $PSScriptRoot
$SDKBin = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64"
$CertSubject = "CN=Rollacode, O=Rollacode, C=LV"
$CertPassword = "PlayniteBridge2026"
$PfxPath = "$FSEDir\PlayniteFSE.pfx"
$CerPath = "$FSEDir\PlayniteFSE.cer"
$MsixPath = "$FSEDir\PlayniteFSE.msix"
$PackageDir = "$FSEDir\PackageFiles"

Write-Host "=== Building Playnite FSE MSIX ===" -ForegroundColor Cyan

# Clean
if (Test-Path $PackageDir) { Remove-Item $PackageDir -Recurse -Force }
New-Item $PackageDir -ItemType Directory | Out-Null
New-Item "$PackageDir\Assets" -ItemType Directory | Out-Null
New-Item "$PackageDir\Public" -ItemType Directory | Out-Null

# Copy files
Copy-Item "$FSEDir\AppxManifest.xml" "$PackageDir\"
Copy-Item "$FSEDir\Assets\*" "$PackageDir\Assets\"
Copy-Item "$FSEDir\PlayniteLauncher\bin\Release\net48\PlayniteLauncher.exe" "$PackageDir\"
Copy-Item "$FSEDir\CustomCapability.SCCD" "$PackageDir\" -ErrorAction SilentlyContinue

# Create cert if not exists
if (-not (Test-Path $PfxPath)) {
    Write-Host "Creating self-signed certificate..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $CertSubject `
        -KeyUsage DigitalSignature `
        -FriendlyName "Playnite Bridge FSE" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

    $pwd = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $PfxPath -Password $pwd | Out-Null
    Export-Certificate -Cert $cert -FilePath $CerPath | Out-Null
    Write-Host "Certificate created: $PfxPath"
} else {
    Write-Host "Certificate exists: $PfxPath"
}

# Pack
Write-Host "Packing MSIX..." -ForegroundColor Yellow
& "$SDKBin\makeappx.exe" pack /d $PackageDir /p $MsixPath /nv /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed" }

# Sign
Write-Host "Signing MSIX..." -ForegroundColor Yellow
& "$SDKBin\signtool.exe" sign /fd SHA256 /a /f $PfxPath /p $CertPassword $MsixPath
if ($LASTEXITCODE -ne 0) { throw "signtool failed" }

Write-Host ""
Write-Host "=== Done: $MsixPath ===" -ForegroundColor Green
Write-Host "Certificate: $CerPath"
$size = (Get-Item $MsixPath).Length / 1KB
Write-Host "Size: $([math]::Round($size, 1)) KB"
