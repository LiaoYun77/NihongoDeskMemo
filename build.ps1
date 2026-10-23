param(
    [switch]$RunTests,
    [switch]$IncludeDesktopTests,
    [string]$ReferencePath
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
$programFiles = ${env:ProgramFiles(x86)}
if (!$programFiles) { $programFiles = $env:ProgramFiles }
$referenceRoot = Join-Path $programFiles 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0'
if ($ReferencePath) { $referenceRoot = $ReferencePath }
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler not found.' }
if (!(Test-Path -LiteralPath $referenceRoot)) { throw '.NET Framework 4.0 reference assemblies are required.' }

$references = @('System', 'System.Core', 'System.Drawing', 'System.Windows.Forms', 'System.Xml') |
    ForEach-Object { '/reference:' + $_ + '.dll' }
$references += @('WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml') |
    ForEach-Object { '/reference:' + (Join-Path $referenceRoot ($_ + '.dll')) }
$references += '/reference:' + (Join-Path $referenceRoot 'System.Speech.dll')
$sources = @('Program.cs', 'WpfMain.cs', 'WpfDialogs.cs', 'GlobalHideHotkey.cs', 'StructuredWordImporter.cs',
    'PracticeScope.cs', 'UnitSelectionControl.cs', 'VersionInfo.cs', 'HotkeyGesture.cs', 'HotkeyRecorder.cs',
    'StudySession.cs', 'XmlFileStore.cs', 'PracticePresets.cs', 'ImportPreview.cs', 'ImportPreviewWindow.cs', 'ReviewData.cs', 'ReviewHistoryWindow.cs', 'JapaneseSpeech.cs') |
    ForEach-Object { Join-Path $root ('src\' + $_) }
$output = Join-Path $root 'bin\Release'
[void][System.IO.Directory]::CreateDirectory($output)

function Compile([string]$target, [string]$entry, [string]$destination, [string[]]$files) {
    & $compiler /nologo /optimize+ /codepage:65001 "/target:$target" "/main:$entry" "/out:$destination" @references @files
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $entry" }
}

Compile 'winexe' 'NihongoDeskMemoWpf.WpfProgram' (Join-Path $output 'NihongoDeskMemo.exe') $sources
Write-Host "Built: $output\NihongoDeskMemo.exe"

if ($RunTests -or $IncludeDesktopTests) {
    $testOutput = Join-Path $root 'bin\Tests'
    [void][System.IO.Directory]::CreateDirectory($testOutput)
    $featureTest = Join-Path $testOutput 'FeatureTests.exe'
    Compile 'exe' 'NihongoDeskMemoWpf.FeatureTests' $featureTest ($sources + (Join-Path $root 'tests\FeatureTests.cs'))
    & $featureTest
    if ($LASTEXITCODE -ne 0) { throw 'Feature tests failed.' }
    $importTest = Join-Path $testOutput 'ImportTests.exe'
    Compile 'exe' 'NihongoDeskMemoWpf.ImportTests' $importTest ($sources + (Join-Path $root 'tests\ImportTests.cs'))
    & $importTest
    if ($LASTEXITCODE -ne 0) { throw 'Import tests failed.' }
    $settingsTest = Join-Path $testOutput 'SettingsTests.exe'
    Compile 'exe' 'NihongoDeskMemoWpf.SettingsTests' $settingsTest ($sources + (Join-Path $root 'tests\SettingsTests.cs'))
    & $settingsTest
    if ($LASTEXITCODE -ne 0) { throw 'Settings tests failed.' }
    $studyTest = Join-Path $testOutput 'StudyTests.exe'
    Compile 'exe' 'NihongoDeskMemoWpf.StudyTests' $studyTest ($sources + (Join-Path $root 'tests\StudyTests.cs'))
    & $studyTest
    if ($LASTEXITCODE -ne 0) { throw 'Study/storage tests failed.' }
    $recordTest = Join-Path $testOutput 'ReviewRecordTests.exe'
    Compile 'exe' 'NihongoDeskMemoWpf.ReviewRecordTests' $recordTest ($sources + (Join-Path $root 'tests\ReviewRecordTests.cs'))
    & $recordTest
    if ($LASTEXITCODE -ne 0) { throw 'Review record tests failed.' }
    $workflowTest = Join-Path $testOutput 'WorkflowTests.exe'
    Compile 'exe' 'NihongoDeskMemoWpf.WorkflowTests' $workflowTest ($sources + (Join-Path $root 'tests\WorkflowTests.cs'))
    & $workflowTest
    if ($LASTEXITCODE -ne 0) { throw 'Desktop workflow tests failed.' }
    $learningUiTest = Join-Path $testOutput 'LearningUiTests.exe'
    Compile 'exe' 'NihongoDeskMemoWpf.LearningUiTests' $learningUiTest ($sources + (Join-Path $root 'tests\LearningUiTests.cs'))
    & $learningUiTest
    if ($LASTEXITCODE -ne 0) { throw 'Learning interface tests failed.' }
    $speechTest = Join-Path $testOutput 'SpeechTests.exe'
    Compile 'exe' 'NihongoDeskMemoWpf.SpeechTests' $speechTest ($sources + (Join-Path $root 'tests\SpeechTests.cs'))
    & $speechTest
    if ($LASTEXITCODE -ne 0) { throw 'Speech tests failed.' }
    if ($IncludeDesktopTests) {
        Write-Host 'Desktop tests temporarily register/send F10-F12 and Ctrl+Shift+Q. Close running copies first.'
        $desktopTest = Join-Path $testOutput 'GlobalHideTests.exe'
        Compile 'exe' 'NihongoDeskMemoWpf.GlobalHideTests' $desktopTest ($sources + (Join-Path $root 'tests\GlobalHideTests.cs'))
        & $desktopTest --click
        if ($LASTEXITCODE -ne 0) { throw 'Native answer-click tests failed.' }
        & $desktopTest --registration
        if ($LASTEXITCODE -ne 0) { throw 'Hotkey registration tests failed.' }
        & $desktopTest
        if ($LASTEXITCODE -ne 0) { throw 'Global hotkey tests failed.' }
        & $desktopTest --app
        if ($LASTEXITCODE -ne 0) { throw 'App integration tests failed.' }
    }
}
