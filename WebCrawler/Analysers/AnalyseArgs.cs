namespace WebCrawler.Analysers
{
    public class AnalyseArgs
    {
        public AnalyseArgs(Document document)
        {
            Document = document;
        }

        public Document Document { get; }
    }
}