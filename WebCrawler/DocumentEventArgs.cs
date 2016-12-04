using System;

namespace WebCrawler
{
    public class DocumentEventArgs : EventArgs
    {
        public Document Document { get; }

        public DocumentEventArgs(Document document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }
    }
}