using AngleSharp.Dom;
using AngleSharp.Dom.Css;

namespace WebCrawler.Analysers
{
    public class CssAnalyseArgs : AnalyseArgs
    {
        public CssAnalyseArgs(Document document, CssAnalyserTargets target, ICssNode node)
            : this(document, target, node, null)
        {
        }

        public CssAnalyseArgs(Document document, CssAnalyserTargets target, ICssNode node, IElement element)
            : base(document)
        {
            Target = target;
            Node = node;
            Element = element;
        }

        public CssAnalyserTargets Target { get; }
        public ICssNode Node { get; }
        public IElement Element { get; }
    }
}