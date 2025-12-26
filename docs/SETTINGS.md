# Settings and Caching

Information about application settings, data storage, and caching strategies.

## Table of Contents

- [Application Data Storage](#application-data-storage)
- [Caching Strategy](#caching-strategy)

## Application Data Storage

Application data is stored in the following directory: `%APPDATA%\WmiExplorer` (typically `C:\Users\<username>\AppData\Roaming\WmiExplorer`)

The following files are stored in this directory:

- **`settings.json`**: General application settings (connection preferences, window position, etc.)
- **`themes.json`**: Theme configuration and accent colors
- **`Cache.db`**: SQLite database containing cached WMI class metadata (expires after 7 days). The cached data is used for auto-completion in **Query** and **Watcher** tabs.
- **`WmiExplorer.log`**: Application log file (daily rolling, retains up to 7 files representing 7 days of logs)

## Caching Strategy

The application uses a multi-tier caching strategy to improve performance.

> **NOTE: Only metadata is cached, not actual instance data or property values.**

### Persistent Cache (Disk)

- **`Cache.db`**: SQLite database storing WMI class metadata (namespaces, classes, property names and types)
- Cache entries expire after a configurable duration (default: 7 days) and are automatically pruned after a configurable interval (default: 45 days)
- Expiration and prune intervals can be configured via `CacheExpirationDays` and `CachePruneIntervalDays` settings in `settings.json`
- Used for auto-completion in Query and Watcher tabs

### In-Memory Caches

- **WMI Metadata Cache**: Loaded from `Cache.db` into memory on first access, providing fast lookups for namespace and class metadata (structure only, not values)
- **Provider Cache**: Caches WMI provider instances per namespace to avoid repeated queries
- **Query Context Cache**: LRU cache (max 100 entries) for WQL query parsing and completion contexts
- **Log Cache**: In-memory buffer storing up to 1,000 log entries for display in the Log tab

### Caching Behavior in the UI

- Once classes are loaded for a namespace, they are cached in memory. **Single-clicking** the namespace will display the cached classes (indicated by a green status indicator).
- Once instances are loaded for a class, they are cached in memory. **Single-clicking** the class will display the cached instances (indicated by a green status indicator).
- **Double-clicking** always reloads data from WMI, refreshing the cache with the latest information.

For more information about navigation and caching behavior, see the [Exploring WMI](README.md#exploring-wmi) section in the main README.

## Configuration Manager Settings

Configuration Manager (ConfigMgr) settings have been moved to the [Configuration Manager Support](CONFIGMGR.md#configuration-manager-settings) documentation page for better organization. All ConfigMgr-related documentation, including settings, is now consolidated in one place.

