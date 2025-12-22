using System;
using Avalonia.Controls;
using Avalonia.Input;
using System.Collections.Specialized;
using Avalonia.Threading;
using Baiss.UI.ViewModels;
using Avalonia;
using System.Threading.Tasks; // added if missing
using System.Threading; // for CancellationToken
using Avalonia.Platform.Storage;
using System.Linq;
using System.IO;
using Baiss.UI.Models;
using Baiss.UI.Services;
using Avalonia.VisualTree;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Baiss.UI.Views;

public partial class MainWindow : Window
{
    public static ToastService ToastServiceInstance { get; } = new();

    private bool _isResizing = false;
    private string? _resizeDirection = null;
    private PixelPoint _startPosition;
    private Size _startSize;
    private PixelPoint _startPointerPosition;
    private Control? _capturingControl = null;
    private const int MinWindowSize = 80;

    private Size _normalSize;
    private PixelPoint _normalPosition;
    private bool _wasMaximized = false;

    // Add missing fields for floating button interaction
    private bool _iconPressed = false;
    private bool _dragStarted = false;
    private DateTime _iconPressStartTime;
    private PixelPoint _iconPressPosition;
    private PointerPressedEventArgs? _originalPressedArgs;
    private const int DragThreshold = 5; // pixels
    private const int ClickMaxDuration = 400; // increased from 200 ms

    public MainWindow()
    {
        InitializeComponent();

        // Bind toast items if the control exists
        this.AttachedToVisualTree += (_, _) =>
        {
            if (this.FindControl<ItemsControl>("ToastItems") is { } toastItems)
            {
                toastItems.ItemsSource = ToastServiceInstance.Messages;
            }
        };

        TopResizeBorder.PointerPressed += (s, e) => StartResize("Top", e, TopResizeBorder);
        TopLeftResizeBorder.PointerPressed += (s, e) => StartResize("TopLeft", e, TopLeftResizeBorder);
        TopRightResizeBorder.PointerPressed += (s, e) => StartResize("TopRight", e, TopRightResizeBorder);

        this.PointerMoved += Window_PointerMoved;
        this.PointerReleased += Window_PointerReleased;

        // Subscribe to DataContext changes to hook up to Messages collection
        this.DataContextChanged += MainWindow_DataContextChanged;

        // Add drag & drop support
        AddHandler(DragDrop.DropEvent, Drop);
        AddHandler(DragDrop.DragOverEvent, DragOver);
        DragDrop.SetAllowDrop(this, true);

        // Ensure ComboBoxes open on primary/touch interaction even if the default handler marks the event handled (macOS issue)
        AddHandler(ComboBox.PointerPressedEvent, OnComboBoxPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void MainWindow_DataContextChanged(object? sender, EventArgs e)
    {
        if (this.DataContext is MainWindowViewModel vm)
        {
            // Subscribe to Messages collection changes for auto-scroll
            vm.Messages.CollectionChanged += Messages_CollectionChanged;

            // Subscribe to collapse state changes
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.IsCollapsed))
                {
                    HandleCollapseStateChanged(vm.IsCollapsed);
                }
            };

            // Keep Topmost in sync with the initial collapse state
            this.Topmost = vm.IsCollapsed;
            
            // Set up scroll detection for the messages list
            Dispatcher.UIThread.Post(() =>
            {
                if (this.FindControl<ScrollViewer>("ChatScrollViewer") is ScrollViewer scrollViewer)
                {
                    scrollViewer.ScrollChanged += MessagesList_ScrollChanged;
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private void HandleCollapseStateChanged(bool isCollapsed)
    {
        // Ensure we're on the UI thread
        Dispatcher.UIThread.Post(async () =>
        {
            // Only stay on top when collapsed
            this.Topmost = isCollapsed;

            if (isCollapsed)
            {
                // Save current window state if it's valid
                if (this.Width > 100 && this.Height > 100)
                {
                    _normalSize = new Size(this.Width, this.Height);
                    _normalPosition = this.Position;
                    _wasMaximized = this.WindowState == WindowState.Maximized;
                }
                else if (_normalSize.Width == 0 || _normalSize.Height == 0)
                {
                    // Set default size if nothing was saved
                    _normalSize = new Size(800, 600);
                    _normalPosition = this.Position;
                    _wasMaximized = false;
                }

                // Set normal state before resizing
                this.WindowState = WindowState.Normal;
                this.CanResize = false;
                
                // Animate to collapsed size (70x70)
                double currentWidth = this.Width;
                double currentHeight = this.Height;
                await AnimateWindowSize(currentWidth, currentHeight, 70, 70);
            }
            else
            {
                // Restore window settings first
                this.CanResize = true;
                
                if (_wasMaximized)
                {
                    this.WindowState = WindowState.Maximized;
                }
                else
                {
                    // Set window state to normal
                    this.WindowState = WindowState.Normal;
                    
                    // Restore position if valid, otherwise center
                    if (_normalPosition.X != 0 || _normalPosition.Y != 0)
                    {
                        this.Position = _normalPosition;
                    }
                    
                    // Animate back to original size
                    double currentWidth = this.Width;
                    double currentHeight = this.Height;
                    double targetWidth = _normalSize.Width > 0 ? _normalSize.Width : 800;
                    double targetHeight = _normalSize.Height > 0 ? _normalSize.Height : 600;
                    
                    await AnimateWindowSize(currentWidth, currentHeight, targetWidth, targetHeight);
                    
                    // Ensure window is still visible on screen
                    await Task.Delay(50); // Small delay to ensure animation completes
                    EnsureWindowVisible();
                }
            }
        });
    }

    // New helper method to clamp the window's position within the working area of its screen.
    private void EnsureWindowVisible()
    {
        var screen = this.Screens.ScreenFromVisual(this);
        if (screen == null) return;

        var workingArea = screen.WorkingArea;
        var pos = this.Position;
        int newX = pos.X;
        int newY = pos.Y;

        if (pos.X < workingArea.X)
            newX = (int)workingArea.X;
        if (pos.Y < workingArea.Y)
            newY = (int)workingArea.Y;
        if (pos.X + this.Width > workingArea.X + workingArea.Width)
            newX = (int)(workingArea.X + workingArea.Width - this.Width);
        if (pos.Y + this.Height > workingArea.Y + workingArea.Height)
            newY = (int)(workingArea.Y + workingArea.Height - this.Height);

        this.Position = new PixelPoint(newX, newY);
    }

    // New helper method for animating the window size
    private async Task AnimateWindowSize(double fromWidth, double fromHeight, double toWidth, double toHeight, int durationMs = 100)
    {
        int frames = 30;
        int delay = durationMs / frames;
        double deltaWidth = toWidth - fromWidth;
        double deltaHeight = toHeight - fromHeight;

        for (int i = 0; i <= frames; i++)
        {
            double t = i / (double)frames;
            double eased = t * t * (3 - 2 * t); // smoothstep easing
            this.Width = fromWidth + deltaWidth * eased;
            this.Height = fromHeight + deltaHeight * eased;
            await Task.Delay(delay);
        }
        this.Width = toWidth;
        this.Height = toHeight;
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            ScrollToBottom();
        }
    }

    public void ScrollToBottom()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (this.FindControl<ScrollViewer>("ChatScrollViewer") is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToEnd();
            }
            
            // Hide the button after scrolling
            if (this.DataContext is MainWindowViewModel vm)
            {
                vm.ShowScrollToBottom = false;
            }
        });
    }

