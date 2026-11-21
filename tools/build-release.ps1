# Builds the signed release packages of Audibly Fork: a Windows MSIX (x64) and an Android APK.
# Keys and passwords live outside the repository (default D:\Signing) and are never committed:
#   NicolasHartmann-signing.pfx + pfx-password.txt          Windows (self-signed, CN=NicolasHartmann)
#   audibly-fork-release.keystore + keystore-password.txt   Android (alias audiblyfork)
# The repository's Package.appxmanifest keeps its identity; the release identity is patched in
# only for the build and restored afterwards.
param(
    [ValidateSet('All', 'Windows', 'Android')] [string] $Target = 'All',
    [string] $SigningDir = 'D:\Signing',
    [string] $OutDir = 'D:\Releases\AudiblyFork'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force $OutDir | Out-Null

function Build-Windows {
    $manifest = Join-Path $repo 'Audibly.App\Package.appxmanifest'
    $original = [IO.File]::ReadAllBytes($manifest)
    # MSIX tooling signs from the certificate store; import the .pfx there when it is missing
    $pfx = Join-Path $SigningDir 'NicolasHartmann-signing.pfx'
    $pfxPassword = ConvertTo-SecureString (Get-Content (Join-Path $SigningDir 'pfx-password.txt') -Raw).Trim() -AsPlainText -Force
    $thumbprint = (Get-PfxData -FilePath $pfx -Password $pfxPassword).EndEntityCertificates[0].Thumbprint
    if (-not (Test-Path "Cert:\CurrentUser\My\$thumbprint")) {
        Import-PfxCertificate -FilePath $pfx -Password $pfxPassword -CertStoreLocation Cert:\CurrentUser\My | Out-Null
    }
    try {
        $xml = [Text.Encoding]::UTF8.GetString($original).TrimStart([char]0xFEFF)
        $xml = $xml -replace '(<Identity[^>]*?\sName=")[^"]*(")', '${1}NicolasHartmann.AudiblyFork${2}'
        $xml = $xml -replace '(<Identity[^>]*?\sPublisher=")[^"]*(")', '${1}CN=NicolasHartmann${2}'
        $xml = $xml -replace '<DisplayName>[^<]*</DisplayName>', '<DisplayName>Audibly Fork</DisplayName>'
        $xml = $xml -replace '<PublisherDisplayName>[^<]*</PublisherDisplayName>', '<PublisherDisplayName>NicolasHartmann</PublisherDisplayName>'
        $xml = $xml -replace '(uap:VisualElements[^>]*?DisplayName=")[^"]*(")', '${1}Audibly Fork${2}'
        [IO.File]::WriteAllText($manifest, $xml, [Text.UTF8Encoding]::new($true))

        $packages = Join-Path $OutDir 'windows\'
        dotnet publish (Join-Path $repo 'Audibly.App\Audibly.App.csproj') -c Release -p:Platform=x64 `
            -r win-x64 -p:GenerateAppxPackageOnBuild=true -p:AppxBundle=Never -p:UapAppxPackageBuildMode=SideloadOnly `
            -p:AppxPackageSigningEnabled=true "-p:PackageCertificateThumbprint=$thumbprint" `
            -p:AppxSymbolPackageEnabled=false -p:SentryUploadSymbols=false -p:SentryUploadSources=false -p:SentrySetCommits=false `
            "-p:AppxPackageDir=$packages"
        if ($LASTEXITCODE -ne 0) { throw 'Windows build failed' }
        Copy-Item (Join-Path $SigningDir 'NicolasHartmann-signing.cer') (Join-Path $OutDir 'AudiblyFork.cer') -Force
        Get-ChildItem $packages -Recurse -Filter *.msix | Select-Object FullName, Length
    }
    finally {
        [IO.File]::WriteAllBytes($manifest, $original)
    }
}

function Build-Android {
    . D:\AndroidDev\env.ps1
    $keystore = Join-Path $SigningDir 'audibly-fork-release.keystore'
    $password = (Get-Content (Join-Path $SigningDir 'keystore-password.txt') -Raw).Trim()
    $apkDir = Join-Path $OutDir 'android'
    dotnet publish (Join-Path $repo 'Audibly.Mobile\Audibly.Mobile.csproj') -f net10.0-android -c Release `
        -p:AndroidPackageFormat=apk "-p:AndroidSdkDirectory=$env:ANDROID_HOME" "-p:JavaSdkDirectory=$env:JAVA_HOME" `
        -p:AndroidKeyStore=true "-p:AndroidSigningKeyStore=$keystore" -p:AndroidSigningKeyAlias=audiblyfork `
        "-p:AndroidSigningKeyPass=$password" "-p:AndroidSigningStorePass=$password" "-p:ApplicationTitle=Audibly Fork" `
        -o $apkDir
    if ($LASTEXITCODE -ne 0) { throw 'Android build failed' }
    Get-ChildItem $apkDir -Filter *-Signed.apk | Select-Object FullName, Length
}

if ($Target -in 'All', 'Android') { Build-Android }
if ($Target -in 'All', 'Windows') { Build-Windows }
