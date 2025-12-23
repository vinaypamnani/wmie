# Command-Line Options

WMI Explorer supports several command-line arguments for automation and debugging.

## Table of Contents

- [`-debug`](#-debug)
- [`-computername`](#-computername)
- [`-username`](#-username)
- [Examples](#examples)

## `-debug`

Enables debug logging to the console window. Useful for troubleshooting and development.

**Usage:**
```bash
WmiExplorer.exe -debug
```

## `-computername <name>`

Specifies the computer name to connect to automatically on startup. The application will connect immediately using the current user's credentials.

**Usage:**
```bash
# Connect to localhost
WmiExplorer.exe -computername localhost

# Connect to a remote computer
WmiExplorer.exe -computername SERVER01

# Alternative format (with equals sign)
WmiExplorer.exe -computername=SERVER01
```

## `-username <name>`

Specifies the username for automatic connection. When combined with `-computername`, the application will:
- **Connect immediately** if the username matches the current Windows user (no password required)
- **Show connection dialog** if the username is different from the current user (password entry required)

**Usage:**
```bash
# Connect with current user credentials (same as -computername alone)
WmiExplorer.exe -computername SERVER01 -username DOMAIN\CurrentUser

# Connect with different user (will prompt for password)
WmiExplorer.exe -computername SERVER01 -username DOMAIN\OtherUser

# Alternative format (with equals sign)
WmiExplorer.exe -computername=SERVER01 -username=DOMAIN\OtherUser
```

**Notes:**
- If only `-computername` is provided, the application connects immediately using current Windows credentials
- If both `-computername` and `-username` are provided and the username matches the current user, connection happens immediately without a password
- If the username differs from the current user, the connection options dialog appears with the computer name and username pre-filled, allowing you to enter the password securely
- WMI does not allow using user credentials for local connections (localhost, `.`, or the current machine name)

## Examples

```bash
# Connect to localhost with current user
WmiExplorer.exe -computername localhost

# Connect to remote server with current user
WmiExplorer.exe -computername SERVER01

# Connect to remote server with different user (will prompt for password)
WmiExplorer.exe -computername SERVER01 -username DOMAIN\AdminUser

# Enable debug logging while auto-connecting
WmiExplorer.exe -debug -computername SERVER01 -username DOMAIN\User
```

