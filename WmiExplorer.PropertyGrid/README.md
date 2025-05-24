# WmiExplorer.PropertyGrid

A reusable WPF PropertyGrid control for .NET, designed for modern appearance, dark mode support, and easy integration into any WPF application.

## Features

- Hierarchical property display
- Custom PropertyTypeProvider support.
- Search/filter support
- Category expansion/collapse
- Customizable help pane
- Virtualization for large property sets
- MVVM-friendly

## Getting Started

### 1. Add Reference

Add a project reference to `WmiExplorer.PropertyGrid` in your WPF application.

### 2. Add Resource Dictionary

In your `App.xaml` (or merged dictionaries), add:

```xml
<ResourceDictionary Source="pack://application:,,,/WmiExplorer.PropertyGrid;component/Styles/PropertyGridTheme.xaml" />
```

### 3. Define Required Brushes/Resources

The control expects the following brushes and color resources to be defined in your application resources (or you can override them):

```xml
<!-- Color resources (define these in your App.xaml or theme dictionary) -->
<Color x:Key="PrimaryBackgroundColor">#FF1E1E1E</Color>
<Color x:Key="PrimaryForegroundColor">#FFF0F0F0</Color>
<Color x:Key="SecondaryBackgroundColor">#FF232323</Color>
<Color x:Key="BorderColor">#FF333333</Color>
<Color x:Key="PrimaryAccentColor">#FF3399FF</Color>
<Color x:Key="SecondaryAccentColor">#FF3399FF</Color>
<Color x:Key="DisabledForegroundColor">#FF888888</Color>

<!-- PropertyGrid brushes (reference the above colors) -->
<SolidColorBrush x:Key="PropertyGridBackgroundBrush" Color="{StaticResource PrimaryBackgroundColor}" />
<SolidColorBrush x:Key="PropertyGridForegroundBrush" Color="{StaticResource PrimaryForegroundColor}" />
<SolidColorBrush x:Key="PropertyGridSecondaryBackgroundBrush" Color="{StaticResource SecondaryBackgroundColor}" />
<SolidColorBrush x:Key="PropertyGridCategoryBackgroundBrush" Color="{StaticResource SecondaryBackgroundColor}" />
<SolidColorBrush x:Key="PropertyGridBorderBrush" Color="{StaticResource BorderColor}" />
<SolidColorBrush x:Key="PropertyGridAccentBrush" Color="{StaticResource PrimaryAccentColor}" />
<SolidColorBrush x:Key="PropertyGridSelectedBackgroundBrush" Color="{StaticResource SecondaryAccentColor}" />
<SolidColorBrush x:Key="PropertyGridDisabledForegroundBrush" Color="{StaticResource DisabledForegroundColor}" />
<SolidColorBrush x:Key="PropertyGridHoverBackgroundBrush" Color="{StaticResource PrimaryAccentColor}" />
```

> **Note:** You can adjust these color values to match your application's theme.

You can copy these into your `App.xaml` or a theme resource dictionary.

### 4. Use the Control in XAML

Add the namespace and use the control:

```xml
xmlns:pg="clr-namespace:WmiExplorer.PropertyGrid;assembly=WmiExplorer.PropertyGrid"

<pg:PropertyGrid SelectedObject="{Binding MyObject}" />
```

#### Example Usage

```xml
<pg:PropertyGrid
    SelectedObject="{Binding SelectedItem}"
    ShowHelpPane="True"
    ShowSearchBox="True"
    IncludeNullValues="False"
    IncludeSystemProperties="True"
    EnableVirtualization="True" />
```

### 5. Optional: Customization

- Override styles or templates in your own resource dictionaries as needed.
- You can bind to properties like `SelectedObject`, `ShowHelpPane`, etc.

## Notes

- The control requires WPF and .NET 8.0 or later.
- All property and category logic is handled internally; you can extend via the provided abstractions.

## License

MIT
