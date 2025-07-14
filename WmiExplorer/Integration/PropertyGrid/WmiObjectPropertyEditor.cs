using System.Windows;
using System.Windows.Controls;
using WmiExplorer.Integration.PropertyTypeProvider;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.Views.Dialogs;
using WmiExplorer.PropertyGrid;
using WmiExplorer.PropertyGrid.Editors.Core;
using WmiExplorer.Services;

namespace WmiExplorer.Integration.PropertyGrid;

public class WmiObjectPropertyEditor : WmiPropertyEditorBase
{
    public WmiObjectPropertyEditor(IWmiService wmiService, IMessengerService messengerService)
        : base(wmiService, messengerService) { }

    public override bool CanHandle(PropertyHierarchyItem propertyItem)
    {
        return propertyItem?.PropertyDescriptor is WmiPropertyDescriptor wmiDescriptor && wmiDescriptor.IsObject && !wmiDescriptor.PropertyData.IsArray;
    }

    public override UIElement CreateEditor(PropertyHierarchyItem propertyItem)
    {
        if (propertyItem?.PropertyDescriptor is not WmiPropertyDescriptor wmiDescriptor)
            throw new ArgumentException("PropertyItem must have a WmiPropertyDescriptor", nameof(propertyItem));

        var viewModel = CreateViewModel(wmiDescriptor);
        var mbo = viewModel.Value as System.Management.ManagementBaseObject;
        var displayText = _propertyValueConverter.ConvertToString(mbo, typeof(System.Management.ManagementBaseObject));
        var textBox = PropertyEditorUtils.CreateStandardTextBox(displayText, null, propertyItem);

        PropertyEditorUtils.InitializeEditor(textBox, propertyItem);
        textBox.IsReadOnly = true;
        textBox.TextWrapping = TextWrapping.Wrap;
        textBox.TextChanged += (s, e) => ApplyValidation(textBox, propertyItem);
        textBox.Loaded += (s, e) => ApplyValidation(textBox, propertyItem);

        var editButton = new Button { IsEnabled = CanEditObject(wmiDescriptor) };
        void UpdateEditButton()
        {
            var mbo = propertyItem.Value as System.Management.ManagementBaseObject;
            if (mbo != null)
            {
                editButton.Content = "Edit";
                editButton.Width = 54;
            }
            else
            {
                editButton.Content = "Create";
                editButton.Width = 64;
            }
        }
        textBox.TextChanged += (s, e) => UpdateEditButton();
        UpdateEditButton();
        editButton.Click += (s, e) =>
        {
            try
            {
                var result = EditObject(wmiDescriptor);
                if (result != null)
                {
                    propertyItem.Value = result;
                    textBox.Text = _propertyValueConverter.ConvertToString(result, typeof(System.Management.ManagementBaseObject));
                    ApplyValidation(textBox, propertyItem);
                }
            }
            catch (Exception ex)
            {
                MessageBoxDialog.Show($"Error editing object: {ex.Message}", "Edit Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Error, System.Windows.Application.Current.MainWindow);
            }
        };
        var removeButton = new Button
        {
            Content = "Remove",
            Width = 60,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        removeButton.Click += (s, e) =>
        {
            propertyItem.Value = null;
            textBox.Text = "<null>";
            UpdateEditButton();
            ApplyValidation(textBox, propertyItem);
        };
        var actionPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        actionPanel.Children.Add(editButton);
        actionPanel.Children.Add(removeButton);
        return PropertyEditorUtils.CreateGridWithActionPanel(textBox, actionPanel, editButton.Width + removeButton.Width + 8);
    }

    private bool CanEditObject(WmiPropertyDescriptor wmiDescriptor)
    {
        try
        {
            var viewModel = GetOrCreateViewModel(wmiDescriptor);
            return viewModel.EditObjectCommand?.CanExecute(null) ?? false;
        }
        catch
        {
            return false;
        }
    }

    private object? EditObject(WmiPropertyDescriptor wmiDescriptor, string? title = null)
    {
        try
        {
            var viewModel = CreateViewModel(wmiDescriptor); // Create a new view model for the edit operation
            if (viewModel.Value == null)
            {
                var className = viewModel.TargetClassName;
                var scope = wmiDescriptor.GetManagementScope();
                if (!string.IsNullOrEmpty(className) && scope != null)
                {
                    viewModel.Value = WmiObjectFactory.CreateTemplateObject(className, scope);
                }
            }
            if (viewModel.EditObjectCommand?.CanExecute(null) == true)
            {
                viewModel.EditObjectCommand.Execute(null);
                return viewModel.Value;
            }
            return null;
        }
        catch (Exception ex)
        {
            MessageBoxDialog.Show($"Error editing object: {ex.Message}", "Edit Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Error, System.Windows.Application.Current.MainWindow);
            return null;
        }
    }
}