namespace WebCrawler {
    interface Document {
        crawledOn: Date;
        language: string;
        requestHeaders: Headers | null;
        responseHeaders: Headers | null;
        id: string;
        redirectUrl: string;
        statusCode: number;
        url: string;
        errorMessage: string;
        fullErrorMessage: string;
        reasonPhrase: string;
        referencedBy: DocumentRef[];
        references: DocumentRef[];
        htmlErrors: HtmlError[];
    }

    interface DocumentRef {
        sourceDocumentId: string;
        targetDocumentId: string;
        sourceDocumentUrl: string;
        targetDocumentUrl: string;
        excerpt: string;
    }

    interface HtmlError {
        excerpt: string;
        excerptPosition: number;
        position: number;
        line: number;
        column: number;
        message: string;
        code: number;
    }

    interface SocketMessage {
        type: number;
        document?: Document;
        exception?: any;
        documentRef?: DocumentRef;
    }

    interface Headers {
        [name: string]: string;
    }

    interface Configuration {
        url?: string;
        urlIncludePatterns?: string;
    }

    export class Crawler {
        private elements = {
            urlInput: document.getElementById("Url") as HTMLInputElement,
            urlIncludePatternInput: document.getElementById("UrlIncludePatterns") as HTMLTextAreaElement,

            documentListContainer: document.getElementById("DocumentListContainer") as HTMLElement,
            documentList: document.getElementById("DocumentList") as HTMLElement,
            documentDetailsContainer: document.getElementById("DocumentDetailsContainer") as HTMLElement,
            documentDetails: document.getElementById("DocumentDetails") as HTMLElement,
            documentCount: document.getElementById("DocumentCount") as HTMLElement,

            startButton: document.getElementById("BtnCrawlStart") as HTMLButtonElement,
            stopButton: document.getElementById("BtnCrawlStop") as HTMLButtonElement,
            exportJsonButton: document.getElementById("BtnCrawlExport") as HTMLButtonElement,
            computeReferencesButton: document.getElementById("BtnCrawlComputeReferences") as HTMLButtonElement
        };

        private socket: WebSocket | null = null;
        private documents: Document[] = [];
        private references: DocumentRef[] = [];

        private get isRunning() {
            return this.socket !== null;
        }

        constructor() {
            this.setButtonVisibility();
            this.registerEvents();
        }

        private setConfiguration(configuration: Configuration | null | undefined) {
            if (configuration) {
                if (configuration.url) {
                    this.elements.urlInput.value = configuration.url;
                }

                if (configuration.urlIncludePatterns) {
                    this.elements.urlIncludePatternInput.value = configuration.urlIncludePatterns;
                }
            }
        }

        private saveConfiguration(configuration: Configuration) {
            localStorage.setItem("configuration", JSON.stringify(configuration));
        }

        public loadConfiguration() {
            const json = localStorage.getItem("configuration");
            if (json) {
                const conf = JSON.parse(json);
                if (conf) {
                    this.setConfiguration(conf);
                }
            }
        }

