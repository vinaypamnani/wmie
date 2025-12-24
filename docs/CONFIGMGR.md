# Configuration Manager (ConfigMgr) Support

WMI Explorer provides specialized support for Microsoft Configuration Manager (ConfigMgr/SCCM) namespaces, making it easier to explore and manage ConfigMgr-specific WMI classes and trigger client actions.

## ConfigMgr Client Namespace Support (`root\CCM`)

When the Configuration Manager client is installed on a system, WMI Explorer automatically detects the `root\CCM` namespace and provides enhanced functionality:

- **Automatic Detection**: The application detects ConfigMgr client installation by checking for the `SMS_Client` class in the `root\CCM` namespace
- **Visual Indicators**: ConfigMgr client namespaces display a special icon in the namespace tree to distinguish them from standard WMI namespaces
- **Client Actions Context Menu**: Right-click on the `root\CCM` namespace to access a **ConfigMgr Client Actions** menu with organized groups of client actions

### Usage

1. Connect to a computer with ConfigMgr client installed
2. Navigate to the `root\CCM` namespace in the namespace tree
3. Right-click on the namespace
4. Select **ConfigMgr Client Actions** from the context menu
5. Choose the desired action from the organized groups
6. The action will be triggered via the `SMS_Client.TriggerSchedule` method

> **Note**: Some client actions may require administrator privileges. If you encounter access denied errors, try running WMI Explorer as administrator.

## ConfigMgr Provider Namespace Support (`root\SMS\SITE_XXX`)

WMI Explorer automatically detects Configuration Manager provider namespaces (pattern: `root\SMS\SITE_XXX` where XXX is alphanumeric) and provides filtering options to manage the large number of classes in these namespaces.

- **Automatic Detection**: Provider namespaces matching the pattern `root\SMS\SITE_XXX` are automatically detected and receive special handling
- **Visual Indicators**: ConfigMgr provider namespaces display a special icon in the namespace tree
- **Class Filtering Options**: The **Classes** tab provides checkboxes to control enumeration of specific class types:
  - **Include Collection Classes**: Controls whether `SMS_CM_RES_COLL_*` classes are enumerated (default: `false`)
    - These classes represent collections in ConfigMgr
    - Excluding them significantly reduces class enumeration time
  - **Include Inventory Classes**: Controls whether inventory classes are enumerated (default: `false`)
    - Includes `SMS_G_System_*`, `SMS_GH_System_*`, and `SMS_GEH_System_*` classes
    - These classes represent hardware and software inventory data
    - Excluding them significantly reduces class enumeration time

### Usage

1. Connect to a Configuration Manager site server
2. Navigate to a provider namespace (e.g., `root\SMS\SITE_ABC123`)
3. The namespace will be automatically detected and show a special icon
4. Navigate to the **Classes** tab
5. Use the checkboxes to include/exclude Collection and Inventory classes as needed
6. Click **Reload** to refresh the class list with the new filter settings

> **Tip**: By default, Collection and Inventory classes are excluded to improve performance. Enable them only when you need to explore these specific class types. The settings are persisted and apply globally to all ConfigMgr provider namespace operations.

## Configuration Manager Settings

WMI Explorer provides specialized settings for working with Microsoft Configuration Manager (ConfigMgr/SCCM) provider namespaces. These settings control which classes are enumerated when exploring ConfigMgr provider namespaces, helping to improve performance by excluding large numbers of collection and inventory classes.

### ConfigMgrSettings Object

Configuration Manager settings are stored in the `ConfigMgrSettings` object within `settings.json`. This object contains two boolean properties that control class enumeration behavior.

### IncludeCollectionClasses

- **Default Value**: `false`
- **Purpose**: Controls whether `SMS_CM_RES_COLL_*` classes are included when enumerating classes in ConfigMgr provider namespaces
- **Location**: Classes tab checkboxes (visible when connected to a ConfigMgr provider namespace)
- **Impact**:
  - When `false` (default): Collection classes (`SMS_CM_RES_COLL_*`) are excluded from class enumeration, significantly reducing the number of classes displayed and improving enumeration performance
  - When `true`: Collection classes are included in enumeration, allowing you to explore ConfigMgr collection-related classes
- **Use Case**: Enable this setting when you need to explore or query ConfigMgr collection classes. Keep it disabled for general exploration to improve performance.

### IncludeInventoryClasses

- **Default Value**: `false`
- **Purpose**: Controls whether inventory classes are included when enumerating classes in ConfigMgr provider namespaces
- **Location**: Classes tab checkboxes (visible when connected to a ConfigMgr provider namespace)
- **Impact**:
  - When `false` (default): Inventory classes are excluded from class enumeration:
    - `SMS_G_System_*` classes (hardware inventory)
    - `SMS_GH_System_*` classes (hardware inventory history)
    - `SMS_GEH_System_*` classes (hardware inventory extended history)
  - This significantly reduces the number of classes displayed and improves enumeration performance
  - When `true`: All inventory classes are included in enumeration, allowing you to explore ConfigMgr inventory data classes
- **Use Case**: Enable this setting when you need to explore or query ConfigMgr inventory classes. Keep it disabled for general exploration to improve performance, as ConfigMgr sites can have thousands of inventory classes.

### Settings Persistence

- **Storage Location**: `%APPDATA%\WmiExplorer\settings.json`
- **JSON Structure**: Settings are stored under the `ConfigMgrSettings` object:
  ```json
  {
    "ConfigMgrSettings": {
      "IncludeCollectionClasses": false,
      "IncludeInventoryClasses": false
    }
  }
  ```
- **Settings Scope**: These are global settings that apply to all ConfigMgr provider namespace operations across all connections
- **Persistence**: Settings are automatically saved when changed and persist across application restarts

### How Settings Are Applied

When you enumerate classes in a ConfigMgr provider namespace (`root\SMS\SITE_XXX`), the application automatically applies these filters to the WQL query used for class enumeration:

- If `IncludeCollectionClasses` is `false`, the query excludes classes matching `SMS_CM_RES_COLL_*`
- If `IncludeInventoryClasses` is `false`, the query excludes classes matching `SMS_G_System*`, `SMS_GH_System*`, and `SMS_GEH_System*`

The filtering is implemented by appending WQL `AND NOT __Class LIKE` clauses to the base class enumeration query. This filtering happens automatically - you don't need to modify your queries manually.

### Changing Settings

1. Connect to a ConfigMgr provider namespace (e.g., `root\SMS\SITE_ABC123`)
2. Navigate to the **Classes** tab
3. Use the checkboxes to enable/disable **Include Collection Classes** and **Include Inventory Classes**
4. Click **Reload** to refresh the class list with the new filter settings
5. Settings are automatically saved and will apply to future class enumerations

> **Note**: The checkboxes are only visible when connected to a ConfigMgr provider namespace. They are hidden when connected to standard WMI namespaces or ConfigMgr client namespaces.