    private void MessagesList_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer && this.DataContext is MainWindowViewModel vm)
        {
            // Check if user is near the bottom (within 100 pixels)
            var isAtBottom = scrollViewer.Offset.Y + scrollViewer.Viewport.Height >= scrollViewer.Extent.Height - 100;
            
            // Show button only if not at bottom and there are messages
            vm.ShowScrollToBottom = !isAtBottom && vm.Messages.Count > 0;
        }
    }

    private void MessageInputBox_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (this.DataContext is MainWindowViewModel vm && vm.SendMessageCommand.CanExecute(null))
            {
                vm.SendMessageCommand.Execute(null);
                if (this.FindControl<TextBox>("MessageInputBox") is TextBox input)
                {
                    input.Focus();
                }
            }
        }
    }

    private void StartResize(string direction, PointerPressedEventArgs e, Control control)
    {
        _isResizing = true;
        _resizeDirection = direction;
        _startPosition = Position;
        _startSize = this.ClientSize;

        // Get the pointer position relative to the screen instead of the window
        var pointerPos = e.GetPosition(this);
        _startPointerPosition = new PixelPoint((int)pointerPos.X, (int)pointerPos.Y) + Position;

        _capturingControl = control;
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void Window_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isResizing || _resizeDirection == null)
            return;

        // Get current pointer position relative to screen
        var currentPointer = e.GetPosition(this);
        var currentPointerPosition = new PixelPoint((int)currentPointer.X, (int)currentPointer.Y) + Position;
        var deltaX = currentPointerPosition.X - _startPointerPosition.X;
        var deltaY = currentPointerPosition.Y - _startPointerPosition.Y;

        var newPosition = _startPosition;
        var newWidth = _startSize.Width;
        var newHeight = _startSize.Height;

        switch (_resizeDirection)
        {
            case "Top":
                var newTopHeight = Math.Max(MinWindowSize, _startSize.Height - deltaY);
                var actualTopDelta = _startSize.Height - newTopHeight;
                newPosition = new PixelPoint(_startPosition.X, _startPosition.Y + (int)actualTopDelta);
                newHeight = newTopHeight;
                break;

            case "Bottom":
                newHeight = Math.Max(MinWindowSize, _startSize.Height + deltaY);
                break;

            case "Left":
                var newLeftWidth = Math.Max(MinWindowSize, _startSize.Width - deltaX);
                var actualLeftDelta = _startSize.Width - newLeftWidth;
                newPosition = new PixelPoint(_startPosition.X + (int)actualLeftDelta, _startPosition.Y);
                newWidth = newLeftWidth;
                break;

            case "Right":
                newWidth = Math.Max(MinWindowSize, _startSize.Width + deltaX);
                break;

            case "TopLeft":
                var newTLWidth = Math.Max(MinWindowSize, _startSize.Width - deltaX);
                var newTLHeight = Math.Max(MinWindowSize, _startSize.Height - deltaY);
                var actualTLDeltaX = _startSize.Width - newTLWidth;
                var actualTLDeltaY = _startSize.Height - newTLHeight;
                newPosition = new PixelPoint(_startPosition.X + (int)actualTLDeltaX, _startPosition.Y + (int)actualTLDeltaY);
                newWidth = newTLWidth;
                newHeight = newTLHeight;
                break;

            case "TopRight":
                var newTRHeight = Math.Max(MinWindowSize, _startSize.Height - deltaY);
                var actualTRDeltaY = _startSize.Height - newTRHeight;
                newPosition = new PixelPoint(_startPosition.X, _startPosition.Y + (int)actualTRDeltaY);
                newWidth = Math.Max(MinWindowSize, _startSize.Width + deltaX);
                newHeight = newTRHeight;
                break;

            case "BottomLeft":
                var newBLWidth = Math.Max(MinWindowSize, _startSize.Width - deltaX);
                var actualBLDeltaX = _startSize.Width - newBLWidth;
                newPosition = new PixelPoint(_startPosition.X + (int)actualBLDeltaX, _startPosition.Y);
                newWidth = newBLWidth;
                newHeight = Math.Max(MinWindowSize, _startSize.Height + deltaY);
                break;

            case "BottomRight":
                newWidth = Math.Max(MinWindowSize, _startSize.Width + deltaX);
                newHeight = Math.Max(MinWindowSize, _startSize.Height + deltaY);
                break;
        }

        // Apply changes in a single operation to reduce flickering
        Position = newPosition;
        Width = newWidth;
        Height = newHeight;

        e.Handled = true;
    }

    private void Window_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isResizing && _capturingControl != null)
        {
            _isResizing = false;
            _resizeDirection = null;
            e.Pointer.Capture(null);
            _capturingControl = null;
            e.Handled = true;
        }
    }

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.Close();
    }

    private void MinimizeButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void CloseSourceSidebar_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.IsSourceSidebarOpen = false;
            viewModel.SourceSidebarWidth = 0;
        }
    }

    private void ScrollToBottomButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ScrollToBottom();
    }



    private void FloatingButton_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _iconPressed = true;
            _dragStarted = false;
            _iconPressStartTime = DateTime.Now;
            _iconPressPosition = new PixelPoint((int)e.GetPosition(this).X, (int)e.GetPosition(this).Y);
            _originalPressedArgs = e;

            // Capture the pointer to receive move events
            e.Pointer.Capture(sender as Border);
            e.Handled = true;
        }
    }

    private void FloatingButton_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_iconPressed && !_dragStarted && _originalPressedArgs != null)
        {
            var currentPos = new PixelPoint((int)e.GetPosition(this).X, (int)e.GetPosition(this).Y);
            var distance = Math.Sqrt(Math.Pow(currentPos.X - _iconPressPosition.X, 2) +
                                   Math.Pow(currentPos.Y - _iconPressPosition.Y, 2));

            // Start drag if we've moved beyond threshold
            if (distance > DragThreshold)
            {
                _dragStarted = true;
                BeginMoveDrag(_originalPressedArgs);
                e.Handled = true;
            }
        }
    }

    private void FloatingButton_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_iconPressed)
        {
            var duration = DateTime.Now - _iconPressStartTime;

            // If it was a short press without drag, treat as click
            if (!_dragStarted && duration.TotalMilliseconds <= ClickMaxDuration)
            {
                if (this.DataContext is MainWindowViewModel vm)
                {
                    vm.ToggleCollapseCommand.Execute(null);
                }
            }

            _iconPressed = false;
            _dragStarted = false;
            _originalPressedArgs = null;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void RenameTextBox_KeyUp(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is NavigationItem navItem)
        {
            if (this.DataContext is MainWindowViewModel vm)
            {
                if (e.Key == Key.Enter)
                {
                    vm.ConfirmRenameCommand.Execute(navItem);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    vm.CancelRenameCommand.Execute(navItem);
                    e.Handled = true;
                }
            }
        }
    }

    private void DragOver(object? sender, DragEventArgs e)
    {
        // Only allow if the drag contains files
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void Drop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files != null && this.DataContext is MainWindowViewModel vm)
            {
                foreach (var file in files)
                {
                    if (file is IStorageFile storageFile)
                    {
                        try
                        {
                            var fileName = Path.GetFileName(storageFile.Name);

                            // Use the new validation method that respects file type settings
                            vm.TryAddDroppedFile(fileName);
                        }
                        catch (Exception ex)
                        {
                            // Add error message if file couldn't be processed
                            vm.Messages.Add(new ChatMessage
                            {
                                Content = $"Error processing file: {ex.Message}",
                                IsMine = true,
                                Timestamp = DateTime.Now
                            });
                        }
                    }
                }
            }
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
}

