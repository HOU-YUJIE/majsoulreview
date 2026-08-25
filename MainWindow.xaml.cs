using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MajsoulReview.Models;
using MajsoulReview.Services;
using MajsoulReview.Windows;
using Microsoft.Win32;

namespace MajsoulReview;

public partial class MainWindow : Window
{
    private readonly CardRepository _cards = new();
    private readonly SettingsRepository _settingsRepository = new();
    private readonly HotkeyService _hotkey = new();
    private readonly ObservableCollection<ReviewCard> _visibleCards = [];

    private AppSettings _settings;
    private BitmapSource? _pendingQuestion;
    private NormalizedCrop? _pendingCrop;
    private string _pendingSourceTitle = string.Empty;
    private bool _captureBusy;
    private bool _showingAnswer;
    private bool _gradedThisView;
    private string _recordedHotkey = "F8";
    private bool _recordedControl;
    private bool _recordedAlt;
    private bool _recordedShift;
    private bool _recordedWindows;
    private bool _hotkeySuspendedForRecording;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsRepository.Load();
        CardsList.ItemsSource = _visibleCards;
        LoadSettingsControls();
        ReloadCards();
        _hotkey.Pressed += (_, _) => Dispatcher.InvokeAsync(HandleCaptureHotkey);
    }

    private ReviewCard? SelectedCard => CardsList.SelectedItem as ReviewCard;

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _hotkey.Initialize(new WindowInteropHelper(this).Handle);
        RegisterCurrentHotkey(showError: true);
    }

    private void Window_Closing(object? sender, CancelEventArgs e) => _hotkey.Dispose();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && ExpandedImageView.Visibility == Visibility.Visible)
        {
            CloseExpandedImage();
            e.Handled = true;
        }
    }

    private void LoadSettingsControls()
    {
        _recordedHotkey = _settings.Hotkey;
        _recordedControl = _settings.UseControl;
        _recordedAlt = _settings.UseAlt;
        _recordedShift = _settings.UseShift;
        _recordedWindows = _settings.UseWindows;
        HotkeyInput.Text = _settings.HotkeyDisplay;
        UpdateStatus();
    }

    private void HotkeyInput_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _hotkey.Unregister();
        _hotkeySuspendedForRecording = true;
        HotkeyInput.Text = "请按下新的快捷键...";
    }

    private void HotkeyInput_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        HotkeyInput.Text = CreateRecordedSettings().HotkeyDisplay;
        if (_hotkeySuspendedForRecording)
        {
            RegisterCurrentHotkey(showError: false);
            _hotkeySuspendedForRecording = false;
        }
    }

    private void HotkeyInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifierKey(key))
        {
            HotkeyInput.Text = "继续按下一个主键...";
            return;
        }

        if (key is Key.None or Key.ImeProcessed or Key.DeadCharProcessed)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        _recordedHotkey = key.ToString();
        _recordedControl = modifiers.HasFlag(ModifierKeys.Control);
        _recordedAlt = modifiers.HasFlag(ModifierKeys.Alt);
        _recordedShift = modifiers.HasFlag(ModifierKeys.Shift);
        _recordedWindows = modifiers.HasFlag(ModifierKeys.Windows);

        HotkeyInput.Text = CreateRecordedSettings().HotkeyDisplay;
        TryApplyRecordedHotkey(showSuccess: false);
        Keyboard.ClearFocus();
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin;

    private AppSettings CreateRecordedSettings() => new()
    {
        Hotkey = _recordedHotkey,
        UseControl = _recordedControl,
        UseAlt = _recordedAlt,
        UseShift = _recordedShift,
        UseWindows = _recordedWindows,
        LastCrop = _settings.LastCrop,
        Categories = [.. _settings.Categories]
    };

    private void ApplyHotkey_Click(object sender, RoutedEventArgs e)
    {
        TryApplyRecordedHotkey(showSuccess: true);
    }

    private bool TryApplyRecordedHotkey(bool showSuccess)
    {
        var previous = _settings;
        _settings = CreateRecordedSettings();

        if (!RegisterCurrentHotkey(showError: false))
        {
            _settings = previous;
            LoadSettingsControls();
            RegisterCurrentHotkey(showError: false);
            _hotkeySuspendedForRecording = false;
            MessageBox.Show(
                "该快捷键已被其他程序占用，请换一个组合。",
                "快捷键不可用",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        _settingsRepository.Save(_settings);
        _hotkeySuspendedForRecording = false;
        HotkeyInput.Text = _settings.HotkeyDisplay;
        UpdateStatus(showSuccess ? "快捷键设置已保存" : $"快捷键已更新为 {_settings.HotkeyDisplay}");
        return true;
    }

    private bool RegisterCurrentHotkey(bool showError)
    {
        var registered = _hotkey.Register(_settings);
        if (!registered && showError)
        {
            MessageBox.Show(
                $"无法注册快捷键 {_settings.HotkeyDisplay}，请在左下角更换组合。",
                "快捷键不可用",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        UpdateStatus();
        return registered;
    }

    private void StartCapture_Click(object sender, RoutedEventArgs e)
    {
        ResetCaptureSession();
        WindowState = WindowState.Minimized;
        ShowToast("准备捕获错误一打", $"切换到雀魂错误一打界面，按 {_settings.HotkeyDisplay}");
    }

    private void ResetCrop_Click(object sender, RoutedEventArgs e)
    {
        _settings.LastCrop = null;
        _settingsRepository.Save(_settings);
        ResetCaptureSession();
        UpdateStatus("截图区域已清除，下次第一张截图时会重新框选");
    }

    private async void HandleCaptureHotkey()
    {
        if (_captureBusy)
        {
            return;
        }

        _captureBusy = true;
        try
        {
            StatusToast.DismissAll();
            await Task.Delay(100);

            var capture = ScreenCaptureService.CaptureForegroundWindow();
            if (_pendingQuestion is null)
            {
                CaptureQuestion(capture);
            }
            else
            {
                CaptureAnswer(capture);
            }
        }
        catch (Exception exception)
        {
            ShowToast("截图未完成", exception.Message, seconds: 4);
            UpdateStatus(exception.Message);
        }
        finally
        {
            _captureBusy = false;
        }
    }

    private void CaptureQuestion(WindowCapture capture)
    {
        var crop = _settings.LastCrop;
        if (crop is null)
        {
            WindowState = WindowState.Normal;
            Show();
            Activate();
            var cropWindow = new CropWindow(capture.Image) { Owner = this };
            if (cropWindow.ShowDialog() != true || cropWindow.SelectedCrop is null)
            {
                UpdateStatus("已取消截图");
                return;
            }

            crop = cropWindow.SelectedCrop;
            _settings.LastCrop = crop;
            _settingsRepository.Save(_settings);
        }

        _pendingCrop = crop;
        _pendingQuestion = ScreenCaptureService.Crop(capture.Image, crop);
        _pendingSourceTitle = capture.WindowTitle;
        WindowState = WindowState.Minimized;
        ShowToast("错误一打已保存", $"切换到正确一打，再按 {_settings.HotkeyDisplay}");
        UpdateStatus("等待第二张截图：正确一打");
    }

    private void CaptureAnswer(WindowCapture capture)
    {
        if (_pendingQuestion is null || _pendingCrop is null)
        {
            ResetCaptureSession();
            return;
        }

        var answer = ScreenCaptureService.Crop(capture.Image, _pendingCrop);
        WindowState = WindowState.Normal;
        Show();
        Activate();

        var editor = new CardEditorWindow(_pendingQuestion, answer, _settings.Categories) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            SaveNewCard(editor);
        }

        ResetCaptureSession();
    }

    private void SaveNewCard(CardEditorWindow editor)
    {
        SaveCategories(editor.Categories);
        var card = new ReviewCard
        {
            Category = editor.Category,
            Tags = editor.Tags,
            Note = editor.Note,
            SourceWindowTitle = _pendingSourceTitle
        };

        var directory = Path.Combine(AppPaths.Images, card.Id.ToString("N"));
        card.QuestionImagePath = Path.Combine(directory, "question.jpg");
        card.AnswerImagePath = Path.Combine(directory, "answer.jpg");
        ScreenCaptureService.SaveJpeg(editor.Question, card.QuestionImagePath);
        ScreenCaptureService.SaveJpeg(editor.Answer, card.AnswerImagePath);
        _cards.Add(card);
        ReloadCards(card.Id);
        UpdateStatus("错题已保存");
    }

    private void ReloadCards(Guid? selectId = null)
    {
        var query = SearchBox?.Text?.Trim() ?? string.Empty;
        var filtered = _cards.GetAll().Where(card =>
            string.IsNullOrWhiteSpace(query) ||
            card.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            card.Tags.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            card.Note.Contains(query, StringComparison.OrdinalIgnoreCase));

        var previousId = selectId ?? SelectedCard?.Id;
        _visibleCards.Clear();
        foreach (var card in filtered)
        {
            _visibleCards.Add(card);
        }

        CardsList.SelectedItem = previousId is Guid id
            ? _visibleCards.FirstOrDefault(card => card.Id == id)
            : _visibleCards.FirstOrDefault();

        if (CardsList.SelectedItem is null)
        {
            ShowEmptyState();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ReloadCards();

    private void CardsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _showingAnswer = false;
        _gradedThisView = false;
        RefreshSelectedCard();
    }

    private void RefreshSelectedCard()
    {
        var card = SelectedCard;
        if (card is null)
        {
            ShowEmptyState();
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        CardView.Visibility = Visibility.Visible;
        CardTitle.Text = card.DisplayTitle;
        CardMeta.Text = $"{card.DisplayMeta}  来源：{card.SourceWindowTitle}";
        CardTags.Text = string.IsNullOrWhiteSpace(card.Tags) ? card.Category : $"{card.Category}  ·  {card.Tags}";
        CardReviewStats.Text = card.DisplayStats;
        CardNote.Text = string.IsNullOrWhiteSpace(card.Note) ? "未添加注释" : card.Note;
        RefreshCardImage(card);
    }

    private void RefreshCardImage(ReviewCard card)
    {
        var path = _showingAnswer ? card.AnswerImagePath : card.QuestionImagePath;
        CardImage.Source = File.Exists(path) ? ScreenCaptureService.LoadImage(path) : null;
        ImageBadgeText.Text = _showingAnswer ? "正确一打" : "错误一打";
        ImageBadge.Background = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(_showingAnswer ? "#147D64" : "#D9A52B"));
        ImageBadgeText.Foreground = _showingAnswer ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D2528"));
        RevealButton.Content = _showingAnswer ? "返回错误一打" : "显示正确一打";
        CardNote.Visibility = _showingAnswer ? Visibility.Visible : Visibility.Collapsed;
        ReviewActions.Visibility = _showingAnswer ? Visibility.Visible : Visibility.Collapsed;
        CorrectButton.IsEnabled = !_gradedThisView;
        WrongButton.IsEnabled = !_gradedThisView;

        if (ExpandedImageView.Visibility == Visibility.Visible)
        {
            RefreshExpandedCard(card);
        }
    }

    private void ShowEmptyState()
    {
        EmptyState.Visibility = Visibility.Visible;
        CardView.Visibility = Visibility.Collapsed;
    }

    private void Reveal_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCard is not { } card)
        {
            return;
        }

        _showingAnswer = !_showingAnswer;
        RefreshCardImage(card);
    }

    private void ExpandImage_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCard is not { } card || CardImage.Source is null)
        {
            return;
        }

        ExpandedImageView.Visibility = Visibility.Visible;
        RefreshExpandedCard(card);
        ExpandedRevealButton.Focus();
    }

    private void RefreshExpandedCard(ReviewCard card)
    {
        ExpandedCardImage.Source = CardImage.Source;
        ExpandedCardTitle.Text = card.DisplayTitle;
        ExpandedImageBadgeText.Text = ImageBadgeText.Text;
        ExpandedImageBadge.Background = ImageBadge.Background;
        ExpandedImageBadgeText.Foreground = ImageBadgeText.Foreground;
        ExpandedCardReviewStats.Text = card.DisplayStats;
        ExpandedCardNote.Text = string.IsNullOrWhiteSpace(card.Note) ? "未添加注释" : card.Note;
        ExpandedCardNote.Visibility = _showingAnswer ? Visibility.Visible : Visibility.Collapsed;
        ExpandedReviewActions.Visibility = _showingAnswer ? Visibility.Visible : Visibility.Collapsed;
        ExpandedRevealButton.Content = RevealButton.Content;
        ExpandedCorrectButton.IsEnabled = !_gradedThisView;
        ExpandedWrongButton.IsEnabled = !_gradedThisView;
    }

    private void CloseExpandedImage_Click(object sender, RoutedEventArgs e) => CloseExpandedImage();

    private void CloseExpandedImage()
    {
        ExpandedImageView.Visibility = Visibility.Collapsed;
        ExpandedCardImage.Source = null;
    }

    private void CorrectAnswer_Click(object sender, RoutedEventArgs e) => RecordReview(correct: true);

    private void WrongAnswer_Click(object sender, RoutedEventArgs e) => RecordReview(correct: false);

    private void RecordReview(bool correct)
    {
        if (SelectedCard is not { } card || !_showingAnswer || _gradedThisView)
        {
            return;
        }

        if (correct)
        {
            card.CorrectCount++;
        }
        else
        {
            card.WrongCount++;
        }

        _cards.Update(card);
        ReloadCards(card.Id);
        _gradedThisView = true;
        RefreshSelectedCard();
        var result = correct ? "已记录：这次做对了" : "已记录：这次做错了";
        UpdateStatus(MoveToNextCard() ? $"{result}，已切换到下一题" : $"{result}，已是最后一题");
    }

    private void PreviousCard_Click(object sender, RoutedEventArgs e)
    {
        if (CardsList.SelectedIndex > 0)
        {
            CardsList.SelectedIndex--;
            CardsList.ScrollIntoView(CardsList.SelectedItem);
        }
    }

    private void NextCard_Click(object sender, RoutedEventArgs e)
    {
        MoveToNextCard();
    }

    private bool MoveToNextCard()
    {
        if (CardsList.SelectedIndex >= 0 && CardsList.SelectedIndex < CardsList.Items.Count - 1)
        {
            CardsList.SelectedIndex++;
            CardsList.ScrollIntoView(CardsList.SelectedItem);
            return true;
        }

        return false;
    }

    private void EditCard_Click(object sender, RoutedEventArgs e)
    {
        var card = SelectedCard;
        if (card is null || !File.Exists(card.QuestionImagePath) || !File.Exists(card.AnswerImagePath))
        {
            return;
        }

        var editor = new CardEditorWindow(
            ScreenCaptureService.LoadImage(card.QuestionImagePath),
            ScreenCaptureService.LoadImage(card.AnswerImagePath),
            _settings.Categories,
            card)
        {
            Owner = this
        };

        if (editor.ShowDialog() != true)
        {
            return;
        }

        card.Category = editor.Category;
        card.Tags = editor.Tags;
        card.Note = editor.Note;
        var previousQuestionPath = card.QuestionImagePath;
        var previousAnswerPath = card.AnswerImagePath;
        var directory = Path.Combine(AppPaths.Images, card.Id.ToString("N"));
        card.QuestionImagePath = Path.Combine(directory, "question.jpg");
        card.AnswerImagePath = Path.Combine(directory, "answer.jpg");
        ScreenCaptureService.SaveJpeg(editor.Question, card.QuestionImagePath);
        ScreenCaptureService.SaveJpeg(editor.Answer, card.AnswerImagePath);
        SaveCategories(editor.Categories);
        _cards.Update(card);
        DeleteReplacedImage(previousQuestionPath, card.QuestionImagePath);
        DeleteReplacedImage(previousAnswerPath, card.AnswerImagePath);
        ReloadCards(card.Id);
        UpdateStatus("错题修改已保存");
    }

    private void DeleteCard_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCard is not { } card)
        {
            return;
        }

        var result = MessageBox.Show(
            "确定删除这道错题及两张截图吗？此操作无法撤销。",
            "删除错题",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _cards.Delete(card);
        ReloadCards();
        UpdateStatus("错题已删除");
    }

    private void OpenData_Click(object sender, RoutedEventArgs e)
    {
        AppPaths.EnsureCreated();
        Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.Root) { UseShellExecute = true });
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        AppPaths.EnsureCreated();
        var dialog = new SaveFileDialog
        {
            Title = "导出错题集备份",
            Filter = "ZIP 备份 (*.zip)|*.zip",
            FileName = $"雀魂错题集-{DateTime.Now:yyyyMMdd-HHmm}.zip"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var exportPath = Path.GetFullPath(dialog.FileName);
        var dataRoot = Path.GetFullPath(AppPaths.Root) + Path.DirectorySeparatorChar;
        if (exportPath.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                "备份文件不能保存在错题集数据目录内部，请选择其他位置。",
                "无法导出",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (File.Exists(exportPath))
        {
            File.Delete(exportPath);
        }

        ZipFile.CreateFromDirectory(AppPaths.Root, exportPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        UpdateStatus("备份已导出");
    }

    private void ResetCaptureSession()
    {
        _pendingQuestion = null;
        _pendingCrop = null;
        _pendingSourceTitle = string.Empty;
        UpdateStatus();
    }

    private static void DeleteReplacedImage(string previousPath, string currentPath)
    {
        if (!string.Equals(previousPath, currentPath, StringComparison.OrdinalIgnoreCase) && File.Exists(previousPath))
        {
            File.Delete(previousPath);
        }
    }

    private void SaveCategories(IReadOnlyList<string> categories)
    {
        _settings.Categories = [.. categories];
        _settings.Normalize();
        _settingsRepository.Save(_settings);
    }

    private void UpdateStatus(string? message = null)
    {
        if (StatusText is null)
        {
            return;
        }

        StatusText.Text = message ?? $"快捷键：{_settings.HotkeyDisplay}  ·  {_cards.GetAll().Count} 道错题";
    }

    private static void ShowToast(string title, string body, int seconds = 3)
    {
        new StatusToast(title, body, TimeSpan.FromSeconds(seconds)).Show();
    }
}
