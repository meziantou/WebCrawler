using AngleSharp.Dom.Css;

namespace WebCrawler.Analysers
{
    public class CssAnalyseArgs : AnalyseArgs
    {
        public CssAnalyseArgs(Document document, ICssStyleSheet styleSheet) : base(document)
        {
            StyleSheet = styleSheet;
        }

        public ICssStyleSheet StyleSheet { get; }
    }
}