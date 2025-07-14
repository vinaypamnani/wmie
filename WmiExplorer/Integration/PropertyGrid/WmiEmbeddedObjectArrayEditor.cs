using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using WmiExplorer.Common.Logging;
using WmiExplorer.Integration.PropertyTypeProvider;
using WmiExplorer.Presentation.ViewModels.Helpers;
using WmiExplorer.Presentation.Views.Dialogs;
using WmiExplorer.PropertyGrid;
using WmiExplorer.PropertyGrid.Editors.Core;
using WmiExplorer.Services;

namespace WmiExplorer.Integration.PropertyGrid;

public class WmiEmbeddedObjectArrayEditor : WmiPropertyEditorBase
{
    public WmiEmbeddedObjectArrayEditor(IWmiService wmiService, IMessengerService messengerService)
        : base(wmiService, messengerService) { }

    public override bool CanHandle(PropertyHierarchyItem propertyItem)
    {
        return propertyItem?.PropertyDescriptor is WmiPropertyDescriptor wmiDescriptor && wmiDescriptor.IsObject && wmiDescriptor.PropertyData.IsArray;
    }

    public override UIElement CreateEditor(PropertyHierarchyItem propertyItem)
    {
        if (propertyItem?.PropertyDescriptor is not WmiPropertyDescriptor wmiDescriptor)
            throw new ArgumentException("PropertyItem must have a WmiPropertyDescriptor", nameof(propertyItem));

        var viewModel = CreateViewModel(wmiDescriptor);
        var panel = new StackPanel();
        var array = propertyItem.Value as System.Management.ManagementBaseObject[];
        var items = new ObservableCollection<System.Management.ManagementBaseObject>(array ?? Array.Empty<System.Management.ManagementBaseObject>());
        var validationProxy = new ComboBox
        {
            Visibility = Visibility.Collapsed,
            Width = 0,
            IsReadOnly = true,
            Focusable = false,
            IsTabStop = false,
            IsHitTestVisible = false
        };
        panel.Children.Add(validationProxy);
        ValidationManager.SetValidationNormal(validationProxy);
        var listView = new ListView
        {
            Margin = new Thickness(0, 0, 0, 0),
            MinHeight = 40,
            MaxHeight = 200,
            Style = (Style)Application.Current.FindResource("ModernListViewStyle"),
            Background = System.Windows.Media.Brushes.Transparent,
            SelectionMode = SelectionMode.Single,
            SelectedIndex = -1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        listView.SetValue(ItemsControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch);
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        var contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        contentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        contentPresenterFactory.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
        contentPresenterFactory.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ContentControl.ContentTemplateProperty));
        contentPresenterFactory.SetValue(ContentPresenter.ContentTemplateSelectorProperty, new TemplateBindingExtension(ContentControl.ContentTemplateSelectorProperty));
        borderFactory.AppendChild(contentPresenterFactory);
        var template = new ControlTemplate(typeof(ListViewItem)) { VisualTree = borderFactory };
        listView.ItemContainerStyle = new Style(typeof(ListViewItem))
        {
            Setters = {
                new Setter(Control.TemplateProperty, template)
            }
        };
        UIElement CreateEmbeddedObjectListItem(System.Management.ManagementBaseObject mbo)
        {
            var displayText = _propertyValueConverter.ConvertToString(mbo, typeof(System.Management.ManagementBaseObject));
            var textBox = PropertyEditorUtils.CreateStandardTextBox(displayText, null, null, new Thickness(4, 0, 0, 0));
            textBox.IsReadOnly = true;
            textBox.TextWrapping = TextWrapping.Wrap;
            ValidationManager.SetValidationNormal(textBox);
            var editButton = new Button
            {
                Content = "Edit",
                Width = 54
            };
            editButton.Click += (s, e) =>
            {
                var edited = EditObject(mbo);
                if (edited is System.Management.ManagementBaseObject editedMbo)
                {
                    ValidationManager.SetValidationModified(validationProxy);
                    var idx = items.IndexOf(mbo);
                    if (idx >= 0)
                    {
                        items[idx] = editedMbo;
                        propertyItem.Value = items.ToArray();
                        textBox.Text = _propertyValueConverter.ConvertToString(editedMbo, typeof(System.Management.ManagementBaseObject));
                        listView.Items[idx] = CreateEmbeddedObjectListItem(editedMbo);
                    }
                }
            };
            var removeButton = new Button
            {
                Content = "Remove",
                Width = 60,
                Margin = new Thickness(4, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            removeButton.Click += (s, e) =>
            {
                items.Remove(mbo);
            };
            var actionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            actionPanel.Children.Add(editButton);
            actionPanel.Children.Add(removeButton);
            return PropertyEditorUtils.CreateGridWithActionPanel(textBox, actionPanel, editButton.Width + removeButton.Width + 8 + 4);
        }
        listView.Items.Clear();
        foreach (var mbo in items)
        {
            listView.Items.Add(CreateEmbeddedObjectListItem(mbo));
        }
        items.CollectionChanged += (s, e) =>
        {
            listView.Items.Clear();
            foreach (var mbo in items)
            {
                listView.Items.Add(CreateEmbeddedObjectListItem(mbo));
            }
            propertyItem.Value = items.ToArray();
            ValidationManager.SetValidationModified(validationProxy);
        };
        var expander = new Expander
        {
            Header = $"Embedded Objects ({items.Count})",
            IsExpanded = true,
            Content = listView
        };
        items.CollectionChanged += (s, e) =>
        {
            expander.Header = $"Embedded Objects ({items.Count})";
        };
        var addButton = new Button
        {
            Content = "Add...",
            Width = 60
        };
        addButton.HorizontalAlignment = HorizontalAlignment.Left;
        addButton.Click += (s, e) =>
        {
            var className = viewModel.TargetClassName;
            var scope = wmiDescriptor.GetManagementScope();
            if (!string.IsNullOrEmpty(className) && scope != null)
            {
                try
                {
                    var newObj = WmiObjectFactory.CreateTemplateObject(className, scope);
                    if (newObj != null)
                    {
                        var owner = Application.Current.MainWindow;
                        var edited = Presentation.Views.Dialogs.PropertyEditorDialog.ShowEditor(owner, newObj, _messengerService, $"Add {className}", _wmiService, false);
                        if (edited != null)
                        {
                            items.Add(edited);
                            propertyItem.Value = items.ToArray();
                            ValidationManager.SetValidationModified(validationProxy);
                        }
                    }
                    else
                    {
                        Log.Error("Error creating embedded object: {ClassName}. New object is null. ", className);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error creating embedded object: {ClassName}", className);
                }
            }
            else
            {
                Log.Error("Error creating embedded object: {PropertyName}. Target class name or scope is null. ", wmiDescriptor.Name);
            }
        };
        panel.Children.Add(expander);
        panel.Children.Add(addButton);
        listView.SelectionChanged += (s, e) =>
        {
            listView.SelectedIndex = -1;
        };
        return panel;
    }

    private object? EditObject(System.Management.ManagementBaseObject? mbo, string? title = null)
    {
        try
        {
            if (mbo == null) return null;
            var owner = Application.Current.MainWindow;
            var displayName = _propertyValueConverter.ConvertToString(mbo, typeof(System.Management.ManagementBaseObject));
            var dialogTitle = title ?? $"Edit object: {displayName}";
            var edited = Presentation.Views.Dialogs.PropertyEditorDialog.ShowEditor(owner, mbo, _messengerService, dialogTitle, _wmiService, false);
            return edited;
        }
        catch (Exception ex)
        {
            MessageBoxDialog.Show($"Error editing object: {ex.Message}", "Edit Error", MessageBoxDialogButton.OK, MessageBoxDialogIcon.Error, System.Windows.Application.Current.MainWindow);
            return null;
        }
    }
}