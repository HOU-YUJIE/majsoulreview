using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MajsoulReview.Models;

namespace MajsoulReview.Windows;

public partial class CropWindow : Window
{
    private readonly BitmapSource _source;
    private Point _dragStart;
    private bool _dragging;
    private Rect _selection;

    public CropWindow(BitmapSource source)
    {
        InitializeComponent();
        _source = source;
        PreviewImage.Source = source;
    }

    public NormalizedCrop? SelectedCrop { get; private set; }

    private void ImageHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(ImageHost);
        var imageRect = GetRenderedImageRect();
        if (!imageRect.Contains(point))
        {
            return;
        }

        _dragStart = ClampToRect(point, imageRect);
        _dragging = true;
        ImageHost.CaptureMouse();
        UpdateSelection(_dragStart);
    }

    private void ImageHost_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        UpdateSelection(ClampToRect(e.GetPosition(ImageHost), GetRenderedImageRect()));
    }

    private void ImageHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        ImageHost.ReleaseMouseCapture();
        UpdateSelection(ClampToRect(e.GetPosition(ImageHost), GetRenderedImageRect()));
    }

    private void UpdateSelection(Point current)
    {
        _selection = new Rect(_dragStart, current);
        Canvas.SetLeft(SelectionRectangle, _selection.Left);
        Canvas.SetTop(SelectionRectangle, _selection.Top);
        SelectionRectangle.Width = _selection.Width;
        SelectionRectangle.Height = _selection.Height;
        SelectionRectangle.Visibility = Visibility.Visible;

        var valid = _selection.Width >= 40 && _selection.Height >= 40;
        ConfirmButton.IsEnabled = valid;
        SelectionText.Text = valid
            ? $"选择区域：{Math.Round(_selection.Width)} x {Math.Round(_selection.Height)}"
            : "区域过小，请重新框选";
    }

    private Rect GetRenderedImageRect()
    {
        var hostWidth = ImageHost.ActualWidth;
        var hostHeight = ImageHost.ActualHeight;
        var imageAspect = (double)_source.PixelWidth / _source.PixelHeight;
        var hostAspect = hostWidth / hostHeight;

        double width;
        double height;
        if (hostAspect > imageAspect)
        {
            height = hostHeight;
            width = height * imageAspect;
        }
        else
        {
            width = hostWidth;
            height = width / imageAspect;
        }

        return new Rect((hostWidth - width) / 2, (hostHeight - height) / 2, width, height);
    }

    private static Point ClampToRect(Point point, Rect rect) => new(
        Math.Clamp(point.X, rect.Left, rect.Right),
        Math.Clamp(point.Y, rect.Top, rect.Bottom));

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var imageRect = GetRenderedImageRect();
        SelectedCrop = new NormalizedCrop(
            (_selection.Left - imageRect.Left) / imageRect.Width,
            (_selection.Top - imageRect.Top) / imageRect.Height,
            _selection.Width / imageRect.Width,
            _selection.Height / imageRect.Height);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }
}
