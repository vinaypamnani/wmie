using System.Windows;
using System.Windows.Controls;

namespace WmiExplorer.Presentation.Views.Tabs;

/// <summary>
/// Interaction logic for WmiQueryTab.xaml
/// </summary>
public partial class WmiQueryTab : UserControl
{
    public WmiQueryTab()
    {
        InitializeComponent();
        DataContextChanged += WmiQueryTab_DataContextChanged;
    }

    private void WmiQueryTab_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SyncColumns();
        if (e.OldValue is WmiExplorer.Presentation.ViewModels.WmiQueryViewModel oldVm)
        {
            oldVm.ResultColumns.CollectionChanged -= ResultColumns_CollectionChanged;
        }
        if (e.NewValue is WmiExplorer.Presentation.ViewModels.WmiQueryViewModel newVm)
        {
            newVm.ResultColumns.CollectionChanged += ResultColumns_CollectionChanged;
        }
    }

    private void ResultColumns_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        SyncColumns();
    }

    private void SyncColumns()
    {
        if (DataContext is WmiExplorer.Presentation.ViewModels.WmiQueryViewModel vm)
        {
            ResultsDataGrid.Columns.Clear();
            foreach (var col in vm.ResultColumns)
            {
                ResultsDataGrid.Columns.Add(col);
            }
        }
    }
}