using System.Windows;
using System.Windows.Controls;
using WmiExplorer.Presentation.ViewModels.Coordinators;

namespace WmiExplorer.Presentation.Views.Tabs;

/// <summary>
/// Interaction logic for QueryTabView.xaml
/// </summary>
public partial class QueryTabView : UserControl
{
    public QueryTabView()
    {
        InitializeComponent();
        DataContextChanged += WmiQueryTab_DataContextChanged;
    }

    private void WmiQueryTab_DataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        SyncColumns();
        if (e.OldValue is QueryTabViewModel oldVm)
        {
            oldVm.ResultColumns.CollectionChanged -= ResultColumns_CollectionChanged;
        }
        if (e.NewValue is QueryTabViewModel newVm)
        {
            newVm.ResultColumns.CollectionChanged += ResultColumns_CollectionChanged;
        }
    }

    private void ResultColumns_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        SyncColumns();
    }

    private void SyncColumns()
    {
        if (DataContext is QueryTabViewModel vm)
        {
            ResultsDataGrid.Columns.Clear();
            foreach (var col in vm.ResultColumns)
            {
                ResultsDataGrid.Columns.Add(col);
            }
        }
    }
}