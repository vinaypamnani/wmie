# ColorPicker Control

This directory contains a custom ColorPicker control implementation for the WmiExplorer application. It was created to replace the dependency on the Xceed Extended.Wpf.Toolkit while maintaining similar functionality and providing a modern, theme-aware UI.

## Features

- Full HSV (Hue, Saturation, Value) color selection with 10 decimal places of precision
- RGB (Red, Green, Blue) color selection via sliders and text inputs
- Hex color code input and display
- Color swatches palette with standard and available colors
- Recent colors memory (never empty, always includes the current color)
- Optional alpha channel support
- Responsive, accessible, and visually consistent layout
- Two-way binding for selected color value
- Advanced section with grouped and aligned RGB/HSV/Preview/Hex inputs
- Modern WPF theming and resource usage

## Controls

The ColorPicker implementation consists of several components:

1. **ColorPicker** - The main control that can be used in XAML
2. **HueSlider** - A custom slider control for selecting hue values
3. **SaturationValueCanvas** - A 2D canvas for selecting saturation and value
4. **RelayCommand** - Helper class for handling commands in the ColorPicker

## Usage

```xaml
<colorpicker:ColorPicker
    Width="80"
    ShowAlphaChannel="False"
    ShowRecentColors="True"
    SelectedColor="{Binding YourColorProperty, Mode=TwoWay}" />
```

## Properties

- **SelectedColor** (`Color`): The currently selected color
- **ShowAlphaChannel** (`bool`): Whether to show alpha channel controls (default: false)
- **ShowRecentColors** (`bool`): Whether to show recent colors (default: true)
- **RecentColors** (`ObservableCollection<SolidColorBrush>`): List of recent colors, always includes the current color
- **StandardColors** (`ObservableCollection<SolidColorBrush>`): List of standard color swatches
- **AvailableColors** (`ObservableCollection<SolidColorBrush>`): List of all available colors except Transparent
- **Red, Green, Blue** (`byte`): RGB components (0-255)
- **Hue, Saturation, Value** (`double`): HSV components (Hue: 0-360, Sat/Val: 0-100, rounded to 10 decimals)
- **HexColor** (`string`): Hex color code (e.g. #FF112233)
- **Alpha** (`byte`): Alpha channel (0-255, if enabled)

## Implementation Notes

### Layout and Usability

- The advanced section is a 3x3 grid with each label/input pair grouped in a horizontal StackPanel for alignment.
- The Preview label and color preview rectangle are grouped in a vertical StackPanel for visual clarity.
- All controls use theme resources for consistent appearance.
- The recent colors list is initialized with the selected color and never empty.

### Binding Update Mechanism

The ColorPicker control ensures proper binding updates by creating a new Color instance when confirming selections:

```csharp
// Inside OKButton_Click
Color currentColor = SelectedColor;
SelectedColor = Color.FromArgb(
    currentColor.A,
    currentColor.R,
    currentColor.G,
    currentColor.B);
```

This method ensures that:
1. Two-way bindings are properly updated
2. PropertyChanged notifications are raised
3. Dependent UI elements refresh correctly

### Temporary Color Storage

The control stores the original color when opening the popup, allowing cancel operations to restore the previous color:

```csharp
if (_colorPickerPopup.IsOpen)
{
    _tempColor = SelectedColor;
}
```

### Recent Colors Logic

- The currently selected color is always added to the recent colors list on initialization.
- Selecting a color or confirming a selection adds it to the top of the recent colors list (max 16).
- Duplicate colors are not added.

## Example

```csharp
// Code-behind example
var picker = new ColorPicker();
picker.SelectedColor = Colors.Red;
picker.ShowAlphaChannel = true;
```

## Dependencies

- .NET 8.0
- WPF (Windows Presentation Foundation)

## Notes

This implementation is designed to match and improve upon the appearance and functionality of the Xceed ColorPicker control, while being fully integrated with the WmiExplorer theming system and codebase. The layout and logic have been refactored for clarity, maintainability, and usability.
