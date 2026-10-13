# HL7 Tools WPF GUI

The GUI provides a PowerShell 7 WPF interface for `Send-HL7Message`. It is Windows-only, uses built-in WPF, and discovers cmdlet panels from `gui/Cmdlets` at startup.

## Requirements and launch

- Windows
- PowerShell 7 or later
- The `hl7tools` module installed, or a built module at `module/hl7tools`

From the repository root, run:

```powershell
pwsh -STA -File gui/Start-HL7ToolsGUI.ps1
```

The launcher relaunches itself with `-STA` when needed. It first imports the repository module manifest and then falls back to an installed `hl7tools` module. If building from source, publish the module first using the commands in the root README.

## Add a cmdlet panel

Add a matching pair of files to `gui/Cmdlets`: `<Cmdlet-Name>.ps1` and `<Cmdlet-Name>.xaml`. The launcher loads every panel script and displays its `DisplayName`; the panel XAML should have a WPF root control. Return a hashtable or object with:

- `Name`: exported cmdlet name
- `DisplayName` and `Description`
- `XamlPath`: path to the panel's XAML
- `Initialize`: scriptblock receiving the loaded root control and shared context
- `BuildParameters`: scriptblock receiving the root control and context, validating fields, and returning a splat hashtable (throw an informative error for invalid input)

Use `Get-NamedControl` to access named XAML controls. Wire the panel's Run button to `& $context.RunCommand $context.PanelDefinition.Name $parameters $button`; this uses the shared background runspace and output/status handling. Use `$context.ShowMessage` to display validation errors and `$context.CloseWindow` to close the shell. Shared helpers are in `gui/Common/GuiHelpers.ps1`.
