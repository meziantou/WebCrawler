class WebCrawler {
    constructor(options) {
        this.url = options.url;
        this.documentTemplate = options.documentTemplate;
        this.container = options.container;
        this.onFinishedHandler = options.onFinished;

        this.documents = [];
        this.references = [];
    }

    start() {
        //console.log("start url");

        let wsUri = "ws://" + window.location.host + "/ws";
        let socket = new WebSocket(wsUri);
        socket.onopen = e => {
            console.log("socket opened");
            socket.send(JSON.stringify({ url: this.url }));
        };

        socket.onclose = e => {
            console.log("socket closed");

            this.computeReferences();
            this.renderAll();

            if (this.onFinishedHandler) {
                this.onFinishedHandler(this);
            }
        };

        socket.onmessage = e => {
            //console.log(e.data);
            let data = JSON.parse(e.data);
            if (data.Type === 1) {
                this.documents.push(data.Document);
                this.render(data.Document);
            } /*else if (data.Type === 2) {
                // Find & Replace doc
                let index = this.documents.findIndex(d => d.Id === data.Document.Id);
                if (index >= 0) {
                    this.documents[index] = data.Document;
                    //this.render(data.Document);
                }
            }*/ else if (data.Type === 3) {
                console.error(data.Exception);
                alert(data.Exception);
            } else if (data.Type === 4) {
                this.references.push(data.DocumentRef);
            }
        };

        socket.onerror = e => {
            console.error(e.data);
        };

        this.socket = socket;
    }

    stop() {
        this.socket.close();
    }

    renderAll() {
        this.container.innerHTML = "";
        for (let doc of this.documents) {
            let div = document.createElement("div");
            div.innerHTML = this.documentTemplate(doc);
            while (div.childNodes.length > 0) {
                this.container.appendChild(div.childNodes[0]);
            }
        }
    }

    render(doc) {
        let existingItem = document.querySelector("[data-document-id='" + doc.Id + "']");
        let div = document.createElement("div");
        div.innerHTML = this.documentTemplate(doc);
        while (div.childNodes.length > 0) {
            if (existingItem) {
                existingItem.parentElement.insertBefore(div.childNodes[0], existingItem);
            } else {
                this.container.appendChild(div.childNodes[0]);
            }
        }

        if (existingItem) {
            existingItem.remove();
        }
    }

    computeReferences() {
        for (let reference of this.references) {
            let sourceDocument = this.documents.find(d => d.Id === reference.SourceDocumentId);
            if (sourceDocument) {
                if (!Array.isArray(sourceDocument.References)) {
                    sourceDocument.References = [];
                }

                sourceDocument.References.push(reference);
            }

            let targetDocument = this.documents.find(d => d.Id === reference.TargetDocumentId);
            if (targetDocument) {
                if (!Array.isArray(targetDocument.ReferencedBy)) {
                    targetDocument.ReferencedBy = [];
                }

                targetDocument.ReferencedBy.push(reference);
            }
        }
    }
}