namespace WebCrawler
{
    public class DiscoveredUrl
    {
        public string Url { get; set; }
        public string Language { get; set; }
        public Document SourceDocument { get; set; }
        public string Excerpt { get; set; }
        public bool IsRedirect { get; set; }

        public bool IsSame(Document document)
        {
            if (document == null)
                return false;

            return document.Url == Url && document.Language == Language;
        }
    }
}