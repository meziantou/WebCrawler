namespace WebCrawler
{
    public class HtmlError
    {
        public string Excerpt { get; set; }
        public int ExcerptPosition { get; set; }
        public int Position { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string Message { get; set; }
        public int Code { get; set; }
    }
}