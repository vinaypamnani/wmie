using System.Management;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WmiExplorer.Presentation.Controls.PropertyGrid.Abstractions;
using WmiExplorer.Presentation.Controls.PropertyGrid.Converters;
using WmiExplorer.Presentation.Controls.PropertyGrid.Providers;

namespace WmiExplorer.Presentation.Controls.PropertyGrid
{
    /// <summary>
    /// A custom PropertyGrid control that mimics Visual Studio's PropertyGrid appearance
    /// with better dark mode support and improved contrast.
    /// </summary>
    public class CustomPropertyGrid : Control
    {
        private TextBlock? _helpTextBlock;
        private TreeView? _propertiesTreeView;
        private TextBox? _searchBox;
        // TreeView for hierarchical display

        private const string _defaultCategory = "Misc";

        #region Commands

        /// <summary>
        /// Whether to enable UI virtualization for better performance with large property sets.
        /// </summary>
        public static readonly DependencyProperty EnableVirtualizationProperty =
            DependencyProperty.Register(
                nameof(EnableVirtualization),
                typeof(bool),
                typeof(CustomPropertyGrid),
                new PropertyMetadata(false, OnVirtualizationChanged));

        /// <summary>
        /// The height of the help pane.
        /// </summary>
        public static readonly DependencyProperty HelpPaneHeightProperty =
            DependencyProperty.Register(
                nameof(HelpPaneHeight),
                typeof(double),
                typeof(CustomPropertyGrid),
                new PropertyMetadata(50.0));

        /// <summary>
        /// Whether to include properties with null values in the property grid.
        /// </summary>
        public static readonly DependencyProperty IncludeNullValuesProperty =
            DependencyProperty.Register(
                nameof(IncludeNullValues),
                typeof(bool),
                typeof(CustomPropertyGrid),
                new PropertyMetadata(false, OnIncludeNullValuesChanged));

        /// <summary>
        /// Whether to include system properties (properties whose names start with "__") in the property grid.
        /// </summary>
        public static readonly DependencyProperty IncludeSystemPropertiesProperty =
            DependencyProperty.Register(
                nameof(IncludeSystemProperties),
                typeof(bool),
                typeof(CustomPropertyGrid),
                new PropertyMetadata(true, OnIncludeSystemPropertiesChanged));

        /// <summary>
        /// The width of the name column.
        /// </summary>
        public static readonly DependencyProperty NameColumnWidthProperty =
            DependencyProperty.Register(
                nameof(NameColumnWidth),
                typeof(double),
                typeof(CustomPropertyGrid),
                new PropertyMetadata(200.0));

        /// <summary>
        /// Gets or sets the currently selected property hierarchy item.
        /// </summary>
        public static readonly DependencyProperty SelectedHierarchyItemProperty =
            DependencyProperty.Register(
                nameof(SelectedHierarchyItem),
                typeof(PropertyHierarchyItem),
                typeof(CustomPropertyGrid),
                new PropertyMetadata(null, OnSelectedHierarchyItemChanged));

        /// <summary>
        /// The object whose properties are displayed in the grid.
        /// </summary>
        public static readonly DependencyProperty SelectedObjectProperty =
            DependencyProperty.Register(
                nameof(SelectedObject),
                typeof(object),
                typeof(CustomPropertyGrid),
                new PropertyMetadata(null, OnSelectedObjectChanged));

        /// <summary>
        /// Whether to show property descriptions as tooltips.
        /// </summary>
        public static readonly DependencyProperty ShowDescriptionByTooltipProperty =
            DependencyProperty.Register(
                nameof(ShowDescriptionByTooltip),
                typeof(bool),
                typeof(CustomPropertyGrid),
                new PropertyMetadata(true));

        /// <summary>
        /// Whether to show the help pane at the bottom.
        /// </summary>
        public static readonly DependencyProperty ShowHelpPaneProperty =
            DependencyProperty.Register(
                nameof(ShowHelpPane),
                typeof(bool),
                typeof(CustomPropertyGrid),
                new PropertyMetadata(true));

        /// <summary>
        /// Whether to show the search box.
        /// </summary>
        public static readonly DependencyProperty ShowSearchBoxProperty =
            DependencyProperty.Register(
                nameof(ShowSearchBox),
                typeof(bool),
                typeof(CustomPropertyGrid),
                new PropertyMetadata(true));

