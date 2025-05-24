using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WmiExplorer.PropertyGrid.Abstractions;
using WmiExplorer.PropertyGrid.Converters;
using WmiExplorer.PropertyGrid.Providers;

namespace WmiExplorer.PropertyGrid;

/// <summary>
/// A custom PropertyGrid control that mimics Visual Studio's PropertyGrid appearance
/// with better dark mode support and improved contrast.
/// </summary>
public class CustomPropertyGrid : Control
{
    // TreeView for hierarchical display

    private const string _defaultCategory = "Misc";

    /// <summary>
    /// Command to copy text to clipboard
    /// </summary>
    public static readonly RoutedUICommand CopyToClipboardCommand = new RoutedUICommand(
        "Copy To Clipboard", "CopyToClipboard", typeof(CustomPropertyGrid));

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
    /// Command to copy text from the help pane
    /// </summary>
    public static readonly RoutedUICommand HelpPaneCopyCommand = new RoutedUICommand(
        string.Empty, "HelpPaneCopy", typeof(CustomPropertyGrid));

    /// <summary>
    /// The height of the help pane.
    /// </summary>
    public static readonly DependencyProperty HelpPaneHeightProperty =
        DependencyProperty.Register(
            nameof(HelpPaneHeight),
            typeof(double),
            typeof(CustomPropertyGrid),
            new PropertyMetadata(90.0));

    /// <summary>
    /// Command to select all text in the help pane
    /// </summary>
    public static readonly RoutedUICommand HelpPaneSelectAllCommand = new RoutedUICommand(
        string.Empty, "HelpPaneSelectAll", typeof(CustomPropertyGrid));

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
    /// The current search text for filtering properties.
    /// </summary>
    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(
            nameof(SearchText),
            typeof(string),
            typeof(CustomPropertyGrid),
            new PropertyMetadata(string.Empty, OnSearchTextChanged));

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

