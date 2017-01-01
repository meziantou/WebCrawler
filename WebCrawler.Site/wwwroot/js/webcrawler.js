"use strict";
var WebCrawler;
(function (WebCrawler) {
    class Crawler {
        constructor() {
            this.elements = {
                urlInput: document.getElementById("Url"),
                urlIncludePatternInput: document.getElementById("UrlIncludePatterns"),
                documentListContainer: document.getElementById("DocumentListContainer"),
                documentList: document.getElementById("DocumentList"),
                documentDetailsContainer: document.getElementById("DocumentDetailsContainer"),
                documentDetails: document.getElementById("DocumentDetails"),
                documentCount: document.getElementById("DocumentCount"),
                startButton: document.getElementById("BtnCrawlStart"),
                stopButton: document.getElementById("BtnCrawlStop"),
                exportJsonButton: document.getElementById("BtnCrawlExport"),
                computeReferencesButton: document.getElementById("BtnCrawlComputeReferences")
            };
            this.socket = null;
            this.documents = [];
            this.references = [];
            this.setButtonVisibility();
            this.registerEvents();
        }
        get isRunning() {
            return this.socket !== null;
        }
        setConfiguration(configuration) {
            if (configuration) {
                if (configuration.url) {
                    this.elements.urlInput.value = configuration.url;
                }
                if (configuration.urlIncludePatterns) {
                    this.elements.urlIncludePatternInput.value = configuration.urlIncludePatterns;
                }
            }
        }
        saveConfiguration(configuration) {
            localStorage.setItem("configuration", JSON.stringify(configuration));
        }
        loadConfiguration() {
            const json = localStorage.getItem("configuration");
            if (json) {
                const conf = JSON.parse(json);
                if (conf) {
                    this.setConfiguration(conf);
                }
            }
        }
        registerEvents() {
            this.elements.startButton.addEventListener("click", e => {
                e.preventDefault();
                this.start();
            });
            this.elements.stopButton.addEventListener("click", e => {
                e.preventDefault();
                this.stop();
            });
            this.elements.computeReferencesButton.addEventListener("click", e => {
                e.preventDefault();
                this.computeReferences();
            });
            this.elements.computeReferencesButton.addEventListener("click", e => {
                e.preventDefault();
                this.computeReferences();
            });
            //this.elements.documentList.addEventListener("click", e => {
            //    e.preventDefault();
            //    if (e.target instanceof Node) {
            //        let node: Node | HTMLElement | null = e.target;
            //        while (node && node instanceof Element) {
            //            const documentId = node.dataset["documentId"];
            //            if (documentId) {
            //                this.selectDocument(documentId);
            //                return;
            //            }
            //            node = node.parentElement;
            //        }
            //    }
            //});
            window.addEventListener("hashchange", this.selectDocumentFromHash.bind(this), false);
        }
        start() {
            if (this.isRunning) {
                return;
            }
            const configuration = {
                url: this.elements.urlInput.value,
                urlIncludePatterns: this.elements.urlIncludePatternInput.value
            };
            this.saveConfiguration(configuration);
            const protocol = location.protocol === "https:" ? "wss:" : "ws:";
            const wsUri = protocol + "//" + window.location.host + "/ws";
            const socket = new WebSocket(wsUri);
            this.socket = socket;
            this.setButtonVisibility();
            socket.onopen = e => {
                console.log("socket opened", e);
                socket.send(JSON.stringify(configuration));
            };
            socket.onclose = e => {
                console.log("socket closed", e);
                this.computeReferences();
                this.socket = null;
                this.setButtonVisibility();
            };
            socket.onmessage = e => {
                let data = JSON.parse(e.data);
                switch (data.type) {
                    case 1:
                        if (data.document) {
                            this.addDocument(data.document);
                        }
                        break;
                    case 2:
                        if (data.documentRef) {
                            this.references.push(data.documentRef);
                        }
                        break;
                    case 3:
                        console.error(data.exception);
                        alert(data.exception);
                        break;
                }
            };
            socket.onerror = e => {
                console.error(e);
            };
        }
        stop() {
            if (this.socket) {
                this.socket.close();
            }
        }
        computeReferences() {
            if (!this.isRunning) {
                return;
            }
            for (let reference of this.references) {
                const sourceDocument = this.documents.find(d => d.id === reference.sourceDocumentId);
                if (sourceDocument) {
                    sourceDocument.references.push(reference);
                }
                const targetDocument = this.documents.find(d => d.id === reference.targetDocumentId);
                if (targetDocument) {
                    targetDocument.referencedBy.push(reference);
                }
            }
        }
        exportJson() {
            const json = JSON.stringify(this.documents);
            const blob = new Blob([json], { type: "text/json" });
            const url = URL.createObjectURL(blob);
            const anchor = document.createElement("a");
            anchor.setAttribute("href", url);
            anchor.setAttribute("download", "export.json");
            anchor.click();
        }
        selectDocumentFromHash() {
            let hash = location.hash;
            if (hash.startsWith("#")) {
                hash = hash.substring(1);
            }
            const params = new URLSearchParams(hash);
            const documentId = params.get("documentId");
            if (documentId) {
                this.selectDocument(documentId);
                return;
            }
            const documentUrl = params.get("documentUrl");
            if (documentUrl) {
                const document = this.documents.find(doc => doc.url === documentUrl);
                if (document) {
                    this.selectDocument(document);
                    return;
                }
            }
            this.selectDocument(null);
        }
        selectDocument(idOrDocument) {
            if (idOrDocument === null) {
                this.elements.documentDetailsContainer.classList.add("hide");
                return;
            }
            if (typeof idOrDocument === "string") {
                const id = idOrDocument;
                const document = this.documents.find(doc => doc.id === id);
                if (document) {
                    this.selectDocument(document);
                    return;
                }
            }
            else {
                const document = idOrDocument;
                this.elements.documentDetails.innerHTML = "";
                const element = this.renderDocument(document, "details");
                this.elements.documentDetails.appendChild(element);
                this.elements.documentDetailsContainer.classList.remove("hide");
            }
        }
        setButtonVisibility() {
            this.elements.urlInput.disabled = this.isRunning;
            this.elements.urlIncludePatternInput.disabled = this.isRunning;
            this.elements.startButton.disabled = this.isRunning;
            this.elements.stopButton.classList.toggle("hide", !this.isRunning);
            this.elements.exportJsonButton.classList.toggle("hide", !this.isRunning);
            this.elements.computeReferencesButton.classList.toggle("hide", !this.isRunning);
        }
        addDocument(document) {
            if (!Array.isArray(document.referencedBy)) {
                document.referencedBy = [];
            }
            if (!Array.isArray(document.references)) {
                document.references = [];
            }
            this.documents.push(document);
            const element = this.renderDocument(document, "list");
            this.elements.documentList.appendChild(element);
            this.elements.documentListContainer.classList.remove("hide");
            this.elements.documentCount.textContent = "" + this.documents.length;
        }
        renderDocument(document, mode) {
            let result;
            if (mode === "list") {
                result =
                    JSX.createElement("div", { className: "document" },
                        JSX.createElement("a", { href: `#documentId=${document.id}` },
                            JSX.createElement("span", { className: `tag tag-${this.getStatusCodeClass(document)}`, title: document.reasonPhrase }, document.statusCode),
                            JSX.createElement("span", { title: document.url }, document.url)));
            }
            else {
                result =
                    JSX.createElement("div", null,
                        JSX.createElement("div", { className: "summary" },
                            JSX.createElement("div", null,
                                JSX.createElement("a", { className: "document-url", href: document.url, target: "_blank" }, document.url)),
                            document.redirectUrl && JSX.createElement("div", null,
                                "\u279C ",
                                JSX.createElement("a", { href: `#documentUrl=${encodeURIComponent(document.redirectUrl)}` }, document.redirectUrl)),
                            JSX.createElement("div", null,
                                JSX.createElement("span", { className: `tag tag-${this.getStatusCodeClass(document)}` }, document.statusCode),
                                " ",
                                document.reasonPhrase)),
                        JSX.createElement("details", { className: document.requestHeaders ? "" : "hide" },
                            JSX.createElement("summary", null, "Request Headers"),
                            JSX.createElement("div", { className: "details" },
                                JSX.createElement("pre", null,
                                    JSX.createElement("code", null, this.formatHeaders(document.requestHeaders))))),
                        JSX.createElement("details", { className: document.responseHeaders ? "" : "hide" },
                            JSX.createElement("summary", null, "Response Headers"),
                            JSX.createElement("div", { className: "details" },
                                JSX.createElement("pre", null,
                                    JSX.createElement("code", null, this.formatHeaders(document.responseHeaders))))),
                        JSX.createElement("details", null,
                            JSX.createElement("summary", null,
                                "References (",
                                document.references.length,
                                ")"),
                            JSX.createElement("div", { className: "details" },
                                JSX.createElement("ul", null, document.references.map(ref => JSX.createElement("li", null,
                                    JSX.createElement("details", null,
                                        JSX.createElement("summary", null,
                                            JSX.createElement("a", { href: `#documentId=${ref.targetDocumentId}` }, ref.targetDocumentUrl)),
                                        JSX.createElement("pre", null,
                                            JSX.createElement("code", null, ref.excerpt)))))))),
                        JSX.createElement("details", null,
                            JSX.createElement("summary", null,
                                "Referenced by (",
                                document.referencedBy.length,
                                ")"),
                            JSX.createElement("div", { className: "details" },
                                JSX.createElement("ul", null, document.referencedBy.map(ref => JSX.createElement("li", null,
                                    JSX.createElement("details", null,
                                        JSX.createElement("summary", null,
                                            JSX.createElement("a", { href: `#documentId=${ref.sourceDocumentId}` }, ref.sourceDocumentUrl)),
                                        JSX.createElement("pre", null,
                                            JSX.createElement("code", null, ref.excerpt)))))))),
                        JSX.createElement("details", { className: document.htmlErrors.length > 0 ? "" : "hide" },
                            JSX.createElement("summary", null,
                                "HTML Errors (",
                                document.htmlErrors.length,
                                ")"),
                            JSX.createElement("div", { className: "details" },
                                JSX.createElement("ul", null, document.htmlErrors.map(htmlError => JSX.createElement("li", null,
                                    JSX.createElement("details", null,
                                        JSX.createElement("summary", null,
                                            "Line ",
                                            htmlError.line,
                                            ", Column ",
                                            htmlError.column,
                                            ": ",
                                            htmlError.message),
                                        JSX.createElement("pre", null,
                                            JSX.createElement("code", null, this.getErrorExcerpt(htmlError))))))))));
            }
            return result;
        }
        getErrorExcerpt(error) {
            const excerptPosition = error.excerptPosition - 1;
            const before = this.replaceNewLines(error.excerpt.substring(0, excerptPosition));
            const current = this.replaceNewLines(error.excerpt.substring(excerptPosition, excerptPosition + 1));
            const after = this.replaceNewLines(error.excerpt.substring(excerptPosition + 1));
            return JSX.createElement("span", null,
                before,
                JSX.createElement("span", { className: "code-error" }, current),
                after);
        }
        replaceNewLines(str) {
            return str
                .replace(/\r\n/g, "↩")
                .replace(/\r|\n/g, "↩");
        }
        getStatusCodeClass(document) {
            const statusCode = document.statusCode;
            if (this.isStatusCodeOk(statusCode)) {
                return "success";
            }
            if (this.isStatusCodeRedirect(statusCode)) {
                return "info";
            }
            if (this.isStatusCodeError(statusCode)) {
                return "danger";
            }
            return "";
        }
        isStatusCodeOk(statusCode) {
            return statusCode >= 200 && statusCode < 300;
        }
        isStatusCodeRedirect(statusCode) {
            return statusCode >= 300 && statusCode < 400;
        }
        isStatusCodeError(statusCode) {
            return statusCode < 200 || statusCode >= 400;
        }
        formatHeaders(headers) {
            let result = "";
            if (headers) {
                for (let key of Object.keys(headers)) {
                    const value = headers[key];
                    if (result !== "") {
                        result += "\n";
                    }
                    result += `${key}: ${value}`;
                }
            }
            return result;
        }
    }
    WebCrawler.Crawler = Crawler;
})(WebCrawler || (WebCrawler = {}));
var JSX;
(function (JSX) {
    function createElement(tagName, attributes, ...children) {
        const element = document.createElement(tagName);
        if (attributes) {
            for (let key of Object.keys(attributes)) {
                if (key === "className") {
                    element.setAttribute("class", attributes[key]);
                }
                else {
                    element.setAttribute(key, attributes[key]);
                }
            }
        }
        for (let child of children) {
            appendChild(element, child);
        }
        return element;
    }
    JSX.createElement = createElement;
    function appendChild(parent, child) {
        if (typeof child === "undefined" || child === null) {
            return;
        }
        if (Array.isArray(child)) {
            for (let value of child) {
                appendChild(parent, value);
            }
        }
        else if (typeof child === "string") {
            parent.appendChild(document.createTextNode(child));
        }
        else if (child instanceof Node) {
            parent.appendChild(child);
        }
        else {
            parent.appendChild(document.createTextNode(String(child)));
        }
    }
})(JSX || (JSX = {}));
//# sourceMappingURL=webcrawler.js.map