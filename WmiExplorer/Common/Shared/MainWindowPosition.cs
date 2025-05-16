using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows;

namespace WmiExplorer.Common.Shared
{
    /// <summary>
    /// Represents the position and size of the main window
    /// </summary>
    public class MainWindowPosition : INotifyPropertyChanged
    {
        // Constants
        public const double DEFAULT_COLUMN_WIDTH = 300;
        public const double DEFAULT_HEIGHT = 960;
        public const double DEFAULT_LEFT = 100;
        public const double DEFAULT_TOP = 100;
        public const double DEFAULT_WIDTH = 1280;
        public const double FLUCTUATION_THRESHOLD = 1;
        public const double MIN_COLUMN_WIDTH = 30;

        // Private fields
        private double _classesColumnWidth = DEFAULT_COLUMN_WIDTH;
        private double _namespaceColumnWidth = DEFAULT_COLUMN_WIDTH;
        private double _propertyGridColumnWidth = DEFAULT_COLUMN_WIDTH;        
        private double _height = DEFAULT_HEIGHT;
        private double _width = DEFAULT_WIDTH;
        private double _left = DEFAULT_LEFT;
        private double _top = DEFAULT_TOP;
        private bool _isClassesExpanded = true;
        private bool _isNamespacesExpanded = true;
        private bool _isPropertyGridExpanded = true;
        private bool _isWindowMaximized = false;

        // Events
        public event PropertyChangedEventHandler? PropertyChanged;

        // Properties: Window geometry
        /// <summary>
        /// Gets or sets the window's left position
        /// </summary>
        public double Left
        {
            get => _left;
            set => SetProperty(ref _left, value);
        }

        /// <summary>
        /// Gets or sets the window's top position
        /// </summary>
        public double Top
        {
            get => _top;
            set => SetProperty(ref _top, value);
        }

        /// <summary>
        /// Gets or sets the window's width
        /// </summary>
        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        /// <summary>
        /// Gets or sets the window's height
        /// </summary>
        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        /// <summary>
        /// </summary>
        public bool IsWindowMaximized
        {
            get => _isWindowMaximized;
            set => SetProperty(ref _isWindowMaximized, value);
        }

        // Properties: Column widths (raw)
        /// <summary>
        /// Gets or sets the width of the classes column when expanded
        /// </summary>
        public double ClassesColumnWidth
        {
            get => _classesColumnWidth;
            set
            {
                if (value > MIN_COLUMN_WIDTH && SetProperty(ref _classesColumnWidth, value))
                {
                    OnPropertyChanged(nameof(ClassesColumnGridLength));
                    System.Diagnostics.Debug.WriteLine($"Saved classes column width: {value}");
                }
            }
        }

        /// <summary>
        /// Gets or sets the width of the namespace column when expanded
        /// </summary>
        public double NamespaceColumnWidth
        {
            get => _namespaceColumnWidth;
            set
            {
                if (value > MIN_COLUMN_WIDTH && SetProperty(ref _namespaceColumnWidth, value))
                {
                    OnPropertyChanged(nameof(NamespaceColumnGridLength));
                    System.Diagnostics.Debug.WriteLine($"Saved namespace column width: {value}");
                }
            }
        }

        /// <summary>
        /// Gets or sets the width of the property grid column when expanded
        /// </summary>
        public double PropertyGridColumnWidth
        {
            get => _propertyGridColumnWidth;
            set
            {
                if (value > MIN_COLUMN_WIDTH && SetProperty(ref _propertyGridColumnWidth, value))
                {
                    OnPropertyChanged(nameof(PropertyGridColumnGridLength));
                    System.Diagnostics.Debug.WriteLine($"Saved property grid column width: {value}");
                }
            }
        }