        /// <summary>
        /// Command to toggle a category's expanded state
        /// </summary>
        public static readonly RoutedUICommand ToggleCategoryCommand = new RoutedUICommand(
            "Toggle Category", "ToggleCategory", typeof(CustomPropertyGrid));

        static CustomPropertyGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CustomPropertyGrid),
                new FrameworkPropertyMetadata(typeof(CustomPropertyGrid)));

            // Register the default providers and converters
            RegisterDefaultProvidersAndConverters();

            // Register command bindings
            CommandManager.RegisterClassCommandBinding(
                typeof(CustomPropertyGrid),
                new CommandBinding(ToggleCategoryCommand, OnToggleCategoryExecuted));
        }

        public CustomPropertyGrid()
        {
            Loaded += CustomPropertyGrid_Loaded;
            Unloaded += CustomPropertyGrid_Unloaded;

            // Initialize the ClearSearchCommand
            ClearSearchCommand = new RelayCommand(
                execute: _ => ClearSearch(),
                canExecute: _ => _searchBox != null && !string.IsNullOrEmpty(_searchBox.Text)
            );
        }

        /// <summary>
        /// Command to clear the search box
        /// </summary>
        public ICommand ClearSearchCommand { get; }

        #endregion Commands

        #region Properties

        /// <summary>
        /// Gets or sets whether virtualization is enabled.
        /// </summary>
        public bool EnableVirtualization
        {
            get => (bool)GetValue(EnableVirtualizationProperty);
            set => SetValue(EnableVirtualizationProperty, value);
        }

        /// <summary>
        /// Gets or sets the height of the help pane.
        /// </summary>
        public double HelpPaneHeight
        {
            get => (double)GetValue(HelpPaneHeightProperty);
            set => SetValue(HelpPaneHeightProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to include properties with null values in the property grid.
        /// </summary>
        public bool IncludeNullValues
        {
            get => (bool)GetValue(IncludeNullValuesProperty);
            set => SetValue(IncludeNullValuesProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to include system properties (properties whose names start with "__") in the property grid.
        /// </summary>
        public bool IncludeSystemProperties
        {
            get => (bool)GetValue(IncludeSystemPropertiesProperty);
            set => SetValue(IncludeSystemPropertiesProperty, value);
        }

        /// <summary>
        /// Gets or sets the width of the name column.
        /// </summary>
        public double NameColumnWidth
        {
            get => (double)GetValue(NameColumnWidthProperty);
            set => SetValue(NameColumnWidthProperty, value);
        }

        /// <summary>
        /// Gets or sets the currently selected property hierarchy item.
        /// </summary>
        public PropertyHierarchyItem? SelectedHierarchyItem
        {
            get => (PropertyHierarchyItem?)GetValue(SelectedHierarchyItemProperty);
            set => SetValue(SelectedHierarchyItemProperty, value);
        }

        /// <summary>
        /// Gets or sets the object whose properties are displayed in the grid.
        /// </summary>
        public object? SelectedObject
        {
            get => GetValue(SelectedObjectProperty);
            set => SetValue(SelectedObjectProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show property descriptions as tooltips.
        /// </summary>
        public bool ShowDescriptionByTooltip
        {
            get => (bool)GetValue(ShowDescriptionByTooltipProperty);
            set => SetValue(ShowDescriptionByTooltipProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the help pane at the bottom.
        /// </summary>
        public bool ShowHelpPane
        {
            get => (bool)GetValue(ShowHelpPaneProperty);
            set => SetValue(ShowHelpPaneProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the search box.
        /// </summary>
        public bool ShowSearchBox
        {
            get => (bool)GetValue(ShowSearchBoxProperty);
            set => SetValue(ShowSearchBoxProperty, value);
        }

        #endregion Properties

        private static void OnIncludeNullValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CustomPropertyGrid grid)
            {
                grid.LoadProperties();
            }
        }

        private static void OnIncludeSystemPropertiesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CustomPropertyGrid grid)
            {
                grid.LoadProperties();
            }
        }

        private static void OnSelectedHierarchyItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CustomPropertyGrid grid && grid.SelectedHierarchyItem != null && grid.ShowHelpPane && !string.IsNullOrEmpty(grid.SelectedHierarchyItem.Description))
            {
                grid.AutoAdjustHelpPaneHeight(grid.SelectedHierarchyItem.Description);
            }
        }

        private static void OnSelectedObjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CustomPropertyGrid grid)
            {
                grid.LoadProperties();
            }
        }

        /// <summary>
        /// Handles execution of the ToggleCategoryCommand
        /// </summary>
        private static void OnToggleCategoryExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is string categoryName)
            {
                CategoryExpansionManager.Instance.ToggleCategory(categoryName);
            }
        }

        private static void OnVirtualizationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not CustomPropertyGrid grid)
                return;

            bool enableVirtualization = (bool)e.NewValue;

            // Apply virtualization settings to TreeView
            if (grid._propertiesTreeView != null)
            {
                VirtualizingStackPanel.SetIsVirtualizing(grid._propertiesTreeView, enableVirtualization);
                VirtualizingStackPanel.SetVirtualizationMode(
                    grid._propertiesTreeView,
                    enableVirtualization ? VirtualizationMode.Recycling : VirtualizationMode.Standard);
                ScrollViewer.SetCanContentScroll(grid._propertiesTreeView, enableVirtualization);
            }
        }

        /// <summary>
        /// Register the default providers and converters with the PropertyTypeProviderRegistry
        /// </summary>
        private static void RegisterDefaultProvidersAndConverters()
        {
            // Register providers with the registry (order matters - more specific first)
            var registry = PropertyTypeProviderRegistry.Instance;            

            // Default provider handles all other types
            registry.RegisterProvider(new DefaultPropertyTypeProvider());

            // Register value converters (order matters - converters with higher priority are tried first)            
            registry.RegisterConverter(new DefaultPropertyValueConverter());
        }

        /// <summary>
        /// Automatically adjusts the help pane height based on the content
        /// </summary>
        private void AutoAdjustHelpPaneHeight(string description)
        {
            // Assume 80 chars per line for wrapping, 20px per line, 16px padding
            int lineCount = description.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            int wrapLines = description.Length / 80;
            int totalLines = 1 + lineCount + wrapLines; // 1 for DisplayName
            double calculatedHeight = Math.Min(300, Math.Max(60, totalLines * 20 + 16));
            if (Math.Abs(HelpPaneHeight - calculatedHeight) > 20)
            {
                HelpPaneHeight = calculatedHeight;
            }
        }

        /// <summary>
        /// Clears the search box text
        /// </summary>
        private void ClearSearch()
        {
            if (_searchBox != null)
            {
                _searchBox.Clear();
                _searchBox.Focus();
            }
        }

        private void CustomPropertyGrid_Loaded(object sender, RoutedEventArgs e)
        {
            // Load properties if an object is selected
            if (SelectedObject != null)
            {
                LoadProperties();
            }
        }

        private void CustomPropertyGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe from event handlers to prevent memory leaks
            if (_searchBox != null)
            {
                _searchBox.TextChanged -= SearchBox_TextChanged;
            }

            if (_propertiesTreeView != null)
            {
                _propertiesTreeView.SelectedItemChanged -= TreeView_SelectedItemChanged;
                _propertiesTreeView.PreviewMouseWheel -= TreeView_PreviewMouseWheel;
            }
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            // Get the main grid that contains our help pane
            if (Template.FindName("PART_MainGrid", this) is Grid mainGrid)
            {
                // The help pane is in the last row (index 3)
                if (mainGrid.RowDefinitions.Count >= 4)
                {
                    // Update our HelpPaneHeight property with the actual height from the grid
                    GridLength gridLength = mainGrid.RowDefinitions[3].Height;
                    if (gridLength.GridUnitType == GridUnitType.Pixel)
                    {
                        HelpPaneHeight = gridLength.Value;
                    }
                }
            }
        }

        /// <summary>
        /// Load properties of the selected object based on the current view mode
        /// </summary>
        private void LoadProperties()
        {
            if (SelectedObject == null)
                return;

            if (_propertiesTreeView != null)
            {
                LoadPropertiesForTreeView();
            }
        }

        /// <summary>
        /// Load properties of the selected object using the hierarchical tree view model.
        /// </summary>
        private void LoadPropertiesForTreeView()
        {
            if (SelectedObject == null || _propertiesTreeView == null)
                return;

            try
            {
                var registry = PropertyTypeProviderRegistry.Instance;
                var descriptors = registry.GetProperties(SelectedObject);

                // Filter out [Browsable(false)] properties
                descriptors = descriptors.Where(p =>
                {
                    if (p is DefaultPropertyDescriptor rpd)
                    {
                        var propInfo = rpd.PropertyInfo;
                        var browsable = propInfo.GetCustomAttribute<System.ComponentModel.BrowsableAttribute>();
                        return browsable == null || browsable.Browsable;
                    }
                    // For non-reflection descriptors, assume browsable
                    return true;
                }).ToList();

                // Filter out system properties at the top level if IncludeSystemProperties is false
                if (!IncludeSystemProperties)
                {
                    descriptors = descriptors.Where(p => !(p.Name?.StartsWith("__") ?? false)).ToList();
                }

                var categoryGroups = descriptors
                    .GroupBy(p => string.IsNullOrEmpty(p.Category) ? _defaultCategory : p.Category)
                    .OrderBy(g => string.Equals(g.Key, _defaultCategory, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ThenBy(g => g.Key)
                    .ToList();

                var rootItems = new List<PropertyHierarchyItem>();

                foreach (var category in categoryGroups)
                {
                    string categoryName = category.Key;
                    var categoryItem = new PropertyCategoryItem(categoryName);
                    rootItems.Add(categoryItem);

                    // Only filter out nulls for category 'Properties' if IncludeNullValues is false
                    IEnumerable<WmiExplorer.Presentation.Controls.PropertyGrid.Abstractions.IPropertyDescriptor> filteredProperties = category;
                    if (string.Equals(categoryName, "Properties", StringComparison.OrdinalIgnoreCase) && !IncludeNullValues)
                    {
                        filteredProperties = filteredProperties.Where(p => p.Value != null);
                    }
                    var filteredList = filteredProperties.OrderBy(p => p.DisplayName).ToList();
                    foreach (var descriptor in filteredList)
                    {
                        // Pass the category name to the PropertyHierarchyItem for child filtering
                        var propertyItem = new PropertyHierarchyItem(descriptor, 1, IncludeSystemProperties, 
                            string.Equals(categoryName, "Properties", StringComparison.OrdinalIgnoreCase) ? IncludeNullValues : true);
                        categoryItem.Children.Add(propertyItem);
                    }
                }

                _propertiesTreeView.ItemsSource = rootItems;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading hierarchical properties: {ex.Message}");

                var errorCategory = new PropertyCategoryItem("Errors");
                var errorProperty = new PropertyHierarchyItem
                {
                    Name = "Error",
                    DisplayName = "Error",
                    Value = $"Error loading properties: {ex.Message}",
                    PropertyType = typeof(string),
                    Category = "Errors",
                    IsReadOnly = true,
                    Description = "Error occurred while loading properties",
                    Level = 1
                };

                errorCategory.Children.Add(errorProperty);
                _propertiesTreeView.ItemsSource = new[] { errorCategory };
            }
        }

        /// <summary>
        /// Handles search box text changes for TreeView mode
        /// </summary>
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_propertiesTreeView != null)
            {
                UpdateTreeViewSearch();
            }
        }

        // Handle mouse wheel events to properly scroll the TreeView
        private void TreeView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Find the parent ScrollViewer for the TreeView
            if (Template.FindName("PART_TreeScrollViewer", this) is ScrollViewer treeScrollViewer)
            {
                if (e.Delta < 0)
                {
                    treeScrollViewer.LineDown();
                }
                else
                {
                    treeScrollViewer.LineUp();
                }

                // Mark the event as handled to prevent it from being routed to parent controls
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles the SelectedItemChanged event of the TreeView.
        /// </summary>
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is PropertyHierarchyItem propertyItem && !propertyItem.IsCategory)
            {
                PropertyGridSelectionManager.Instance.SelectedItem = propertyItem;
                SelectedHierarchyItem = propertyItem;
            }
        }

        /// <summary>
        /// Updates the TreeView to filter items based on the search text.
        /// </summary>
        private void UpdateTreeViewSearch()
        {
            if (_propertiesTreeView == null || !(_propertiesTreeView.ItemsSource is IEnumerable<PropertyHierarchyItem> items))
                return;

            string? searchText = _searchBox?.Text?.Trim();
            bool hasSearchText = !string.IsNullOrEmpty(searchText);

            if (!hasSearchText)
            {
                foreach (var category in items.OfType<PropertyCategoryItem>())
                {
                    category.IsExpanded = CategoryExpansionManager.Instance.IsCategoryExpanded(category.Name);
                    category.Visibility = Visibility.Visible;
                    category.ResetVisibilityRecursive();
                }
                return;
            }

            foreach (var category in items.OfType<PropertyCategoryItem>())
            {
                bool hasMatchInCategory = false;
                foreach (var property in category.Children)
                {
                    bool isMatch = (property.Name?.IndexOf(searchText!, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                   (property.DisplayName?.IndexOf(searchText!, StringComparison.OrdinalIgnoreCase) >= 0) ||
                                   (property.Description?.IndexOf(searchText!, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (isMatch)
                    {
                        property.Visibility = Visibility.Visible;
                        hasMatchInCategory = true;
                    }
                    else
                    {
                        property.Visibility = Visibility.Collapsed;
                    }
                }
                if (hasMatchInCategory)
                {
                    category.IsExpanded = true;
                    category.Visibility = Visibility.Visible;
                }
                else
                {
                    category.IsExpanded = false;
                    category.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Collapses all categories in the TreeView.
        /// </summary>
        public void CollapseAllCategories()
        {
            if (_propertiesTreeView != null && _propertiesTreeView.ItemsSource is IEnumerable<PropertyHierarchyItem> items)
            {
                foreach (var category in items.OfType<PropertyCategoryItem>())
                {
                    category.IsExpanded = false;
                }
            }
        }

        /// <summary>
        /// Expands all categories in the TreeView.
        /// </summary>
        public void ExpandAllCategories()
        {
            if (_propertiesTreeView != null && _propertiesTreeView.ItemsSource is IEnumerable<PropertyHierarchyItem> items)
            {
                foreach (var category in items.OfType<PropertyCategoryItem>())
                {
                    category.IsExpanded = true;
                }
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Detach previous event handlers if necessary
            if (_searchBox != null)
            {
                _searchBox.TextChanged -= SearchBox_TextChanged;
            }
            if (_propertiesTreeView != null)
            {
                _propertiesTreeView.SelectedItemChanged -= TreeView_SelectedItemChanged;
                _propertiesTreeView.PreviewMouseWheel -= TreeView_PreviewMouseWheel;
            }

            _searchBox = GetTemplateChild("PART_SearchBox") as TextBox;
            _helpTextBlock = GetTemplateChild("PART_HelpText") as TextBlock;
            _propertiesTreeView = GetTemplateChild("PART_PropertiesTree") as TreeView;
            ScrollViewer? treeScrollViewer = GetTemplateChild("PART_TreeScrollViewer") as ScrollViewer;

            if (_searchBox != null)
            {
                _searchBox.TextChanged += SearchBox_TextChanged;
            }

            if (_propertiesTreeView != null)
            {
                _propertiesTreeView.SelectedItemChanged += TreeView_SelectedItemChanged;
                _propertiesTreeView.PreviewMouseWheel += TreeView_PreviewMouseWheel;
                VirtualizingStackPanel.SetIsVirtualizing(_propertiesTreeView, EnableVirtualization);
                VirtualizingStackPanel.SetVirtualizationMode(
                    _propertiesTreeView,
                    EnableVirtualization ? VirtualizationMode.Recycling : VirtualizationMode.Standard);
                ScrollViewer.SetCanContentScroll(_propertiesTreeView, EnableVirtualization);
            }

            if (ShowHelpPane && Template != null)
            {
                if (Template.FindName("PART_GridSplitter", this) is GridSplitter splitter)
                {
                    splitter.DragCompleted += GridSplitter_DragCompleted;
                }
            }

            LoadProperties();
        }
    }
}