using AngleSharp.Dom;

namespace WebCrawler.Analysers
{
    public class HtmlAnalyseArgs : AnalyseArgs
    {
        public HtmlAnalyseArgs(Document document, IDocument htmlDocument) : base(document)
        {
            HtmlDocument = htmlDocument;
        }

        public IDocument HtmlDocument { get; }
    }
}