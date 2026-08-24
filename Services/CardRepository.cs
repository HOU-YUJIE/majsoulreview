using MajsoulReview.Models;

namespace MajsoulReview.Services;

public sealed class CardRepository
{
    private readonly List<ReviewCard> _cards;

    public CardRepository()
    {
        AppPaths.EnsureCreated();
        _cards = JsonStorage.Load(AppPaths.CardsFile, new List<ReviewCard>());
    }

    public IReadOnlyList<ReviewCard> GetAll() => _cards
        .OrderByDescending(card => card.CreatedAt)
        .ToList();

    public void Add(ReviewCard card)
    {
        _cards.Add(card);
        Save();
    }

    public void Update(ReviewCard card)
    {
        var index = _cards.FindIndex(item => item.Id == card.Id);
        if (index < 0)
        {
            throw new InvalidOperationException("找不到要更新的错题。");
        }

        card.UpdatedAt = DateTime.Now;
        _cards[index] = card;
        Save();
    }

    public void Delete(ReviewCard card)
    {
        _cards.RemoveAll(item => item.Id == card.Id);
        Save();

        var imageDirectory = Path.Combine(AppPaths.Images, card.Id.ToString("N"));
        if (Directory.Exists(imageDirectory))
        {
            Directory.Delete(imageDirectory, recursive: true);
        }
    }

    private void Save() => JsonStorage.Save(AppPaths.CardsFile, _cards);
}
