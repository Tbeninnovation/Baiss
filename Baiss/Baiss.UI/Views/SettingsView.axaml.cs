using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Baiss.UI.ViewModels;

namespace Baiss.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        // Ensure ComboBoxes open on primary/touch interaction even if the default handler marks the event handled (macOS issue)
        AddHandler(ComboBox.PointerPressedEvent, OnComboBoxPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);

        // Ensure Buttons respond to left-click on all platforms
        AddHandler(Button.PointerPressedEvent, OnButtonPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnModelSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && DataContext is SettingsViewModel viewModel)
        {
            viewModel.SelectedModelIndex = comboBox.SelectedIndex;
        }
    }

    private void OnComboBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Control sourceControl)
        {
            return;
        }

        var comboBox = (sourceControl as ComboBox) ?? sourceControl.FindAncestorOfType<ComboBox>();
        if (comboBox is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(comboBox);
        var properties = point.Properties;
        var pointerType = e.Pointer.Type;

        var isPrimaryMouse = pointerType == PointerType.Mouse && properties.IsLeftButtonPressed;
        var isTouchOrPen = pointerType is PointerType.Touch or PointerType.Pen;

        if ((isPrimaryMouse || isTouchOrPen) && !comboBox.IsDropDownOpen)
        {
            comboBox.Focus();
            comboBox.IsDropDownOpen = true;
            e.Handled = true;
        }
    }

    private void OnButtonPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Control sourceControl)
        {
            return;
        }

        var button = (sourceControl as Button) ?? sourceControl.FindAncestorOfType<Button>();
        if (button is null || !button.IsEnabled)
        {
            return;
        }

        var point = e.GetCurrentPoint(button);
        var properties = point.Properties;
        var pointerType = e.Pointer.Type;

        // Only handle left mouse button or touch/pen
        var isPrimaryMouse = pointerType == PointerType.Mouse && properties.IsLeftButtonPressed;
        var isTouchOrPen = pointerType is PointerType.Touch or PointerType.Pen;

        if (isPrimaryMouse || isTouchOrPen)
        {
            // Let the button handle the click normally
            // This handler ensures the event is properly routed
            button.Focus();
        }
    }
}
