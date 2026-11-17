param(
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'output\MaintenanceRequestSetup')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$appProject = Join-Path $root 'Maintenance request\Maintenance request.csproj'
$setupProject = Join-Path $PSScriptRoot 'Setup\Setup.csproj'
$work = Join-Path ([IO.Path]::GetTempPath()) ('maintenance-request-setup-' + [Guid]::NewGuid().ToString('N'))
$app = Join-Path $work 'Application'
$setup = Join-Path $work 'Setup'
$payload = Join-Path $work 'Application.zip'

# نثبت إصدار وقت التشغيل في التطبيق والمثبّت حتى تحتوي الحزمة على الملفات نفسها.
$publish = @('publish', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
    '-p:RuntimeFrameworkVersion=10.0.14', '-p:DebugType=None', '-p:DebugSymbols=false')

try {
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    & dotnet @publish $appProject -p:Platform=x64 -p:InstalledBuild=true -o $app
    if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }

    # نغيّر اسم المشغّل فقط ليبقى اسم التجميعة ومراجع موارد WPF كما هي.
    Rename-Item -LiteralPath (Join-Path $app 'Maintenance request.exe') -NewName 'تطبيق طلبات الصيانة.exe'
    $legacyConnection = Join-Path $app 'ConectionString'
    if (Test-Path -LiteralPath $legacyConnection) {
        Remove-Item -LiteralPath $legacyConnection
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($app, $payload, [IO.Compression.CompressionLevel]::Optimal, $false)

    # ملفات المثبّت وملفات التطبيق مضمنة في EXE واحد ولا تُنزّل وقت التثبيت.
    & dotnet @publish $setupProject -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true "-p:ApplicationPayload=$payload" -o $setup
    if ($LASTEXITCODE -ne 0) { throw 'Setup publish failed.' }

    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $installer = Join-Path $OutputDirectory 'Setup.exe'
    Copy-Item -LiteralPath (Join-Path $setup 'MaintenanceRequestSetup.exe') -Destination $installer -Force
    Get-FileHash -LiteralPath $installer -Algorithm SHA256
}
finally {
    # مجلد عمل معزول ومولّد لهذه العملية داخل المسار المؤقت.
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if ((Test-Path -LiteralPath $work) -and [IO.Path]::GetFullPath($work).StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $work -Recurse -Force
    }
}
