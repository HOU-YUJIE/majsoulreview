using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
using MajsoulReview.Models;

namespace MajsoulReview.Windows;

public partial class CardEditorWindow : Window
{
    private BitmapSource _question;
    private BitmapSource _answer;
    private readonly ObservableCollection<string> _categories;

    public CardEditorWindow(
        BitmapSource question,
        BitmapSource answer,
        IEnumerable<string> categories,
        ReviewCard? existing = null)
    {
        InitializeComponent();
        _question = question;
        _answer = answer;
        _categories = new ObservableCollection<string>(categories);
        CategoryBox.ItemsSource = _categories;
        CategoryBox.SelectedIndex = 0;
        RefreshImages();

        if (existing is not null)
        {
            SelectCategory(existing.Category);
            TagsBox.Text = existing.Tags;
            NoteBox.Text = existing.Note;
        }
    }

    public BitmapSource Question => _question;
    public BitmapSource Answer => _answer;
    public string Category => CategoryBox.Text.Trim();
    public IReadOnlyList<string> Categories => _categories.ToList();
    public string Tags => TagsBox.Text.Trim();
    public string Note => NoteBox.Text.Trim();

    private void SelectCategory(string category)
    {
        CategoryBox.Text = category;
    }

    private void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        var category = CategoryBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(category))
        {
            MessageBox.Show("请先在分类框中输入分类名称。", "添加分类", MessageBoxButton.OK, MessageBoxImage.Information);
            CategoryBox.Focus();
            return;
        }

        var existing = _categories.FirstOrDefault(item =>
            string.Equals(item, category, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _categories.Add(category);
            CategoryBox.SelectedItem = category;
        }
        else
        {
            CategoryBox.SelectedItem = existing;
        }
    }

    private void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        var category = CategoryBox.Text.Trim();
        var existing = _categories.FirstOrDefault(item =>
            string.Equals(item, category, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return;
        }

        if (_categories.Count == 1)
        {
            MessageBox.Show("至少需要保留一个分类。", "删除分类", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _categories.Remove(existing);
        CategoryBox.SelectedIndex = 0;
    }

    private void Swap_Click(object sender, RoutedEventArgs e)
    {
        (_question, _answer) = (_answer, _question);
        RefreshImages();
    }

    private void RefreshImages()
    {
        QuestionImage.Source = _question;
        AnswerImage.Source = _answer;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Category))
        {
            MessageBox.Show("请选择或输入一个分类。", "无法保存", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_categories.Any(item => string.Equals(item, Category, StringComparison.OrdinalIgnoreCase)))
        {
            _categories.Add(Category);
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
