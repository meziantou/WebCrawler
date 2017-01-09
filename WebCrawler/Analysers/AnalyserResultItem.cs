namespace WebCrawler.Analysers
{
    public class AnalyserResultItem
    {
        public AnalyserResultType Type { get; set; }
        public string Message { get; set; }
        public string FullMessage { get; set; }
        public string Category { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string Excerpt { get; set; }
        public string DocumentationUrl { get; set; }
    }
}