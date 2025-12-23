# WMI Explorer

A modern Windows desktop application for exploring and managing Windows Management Instrumentation (WMI) namespaces, classes, instances, properties, and methods. Built with WPF and .NET 8.0.

> **Note:** This project replaces [wmie2](https://github.com/vinaypamnani/wmie2). Please use this repository for the latest version.

## Features

- **Namespace Browser**: Navigate through WMI namespaces in a tree view - see [Connecting to a WMI Namespace](#connecting-to-a-wmi-namespace) and [Exploring Namespaces and Classes](#exploring-namespaces-and-classes)
- **Class Explorer**: Browse WMI classes with their properties, methods, and qualifiers - see [Exploring Namespaces and Classes](#exploring-namespaces-and-classes)
- **Instance Viewer**: View and filter WMI instances with detailed property information - see [Exploring Instances](#exploring-instances)
- **Instance Management**: Create, edit, and delete WMI instances with a dedicated property editor dialog - see [Managing WMI Instances](#managing-wmi-instances)
- **Method Execution**: Execute WMI methods (both static and instance methods) with parameter input and output display - see [Executing WMI Methods](#executing-wmi-methods)
- **WQL Query Editor**: Execute WQL queries with syntax highlighting and result display - see [Executing WQL Queries](#executing-wql-queries)
- **Event Watcher**: Monitor WMI events in real-time - see [Monitoring WMI Events](#monitoring-wmi-events)
- **Search**: Search for classes, properties, and methods across namespaces - see [Searching WMI](#searching-wmi)
- **Script Generator**: Generate PowerShell scripts for WMI operations (classes, instances, methods) - see [Generating PowerShell Scripts](#generating-powershell-scripts)
- **Property Grid**: Detailed property editor with support for WMI-specific types - see [Property Grid](docs/PROPERTY_GRID.md)
- **Logging**: Built-in logging with configurable log levels - see [Viewing Logs](#viewing-logs)
- **Theme Support**: Dark and Light themes with customizable accent colors - see [Menu Options](#menu-options)
- **Auto-updates**: Check for updates from GitHub releases - see [Menu Options](#menu-options)

## Getting Started

### Prerequisites

- Windows 10/11 or Windows Server 2016+
- .NET 8.0 Runtime (if running published binaries)
- Administrator privileges (recommended for full WMI access)

### Running the Application

Download the latest release from GitHub. Extract and run `WmiExplorer.exe`.

> 💡 NOTE: If prompted, download and install the required .NET runtime, and then launch `WmiExplorer.exe` again.

### Command-Line Options

WMI Explorer supports command-line arguments for automation and debugging. See [Command-Line Options](docs/COMMAND_LINE.md) for complete documentation.

**Quick Reference:**
- `-debug` - Enable debug logging to console
- `-computername <name>` - Auto-connect to specified computer
- `-username <name>` - Specify username for auto-connection

**Example:**
```bash
WmiExplorer.exe -computername SERVER01 -username DOMAIN\User
```

## Connecting to a WMI Namespace

1. In the **Options** panel at the top, enter in the **Computer Name** field:
   - **Computer Name**: Local computer (`.`, `localhost`) or remote computer name/IP
   - **Namespace Path**: You can also enter a namespace path directly:
     - Full path: `\\computer\root\cimv2` (for remote computer)
     - Namespace only: `root\cimv2` (assumes local computer)

2. Click **Connect** to establish the connection using your current Windows credentials

   - For advanced connection options (username, password, authentication, impersonation), click the dropdown arrow next to the Connect button and select **Connection Options**

3. Once connected, the namespace tree will populate in the left panel. Browse the tree to select the specific namespace you want to explore (e.g., `root\cimv2`)

For automatic connection via command-line, see [Command-Line Options](#command-line-options) or [detailed documentation](docs/COMMAND_LINE.md).

## Exploring WMI

Understanding how to navigate and explore WMI objects is essential for efficient use.

### Navigation Basics

**Single Click Actions:**
- **Namespaces** (left tree view): Expands the namespace to show child namespaces and selects it
- **Classes** (Classes tab): Loads properties and methods for the class and displays them in the Properties and Methods sub-tabs
- **Instances** (Instances tab): Selects the instance and shows its properties in the Property Grid
- **Search Results** (Search tab): Selects the result and shows its details in the Property Grid

**Double Click Actions:**
- **Namespaces** (left tree view): Loads and displays classes for that namespace in the Classes tab
- **Classes** (Classes tab): Loads and displays instances for that class in the Instances tab
- **Search Results** (Search tab): Navigates to the class containing the search result

**Caching Behavior:**

> 💡 Use single-click to quickly navigate and view cached data. Use double-click when you need to explicitly refresh data from WMI.

- Once classes are loaded for a namespace, they are cached in memory. **Single-clicking** the namespace will display the cached classes (indicated by a green status indicator).
- Once instances are loaded for a class, they are cached in memory. **Single-clicking** the class will display the cached instances (indicated by a green status indicator).
- **Double-clicking** always reloads data from WMI, refreshing the cache with the latest information.

For more information about caching, see [Settings and Caching](docs/SETTINGS.md).

### Exploring Namespaces and Classes

1. **Expand a Namespace**: Single click on a namespace in the left tree view to expand it and see child namespaces
2. **Load Classes**: Double click on a namespace to load and display all classes in the **Classes** tab
3. **View Class Details**: Single click on a class to load and view:
   - **Properties**: Class properties with types, qualifiers, and values (shown in Properties sub-tab)
   - **Methods**: Available methods with parameters and return types (shown in Methods sub-tab)
4. **Load Instances**: Double click on a class to load all instances of that class

### Exploring Instances

1. After double-clicking a class, navigate to the **Instances** tab (sub-tab under Classes)
2. View all instances of the selected class
3. Use the filter/search box to find specific instances
4. Click on an instance to view its properties in the Property Grid (right panel)

For more information about the Property Grid, see [Property Grid](docs/PROPERTY_GRID.md).

### Managing WMI Instances

Create, edit, and delete WMI instances directly from the application:

#### Editing Instances

1. **Load instances** of a class (double-click the class to load instances)
2. Navigate to the **Instances** sub-tab
3. **Right-click** on an instance
4. Select **Edit Properties...** from the context menu
   - This option is only available if the class has writable properties
   - If the class has no writable properties, the option will be disabled
5. In the **Property Editor Dialog**:
   - View and edit writable properties of the instance
   - Properties are displayed with their types, descriptions, and current values
   - Required properties are marked and must be filled in
   - Read-only properties are displayed but cannot be modified
6. Click **OK** to save your changes
7. The instance will be updated in WMI and refreshed in the application

#### Creating Instances

1. **Load a class** (double-click a namespace to load classes)
2. Navigate to the **Classes** tab
3. **Right-click** on a class
4. Select **Create instance** from the context menu
5. In the **Property Editor Dialog**:
   - A new template instance is created with default values
   - Fill in the required properties (marked as required)
   - Optionally set optional writable properties
   - Read-only properties are displayed but cannot be modified
6. Click **OK** to create the instance
7. The new instance will be saved to WMI and added to the instances list

> **Note**: Not all WMI classes support instance creation. Some classes are abstract or read-only, and creating instances may not be supported by the WMI provider.

#### Deleting Instances

1. **Load instances** of a class (double-click the class to load instances)
2. Navigate to the **Instances** sub-tab
3. **Right-click** on an instance
4. Select **Delete Instance...** from the context menu
5. Confirm the deletion in the confirmation dialog
6. The instance will be permanently deleted from WMI and removed from the instances list

> **Warning**: Deleting instances is permanent and cannot be undone. Make sure you want to delete the instance before confirming.

**Availability:**
- **Editing**: The **Edit Properties...** menu option is only enabled for classes that have writable properties
- **Creating**: The **Create instance** option is always available in the context menu. If the class doesn't support instance creation, an error message will be displayed when attempting to create the instance
- **Deleting**: The **Delete Instance...** option is always available. If the instance cannot be deleted (e.g., protected by the WMI provider), an error message will be displayed when attempting to delete

### Executing WQL Queries

1. Navigate to the **Query** tab
2. Enter a WQL query in the editor (syntax highlighting is provided)
3. Select query options:
   - **Direct Read**: Bypass class provider
   - **Use Amended Qualifiers**: Include amended qualifiers in results
4. Click **Execute** or press `F5` to run the query
5. Results appear in a grid below the query editor
6. Click on a result to view details in the Property Grid

**Example Queries:**
```sql
-- Get all processes
SELECT * FROM Win32_Process

-- Get processes using more than 100MB memory
SELECT Name, ProcessId, WorkingSetSize FROM Win32_Process WHERE WorkingSetSize > 104857600

-- Get all services
SELECT * FROM Win32_Service
```

### Monitoring WMI Events

1. Navigate to the **Watcher** tab
2. Select a WMI event class (e.g., `__InstanceCreationEvent`, `__InstanceDeletionEvent`)
3. Select a target class to monitor (e.g., `Win32_Process`)
4. Optionally, select **Target Class Property** and specify the value to monitor for the property. For example, **Target Class Property** = `Caption`, and **Target Class Property Value** = `wbemtest.exe` will monitor for process creation events for `wbemtest.exe` only.
5. Click **Add Watcher** to start monitoring
6. Events will appear in real-time as they occur
7. Use **Start/Stop** buttons to control individual watchers
8. Click **Remove** to stop and remove a watcher

**Common Event Queries:**
- Monitor process creation: `__InstanceCreationEvent` with target `Win32_Process`
- Monitor service state changes: `__InstanceModificationEvent` with target `Win32_Service`
- Monitor file system changes: `__InstanceCreationEvent` with target `Win32_LogicalDisk`

### Searching WMI

1. Navigate to the **Search** tab.
   > **Note** that the search scope is limited to the selected namespace. To search entire WMI, select the root namespace, but note that searching root namespace recursively would take a long time.
2. Select what to search for using the radio buttons:
   - **Classes**: Search for WMI class names
   - **Methods**: Search for method names
   - **Properties**: Search for property names
3. Enter a search term in the search box
4. Configure search options:
   - **Recursive**: Search all child namespaces recursively (can take a long time)
   - **Exclude LDAP**: Exclude the `root\directory\LDAP` namespace from recursive searches (only shown when applicable)
5. Click **Search** or press `Enter` to find matching items
6. Results show the namespace, class, and matching item with descriptions
7. Click on a result to view details in the Property Grid
8. Right-click a result and select **Go to Class** to navigate to that class.

### Viewing Logs

1. Navigate to the **Log** tab
2. View application logs with different severity levels:
   - **Debug**: Detailed diagnostic information
   - **Information**: General application events
   - **Warning**: Warning messages
   - **Error**: Error messages and exceptions
3. Use filters to show only specific log levels
4. Logs are also saved to a file in the application directory

For more information about log files and storage, see [Settings and Caching](docs/SETTINGS.md).

### Menu Options

**File Menu:**
- **Run as Administrator**: Restart the application with elevated privileges
- **Exit**: Close the application (`Ctrl+Q`)

**Options Menu:**
- **Check for Updates on Startup**: Enable/disable automatic update checks
- **Reset Theme**: Reset theme colors to defaults (preserves accent colors)

**Start Menu:**
- **Wbemtest.exe**: Launch Windows WMI Tester
- **WmiMgmt.msc**: Launch Windows WMI Management Console
- **DCOMCnfg.exe**: Launch Windows Component Services

**Help Menu:**
- **About WmiExplorer**: Show application information
- **Check for Updates**: Manually check for updates

**Theme Toggle:**
- Click the theme name in the menu bar to toggle between Dark and Light themes

### Executing WMI Methods

Execute WMI methods (both static class methods and instance methods) directly from the application:

1. **For Static Methods** (class-level methods):
   - Navigate to the **Methods** sub-tab under the **Classes** tab
   - **Right-click** on a static method (indicated by a "C" icon)
   - Select **Execute Method...** from the context menu
   - Alternatively, right-click on the class in the Classes tab and select **Execute Static Methods...**

2. **For Instance Methods** (instance-level methods):
   - Load instances of a class (double-click the class)
   - Navigate to the **Instances** sub-tab
   - **Right-click** on an instance
   - Select **Execute Methods...** from the context menu
   - Choose the method you want to execute

3. **In the Method Execution Dialog**:
   - Review the method information (name, description, class, instance if applicable)
   - **Input Parameters**: Fill in any required or optional input parameters
     - Parameters are displayed with their types, descriptions, and default values
     - Select which parameters to include in the execution
     - Enter values for each parameter based on the parameter type
   - Click **Execute** to run the method
   - **Output Parameters**: After execution, view the output parameters and return values in the Output tab
   - The dialog shows execution status and any error messages

   > **Tip**: You can click the **Generate Script** button to create a PowerShell script for this method. The script generator will pre-populate with the parameter values you've entered in the dialog, allowing you to test method parameters first, then generate a script with those exact values.

**Method Types:**
- **Static Methods** (Class methods): Execute on the class itself, not requiring a specific instance
- **Instance Methods**: Execute on a specific instance of a class

**Example Use Cases:**
- Start or stop Windows services using `Win32_Service` methods
- Create or delete WMI instances
- Trigger scheduled tasks or operations
- Invoke management operations on system resources

### Generating PowerShell Scripts

Generate PowerShell scripts for WMI operations directly from the application:

1. **Access the Script Generator**:
   - **Right-click** on any WMI item (class, instance, or method) in the application and select **Generate Script...** from the context menu
   - **From Method Execution Dialog**: Click the **Generate Script** button in the Method Execution Dialog
     - When launched from the Method Execution Dialog, the script generator will pre-populate with the parameter values you've already entered
     - Only parameters that you've selected and filled in the dialog will be included in the generated script
     - This allows you to test method parameters first, then generate a script with those exact values

2. **Configure script options**:
   - **PowerShell Module**: Choose between Legacy WMI module or CimCmdlets
   - **PowerShell Version**: Select PowerShell 5.1 or PowerShell 7+ syntax
   - **Connection Options**: Include computer name and credentials in the script
   - **Script Options**: Include comments, error handling, and full namespace paths
4. The generated script will appear in the dialog
5. You can:
   - **Edit** the script (if enabled)
   - **Copy** the script to clipboard
   - **Save** the script to a file
   - **Execute** the script directly and view output

**What Gets Generated:**
- **For Classes**: PowerShell script to query all instances of the class
- **For Instances**: PowerShell script to retrieve the specific instance
- **For Methods**: PowerShell script to execute the method with parameters

**Example Use Cases:**
- Generate scripts for automation and deployment
- Create reusable PowerShell scripts for common WMI operations
- Test WMI operations before implementing in larger scripts
- Document WMI operations for team members

## Additional Documentation

Additional documentation is available in the [`docs/`](docs/) folder:

- **[Command-Line Options](docs/COMMAND_LINE.md)** - Detailed command-line argument reference
- **[Property Grid](docs/PROPERTY_GRID.md)** - Property Grid features and capabilities
- **[Settings and Caching](docs/SETTINGS.md)** - Application settings, data storage, and caching strategy
- **[Troubleshooting](docs/TROUBLESHOOTING.md)** - Common issues and solutions
- **[Building from Source](docs/BUILDING.md)** - Instructions for building the application from source

## Application Layout

The application is divided into several panels:

1. **Left Panel**: Namespace tree view
2. **Center Panel**: Main content area with tabs:
   - Classes (with sub-tabs: Instances, Properties, Methods)
   - Search
   - Query
   - Watcher
   - Log
3. **Right Panel**: Property Grid for detailed item information

All panels can be resized using the splitters between them. The layout is saved and restored on application restart.

## License

See [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Support

For issues, questions, or feature requests, please open an issue on GitHub.

Do keep in mind that this is a hobby project, and I may not always respond immediately.
