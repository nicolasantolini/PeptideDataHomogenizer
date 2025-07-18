using Entities;

namespace PeptideDataHomogenizer.State
{
    public class ArticleBrowserState
    {
        public Dictionary<int, List<ArticlePerProject>> ArticlesByPage { get; } = new();
        public string CurrentQuery { get; set; } = "prion protein molecular dynamics simulation";
        public int PageSize { get; set; } = 5;
        public int PageNr { get; set; } = 1;

        public event Action? OnChange;

        public void NotifyStateChanged() => OnChange?.Invoke();
    }
}