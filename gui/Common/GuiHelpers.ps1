function Import-HL7ToolsModule {
    param(
        [Parameter(Mandatory)]
        [string] $ModulePath
    )

    $manifest = Join-Path $ModulePath 'hl7tools.psd1'
    $localError = $null
    if (Test-Path -LiteralPath $manifest) {
        try {
            Import-Module -Name $manifest -ErrorAction Stop
            return
        }
        catch {
            $localError = $_.Exception.Message
        }
    }

    try {
        Import-Module -Name 'hl7tools' -ErrorAction Stop
    }
    catch {
        $localMessage = if ($localError) { "Local module import failed: $localError`n" } else { "Local module manifest not found: $manifest`n" }
        throw "$localMessage" + "Installed module import failed: $($_.Exception.Message)"
    }
}

function Import-WpfXaml {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    $reader = [System.Xml.XmlReader]::Create((Resolve-Path -LiteralPath $Path).Path)
    try {
        return [Windows.Markup.XamlReader]::Load($reader)
    }
    finally {
        $reader.Close()
    }
}

function Get-NamedControl {
    param(
        [Parameter(Mandatory)]
        [object] $Root,
        [Parameter(Mandatory)]
        [string] $Name
    )

    $control = $Root.FindName($Name)
    if ($null -eq $control) {
        throw "The control '$Name' was not found in the XAML."
    }
    return $control
}

function Write-GuiLog {
    param(
        [Parameter(Mandatory)]
        [string] $Text,
        [Parameter(Mandatory)]
        [object] $LogBox,
        [string] $Level = 'INFO'
    )

    $LogBox.AppendText("[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] [$Level] $Text`r`n")
    $LogBox.ScrollToEnd()
}

function Set-GuiStatus {
    param(
        [Parameter(Mandatory)]
        [string] $Text,
        [Parameter(Mandatory)]
        [object] $StatusText
    )

    $StatusText.Text = $Text
}

function Show-GuiMessage {
    param(
        [Parameter(Mandatory)]
        [string] $Message,
        [string] $Title = 'HL7 Tools',
        [ValidateSet('Error', 'Information', 'Warning')]
        [string] $Icon = 'Information'
    )

    $image = switch ($Icon) {
        'Error' { [System.Windows.MessageBoxImage]::Error }
        'Warning' { [System.Windows.MessageBoxImage]::Warning }
        default { [System.Windows.MessageBoxImage]::Information }
    }
    [System.Windows.MessageBox]::Show($Message, $Title, [System.Windows.MessageBoxButton]::OK, $image) | Out-Null
}

function Start-GuiCommand {
    param(
        [Parameter(Mandatory)]
        [string] $CommandName,
        [Parameter(Mandatory)]
        [hashtable] $Parameters,
        [Parameter(Mandatory)]
        [string] $ModulePath,
        [Parameter(Mandatory)]
        [object] $LogBox,
        [Parameter(Mandatory)]
        [object] $StatusText,
        [Parameter(Mandatory)]
        [object] $Button,
        [Parameter(Mandatory)]
        [object] $NavigationList
    )

    $worker = [powershell]::Create()
    $workerScript = {
        param($localModulePath, $name, $parameters)
        $VerbosePreference = 'Continue'
        $WarningPreference = 'Continue'
        $localManifest = Join-Path $localModulePath 'hl7tools.psd1'
        $localImportError = $null
        if (Test-Path -LiteralPath $localManifest) {
            try {
                Import-Module -Name $localManifest -ErrorAction Stop
            }
            catch {
                $localImportError = $_.Exception.Message
            }
        }

        if (-not (Get-Module -Name hl7tools)) {
            try {
                Import-Module -Name hl7tools -ErrorAction Stop
            }
            catch {
                $localMessage = if ($localImportError) { "Local module import failed: $localImportError`n" } else { "Local module manifest not found: $localManifest`n" }
                throw "$localMessage" + "Installed module import failed: $($_.Exception.Message)"
            }
        }

        & $name @parameters
    }
    $worker.AddScript($workerScript).AddArgument($ModulePath).AddArgument($CommandName).AddArgument($Parameters) | Out-Null
    $worker.Streams.Warning.Clear()
    $worker.Streams.Verbose.Clear()
    $worker.Streams.Error.Clear()
    $asyncResult = $worker.BeginInvoke()
    $Button.IsEnabled = $false
    $NavigationList.IsEnabled = $false
    Set-GuiStatus -Text "Running $CommandName..." -StatusText $StatusText
    Write-GuiLog -Text "Started $CommandName." -LogBox $LogBox

    $timer = [System.Windows.Threading.DispatcherTimer]::new()
    $timer.Interval = [TimeSpan]::FromMilliseconds(100)
    $indices = @{ Warning = 0; Verbose = 0; Error = 0 }
    $timer.Add_Tick({
        foreach ($entry in @(
            @{ Name = 'Warning'; Level = 'WARNING'; Stream = $worker.Streams.Warning },
            @{ Name = 'Verbose'; Level = 'VERBOSE'; Stream = $worker.Streams.Verbose },
            @{ Name = 'Error'; Level = 'ERROR'; Stream = $worker.Streams.Error }
        )) {
            while ($indices[$entry.Name] -lt $entry.Stream.Count) {
                Write-GuiLog -Text ([string]$entry.Stream[$indices[$entry.Name]]) -LogBox $LogBox -Level $entry.Level
                $indices[$entry.Name]++
            }
        }

        if (-not $asyncResult.IsCompleted) {
            return
        }

        try {
            $output = $worker.EndInvoke($asyncResult)
            foreach ($item in $output) {
                Write-GuiLog -Text ([string]$item) -LogBox $LogBox
            }
            foreach ($entry in @(
                @{ Name = 'Warning'; Level = 'WARNING'; Stream = $worker.Streams.Warning },
                @{ Name = 'Verbose'; Level = 'VERBOSE'; Stream = $worker.Streams.Verbose },
                @{ Name = 'Error'; Level = 'ERROR'; Stream = $worker.Streams.Error }
            )) {
                while ($indices[$entry.Name] -lt $entry.Stream.Count) {
                    Write-GuiLog -Text ([string]$entry.Stream[$indices[$entry.Name]]) -LogBox $LogBox -Level $entry.Level
                    $indices[$entry.Name]++
                }
            }

            if ($worker.InvocationStateInfo.State -eq [System.Management.Automation.PSInvocationState]::Failed -or
                $worker.Streams.Error.Count -gt 0) {
                Set-GuiStatus -Text "$CommandName failed" -StatusText $StatusText
            }
            else {
                Set-GuiStatus -Text "$CommandName completed successfully" -StatusText $StatusText
            }
        }
        catch {
            Write-GuiLog -Text $_.Exception.Message -LogBox $LogBox -Level 'ERROR'
            Set-GuiStatus -Text "$CommandName failed" -StatusText $StatusText
        }
        finally {
            $timer.Stop()
            $Button.IsEnabled = $true
            $NavigationList.IsEnabled = $true
            $worker.Dispose()
        }
    }.GetNewClosure())
    $timer.Start()
}
