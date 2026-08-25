using System.Windows;
using System.Windows.Threading;

namespace MajsoulReview.Windows;

public partial class StatusToast : Window
{
    private static readonly HashSet<StatusToast> OpenToasts = [];
    private readonly DispatcherTimer _timer;

    public StatusToast(string title, string body, TimeSpan? duration = null)
    {
        InitializeComponent();
        TitleText.Text = title;
        BodyText.Text = body;

        Loaded += (_, _) =>
        {
            Left = SystemParameters.WorkArea.Right - ActualWidth - 24;
            Top = SystemParameters.WorkArea.Bottom - ActualHeight - 24;
        };

        _timer = new DispatcherTimer { Interval = duration ?? TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Close();
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            OpenToasts.Remove(this);
        };
    }

    public new void Show()
    {
        OpenToasts.Add(this);
        base.Show();
        _timer.Start();
    }

    public static void DismissAll()
    {
        foreach (var toast in OpenToasts.ToArray())
        {
            toast.Close();
        }
    }
}