    private TextBlock? _helpTextBlock;
    private TreeView? _propertiesTreeView;
    private TextBox? _searchBox;

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
        CommandManager.RegisterClassCommandBinding(
            typeof(CustomPropertyGrid),
            new CommandBinding(CopyToClipboardCommand, OnCopyToClipboardExecuted, OnCopyToClipboardCanExecute));
        CommandManager.RegisterClassCommandBinding(
            typeof(CustomPropertyGrid),
            new CommandBinding(HelpPaneCopyCommand, OnHelpPaneCopyExecuted, OnHelpPaneCopyCanExecute));
        CommandManager.RegisterClassCommandBinding(
            typeof(CustomPropertyGrid),
            new CommandBinding(HelpPaneSelectAllCommand, OnHelpPaneSelectAllExecuted, OnHelpPaneSelectAllCanExecute));
    }

    /// <summary>
    /// Command to clear the search box
    /// </summary>
    public ICommand ClearSearchCommand { get; }

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
    /// Gets or sets the current search text for filtering properties.
    /// </summary>
    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
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
    /// Recursively loads all children for the given property hierarchy item.
    /// </summary>
    private void LoadAllChildrenRecursive(PropertyHierarchyItem item)
    {
        // If the item is expandable and has no children, load them
        if (item.HasItems && item.Children.Count == 0)
        {
            item.LoadChildren(true, true);
        }
        // Recursively load children for each child
        foreach (var child in item.Children)
        {
            LoadAllChildrenRecursive(child);
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

                // Only filter out nulls if IncludeNullValues is false
                IEnumerable<Abstractions.IPropertyDescriptor> filteredProperties = category;
                if (!IncludeNullValues)
                {
                    filteredProperties = filteredProperties.Where(p => p.Value != null);
                }
                var filteredList = filteredProperties.OrderBy(p => p.DisplayName).ToList();
                foreach (var descriptor in filteredList)
                {
                    // Check for ShowChildrenAsParentAttribute
                    bool showChildAsParent = PropertyGridAttributeHelpers.HasPropertyAttribute<ShowChildrenAsParentAttribute>(descriptor);
                    if (showChildAsParent)
                    {
                        // Promote children: add child properties directly to the parent category
                        var childDescriptors = PropertyTypeProviderRegistry.Instance.GetChildItems(descriptor.Value, descriptor.Name, descriptor.Category);
                        // Filter childDescriptors based on IncludeNullValues
                        if (!IncludeNullValues)
                        {
                            childDescriptors = childDescriptors.Where(cd => cd.Value != null).ToList();
                        }
                        foreach (var childDesc in childDescriptors)
                        {
                            var childItem = new PropertyHierarchyItem(childDesc, 1, IncludeSystemProperties, IncludeNullValues);
                            categoryItem.Children.Add(childItem);
                        }
                        continue; // Do not add the property itself
                    }
                    var propertyItem = new PropertyHierarchyItem(descriptor, 1, IncludeSystemProperties, IncludeNullValues);
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

    private static void OnCopyToClipboardCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = e.Parameter is string s && !string.IsNullOrEmpty(s);
    }

    private static void OnCopyToClipboardExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string text && !string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
        }
    }

    private static void OnHelpPaneCopyCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = e.OriginalSource is TextBox tb && tb.IsReadOnly && !string.IsNullOrEmpty(tb.SelectedText);
    }

    private static void OnHelpPaneCopyExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.OriginalSource is TextBox tb && tb.IsReadOnly && !string.IsNullOrEmpty(tb.SelectedText))
        {
            Clipboard.SetText(tb.SelectedText);
        }
    }

    private static void OnHelpPaneSelectAllCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = e.OriginalSource is TextBox tb && tb.IsReadOnly && tb.Text?.Length > 0;
    }

    private static void OnHelpPaneSelectAllExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.OriginalSource is TextBox tb && tb.IsReadOnly)
        {
            tb.SelectAll();
        }
    }

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

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CustomPropertyGrid grid && grid._searchBox != null)
        {
            grid._searchBox.Text = (string)e.NewValue;
        }
    }

    private static void OnSelectedHierarchyItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CustomPropertyGrid grid && grid.SelectedHierarchyItem != null && grid.ShowHelpPane && !string.IsNullOrEmpty(grid.SelectedHierarchyItem.Description))
        {
            // Disabled auto sizing help pane for now
            // grid.AutoAdjustHelpPaneHeight(grid.SelectedHierarchyItem.Description);
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

        bool enableVirtualization = (bool)e.NewValue;        // Apply virtualization settings to TreeView
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
    /// Handles search box text changes for TreeView mode
    /// </summary>
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            // Update the SearchText property to sync with the search box
            SearchText = textBox.Text ?? string.Empty;
        }

        if (_propertiesTreeView != null)
        {
            UpdateTreeViewSearch();
        }
    }

    /// <summary>
    /// Recursively searches property hierarchy items for a match and sets visibility/expansion accordingly.
    /// Expands all parent categories of a match.
    /// </summary>
    private bool SearchPropertyRecursive(PropertyHierarchyItem item, string searchText)
    {
        // Early exit if search text is null or empty
        if (string.IsNullOrEmpty(searchText))
        {
            item.Visibility = Visibility.Visible;
            return true;
        }

        // Use StringComparison.OrdinalIgnoreCase consistently and cache it
        const StringComparison comparison = StringComparison.OrdinalIgnoreCase;

        // Pre-convert search text to avoid repeated operations
        // Check if this item matches the search with more efficient null-safe checks
        bool isMatch = false;

        // Check name fields first (most likely to match)
        if (!string.IsNullOrEmpty(item.Name))
            isMatch = item.Name.Contains(searchText, comparison);

        if (!isMatch && !string.IsNullOrEmpty(item.DisplayName))
            isMatch = item.DisplayName.Contains(searchText, comparison);

        // Only check description and value if name fields don't match
        if (!isMatch && !string.IsNullOrEmpty(item.Description))
            isMatch = item.Description.Contains(searchText, comparison);

        if (!isMatch && item.Value != null)
        {
            // Use null-conditional operator and fallback to empty string to avoid nullable warning
            string valueString = item.Value?.ToString() ?? string.Empty;
            if (!string.IsNullOrEmpty(valueString))
                isMatch = valueString.Contains(searchText, comparison);
        }

        bool hasVisibleChild = false;

        // Only search children if they exist - avoid unnecessary iterations
        if (item.Children?.Count > 0)
        {
            // Use for loop instead of foreach for better performance with collections
            for (int i = 0; i < item.Children.Count; i++)
            {
                if (SearchPropertyRecursive(item.Children[i], searchText))
                {
                    hasVisibleChild = true;
                    // Don't break here - we need to process all children for proper visibility
                }
            }
        }

        // Determine final visibility
        bool shouldBeVisible = isMatch || hasVisibleChild;
        Visibility targetVisibility = shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;

        // Only set visibility if it actually needs to change
        if (item.Visibility != targetVisibility)
        {
            item.Visibility = targetVisibility;
        }

        // Only modify expansion state if the item can be expanded AND the state needs to change
        if (item.HasItems && item.IsExpanded != shouldBeVisible)
        {
            item.IsExpanded = shouldBeVisible;
        }

        return shouldBeVisible;
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
    /// Loads all children on demand if a search is performed.
    /// </summary>
    private void UpdateTreeViewSearch()
    {
        if (_propertiesTreeView == null || !(_propertiesTreeView.ItemsSource is IEnumerable<PropertyHierarchyItem> items))
            return;

        string? searchText = _searchBox?.Text?.Trim();
        bool hasSearchText = !string.IsNullOrEmpty(searchText);

        if (!hasSearchText)
        {
            // Expand all categories when search is cleared
            ExpandAllCategories();

            // Collapse all root items (categories) so only categories are expanded, not their children
            foreach (var category in items.OfType<PropertyCategoryItem>())
            {
                foreach (var child in category.Children)
                {
                    if (child.HasItems && child.IsExpanded)
                        child.IsExpanded = false;

                }
                category.Visibility = Visibility.Visible;
                category.ResetVisibilityRecursive();
            }
            return;
        }

        // On-demand: load all children for all root items before searching
        foreach (var category in items.OfType<PropertyCategoryItem>())
        {
            LoadAllChildrenRecursive(category);
        }

        // Recursive search for all categories
        foreach (var category in items.OfType<PropertyCategoryItem>())
        {
            SearchPropertyRecursive(category, searchText!);
        }
    }
}