        private registerEvents() {
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

        public start() {
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
                let data: SocketMessage = JSON.parse(e.data);
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

        public stop() {
            if (this.socket) {
                this.socket.close();
            }
        }

        public computeReferences() {
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

        public exportJson() {
            const json = JSON.stringify(this.documents);
            const blob = new Blob([json], { type: "text/json" });
            const url = URL.createObjectURL(blob);

            const anchor = document.createElement("a");
            anchor.setAttribute("href", url);
            anchor.setAttribute("download", "export.json");
            anchor.click();
        }

        private selectDocumentFromHash() {
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

        public selectDocument(idOrDocument: string | Document | null) {
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
            } else {
                const document = idOrDocument;
                this.elements.documentDetails.innerHTML = "";

                const element = this.renderDocument(document, "details");
                this.elements.documentDetails.appendChild(element);

                this.elements.documentDetailsContainer.classList.remove("hide");
            }
        }

        private setButtonVisibility() {
            this.elements.urlInput.disabled = this.isRunning;
            this.elements.urlIncludePatternInput.disabled = this.isRunning;
            this.elements.startButton.disabled = this.isRunning;
            this.elements.stopButton.classList.toggle("hide", !this.isRunning);
            this.elements.exportJsonButton.classList.toggle("hide", !this.isRunning);
            this.elements.computeReferencesButton.classList.toggle("hide", !this.isRunning);
        }

        private addDocument(document: Document) {
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

        private renderDocument(document: Document, mode: "list" | "details") {
            let result: JSX.Element;
            if (mode === "list") {
                result =
                    <div className="document">
                        <a href={`#documentId=${document.id}`}>
                            <span className={`tag tag-${this.getStatusCodeClass(document)}`} title={document.reasonPhrase}>{document.statusCode}</span>
                            <span title={document.url}>{document.url}</span>
                        </a>
                    </div>;
            } else {
                result =
                    <div>
                        <div className="summary">
                            <div><a className="document-url" href={document.url} target="_blank">{document.url}</a></div>
                            {document.redirectUrl && <div>➜ <a href={`#documentUrl=${encodeURIComponent(document.redirectUrl)}`}>{document.redirectUrl}</a></div>}
                            <div><span className={`tag tag-${this.getStatusCodeClass(document)}`}>{document.statusCode}</span> {document.reasonPhrase}</div>
                        </div>
                        <details className={document.requestHeaders ? "" : "hide"}>
                            <summary>Request Headers</summary>
                            <div className="details">
                                <pre><code>{this.formatHeaders(document.requestHeaders)}</code></pre>
                            </div>
                        </details>

                        <details className={document.responseHeaders ? "" : "hide"}>
                            <summary>Response Headers</summary>
                            <div className="details">
                                <pre><code>{this.formatHeaders(document.responseHeaders)}</code></pre>
                            </div>
                        </details>


                        <details>
                            <summary>References ({document.references.length})</summary>
                            <div className="details">
                                <ul>
                                    {document.references.map(ref =>
                                        <li>
                                            <details>
                                                <summary><a href={`#documentId=${ref.targetDocumentId}`}>{ref.targetDocumentUrl}</a></summary>
                                                <pre><code>{ref.excerpt}</code></pre>
                                            </details>
                                        </li>
                                    )}
                                </ul>
                            </div>
                        </details>

                        <details>
                            <summary>Referenced by ({document.referencedBy.length})</summary>
                            <div className="details">
                                <ul>
                                    {document.referencedBy.map(ref =>
                                        <li>
                                            <details>
                                                <summary><a href={`#documentId=${ref.sourceDocumentId}`}>{ref.sourceDocumentUrl}</a></summary>
                                                <pre><code>{ref.excerpt}</code></pre>
                                            </details>
                                        </li>
                                    )}
                                </ul>
                            </div>
                        </details>

                        <details className={document.htmlErrors.length > 0 ? "" : "hide"}>
                            <summary>HTML Errors ({document.htmlErrors.length})</summary>
                            <div className="details">
                                <ul>
                                    {document.htmlErrors.map(htmlError =>
                                        <li>
                                            <details>
                                                <summary>Line {htmlError.line}, Column {htmlError.column}: {htmlError.message}</summary>
                                                <pre><code>{this.getErrorExcerpt(htmlError)}</code></pre>
                                            </details>
                                        </li>
                                    )}
                                </ul>
                            </div>
                        </details>
                    </div>;
            }

            return result;
        }

        private getErrorExcerpt(error: HtmlError) {
            const excerptPosition = error.excerptPosition - 1;
            const before = this.replaceNewLines(error.excerpt.substring(0, excerptPosition));
            const current = this.replaceNewLines(error.excerpt.substring(excerptPosition, excerptPosition + 1));
            const after = this.replaceNewLines(error.excerpt.substring(excerptPosition + 1));
            return <span>{before}<span className="code-error">{current}</span>{after}</span>;
        }

        private replaceNewLines(str: string) {
            return str
                .replace(/\r\n/g, "↩")
                .replace(/\r|\n/g, "↩");
        }

        private getStatusCodeClass(document: Document) {
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

        private isStatusCodeOk(statusCode: number) {
            return statusCode >= 200 && statusCode < 300;
        }

        private isStatusCodeRedirect(statusCode: number) {
            return statusCode >= 300 && statusCode < 400;
        }

        private isStatusCodeError(statusCode: number) {
            return statusCode < 200 || statusCode >= 400;
        }

        private formatHeaders(headers: Headers | null) {
            let result = "";

            if (headers) {
                for (let key of Object.keys(headers)) {
                    const value: any = headers[key];

                    if (result !== "") {
                        result += "\n";
                    }

                    result += `${key}: ${value}`;
                }
            }

            return result;
        }
    }
}

namespace JSX {
    export interface IntrinsicElements {
        div: Partial<HTMLDivElement>;
        span: Partial<HTMLSpanElement>;
        h1: Partial<HTMLHeadingElement>;
        h2: Partial<HTMLHeadingElement>;
        h3: Partial<HTMLHeadingElement>;
        h4: Partial<HTMLHeadingElement>;
        h5: Partial<HTMLHeadingElement>;
        h6: Partial<HTMLHeadingElement>;
        details: Partial<HTMLElement>;
        summary: Partial<HTMLElement>;
        pre: Partial<HTMLPreElement>;
        ul: Partial<HTMLUListElement>;
        ol: Partial<HTMLOListElement>;
        li: Partial<HTMLLIElement>;
        code: Partial<HTMLElement>;
        a: Partial<HTMLAnchorElement>;
    }

    export interface Element extends HTMLElement {
    }

    interface AttributeCollection {
        [name: string]: string;
    }

    export function createElement(tagName: string, attributes: AttributeCollection | null, ...children: any[]): Element {
        const element = document.createElement(tagName);

        if (attributes) {
            for (let key of Object.keys(attributes)) {
                if (key === "className") {
                    element.setAttribute("class", attributes[key]);
                } else {
                    element.setAttribute(key, attributes[key]);
                }
            }
        }

        for (let child of children) {
            appendChild(element, child);
        }

        return element;
    }

    function appendChild(parent: Node, child: any) {
        if (typeof child === "undefined" || child === null) {
            return;
        }

        if (Array.isArray(child)) {
            for (let value of child) {
                appendChild(parent, value);
            }
        } else if (typeof child === "string") {
            parent.appendChild(document.createTextNode(child));
        } else if (child instanceof Node) {
            parent.appendChild(child);
        } else {
            parent.appendChild(document.createTextNode(String(child)));
        }
    }
}

declare class URLSearchParams {
    constructor(url?: string);
    append(name: string, value: any): void;
    set(name: string, value: any): void;
    delete(name: string): void;
    has(name: string): boolean;
    get(name: string): string;
    getAll(name: string): string[];
    entries(): Iterable<string[]>;
    keys(): Iterable<string>;
    values(): Iterable<string>;
    toString(): string;
}