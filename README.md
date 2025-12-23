# WMI Explorer

A modern Windows desktop application for exploring and managing Windows Management Instrumentation (WMI) namespaces, classes, instances, properties, and methods. Built with WPF and .NET 8.0.

> **Note:** This project replaces [wmie2](https://github.com/vinaypamnani/wmie2). Please use this repository for the latest version.

## Features

- **Namespace Browser**: Navigate through WMI namespaces in a tree view
- **Class Explorer**: Browse WMI classes with their properties, methods, and qualifiers
- **Instance Viewer**: View and filter WMI instances with detailed property information
- **Instance Editor**: Edit writable properties of WMI instances through a dedicated property editor dialog
- **WQL Query Editor**: Execute WQL queries with syntax highlighting and result display
- **Event Watcher**: Monitor WMI events in real-time
- **Search**: Search for classes, properties, and methods across namespaces
- **Property Grid**: Detailed property editor with support for WMI-specific types
- **Theme Support**: Dark and Light themes with customizable accent colors
- **Logging**: Built-in logging with configurable log levels
- **Auto-updates**: Check for updates from GitHub releases

## Getting Started

### Prerequisites

- Windows 10/11 or Windows Server 2016+
- .NET 8.0 Runtime (if running published binaries)
- Administrator privileges (recommended for full WMI access)

### Running the Application

1. **From Source**:
   ```bash
   dotnet run --project WmiExplorer
   OR
   dotnet run --project WmiExplorer -- -debug
   ```

2. **Published Binary**:
   - Download the latest release from GitHub
   - Extract and run `WmiExplorer.exe`

3. **Debug Mode** (with console output):
   ```bash
   WmiExplorer.exe -debug
   ```

## Usage Guide

### Connecting to a WMI Namespace

1. In the **Options** panel at the top, enter:
   - **Computer Name**: Local computer (`.`) or remote computer name/IP
   - **Namespace**: WMI namespace path (e.g., `root\cimv2`)
   - **Authentication**: Choose authentication method if connecting to a remote computer
   - **Impersonation**: Select impersonation level (default: Impersonate)

2. Click **Connect** to establish the connection

3. Once connected, the namespace tree will populate in the left panel

## Exploring WMI

Understanding how to navigate and explore WMI objects is essential for efficient use:

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

> 💡Use single-click to quickly navigate and view cached data. Use double-click when you need to explicitly refresh data from WMI.

- Once classes are loaded for a namespace, they are cached in memory. **Single-clicking** the namespace will display the cached classes (indicated by a green status indicator).
- Once instances are loaded for a class, they are cached in memory. **Single-clicking** the class will display the cached instances (indicated by a green status indicator).
- **Double-clicking** always reloads data from WMI, refreshing the cache with the latest information.

### Exploring Namespaces and Classes

1. **Expand a Namespace**: Single click on a namespace in the left tree view to expand it and see child namespaces
2. **Load Classes**: Double click on a namespace to load and display all classes in the **Classes** tab
3. **View Class Details**: Single click on a class to load and view:
   - **Properties**: Class properties with types, qualifiers, and values (shown in Properties sub-tab)
   - **Methods**: Available methods with parameters and return types (shown in Methods sub-tab)
4. **Load Instances**: Double click on a class to load all instances of that class

### Working with Instances

1. After double-clicking a class, navigate to the **Instances** tab (sub-tab under Classes)
2. View all instances of the selected class
3. Use the filter/search box to find specific instances
4. Click on an instance to view its properties in the Property Grid (right panel)
5. To edit instance properties: Right-click on an instance and select **Edit Properties...** (only available if the class has writable properties)

### Executing WQL Queries

1. Navigate to the **Query** tab
2. Enter a WQL query in the editor (syntax highlighting is provided)
3. Select query options:
   - **Direct Read**: Bypass class provider
   - **Use Amended Qualifiers**: Include amended qualifiers in results
4. Click **Execute** or press `F5` to run the query
5. Results appear in a grid below the query editor
6. Click on a result to view details in the Property Grid

**Example Queries**:
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
4. Click **Add Watcher** to start monitoring
5. Events will appear in real-time as they occur
6. Use **Start/Stop** buttons to control individual watchers
7. Click **Remove** to stop and remove a watcher

**Common Event Queries**:
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

## Menu Options

### File Menu
- **Run as Administrator**: Restart the application with elevated privileges
- **Exit**: Close the application (`Ctrl+Q`)

### Options Menu
- **Check for Updates on Startup**: Enable/disable automatic update checks
- **Reset Theme**: Reset theme colors to defaults (preserves accent colors)

### Start Menu
- **Wbemtest.exe**: Launch Windows WMI Tester
- **WmiMgmt.msc**: Launch Windows WMI Management Console
- **DCOMCnfg.exe**: Launch Windows Component Services

### Help Menu
- **About WmiExplorer**: Show application information
- **Check for Updates**: Manually check for updates

### Theme Toggle
- Click the theme name in the menu bar to toggle between Dark and Light themes

## Property Grid

The Property Grid (right panel) displays detailed information about selected items. **The Property Grid is read-only** - it shows property values but does not allow direct editing.

- **WMI Classes**: Shows class qualifiers, properties, and methods
- **WMI Instances**: Shows all properties with their current values
- **WMI Properties**: Shows property qualifiers, type, and value
- **WMI Methods**: Shows method parameters and return types
- **Event Watchers**: Shows watcher configuration and status

**Editing Instance Properties:**
- To edit instance properties, right-click on an instance in the Instances tab and select **Edit Properties...**
- This opens a separate dialog where you can modify writable properties
- The option is only available if the class has writable properties

The Property Grid supports displaying:
- Standard data types (string, int, bool, etc.)
- Arrays and collections
- WMI-specific types (CIM types, datetime, etc.)
- Custom property editors for complex types

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

## Settings

Application data is stored in the following directory: `%APPDATA%\WmiExplorer` (typically `C:\Users\<username>\AppData\Roaming\WmiExplorer`)

The following files are stored in this directory:

- **`settings.json`**: General application settings (connection preferences, window position, etc.)
- **`themes.json`**: Theme configuration and accent colors
- **`Cache.db`**: SQLite database containing cached WMI class metadata (expires after 7 days). The cached data is used for auto-completion in **Query** and **Watcher** tabs.
- **`WmiExplorer.log`**: Application log file (daily rolling, kept for 7 days)

### Caching

The application uses a multi-tier caching strategy to improve performance.

> **NOTE: Only metadata is cached, not actual instance data or property values.**

**Persistent Cache (Disk)**:

- **`Cache.db`**: SQLite database storing WMI class metadata (namespaces, classes, property names and types)
- Cache entries expire after 7 days and are automatically pruned after 45 days
- Used for auto-completion in Query and Watcher tabs

**In-Memory Caches**:
- **WMI Metadata Cache**: Loaded from `Cache.db` into memory on first access, providing fast lookups for namespace and class metadata (structure only, not values)
- **Provider Cache**: Caches WMI provider instances per namespace to avoid repeated queries
- **Query Context Cache**: LRU cache (max 100 entries) for WQL query parsing and completion contexts
- **Log Cache**: In-memory buffer storing up to 1,000 log entries for display in the Log tab

### Status Indicator Colors

The circular status indicator in the status bar uses the following colors to represent application state:

- **Green**: Ready or Success state
- **Blue**: Busy state (operation in progress, with pulsing animation)
- **Orange**: Warning state
- **Tan/Beige**: Partial Success state.
    - For Namespaces, indicates that Namespace metadata is loaded, but Classes are not.
    - For Classes, indicates that Class metadata is loaded, but Instances are not.
- **Red**: Error state
- **Gray**: Unknown state

## Troubleshooting

### Connection Issues

- **"Access Denied"**: Run the application as Administrator
- **"Invalid Namespace"**: Verify the namespace path is correct (e.g., `root\cimv2`)
- **Remote Connection Fails**:
  - Ensure Windows Firewall allows WMI traffic (port 135 and dynamic ports)
  - Verify credentials have WMI access on the remote computer
  - Check DCOM configuration for remote access

### Query Issues

- **"Invalid Query"**: Verify WQL syntax is correct
- **"No Results"**: The query may be valid but return no matching instances
- **Slow Queries**: Some queries can take time on large namespaces; use filters to narrow results

### Event Watcher Issues

- **No Events Received**:
  - Verify the event class exists in the namespace
  - Check that events are actually occurring
  - Some events require administrator privileges

## Building from Source

### Requirements

- .NET 8.0 SDK
- Visual Studio 2022 or VS Code with C# extension

### Build Steps

1. Clone the repository:
   ```bash
   git clone https://github.com/vinaypamnani/wmie.git
   cd wmie
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Build the solution:
   ```bash
   dotnet build
   ```

4. Run the application:
   ```bash
   dotnet run --project WmiExplorer/WmiExplorer.csproj
   ```

## Project Structure

- `WmiExplorer/`: Main application project
  - `Models/`: WMI data models (classes, instances, properties, methods)
  - `Services/`: Core services (WMI, caching, settings, messaging)
  - `Presentation/`: UI components (ViewModels, Views, Controls)
  - `Common/`: Shared utilities and base classes
  - `Integration/`: Third-party integrations (AvalonEdit, PropertyGrid, Serilog)
- `WmiExplorer.PropertyGrid/`: Custom property grid component
- `WmiExplorer.Tests/`: Unit tests

## License

See [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Support

For issues, questions, or feature requests, please open an issue on GitHub.

Do keep in mind that this is a hobby project, and I may not always respond immediately.
