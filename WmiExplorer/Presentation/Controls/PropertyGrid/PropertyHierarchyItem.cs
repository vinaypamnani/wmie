using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using WmiExplorer.Presentation.Controls.PropertyGrid.Abstractions;

namespace WmiExplorer.Presentation.Controls.PropertyGrid
{
    /// <summary>
    /// Represents a property item in the hierarchical property grid.
    /// This is the base class for all items in the tree structure.
    /// </summary>
    public class PropertyHierarchyItem : DependencyObject, INotifyPropertyChanged
    {
        private string _category = string.Empty;
        private string _description = string.Empty;
        private string _displayName = string.Empty;
        private bool _isExpanded;
        private bool _isReadOnly;
        private bool _isSelected;
        private string _name = string.Empty;
        private Type _propertyType = typeof(object);
        private object? _value;
        private Visibility _visibility = Visibility.Visible;
        private readonly bool _includeSystemProperties = true;
        private readonly bool _includeNullValues = true;

        /// <summary>
        /// Creates a new instance of PropertyHierarchyItem.
        /// </summary>
        public PropertyHierarchyItem()
        {
        }

        /// <summary>
        /// Creates a new instance of PropertyHierarchyItem from a property descriptor.
        /// </summary>
        public PropertyHierarchyItem(IPropertyDescriptor descriptor, int level = 0, bool includeSystemProperties = true, bool includeNullValues = true)
        {
            PropertyDescriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

            // Initialize from property descriptor
            Name = descriptor.Name;
            DisplayName = descriptor.DisplayName;
            Value = descriptor.Value;
            PropertyType = descriptor.PropertyType ?? typeof(object);
            IsReadOnly = descriptor.IsReadOnly;
            Category = descriptor.Category;
            Description = descriptor.Description;
            Level = level;
            _includeSystemProperties = includeSystemProperties;
            _includeNullValues = includeNullValues;

            // Check if this property is expandable
            HasItems = PropertyTypeProviderRegistry.Instance.IsExpandable(Value, PropertyType);
        }

        /// <summary>
        /// Event that is raised when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets or sets the category of the property.
        /// </summary>
        public string Category
        {
            get => _category;
            set
            {
                if (_category != value)
                {
                    _category = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets child property items.
        /// </summary>
        public ObservableCollection<PropertyHierarchyItem> Children { get; } = new ObservableCollection<PropertyHierarchyItem>();

        /// <summary>
        /// Gets or sets the description of the property.
        /// </summary>
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the display name of the property.
        /// </summary>
        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName != value)
                {
                    _displayName = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets the formatted value as a string.
        /// </summary>
        public string FormattedValue
        {
            get
            {
                // Use the PropertyTypeProviderRegistry to format the value
                return PropertyTypeProviderRegistry.Instance.FormatValue(Value, PropertyType);
            }
        }

        /// Gets or sets whether this item has child items.
        /// </summary>
        public bool HasItems { get; set; }

        /// <summary>
        /// Gets or sets whether this item represents a category.
        /// </summary>
        public bool IsCategory { get; set; }

        /// <summary>
        /// Gets whether this item is expandable in the TreeView.
        /// This is used by the TreeView to determine whether to show the expander arrow.
        /// </summary>
        public bool IsExpandable
        {
            get
            {
                // Always expandable if it already has children loaded
                if (Children.Count > 0)
                    return true;

                // Otherwise check if it has items that can be loaded
                return HasItems;
            }
        }

        /// <summary>
        /// Gets or sets whether the property is expanded in the UI.
        /// </summary>
        public virtual bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();

                    // When expanded, ensure child items are loaded
                    if (_isExpanded && HasItems && Children.Count == 0)
                    {
                        LoadChildren(_includeSystemProperties, _includeNullValues);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the property is read-only.
        /// </summary>
        public bool IsReadOnly
        {
            get => _isReadOnly;
            set
            {
                if (_isReadOnly != value)
                {
                    _isReadOnly = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the property is selected in the UI.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();

                    // When this item is selected, notify the grid
                    if (_isSelected)
                    {
                        PropertyGridSelectionManager.Instance.SelectedItem = this;
                    }
                }
            }
        }

        /// <summary>
        /// <summary>
        /// Gets or sets the level in the hierarchy (used for indentation).
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Gets or sets the name of the property.
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the property descriptor for this item.
        /// </summary>
        public IPropertyDescriptor? PropertyDescriptor { get; set; }

        /// <summary>
        /// Gets or sets the type of the property.
        /// </summary>
        public Type PropertyType
        {
            get => _propertyType;
            set
            {
                if (_propertyType != value)
                {
                    _propertyType = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the value of the property.
        /// </summary>
        public object? Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedValue));
                    OnPropertyValueChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the visibility of this item in the UI.
        /// </summary>
        public Visibility Visibility
        {
            get => _visibility;
            set
            {
                if (_visibility != value)
                {
                    _visibility = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Loads child items when the property is expandable.
        /// </summary>
        protected virtual void LoadChildren(bool includeSystemProperties = true, bool includeNullValues = true)
        {
            if (Value == null || !HasItems)
                return;

            try
            {
                var registry = PropertyTypeProviderRegistry.Instance;

                // Get child properties from the registry
                var childDescriptors = registry.GetChildItems(Value, Name, Category);

                foreach (var descriptor in childDescriptors)
                {
                    if (!includeSystemProperties && descriptor.Name != null && descriptor.Name.StartsWith("__"))
                        continue;
                    if (!includeNullValues && descriptor.Value == null)
                        continue;
                    // Create child item with incremented level
                    var childItem = new PropertyHierarchyItem(descriptor, Level + 1, includeSystemProperties, includeNullValues);
                    // Add to children collection
                    Children.Add(childItem);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading child properties: {ex.Message}");
            }
        }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Called when the property value changes.
        /// </summary>
        protected virtual void OnPropertyValueChanged()
        {
            // Update the value in the underlying property descriptor if possible
            PropertyDescriptor?.SetValue(Value);
        }

        /// <summary>
        /// Resets visibility for this item and all its children recursively.
        /// Used when clearing a search filter.
        /// </summary>
        public virtual void ResetVisibilityRecursive()
        {
            // Reset this item's visibility
            Visibility = Visibility.Visible;

            // Reset all children's visibility recursively
            foreach (var child in Children)
            {
                child.ResetVisibilityRecursive();
            }
        }
    }
}