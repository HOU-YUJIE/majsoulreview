namespace MajsoulReview.Models;

public sealed class ReviewCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string QuestionImagePath { get; set; } = string.Empty;
    public string AnswerImagePath { get; set; } = string.Empty;
    public string Category { get; set; } = "牌效";
    public string Tags { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string SourceWindowTitle { get; set; } = string.Empty;
    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public string DisplayTitle => $"{Category}错题";

    public string DisplayMeta => $"{CreatedAt:yyyy-MM-dd HH:mm}  {Category}";

    public string DisplayStats => $"做对 {CorrectCount}  ·  做错 {WrongCount}";
}