        // Properties: Column widths (UI-friendly)
        /// <summary>
        /// Gets a UI-friendly GridLength for the classes column width based on expander state
        /// </summary>
        [JsonIgnore]
        public GridLength ClassesColumnGridLength
        {
            get => GetColumnWidth(IsClassesExpanded, _classesColumnWidth);
            set
            {
                if (value.GridUnitType == GridUnitType.Pixel && value.Value > MIN_COLUMN_WIDTH &&
                    IsClassesExpanded &&
                    Math.Abs(value.Value - _classesColumnWidth) > FLUCTUATION_THRESHOLD) // Avoid minor fluctuations
                {
                    ClassesColumnWidth = value.Value;
                }
            }
        }

        /// <summary>
        /// Gets a UI-friendly GridLength for the namespaces column width based on expander state
        /// </summary>
        [JsonIgnore]
        public GridLength NamespaceColumnGridLength
        {
            get => GetColumnWidth(IsNamespacesExpanded, _namespaceColumnWidth);
            set
            {
                if (value.GridUnitType == GridUnitType.Pixel && value.Value > MIN_COLUMN_WIDTH &&
                    IsNamespacesExpanded &&
                    Math.Abs(value.Value - _namespaceColumnWidth) > FLUCTUATION_THRESHOLD) // Avoid minor fluctuations
                {
                    NamespaceColumnWidth = value.Value;
                }
            }
        }

        /// <summary>
        /// Gets a UI-friendly GridLength for the property grid column width based on expander state
        /// </summary>
        [JsonIgnore]
        public GridLength PropertyGridColumnGridLength
        {
            get => GetColumnWidth(IsPropertyGridExpanded, _propertyGridColumnWidth);
            set
            {
                if (value.GridUnitType == GridUnitType.Pixel && value.Value > MIN_COLUMN_WIDTH &&
                    IsPropertyGridExpanded &&
                    Math.Abs(value.Value - _propertyGridColumnWidth) > FLUCTUATION_THRESHOLD) // Avoid minor fluctuations
                {
                    PropertyGridColumnWidth = value.Value;
                }
            }
        }

        // Properties: Expansion states
        /// <summary>
        /// Gets or sets the Classes expander's expansion state
        /// </summary>
        public bool IsClassesExpanded
        {
            get => _isClassesExpanded;
            set
            {
                if (_isClassesExpanded != value)
                {
                    SetProperty(ref _isClassesExpanded, value);
                    OnPropertyChanged(nameof(ClassesColumnGridLength));
                }
            }
        }

        /// <summary>
        /// Gets or sets the Namespace expander's expansion state
        /// </summary>
        public bool IsNamespacesExpanded
        {
            get => _isNamespacesExpanded;
            set
            {
                if (_isNamespacesExpanded != value)
                {
                    SetProperty(ref _isNamespacesExpanded, value);
                    OnPropertyChanged(nameof(NamespaceColumnGridLength));
                }
            }
        }

        /// <summary>
        /// Gets or sets the Property Grid expander's expansion state
        /// </summary>
        public bool IsPropertyGridExpanded
        {
            get => _isPropertyGridExpanded;
            set
            {
                if (_isPropertyGridExpanded != value)
                {
                    SetProperty(ref _isPropertyGridExpanded, value);
                    OnPropertyChanged(nameof(PropertyGridColumnGridLength));
                }
            }
        }

        /// <summary>
        /// Updates the position with new values (only window geometry)
        /// </summary>
        public void UpdatePosition(
            double left,
            double top,
            double width,
            double height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        // Protected Methods
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Private Methods
        // Returns a GridLength for a column, using Pixel or Auto
        private GridLength GetColumnWidth(bool isExpanded, double savedWidth, GridUnitType unitType = GridUnitType.Pixel)
        {
            if (isExpanded)
            {
                // Use saved width (or safe default) when expanded
                double width = savedWidth >= MIN_COLUMN_WIDTH ? savedWidth : DEFAULT_COLUMN_WIDTH;
                return new GridLength(width, unitType);
            }
            else
            {
                // When collapsed, return Auto width
                // The expander header width is controlled by the fixed Border width in XAML
                return new GridLength(0, GridUnitType.Auto);
            }
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}