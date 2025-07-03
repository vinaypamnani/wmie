using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WmiExplorer.Presentation.Behaviors;

/// <summary>
/// Behavior to suppress horizontal auto-scroll when selecting a DataGrid cell or row.
/// Uses a time-based approach to detect and prevent auto-scrolling caused by cell clicks.
/// Reference: https://www.codeproject.com/Tips/5165488/Prevent-WPF-DataGrid-Auto-scrolling-Due-to-Clickin
/// </summary>
public static class DataGridHorizontalScrollPreventionBehavior
{
    private const double AutoScrollPreventionMilliseconds = 100;

    public static readonly DependencyProperty EnableProperty =
        DependencyProperty.RegisterAttached(
            "Enable",
            typeof(bool),
            typeof(DataGridHorizontalScrollPreventionBehavior),
            new PropertyMetadata(false, OnEnableChanged));

    // Attached property for associating click info with DataGrid instances
    private static readonly DependencyProperty ClickInfoProperty =
        DependencyProperty.RegisterAttached(
            "ClickInfo",
            typeof(ClickInfo?),
            typeof(DataGridHorizontalScrollPreventionBehavior),
            new PropertyMetadata(null));

    public static bool GetEnable(DependencyObject element)
    {
        return (bool)element.GetValue(EnableProperty);
    }

    public static void SetEnable(DependencyObject element, bool value)
    {
        element.SetValue(EnableProperty, value);
    }

    private static void AddCellStyle(DataGrid dataGrid)
    {
        // Create a style for DataGridCell that includes our PreviewMouseDown handler
        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new EventSetter(DataGridCell.PreviewMouseDownEvent,
            new MouseButtonEventHandler(DataGridCell_PreviewMouseDown)));

        // If there's already a cell style, merge with it
        if (dataGrid.CellStyle != null)
        {
            cellStyle.BasedOn = dataGrid.CellStyle;
        }

        dataGrid.CellStyle = cellStyle;
    }

    private static void CleanupDataGrid(DataGrid dataGrid)
    {
        var scrollViewer = GetScrollViewer(dataGrid);
        if (scrollViewer != null)
        {
            scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
        }
    }

    private static void DataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid dataGrid)
        {
            // Add cell style for PreviewMouseDown if not already present
            AddCellStyle(dataGrid);

            // Hook up ScrollViewer event handler
            var scrollViewer = GetScrollViewer(dataGrid);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            }
        }
    }

    private static void DataGrid_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid dataGrid)
        {
            CleanupDataGrid(dataGrid);
        }
    }

    private static void DataGridCell_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridCell cell)
        {
            var dataGrid = FindParent<DataGrid>(cell);
            if (dataGrid != null)
            {
                var scrollViewer = GetScrollViewer(dataGrid);
                if (scrollViewer != null)
                {
                    // Store the current horizontal offset with a timestamp
                    SetClickInfo(dataGrid, new ClickInfo(scrollViewer.HorizontalOffset));
                }
            }
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T result)
                return result;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;

            var childResult = FindVisualChild<T>(child);
            if (childResult != null)
                return childResult;
        }
        return null;
    }

    private static ClickInfo? GetClickInfo(DependencyObject element)
    {
        return (ClickInfo?)element.GetValue(ClickInfoProperty);
    }

    private static ScrollViewer? GetScrollViewer(DataGrid dataGrid)
    {
        if (dataGrid.Template?.FindName("PART_ScrollViewer", dataGrid) is ScrollViewer scrollViewer)
        {
            return scrollViewer;
        }

        // Fallback: Find ScrollViewer in visual tree
        return FindVisualChild<ScrollViewer>(dataGrid);
    }

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DataGrid dataGrid)
        {
            if ((bool)e.NewValue)
            {
                // Hook up event handlers for the click-based approach
                dataGrid.Loaded += DataGrid_Loaded;
                dataGrid.Unloaded += DataGrid_Unloaded;
            }
            else
            {
                // Clean up event handlers
                dataGrid.Loaded -= DataGrid_Loaded;
                dataGrid.Unloaded -= DataGrid_Unloaded;
                CleanupDataGrid(dataGrid);
            }
        }
    }

    private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer && e.HorizontalChange != 0)
        {
            var dataGrid = FindParent<DataGrid>(scrollViewer);
            if (dataGrid != null)
            {
                var clickInfo = GetClickInfo(dataGrid);
                if (clickInfo.HasValue && clickInfo.Value.IsRecent(AutoScrollPreventionMilliseconds))
                {
                    // Restore the horizontal position to what it was when the mouse was clicked
                    scrollViewer.ScrollToHorizontalOffset(clickInfo.Value.HorizontalOffset);
                }
            }
        }
    }

    private static void SetClickInfo(DependencyObject element, ClickInfo? value)
    {
        element.SetValue(ClickInfoProperty, value);
    }

    /// <summary>
    /// Stores horizontal offset with a timestamp for tracking recent clicks
    /// </summary>
    private struct ClickInfo
    {
        public ClickInfo(double horizontalOffset)
        {
            HorizontalOffset = horizontalOffset;
            Timestamp = DateTime.Now;
        }

        public double HorizontalOffset { get; }
        public DateTime Timestamp { get; }

        public bool IsRecent(double withinMilliseconds) =>
            (DateTime.Now - Timestamp).TotalMilliseconds <= withinMilliseconds;
    }
}