using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MajsoulReview.Models;

namespace MajsoulReview.Services;

public sealed record WindowCapture(BitmapSource Image, IntPtr WindowHandle, string WindowTitle);

public static class ScreenCaptureService
{
    private const int DwmExtendedFrameBounds = 9;
    private const int SourceCopy = 0x00CC0020;
    private const int CaptureBlt = 0x40000000;

    public static WindowCapture CaptureForegroundWindow()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            throw new InvalidOperationException("无法取得当前前台窗口。");
        }

        GetWindowThreadProcessId(window, out var processId);
        if (processId == Environment.ProcessId)
        {
            throw new InvalidOperationException("请先切换到雀魂复盘窗口，再按截图快捷键。");
        }

        if (!TryGetWindowBounds(window, out var bounds) || bounds.Width < 40 || bounds.Height < 40)
        {
            throw new InvalidOperationException("当前窗口不可截图，请确认窗口未最小化。");
        }

        var image = CaptureScreenRegion(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        return new WindowCapture(image, window, GetWindowTitle(window));
    }

    public static BitmapSource Crop(BitmapSource source, NormalizedCrop crop)
    {
        var x = Math.Clamp((int)Math.Round(crop.X * source.PixelWidth), 0, source.PixelWidth - 1);
        var y = Math.Clamp((int)Math.Round(crop.Y * source.PixelHeight), 0, source.PixelHeight - 1);
        var width = Math.Clamp(
            (int)Math.Round(crop.Width * source.PixelWidth),
            1,
            source.PixelWidth - x);
        var height = Math.Clamp(
            (int)Math.Round(crop.Height * source.PixelHeight),
            1,
            source.PixelHeight - y);

        var result = new CroppedBitmap(source, new Int32Rect(x, y, width, height));
        result.Freeze();
        return result;
    }

    public static void SaveJpeg(BitmapSource image, string path, int maxEdge = 1600, int quality = 85)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEdge, 320);
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, 100);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var output = ResizeToLongestEdge(image, maxEdge);
        var converted = new FormatConvertedBitmap(output, PixelFormats.Bgr24, null, 0);
        converted.Freeze();

        var encoder = new JpegBitmapEncoder { QualityLevel = quality };
        encoder.Frames.Add(BitmapFrame.Create(converted));

        var temporaryPath = path + ".tmp";
        try
        {
            using (var stream = File.Create(temporaryPath))
            {
                encoder.Save(stream);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static BitmapSource LoadImage(string path)
    {
        using var stream = File.OpenRead(path);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static BitmapSource CaptureScreenRegion(int x, int y, int width, int height)
    {
        var screenDc = GetDC(IntPtr.Zero);
        var memoryDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, width, height);
        var previous = SelectObject(memoryDc, bitmap);

        try
        {
            if (!BitBlt(memoryDc, 0, 0, width, height, screenDc, x, y, SourceCopy | CaptureBlt))
            {
                throw new InvalidOperationException("屏幕截图失败，请尝试退出全屏模式后重试。");
            }

            var source = Imaging.CreateBitmapSourceFromHBitmap(
                bitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            SelectObject(memoryDc, previous);
            DeleteObject(bitmap);
            DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static BitmapSource ResizeToLongestEdge(BitmapSource source, int maxEdge)
    {
        var longestEdge = Math.Max(source.PixelWidth, source.PixelHeight);
        if (longestEdge <= maxEdge)
        {
            return source;
        }

        var scale = maxEdge / (double)longestEdge;
        var resized = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        resized.Freeze();
        return resized;
    }

    private static bool TryGetWindowBounds(IntPtr window, out NativeRect rect)
    {
        try
        {
            if (DwmGetWindowAttribute(
                    window,
                    DwmExtendedFrameBounds,
                    out rect,
                    Marshal.SizeOf<NativeRect>()) == 0)
            {
                return true;
            }
        }
        catch (DllNotFoundException)
        {
            // GetWindowRect is available on older Windows versions.
        }

        return GetWindowRect(window, out rect);
    }

    private static string GetWindowTitle(IntPtr window)
    {
        var length = GetWindowTextLength(window);
        if (length <= 0)
        {
            return "雀魂复盘";
        }

        var buffer = new StringBuilder(length + 1);
        GetWindowText(window, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr window,
        int attribute,
        out NativeRect value,
        int valueSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr value);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool BitBlt(
        IntPtr destination,
        int destinationX,
        int destinationY,
        int width,
        int height,
        IntPtr source,
        int sourceX,
        int sourceY,
        int operation);
}
