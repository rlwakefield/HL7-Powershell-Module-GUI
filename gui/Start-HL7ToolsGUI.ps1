#Requires -Version 7.0

if (-not $IsWindows) {
    throw 'The HL7 Tools GUI requires Windows and PowerShell 7 or later.'
}

if ([System.Threading.Thread]::CurrentThread.GetApartmentState() -ne [System.Threading.ApartmentState]::STA) {
    $pwsh = Join-Path $PSHOME 'pwsh.exe'
    $arguments = @('-NoProfile', '-STA', '-File', "$PSScriptRoot/Start-HL7ToolsGUI.ps1")
    $process = Start-Process -FilePath $pwsh -ArgumentList $arguments -Wait -PassThru
    exit $process.ExitCode
}

Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

. (Join-Path $PSScriptRoot 'Common/GuiHelpers.ps1')

try {
    Import-HL7ToolsModule -ModulePath (Join-Path $PSScriptRoot '../module/hl7tools')
}
catch {
    Show-GuiMessage -Message $_.Exception.Message -Title 'Unable to load hl7tools' -Icon Error
    exit 1
}

$window = Import-WpfXaml -Path (Join-Path $PSScriptRoot 'MainWindow.xaml')
$cmdletList = Get-NamedControl -Root $window -Name 'CmdletList'
$panelHost = Get-NamedControl -Root $window -Name 'PanelHost'
$logBox = Get-NamedControl -Root $window -Name 'LogTextBox'
$statusText = Get-NamedControl -Root $window -Name 'StatusText'
$definitions = @()

foreach ($definitionFile in Get-ChildItem -Path (Join-Path $PSScriptRoot 'Cmdlets') -Filter '*.ps1' -File | Sort-Object Name) {
    try {
        $definition = & $definitionFile.FullName
        if ($null -eq $definition.Name -or $null -eq $definition.DisplayName -or
            $null -eq $definition.XamlPath -or $null -eq $definition.Initialize -or
            $null -eq $definition.BuildParameters) {
            throw 'Panel definition must provide Name, DisplayName, XamlPath, Initialize, and BuildParameters.'
        }

        $definitions += [pscustomobject]$definition
    }
    catch {
        Write-GuiLog -Text "Unable to load panel '$($definitionFile.Name)': $($_.Exception.Message)" -LogBox $logBox -Level 'ERROR'
    }
}

$cmdletList.ItemsSource = $definitions
$context = [pscustomobject]@{
    Window = $window
    LogBox = $logBox
    StatusText = $statusText
    NavigationList = $cmdletList
    PanelDefinition = $null
    RunCommand = $null
    ShowMessage = { param($message, $title, $icon) Show-GuiMessage -Message $message -Title $title -Icon $icon }.GetNewClosure()
    CloseWindow = { $window.Close() }.GetNewClosure()
}

$modulePath = Join-Path $PSScriptRoot '../module/hl7tools'
$context.RunCommand = {
    param($commandName, $parameters, $sendButton)
    Start-GuiCommand -CommandName $commandName -Parameters $parameters -ModulePath $modulePath -LogBox $logBox -StatusText $statusText -Button $sendButton -NavigationList $cmdletList
}.GetNewClosure()

$cmdletList.Add_SelectionChanged({
    if ($null -eq $cmdletList.SelectedItem) {
        return
    }

    $definition = $cmdletList.SelectedItem
    try {
        $panel = Import-WpfXaml -Path $definition.XamlPath
        $panelHost.Content = $panel
        $context.PanelDefinition = $definition
        & $definition.Initialize $panel $context
        Set-GuiStatus -Text 'Ready' -StatusText $statusText
    }
    catch {
        $panelHost.Content = $null
        Write-GuiLog -Text "Unable to display '$($definition.DisplayName)': $($_.Exception.Message)" -LogBox $logBox -Level 'ERROR'
        Show-GuiMessage -Message $_.Exception.Message -Title 'Unable to load cmdlet panel' -Icon Error
    }
}.GetNewClosure())

if ($definitions.Count -gt 0) {
    $cmdletList.SelectedIndex = 0
}
else {
    Write-GuiLog -Text 'No valid cmdlet panels were discovered.' -LogBox $logBox -Level 'ERROR'
    Set-GuiStatus -Text 'No cmdlet panels available' -StatusText $statusText
}

$window.ShowDialog() | Out-Null
