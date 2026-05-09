# Mermaid Viewer

Applicazione Windows (.NET 8, WinForms + WebView2) per aprire e visualizzare file **Mermaid** (`.mmd`, `.mermaid`, `.md`), con più schede, zoom/pan, export PNG/SVG/PDF e associazioni file opzionali.

## Requisiti

- Windows con [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)

## Build

```bash
dotnet build MermaidViewer.csproj -c Release
```

Durante la build viene scaricato `mermaid.min.js` se non è presente in `Assets/`.

## Licenza

Icona applicazione: branding Mermaid (sito [mermaid.live](https://mermaid.live)). Il progetto Mermaid è sotto licenza MIT.
