using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WmiExplorer.Integration.PropertyTypeProvider;
using WmiExplorer.Presentation.Views.Dialogs;
using WmiExplorer.PropertyGrid;
using WmiExplorer.PropertyGrid.Editors.Core;
using WmiExplorer.Services;

namespace WmiExplorer.Integration.PropertyGrid;

public class WmiReferencePropertyEditor : WmiPropertyEditorBase
{
    public WmiReferencePropertyEditor(IWmiService wmiService, IMessengerService messengerService)
        : base(wmiService, messengerService) { }

    public override bool CanHandle(PropertyHierarchyItem propertyItem)
    {
        return propertyItem?.PropertyDescriptor is WmiPropertyDescriptor wmiDescriptor && wmiDescriptor.IsReference;
    }

    public override UIElement CreateEditor(PropertyHierarchyItem propertyItem)
    {
        if (propertyItem?.PropertyDescriptor is not WmiPropertyDescriptor wmiDescriptor)
            throw new ArgumentException("PropertyItem must have a WmiPropertyDescriptor", nameof(propertyItem));

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var comboBox = new ComboBox
        {
            IsEditable = true,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = PropertyEditorUtils.CONTROL_MARGIN_STANDARD,
            ItemsSource = GetReferenceValues(wmiDescriptor),
            Text = GetReferenceText(wmiDescriptor)
        };
        PropertyEditorUtils.InitializeEditor(comboBox, propertyItem);
        comboBox.SetBinding(ComboBox.SelectedItemProperty, new Binding("Value")
        {
            Source = propertyItem,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        PropertyEditorUtils.ApplyMaxWidthConstraint(comboBox, grid, 120);
        if (comboBox.IsEditable)
        {
            comboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) =>
            {
                var tb = (s as ComboBox)?.Template.FindName("PART_EditableTextBox", comboBox) as TextBox;
                if (tb != null && comboBox.Text != GetReferenceText(wmiDescriptor))
                {
                    SetReferenceText(wmiDescriptor, comboBox.Text);
                }
                ApplyValidation(comboBox, propertyItem);
            }));
        }
        comboBox.SelectionChanged += (s, e) =>
        {
            if (comboBox.SelectedItem is string selectedText && selectedText != GetReferenceText(wmiDescriptor))
            {
                SetReferenceText(wmiDescriptor, selectedText);
                comboBox.Text = selectedText;
            }
            ApplyValidation(comboBox, propertyItem);
        };
        PropertyEditorUtils.AttachSelectOnFocus(comboBox, propertyItem);
        Grid.SetColumn(comboBox, 0);
        grid.Children.Add(comboBox);

        var loadButton = new Button
        {
            Content = "Load",
            Width = 60,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = CanLoadReferenceValues(wmiDescriptor)
        };
        loadButton.Click += async (s, e) =>
        {
            try
            {
                loadButton.IsEnabled = false;
                loadButton.Content = "Loading...";
                var previousSelected = comboBox.SelectedItem as string;
                if (string.IsNullOrEmpty(previousSelected))
                    previousSelected = comboBox.Text;
                await LoadReferenceValuesAsync(wmiDescriptor);
                var newItems = GetReferenceValues(wmiDescriptor);
                comboBox.ItemsSource = newItems;
                if (!string.IsNullOrEmpty(previousSelected) && newItems.Contains(previousSelected))
                {
                    comboBox.SelectedItem = previousSelected;
                }
                else if (newItems.Count > 0)
                {
                    comboBox.SelectedItem = newItems[0];
                }
                loadButton.IsEnabled = CanLoadReferenceValues(wmiDescriptor);
                loadButton.Content = "Load";
            }
            catch (Exception ex)
            {
                MessageBoxDialog.Show($"Error loading reference values: {ex.Message}", "Load Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Warning, System.Windows.Application.Current.MainWindow);
                loadButton.IsEnabled = true;
                loadButton.Content = "Load";
            }
        };
        Grid.SetColumn(loadButton, 1);
        grid.Children.Add(loadButton);

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 60,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = CanCancelLoadReferenceValues(wmiDescriptor)
        };
        cancelButton.Click += (s, e) =>
        {
            try
            {
                CancelLoadReferenceValues(wmiDescriptor);
                loadButton.IsEnabled = CanLoadReferenceValues(wmiDescriptor);
                cancelButton.IsEnabled = CanCancelLoadReferenceValues(wmiDescriptor);
            }
            catch (Exception ex)
            {
                MessageBoxDialog.Show($"Error cancelling load: {ex.Message}", "Cancel Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Warning, System.Windows.Application.Current.MainWindow);
            }
        };
        Grid.SetColumn(cancelButton, 2);
        grid.Children.Add(cancelButton);
        return grid;
    }

    private bool CanCancelLoadReferenceValues(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            return viewModel.CancelLoadReferenceValuesCommand?.CanExecute(null) ?? false;
        }
        catch
        {
            return false;
        }
    }

    private void CancelLoadReferenceValues(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            viewModel.CancelLoadReferenceValuesCommand?.Execute(null);
        }
        catch (Exception ex)
        {
            WmiExplorer.Common.Logging.Log.Error(ex, "Error cancelling reference values load for property '{PropertyName}'", wmiDescriptor.Name);
        }
    }

    private bool CanLoadReferenceValues(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            return viewModel.LoadReferenceValuesCommand?.CanExecute(null) ?? false;
        }
        catch
        {
            return false;
        }
    }

    private async Task LoadReferenceValuesAsync(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            if (viewModel.LoadReferenceValuesCommand?.CanExecute(null) == true)
            {
                await viewModel.LoadReferenceValuesCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            WmiExplorer.Common.Logging.Log.Error(ex, "Error loading reference values for property '{PropertyName}'", wmiDescriptor.Name);
            throw;
        }
    }
}