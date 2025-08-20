using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text;
using System.Windows;
using WmiExplorer.Common.Base;
using WmiExplorer.Common.Logging;
using WmiExplorer.Models;
using WmiExplorer.Presentation.Views.Dialogs;

namespace WmiExplorer.Presentation.ViewModels.Dialogs;

/// <summary>
/// ViewModel for the GenerateScriptDialog that generates PowerShell scripts for WMI operations.
/// </summary>
public partial class GenerateScriptDialogViewModel : DisposableObservableObject
{
    public enum WmiModule
    {
        LegacyModule,
        CimCmdlets
    }

    [ObservableProperty]
    private string _computerName = Environment.MachineName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyOutputCommand), nameof(SaveOutputCommand))]
    private string _executionOutput = string.Empty;

    [ObservableProperty]
    private string _generatedScript = string.Empty;

    [ObservableProperty]
    private bool _includeComments = true;

    [ObservableProperty]
    private bool _includeErrorHandling = true;

    [ObservableProperty]
    private bool _isExecuting = false;

    [ObservableProperty]
    private bool _isScriptEditable = false;

    private readonly ManagementScope _managementScope;
    private readonly Dictionary<string, object>? _parameterValues;

    [ObservableProperty]
    private string _scriptTitle = "Generated PowerShell Script";

    private readonly object _selectedItem;

    [ObservableProperty]
    private WmiModule _selectedModule = WmiModule.CimCmdlets;

    [ObservableProperty]
    private bool _useComputerName = false;

    [ObservableProperty]
    private bool _useCredentials = false;

    [ObservableProperty]
    private bool _useFullNamespace = false;

    [ObservableProperty]
    private bool _usePS7 = true;

    private readonly Window _window;

    /// <summary>
    /// Initializes a new instance of the GenerateScriptDialogViewModel.
    /// </summary>
    /// <param name="window">The dialog window instance</param>
    /// <param name="selectedItem">The WMI item to generate script for (WmiClass, WmiInstance, or WmiMethod)</param>
    /// <param name="managementScope">The WMI management scope</param>
    public GenerateScriptDialogViewModel(Window window, object selectedItem, ManagementScope managementScope)
        : this(window, selectedItem, managementScope, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the GenerateScriptDialogViewModel with parameter values.
    /// </summary>
    /// <param name="window">The dialog window instance</param>
    /// <param name="selectedItem">The WMI item to generate script for (WmiClass, WmiInstance, or WmiMethod)</param>
    /// <param name="managementScope">The WMI management scope</param>
    /// <param name="parameterValues">Dictionary of parameter names and their values (for methods)</param>
    public GenerateScriptDialogViewModel(Window window, object selectedItem, ManagementScope managementScope, Dictionary<string, object>? parameterValues)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _selectedItem = selectedItem ?? throw new ArgumentNullException(nameof(selectedItem));
        _managementScope = managementScope ?? throw new ArgumentNullException(nameof(managementScope));
        _parameterValues = parameterValues;

        // Set computer name from scope if available
        if (!string.IsNullOrWhiteSpace(_managementScope.Path?.Server))
        {
            ComputerName = _managementScope.Path.Server;
        }

        // Generate initial script
        GenerateScript();
    }

    /// <summary>
    /// Gets whether this is a method item (affects which script type options are shown).
    /// </summary>
    public bool IsMethodItem => _selectedItem is WmiMethod;

    /// <summary>
    /// Gets whether this is a queryable item (class or instance).
    /// </summary>
    public bool IsQueryableItem => _selectedItem is WmiClass or WmiInstance;

    /// <summary>
    /// Gets the item type description.
    /// </summary>
    public string ItemTypeDescription
    {
        get
        {
            return _selectedItem switch
            {
                WmiClass => "WMI Class",
                WmiInstance => "WMI Instance",
                WmiMethod => "WMI Method",
                _ => "Unknown"
            };
        }
    }

    /// <summary>
    /// Gets the namespace path from the management scope.
    /// </summary>
    public string NamespacePath => _managementScope.Path?.Path ?? "ROOT\\CIMV2";

    /// <summary>
    /// Gets the script type description for the current item.
    /// </summary>
    public string ScriptTypeDescription
    {
        get
        {
            return _selectedItem switch
            {
                WmiClass => "Query all instances of the class",
                WmiInstance => "Retrieve the specific instance",
                WmiMethod => "Execute the method",
                _ => "Unknown operation"
            };
        }
    }

    /// <summary>
    /// Gets the display name of the selected item.
    /// </summary>
    public string SelectedItemName
    {
        get
        {
            return _selectedItem switch
            {
                WmiClass wmiClass => wmiClass.ClassName ?? "Unknown Class",
                WmiInstance wmiInstance => $"{wmiInstance.ClassPath?.ClassName ?? "Unknown"} Instance",
                WmiMethod wmiMethod => $"{wmiMethod.Name ?? "Unknown"} Method",
                _ => "Unknown Item"
            };
        }
    }

    /// <summary>
    /// Adds connection parameters to the script.
    /// </summary>
    private void AddConnectionParameters(StringBuilder scriptBuilder)
    {
        if (IncludeComments)
        {
            scriptBuilder.AppendLine("# Connection Parameters");
        }

        // Add computer name if toggle is enabled
        if (UseComputerName && !string.IsNullOrWhiteSpace(ComputerName))
        {
            scriptBuilder.AppendLine($"$ComputerName = '{ComputerName}'");
        }

        // Add namespace - use full path if toggle is enabled, otherwise use relative
        var namespacePath = UseFullNamespace ? NamespacePath : GetRelativeNamespacePath();
        scriptBuilder.AppendLine($"$Namespace = '{namespacePath}'");

        if (UseCredentials)
        {
            scriptBuilder.AppendLine("$Username = 'YOUR_USERNAME'  # Replace with actual username");
            scriptBuilder.AppendLine("$Password = ConvertTo-SecureString -String 'YOUR_PASSWORD' -AsPlainText -Force # Replace with actual password");
            scriptBuilder.AppendLine("$Credential = New-Object System.Management.Automation.PSCredential($Username, $Password)");
        }

        scriptBuilder.AppendLine();
    }

    /// <summary>
    /// Builds a WQL WHERE clause from a WMI instance path.
    /// </summary>
    /// <param name="instancePath">The WMI instance path</param>
    /// <param name="className">The class name</param>
    /// <returns>The WHERE clause for filtering the instance</returns>
    private string BuildWhereClauseFromPath(string instancePath, string className)
    {
        if (string.IsNullOrWhiteSpace(instancePath))
            return "";

        // Remove the class name from the beginning of the path
        var pathWithoutClass = instancePath;
        if (instancePath.StartsWith($"{className}.", StringComparison.OrdinalIgnoreCase))
        {
            pathWithoutClass = instancePath.Substring(className.Length + 1);
        }

        // Convert the path format to WQL WHERE clause
        // Example: "Name='notepad.exe',ProcessId=1234" becomes "Name='notepad.exe' AND ProcessId=1234"
        var whereClause = pathWithoutClass.Replace(",", " AND ");

        return whereClause;
    }

    /// <summary>
    /// Command to cancel the dialog.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        _window.DialogResult = false;
        _window.Close();
    }

    /// <summary>
    /// Command to copy the execution output to clipboard.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyOutput()
    {
        try
        {
            Clipboard.SetText(ExecutionOutput);
            Log.Information("Execution output copied to clipboard");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to copy execution output to clipboard");
            MessageBoxDialog.Show($"Failed to copy execution output to clipboard: {ex.Message}", "Copy Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Error, _window);
        }
    }

    /// <summary>
    /// Command to copy the generated script to clipboard.
    /// </summary>
    [RelayCommand]
    private void CopyToClipboard()
    {
        try
        {
            Clipboard.SetText(GeneratedScript);
            Log.Information("Script copied to clipboard for {ItemType}: {ItemName}", ItemTypeDescription, SelectedItemName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to copy script to clipboard");
            MessageBoxDialog.Show($"Failed to copy script to clipboard: {ex.Message}", "Copy Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Error, _window);
        }
    }

    /// <summary>
    /// Command to execute the generated script using PowerShell.
    /// </summary>
    [RelayCommand]
    private async Task ExecuteScriptAsync()
    {
        if (string.IsNullOrWhiteSpace(GeneratedScript))
        {
            MessageBoxDialog.Show("No script to execute.", "Execution Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Warning, _window);
            return;
        }

        try
        {
            IsExecuting = true;
            ExecutionOutput = "Executing script...\r\n";

            var tempScriptPath = Path.GetTempFileName() + ".ps1";

            // Create a wrapper script that disables formatting
            var wrapperScript = $@"
# Disable formatting and colors
$PSStyle.OutputRendering = [System.Management.Automation.OutputRendering]::PlainText
$Host.UI.RawUI.ForegroundColor = 'White'
$Host.UI.RawUI.BackgroundColor = 'Black'

# Execute the actual script
{GeneratedScript}
";

            File.WriteAllText(tempScriptPath, wrapperScript, Encoding.UTF8);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = UsePS7 ? "pwsh.exe" : "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -NoProfile -NonInteractive -File \"{tempScriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Set environment variables to disable colors
            processStartInfo.EnvironmentVariables["NO_COLOR"] = "1";
            processStartInfo.EnvironmentVariables["TERM"] = "dumb";
            processStartInfo.EnvironmentVariables["FORCE_COLOR"] = "0";

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var result = new StringBuilder();
            result.AppendLine($"Exit Code: {process.ExitCode}");
            result.AppendLine();

            if (!string.IsNullOrEmpty(output))
            {
                result.AppendLine("Output:");
                result.AppendLine(output);
            }

            if (!string.IsNullOrEmpty(error))
            {
                result.AppendLine("Errors:");
                result.AppendLine(error);
            }

            ExecutionOutput = result.ToString();

            // Clean up temp file
            try
            {
                File.Delete(tempScriptPath);
            }
            catch
            {
                // Ignore cleanup errors
            }

            Log.Information("Script executed successfully with exit code: {ExitCode}", process.ExitCode);
        }
        catch (Exception ex)
        {
            ExecutionOutput = $"Execution failed: {ex.Message}";
            Log.Error(ex, "Failed to execute script");
            MessageBoxDialog.Show($"Failed to execute script: {ex.Message}", "Execution Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Error, _window);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    /// <summary>
    /// Formats a parameter value for PowerShell script output.
    /// </summary>
    private string FormatParameterValue(object value, WmiParameter parameter)
    {
        if (value == null)
            return "$null";

        var cimType = parameter.CimType?.ToLowerInvariant() ?? parameter.Type?.ToLowerInvariant() ?? "";

        return value switch
        {
            string str => $"'{str.Replace("'", "''")}'", // Escape single quotes
            bool b => b ? "$true" : "$false",
            int or long or short or byte => value.ToString() ?? "0",
            uint or ulong or ushort or sbyte => value.ToString() ?? "0",
            float or double or decimal => value.ToString() ?? "0.0",
            DateTime dt => $"Get-Date '{dt:yyyy-MM-dd HH:mm:ss}'",
            _ => $"'{value?.ToString() ?? "null"}'" // Default to string representation
        };
    }

    /// <summary>
    /// Command to generate the script.
    /// </summary>


    /// <summary>
    /// Generates script for a WMI class.
    /// </summary>
    private void GenerateClassScript(StringBuilder scriptBuilder, WmiClass wmiClass)
    {
        if (IncludeComments)
        {
            scriptBuilder.AppendLine("# Query all instances of the WMI class");
        }

        var className = wmiClass.ClassName;
        var indentation = IncludeErrorHandling ? "    " : "";

        scriptBuilder.AppendLine($"$ClassName = '{className}'");
        scriptBuilder.AppendLine();
        if (IncludeErrorHandling)
        {
            scriptBuilder.AppendLine("try {");
        }

        if (SelectedModule == WmiModule.LegacyModule)
        {
            if (UseComputerName && !string.IsNullOrWhiteSpace(ComputerName))
            {
                scriptBuilder.AppendLine($"{indentation}$Instances = Get-WmiObject -Class $ClassName -Namespace $Namespace -ComputerName $ComputerName");
            }
            else
            {
                scriptBuilder.AppendLine($"{indentation}$Instances = Get-WmiObject -Class $ClassName -Namespace $Namespace");
            }
        }
        else // CimCmdlets
        {
            if (UseComputerName && !string.IsNullOrWhiteSpace(ComputerName))
            {
                if (UseCredentials)
                {
                    scriptBuilder.AppendLine($"{indentation}$Instances = Get-CimInstance -ClassName $ClassName -Namespace $Namespace -ComputerName $ComputerName -Credential $Credential");
                }
                else
                {
                    scriptBuilder.AppendLine($"{indentation}$Instances = Get-CimInstance -ClassName $ClassName -Namespace $Namespace -ComputerName $ComputerName");
                }
            }
            else
            {
                if (UseCredentials)
                {
                    scriptBuilder.AppendLine($"{indentation}$Instances = Get-CimInstance -ClassName $ClassName -Namespace $Namespace -Credential $Credential");
                }
                else
                {
                    scriptBuilder.AppendLine($"{indentation}$Instances = Get-CimInstance -ClassName $ClassName -Namespace $Namespace");
                }
            }
        }

        scriptBuilder.AppendLine($"{indentation}$Instances | Format-Table -AutoSize");
        if (IncludeErrorHandling)
        {
            scriptBuilder.AppendLine("}");
            scriptBuilder.AppendLine("catch {");
            scriptBuilder.AppendLine("    Write-Error \"Failed to query WMI class: $($_.Exception.Message)\"");
            scriptBuilder.AppendLine("}");
        }
    }

    /// <summary>
    /// Generates script for a WMI instance.
    /// </summary>
    private void GenerateInstanceScript(StringBuilder scriptBuilder, WmiInstance wmiInstance)
    {
        if (IncludeComments)
        {
            scriptBuilder.AppendLine("# Query specific WMI instance");
        }

        var className = wmiInstance.ClassPath?.ClassName ?? "Unknown";
        var instancePath = wmiInstance.Path?.RelativePath ?? "";
        var indentation = IncludeErrorHandling ? "    " : "";

        scriptBuilder.AppendLine($"$ClassName = '{className}'");
        scriptBuilder.AppendLine($"$InstancePath = '{instancePath}'");
        scriptBuilder.AppendLine();
        if (IncludeErrorHandling)
        {
            scriptBuilder.AppendLine("try {");
        }

        if (SelectedModule == WmiModule.LegacyModule)
        {
            if (UseComputerName && !string.IsNullOrWhiteSpace(ComputerName))
            {
                scriptBuilder.AppendLine($"{indentation}$Instance = Get-WmiObject -Path $InstancePath -ComputerName $ComputerName");
            }
            else
            {
                scriptBuilder.AppendLine($"{indentation}$Instance = Get-WmiObject -Path $InstancePath");
            }
        }
        else // CimCmdlets
        {
            // For CIM cmdlets, we need to construct a WQL query with WHERE clause
            // Extract the key properties from the instance path to build the WHERE clause
            var whereClause = BuildWhereClauseFromPath(instancePath, className);
            scriptBuilder.AppendLine($"{indentation}$WhereClause = '{whereClause}'");

            if (UseComputerName && !string.IsNullOrWhiteSpace(ComputerName))
            {
                if (UseCredentials)
                {
                    scriptBuilder.AppendLine($"{indentation}$Instance = Get-CimInstance -ClassName $ClassName -Namespace $Namespace -Filter $WhereClause -ComputerName $ComputerName -Credential $Credential");
                }
                else
                {
                    scriptBuilder.AppendLine($"{indentation}$Instance = Get-CimInstance -ClassName $ClassName -Namespace $Namespace -Filter $WhereClause -ComputerName $ComputerName");
                }
            }
            else
            {
                if (UseCredentials)
                {
                    scriptBuilder.AppendLine($"{indentation}$Instance = Get-CimInstance -ClassName $ClassName -Namespace $Namespace -Filter $WhereClause -Credential $Credential");
                }
                else
                {
                    scriptBuilder.AppendLine($"{indentation}$Instance = Get-CimInstance -ClassName $ClassName -Namespace $Namespace -Filter $WhereClause");
                }
            }
        }

        scriptBuilder.AppendLine($"{indentation}$Instance | Format-List");
        if (IncludeErrorHandling)
        {
            scriptBuilder.AppendLine("}");
            scriptBuilder.AppendLine("catch {");
            scriptBuilder.AppendLine("    Write-Error \"Failed to get WMI instance: $($_.Exception.Message)\"");
            scriptBuilder.AppendLine("}");
        }
    }

    /// <summary>
    /// Generates script for a WMI method.
    /// </summary>
    private void GenerateMethodScript(StringBuilder scriptBuilder, WmiMethod wmiMethod)
    {
        if (IncludeComments)
        {
            scriptBuilder.AppendLine("# Execute WMI method");
        }

        var methodName = wmiMethod.Name;
        var className = wmiMethod.ClassName;
        var indentation = IncludeErrorHandling ? "    " : "";

        // Add method parameter information if available
        if (IncludeComments && wmiMethod.InParameters.Count > 0)
        {
            scriptBuilder.AppendLine("# Method Parameters:");
            foreach (var param in wmiMethod.InParameters)
            {
                var paramType = param.CimType ?? param.Type ?? "Unknown";
                var optional = param.HasOptionalQualifier ? " (Optional)" : "";
                scriptBuilder.AppendLine($"#   {param.Name}: {paramType}{optional}");
            }
            scriptBuilder.AppendLine();
        }

        scriptBuilder.AppendLine($"$ClassName = '{className}'");
        scriptBuilder.AppendLine($"$MethodName = '{methodName}'");

        // Add parameter variables if method has parameters
        if (wmiMethod.InParameters.Count > 0)
        {
            scriptBuilder.AppendLine();
            scriptBuilder.AppendLine("# Method Parameters:");
            foreach (var param in wmiMethod.InParameters)
            {
                if (string.IsNullOrEmpty(param.Name))
                    continue;

                var paramType = param.CimType?.ToLowerInvariant() ?? param.Type?.ToLowerInvariant() ?? "";

                // Check if this is an Object type parameter (not supported)
                if (paramType == "object")
                {
                    scriptBuilder.AppendLine($"# ${param.Name} = <not supported - Object type parameters are not supported>  # {param.CimType ?? param.Type ?? "Unknown"} (Object type)");
                    continue;
                }

                // Handle parameters based on whether we have values from dialog or not
                if (_parameterValues != null)
                {
                    // Dialog was shown - only include parameters that were actually provided
                    if (_parameterValues.TryGetValue(param.Name, out var value))
                    {
                        var paramValue = FormatParameterValue(value, param);
                        scriptBuilder.AppendLine($"${param.Name} = {paramValue}  # {param.CimType ?? param.Type ?? "Unknown"} (from dialog)");
                    }
                    // Skip parameters that weren't provided in dialog
                }
                else
                {
                    // No dialog shown - list all supported parameters for user to fill out
                    var defaultValue = GetParameterDefaultValue(param);
                    scriptBuilder.AppendLine($"${param.Name} = {defaultValue}  # {param.CimType ?? param.Type ?? "Unknown"} (fill in value)");
                }
            }
        }

        scriptBuilder.AppendLine();
        if (IncludeErrorHandling)
        {
            scriptBuilder.AppendLine("try {");
        }

        if (SelectedModule == WmiModule.LegacyModule)
        {
            // Get the class using Legacy Module
            if (UseComputerName && !string.IsNullOrWhiteSpace(ComputerName))
            {
                scriptBuilder.AppendLine($"{indentation}$Class = Get-WmiObject -Class $ClassName -Namespace $Namespace -ComputerName $ComputerName");
            }
            else
            {
                scriptBuilder.AppendLine($"{indentation}$Class = Get-WmiObject -Class $ClassName -Namespace $Namespace");
            }

            // Execute method using Legacy Module
            if (wmiMethod.InParameters.Count > 0)
            {
                var providedParams = GetProvidedParameters(wmiMethod.InParameters);
                if (providedParams.Count > 0)
                {
                    var paramList = string.Join(", ", providedParams.Select(p => $"${p.Name}"));
                    scriptBuilder.AppendLine($"{indentation}$Result = $Class.$MethodName({paramList})");
                }
                else
                {
                    scriptBuilder.AppendLine($"{indentation}$Result = $Class.$MethodName()");
                }
            }
            else
            {
                scriptBuilder.AppendLine($"{indentation}$Result = $Class.$MethodName()");
            }
        }
        else // CimCmdlets
        {
            // Get the class using CIM Module
            if (UseComputerName && !string.IsNullOrWhiteSpace(ComputerName))
            {
                if (UseCredentials)
                {
                    scriptBuilder.AppendLine($"{indentation}$Class = Get-CimClass -ClassName $ClassName -Namespace $Namespace -ComputerName $ComputerName -Credential $Credential");
                }
                else
                {
                    scriptBuilder.AppendLine($"{indentation}$Class = Get-CimClass -ClassName $ClassName -Namespace $Namespace -ComputerName $ComputerName");
                }
            }
            else
            {
                if (UseCredentials)
                {
                    scriptBuilder.AppendLine($"{indentation}$Class = Get-CimClass -ClassName $ClassName -Namespace $Namespace -Credential $Credential");
                }
                else
                {
                    scriptBuilder.AppendLine($"{indentation}$Class = Get-CimClass -ClassName $ClassName -Namespace $Namespace");
                }
            }

            // Execute method using CIM Module
            if (wmiMethod.InParameters.Count > 0)
            {
                var providedParams = GetProvidedParameters(wmiMethod.InParameters);
                if (providedParams.Count > 0)
                {
                    scriptBuilder.AppendLine($"{indentation}$Params = @{{");
                    foreach (var param in providedParams)
                    {
                        scriptBuilder.AppendLine($"{indentation}    {param.Name} = ${param.Name}");
                    }
                    scriptBuilder.AppendLine($"{indentation}}}");
                    scriptBuilder.AppendLine($"{indentation}$Result = Invoke-CimMethod -CimClass $Class -MethodName $MethodName -Arguments $Params");
                }
                else
                {
                    scriptBuilder.AppendLine($"{indentation}$Result = Invoke-CimMethod -CimClass $Class -MethodName $MethodName");
                }
            }
            else
            {
                scriptBuilder.AppendLine($"{indentation}$Result = Invoke-CimMethod -CimClass $Class -MethodName $MethodName");
            }
        }

        scriptBuilder.AppendLine($"{indentation}$Result");
        if (IncludeErrorHandling)
        {
            scriptBuilder.AppendLine("}");
            scriptBuilder.AppendLine("catch {");
            scriptBuilder.AppendLine("    Write-Error \"Failed to execute WMI method: $($_.Exception.Message)\"");
            scriptBuilder.AppendLine("}");
        }
    }

    /// <summary>
    /// Generates the PowerShell script based on the selected item and options.
    /// </summary>
    private void GenerateScript()
    {
        try
        {
            var scriptBuilder = new StringBuilder();

            // Add header comments
            if (IncludeComments)
            {
                scriptBuilder.AppendLine("# PowerShell Script Generated by WMI Explorer");
                scriptBuilder.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                scriptBuilder.AppendLine($"# Item Type: {ItemTypeDescription}");
                scriptBuilder.AppendLine($"# Item Name: {SelectedItemName}");

                // Show the actual namespace that will be used in the script
                var actualNamespace = UseFullNamespace ? NamespacePath : GetRelativeNamespacePath();
                scriptBuilder.AppendLine($"# Namespace: {actualNamespace}");
                scriptBuilder.AppendLine();
            }

            // Add connection parameters
            AddConnectionParameters(scriptBuilder);

            // Generate script based on item type
            switch (_selectedItem)
            {
                case WmiClass wmiClass:
                    GenerateClassScript(scriptBuilder, wmiClass);
                    break;
                case WmiInstance wmiInstance:
                    GenerateInstanceScript(scriptBuilder, wmiInstance);
                    break;
                case WmiMethod wmiMethod:
                    GenerateMethodScript(scriptBuilder, wmiMethod);
                    break;
            }

            GeneratedScript = scriptBuilder.ToString();
            Log.Debug("Generated PowerShell script for {ItemType}: {ItemName}", ItemTypeDescription, SelectedItemName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating PowerShell script");
            GeneratedScript = $"# Error generating script: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets a default value for a WMI parameter based on its type.
    /// </summary>
    private string GetParameterDefaultValue(WmiParameter parameter)
    {
        var cimType = parameter.CimType?.ToLowerInvariant() ?? parameter.Type?.ToLowerInvariant() ?? "";

        return cimType switch
        {
            "string" => "''",
            "uint32" or "int32" or "uint16" or "int16" or "uint8" or "int8" => "0",
            "uint64" or "int64" => "0L",
            "real32" or "real64" => "0.0",
            "boolean" => "$false",
            "datetime" => "Get-Date",
            "object" => "$null",
            _ => "$null"
        };
    }

    /// <summary>
    /// Gets the list of parameters that were provided and are supported.
    /// </summary>
    /// <param name="parameters">The list of method parameters</param>
    /// <returns>List of parameters that should be included in the method call</returns>
    private List<WmiParameter> GetProvidedParameters(IEnumerable<WmiParameter> parameters)
    {
        var providedParams = new List<WmiParameter>();

        foreach (var param in parameters)
        {
            if (string.IsNullOrEmpty(param.Name))
                continue;

            var paramType = param.CimType?.ToLowerInvariant() ?? param.Type?.ToLowerInvariant() ?? "";

            // Skip Object type parameters (not supported)
            if (paramType == "object")
                continue;

            if (_parameterValues != null)
            {
                // Dialog was shown - only include parameters that were actually provided
                if (_parameterValues.TryGetValue(param.Name, out _))
                {
                    providedParams.Add(param);
                }
                // Skip parameters that weren't provided in dialog
            }
            else
            {
                // No dialog shown - include all supported parameters
                providedParams.Add(param);
            }
        }

        return providedParams;
    }

    /// <summary>
    /// Gets the relative namespace path from the selected WMI object.
    /// </summary>
    /// <returns>The relative namespace path</returns>
    private string GetRelativeNamespacePath()
    {
        return _selectedItem switch
        {
            WmiClass wmiClass => wmiClass.Path?.NamespacePath ?? "ROOT\\CIMV2",
            WmiInstance wmiInstance => wmiInstance.Path?.NamespacePath ?? "ROOT\\CIMV2",
            WmiMethod wmiMethod => _managementScope.Path?.NamespacePath ?? "ROOT\\CIMV2",
            _ => "ROOT\\CIMV2"
        };
    }

    /// <summary>
    /// Determines if the copy output command can be executed.
    /// </summary>
    private bool HasOutput() => !string.IsNullOrWhiteSpace(ExecutionOutput);

    partial void OnComputerNameChanged(string value)
    {
        if (!IsScriptEditable)
        {
            GenerateScript();
        }
    }

    partial void OnIncludeCommentsChanged(bool value)
    {
        if (!IsScriptEditable)
        {
            GenerateScript();
        }
    }

    partial void OnIncludeErrorHandlingChanged(bool value)
    {
        if (!IsScriptEditable)
        {
            GenerateScript();
        }
    }

    partial void OnIsScriptEditableChanged(bool value)
    {
        // When user disables script editing, regenerate the script with current options
        if (!value)
        {
            GenerateScript();
        }
    }

    /// <summary>
    /// Called when script generation options change.
    /// </summary>
    partial void OnSelectedModuleChanged(WmiModule value)
    {
        if (!IsScriptEditable)
        {
            GenerateScript();
        }
    }

    partial void OnUseComputerNameChanged(bool value)
    {
        if (!IsScriptEditable)
        {
            GenerateScript();
        }
    }

    partial void OnUseCredentialsChanged(bool value)
    {
        if (!IsScriptEditable)
        {
            GenerateScript();
        }
    }

    partial void OnUseFullNamespaceChanged(bool value)
    {
        if (!IsScriptEditable)
        {
            GenerateScript();
        }
    }

    partial void OnUsePS7Changed(bool value)
    {
        // No need to regenerate script for this change
    }

    /// <summary>
    /// Command to save the execution output to a file.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void SaveOutput()
    {
        try
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|Log Files (*.log)|*.log|All Files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"WMI_Output_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, ExecutionOutput, Encoding.UTF8);
                Log.Information("Execution output saved to file: {FileName}", saveFileDialog.FileName);
                MessageBoxDialog.Show($"Execution output saved successfully to:\n{saveFileDialog.FileName}", "Save Successful", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Information, _window);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save execution output to file");
            MessageBoxDialog.Show($"Failed to save execution output to file: {ex.Message}", "Save Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Error, _window);
        }
    }

    /// <summary>
    /// Command to save the script to a file.
    /// </summary>
    [RelayCommand]
    private void SaveToFile()
    {
        try
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PowerShell Scripts (*.ps1)|*.ps1|All Files (*.*)|*.*",
                DefaultExt = "ps1",
                FileName = $"WMI_Script_{DateTime.Now:yyyyMMdd_HHmmss}.ps1"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, GeneratedScript, Encoding.UTF8);
                Log.Information("Script saved to file: {FileName}", saveFileDialog.FileName);
                MessageBoxDialog.Show($"Script saved successfully to:\n{saveFileDialog.FileName}", "Save Successful", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Information, _window);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save script to file");
            MessageBoxDialog.Show($"Failed to save script to file: {ex.Message}", "Save Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Error, _window);
        }
    }
}