$definition = @{
    Name = 'Send-HL7Message'
    DisplayName = 'Send HL7 Message'
    Description = 'Send HL7 messages to a remote host using MLLP.'
    XamlPath = Join-Path $PSScriptRoot 'Send-HL7Message.xaml'
    Initialize = {
        param($root, $context)
        $useTls = Get-NamedControl -Root $root -Name 'UseTlsCheckBox'
        $skipCertificate = Get-NamedControl -Root $root -Name 'SkipCertificateCheckBox'
        $pathMode = Get-NamedControl -Root $root -Name 'PathRadioButton'
        $literalMode = Get-NamedControl -Root $root -Name 'LiteralPathRadioButton'
        $messageMode = Get-NamedControl -Root $root -Name 'MessageRadioButton'
        $pathPanel = Get-NamedControl -Root $root -Name 'PathInputPanel'
        $messageBox = Get-NamedControl -Root $root -Name 'MessageTextBox'
        $sendButton = Get-NamedControl -Root $root -Name 'SendButton'

        $useTls.Add_Checked({ $skipCertificate.IsEnabled = $true }.GetNewClosure())
        $useTls.Add_Unchecked({ $skipCertificate.IsEnabled = $false; $skipCertificate.IsChecked = $false }.GetNewClosure())
        $updateSourceVisibility = {
            $isMessage = $messageMode.IsChecked
            $pathPanel.Visibility = if ($isMessage) { [System.Windows.Visibility]::Collapsed } else { [System.Windows.Visibility]::Visible }
            $messageBox.Visibility = if ($isMessage) { [System.Windows.Visibility]::Visible } else { [System.Windows.Visibility]::Collapsed }
        }.GetNewClosure()
        $pathMode.Add_Checked($updateSourceVisibility)
        $literalMode.Add_Checked($updateSourceVisibility)
        $messageMode.Add_Checked($updateSourceVisibility)

        (Get-NamedControl -Root $root -Name 'BrowseButton').Add_Click({
            $dialog = [Microsoft.Win32.OpenFileDialog]::new()
            $dialog.Multiselect = $true
            $dialog.CheckFileExists = $true
            $dialog.Title = 'Select HL7 message file(s)'
            if ($dialog.ShowDialog($context.Window) -eq $true) {
                (Get-NamedControl -Root $root -Name 'PathTextBox').Text = $dialog.FileNames -join ';'
            }
        }.GetNewClosure())

        $sendButton.Add_Click({
            try {
                $parameters = & $context.PanelDefinition.BuildParameters $root $context
                & $context.RunCommand $context.PanelDefinition.Name $parameters $sendButton
            }
            catch {
                & $context.ShowMessage $_.Exception.Message 'Invalid input' 'Error'
                Set-GuiStatus -Text 'Validation failed' -StatusText $context.StatusText
            }
        }.GetNewClosure())

        (Get-NamedControl -Root $root -Name 'ClearButton').Add_Click({
            (Get-NamedControl -Root $root -Name 'HostNameTextBox').Clear()
            (Get-NamedControl -Root $root -Name 'PortTextBox').Clear()
            (Get-NamedControl -Root $root -Name 'PathTextBox').Clear()
            $messageBox.Clear()
            (Get-NamedControl -Root $root -Name 'DelayTextBox').Text = '0'
            (Get-NamedControl -Root $root -Name 'EncodingComboBox').SelectedIndex = 0
            (Get-NamedControl -Root $root -Name 'NoAckCheckBox').IsChecked = $false
            $useTls.IsChecked = $false
            $skipCertificate.IsChecked = $false
            $pathMode.IsChecked = $true
            $context.LogBox.Clear()
            Set-GuiStatus -Text 'Ready' -StatusText $context.StatusText
        }.GetNewClosure())

        (Get-NamedControl -Root $root -Name 'CloseButton').Add_Click({ $context.CloseWindow.Invoke() }.GetNewClosure())
    }.GetNewClosure()
    BuildParameters = {
        param($root, $context)
        $hostName = (Get-NamedControl -Root $root -Name 'HostNameTextBox').Text.Trim()
        if ([string]::IsNullOrWhiteSpace($hostName)) {
            throw 'Enter a host name or IP address.'
        }

        $portValue = (Get-NamedControl -Root $root -Name 'PortTextBox').Text.Trim()
        $port = 0
        if (-not [int]::TryParse($portValue, [ref]$port) -or $port -lt 1 -or $port -gt 65535) {
            throw 'Port must be a whole number from 1 through 65535.'
        }

        $delayValue = (Get-NamedControl -Root $root -Name 'DelayTextBox').Text.Trim()
        $delay = 0
        if (-not [int]::TryParse($delayValue, [ref]$delay) -or $delay -lt 0 -or $delay -gt 600) {
            throw 'Delay must be a whole number from 0 through 600 seconds.'
        }

        $parameters = @{ HostName = $hostName; Port = $port }
        if ((Get-NamedControl -Root $root -Name 'MessageRadioButton').IsChecked) {
            $message = (Get-NamedControl -Root $root -Name 'MessageTextBox').Text
            if ([string]::IsNullOrWhiteSpace($message)) {
                throw 'Enter the HL7 message text to send.'
            }
            $parameters.MessageString = [string[]]@($message -split '\r\n|\n|\r')
        }
        else {
            $paths = (Get-NamedControl -Root $root -Name 'PathTextBox').Text -split '[,;]' |
                ForEach-Object { $_.Trim() } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
            if (@($paths).Count -eq 0) {
                throw 'Enter or browse to at least one message file.'
            }

            if ((Get-NamedControl -Root $root -Name 'LiteralPathRadioButton').IsChecked) {
                $parameters.LiteralPath = [string[]]$paths
            }
            else {
                $parameters.Path = [string[]]$paths
            }
        }

        $parameters.Encoding = (Get-NamedControl -Root $root -Name 'EncodingComboBox').SelectedItem.Content
        if ($delay -gt 0) {
            $parameters.Delay = $delay
        }
        if ((Get-NamedControl -Root $root -Name 'NoAckCheckBox').IsChecked) {
            $parameters.NoACK = $true
        }
        if ((Get-NamedControl -Root $root -Name 'UseTlsCheckBox').IsChecked) {
            $parameters.UseTLS = $true
            if ((Get-NamedControl -Root $root -Name 'SkipCertificateCheckBox').IsChecked) {
                $parameters.SkipCertificateCheck = $true
            }
        }

        return $parameters
    }.GetNewClosure()
}
$definition
