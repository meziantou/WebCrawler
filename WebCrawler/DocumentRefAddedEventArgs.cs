using System;

namespace WebCrawler
{
    public class DocumentRefAddedEventArgs : EventArgs
    {
        public DocumentRef DocumentRef { get; set; }

        public DocumentRefAddedEventArgs(DocumentRef documentRef)
        {
            DocumentRef = documentRef ?? throw new ArgumentNullException(nameof(documentRef));
        }
    }
}