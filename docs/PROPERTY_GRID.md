# Property Grid

The Property Grid (right panel) displays detailed information about selected items. **The Property Grid is read-only** - it shows property values but does not allow direct editing.

## Table of Contents

- [Overview](#overview)
- [What It Displays](#what-it-displays)
- [Editing Instance Properties](#editing-instance-properties)
- [Supported Data Types](#supported-data-types)

## Overview

The Property Grid provides a detailed view of WMI objects, showing all properties, qualifiers, and metadata in an organized, hierarchical format. It automatically updates when you select different items in the application.

## What It Displays

The Property Grid shows different information depending on what is selected:

- **WMI Classes**: Shows class qualifiers, properties, and methods
- **WMI Instances**: Shows all properties with their current values
- **WMI Properties**: Shows property qualifiers, type, and value
- **WMI Methods**: Shows method parameters and return types
- **Event Watchers**: Shows watcher configuration and status

## Editing Instance Properties

To edit instance properties:

1. Right-click on an instance in the Instances tab
2. Select **Edit Properties...**
3. This opens a separate dialog where you can modify writable properties
4. The option is only available if the class has writable properties

> **Note**: The Property Grid itself is read-only. Editing must be done through the dedicated Edit Properties dialog.

## Supported Data Types

The Property Grid supports displaying:

- Standard data types (string, int, bool, etc.)
- Arrays and collections
- WMI-specific types (CIM types, datetime, etc.)
- Custom property editors for complex types

The Property Grid uses specialized editors for WMI-specific types to provide a better user experience when viewing complex data structures.

