namespace MermaidViewer;

internal static class WebScripts
{
    public static string HostHtml { get; } = """
<!doctype html>
<html>
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width,initial-scale=1" />
    <style>
      :root { color-scheme: light dark; }
      html, body { margin: 0; height: 100%; overflow: hidden; }
      body { font-family: system-ui, -apple-system, Segoe UI, Roboto, Arial, sans-serif; }
      #stage {
        height: 100vh;
        position: relative;
        overflow: auto;
        background: transparent;
        cursor: grab;
        user-select: none;
        -webkit-user-select: none;
      }
      #stage.grabbing { cursor: grabbing; }
      #panzoom {
        display: inline-block;
        vertical-align: top;
        line-height: normal;
      }
      #panzoom svg {
        display: block;
        max-width: none;
        overflow: visible;
      }
      #exportLane {
        position: absolute;
        left: -99999px;
        top: 0;
        visibility: hidden;
        pointer-events: none;
        margin: 0;
        padding: 0;
        line-height: normal;
      }
      #exportLane svg { display: block; max-width: none; overflow: visible; }
      @media print {
        html, body { margin: 0 !important; padding: 0 !important; height: auto !important; overflow: visible !important; }
        #stage { display: none !important; }
        #exportLane {
          position: static !important;
          left: auto !important;
          visibility: visible !important;
          pointer-events: none;
          display: block !important;
        }
      }
      .error {
        color: #b00020;
        white-space: pre-wrap;
        font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
        font-size: 12px;
        padding: 10px;
      }
    </style>
  </head>
  <body>
    <div id="stage"></div>
    <div id="exportLane" aria-hidden="true"></div>
  </body>
</html>
""";

    public static string Bootstrap { get; } = """
(() => {
  const stageEl = () => document.getElementById("stage");

  function setStatus(t) {
    try {
      if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
        window.chrome.webview.postMessage(JSON.stringify({ type: "status", text: t || "" }));
      }
    } catch (e) {}
  }

  let seq = 0;
  let scale = 1;
  let dragLeft = false;
  let dragMid = false;

  function escapeHtml(s) {
    return (s||"").replaceAll("&","&amp;").replaceAll("<","&lt;").replaceAll(">","&gt;");
  }

  function contentNaturalSize() {
    const pz = document.getElementById("panzoom");
    if (!pz) return { w: 0, h: 0 };
    const svg = pz.querySelector("svg");
    if (!svg) return { w: 0, h: 0 };
    const vb = svg.viewBox && svg.viewBox.baseVal ? svg.viewBox.baseVal : null;
    if (vb && vb.width > 0 && vb.height > 0) return { w: vb.width, h: vb.height };
    try {
      const bb = svg.getBBox();
      if (bb.width > 0 && bb.height > 0) return { w: bb.width, h: bb.height };
    } catch (e) {}
    const cw = svg.clientWidth || 300;
    const ch = svg.clientHeight || 150;
    return { w: cw, h: ch };
  }

  function applyLayout() {
    const pz = document.getElementById("panzoom");
    if (!pz) return;
    const svg = pz.querySelector("svg");
    const { w, h } = contentNaturalSize();
    if (!w || !h) return;
    const sw = Math.max(1, w * scale);
    const sh = Math.max(1, h * scale);
    pz.style.width = sw + "px";
    pz.style.height = sh + "px";
    if (svg) {
      svg.style.width = sw + "px";
      svg.style.height = sh + "px";
      svg.style.overflow = "visible";
    }
  }

  function fitToStage() {
    const stage = stageEl();
    const pz = document.getElementById("panzoom");
    if (!stage || !pz) return;
    const svg = pz.querySelector("svg");
    if (!svg) return;
    const { w, h } = contentNaturalSize();
    if (!w || !h) return;
    const pad = 16;
    const cw = Math.max(50, stage.clientWidth - pad);
    const ch = Math.max(50, stage.clientHeight - pad);
    const z = Math.min(cw / w, ch / h);
    if (!isFinite(z) || z <= 0) return;
    scale = Math.max(0.05, Math.min(20, z));
    applyLayout();
    const sl = Math.max(0, (stage.scrollWidth - stage.clientWidth) / 2);
    const st = Math.max(0, (stage.scrollHeight - stage.clientHeight) / 2);
    stage.scrollLeft = sl;
    stage.scrollTop = st;
  }

  function zoomAt(stageX, stageY, factor) {
    const stage = stageEl();
    const pz = document.getElementById("panzoom");
    if (!stage || !pz) return;
    const { w, h } = contentNaturalSize();
    if (!w || !h) return;
    const oldScale = scale;
    const f = Math.max(0.9, Math.min(1.1, factor));
    const newScale = Math.max(0.05, Math.min(20, oldScale * f));
    if (Math.abs(newScale - oldScale) < 1e-6) return;

    const contentX = stage.scrollLeft + stageX;
    const contentY = stage.scrollTop + stageY;
    const ratio = newScale / oldScale;

    scale = newScale;
    applyLayout();

    stage.scrollLeft = Math.max(0, contentX * ratio - stageX);
    stage.scrollTop = Math.max(0, contentY * ratio - stageY);
  }

  function zoomAroundCenter(factor) {
    const stage = stageEl();
    if (!stage) return;
    zoomAt(stage.clientWidth / 2, stage.clientHeight / 2, factor);
  }

  function resetHundred() {
    const stage = stageEl();
    const pz = document.getElementById("panzoom");
    if (!stage || !pz) return;
    scale = 1;
    applyLayout();
    const sl = Math.max(0, (stage.scrollWidth - stage.clientWidth) / 2);
    const st = Math.max(0, (stage.scrollHeight - stage.clientHeight) / 2);
    stage.scrollLeft = sl;
    stage.scrollTop = st;
  }

  function setGrabbing(on) {
    const s = stageEl();
    if (!s) return;
    if (on) s.classList.add("grabbing");
    else s.classList.remove("grabbing");
  }

  function installHandlersOnce() {
    const stage = stageEl();
    if (!stage || stage.dataset.pzBound) return;
    stage.dataset.pzBound = "1";

    stage.addEventListener("wheel", (e) => {
      if (e.ctrlKey || e.metaKey) {
        e.preventDefault();
        const f = e.deltaY < 0 ? 1.1 : 1 / 1.1;
        const rect = stage.getBoundingClientRect();
        zoomAt(e.clientX - rect.left, e.clientY - rect.top, f);
        return;
      }
      if (e.shiftKey) {
        e.preventDefault();
        stage.scrollLeft += e.deltaY;
        if (e.deltaX) stage.scrollLeft += e.deltaX;
        return;
      }
      if (Math.abs(e.deltaX) > Math.abs(e.deltaY) && e.deltaX !== 0) {
        e.preventDefault();
        stage.scrollLeft += e.deltaX;
        return;
      }
    }, { passive: false });

    stage.addEventListener("pointerdown", (e) => {
      if (e.button === 0) {
        dragLeft = true;
        setGrabbing(true);
        try { stage.setPointerCapture(e.pointerId); } catch (err) {}
      } else if (e.button === 1) {
        e.preventDefault();
        dragMid = true;
        setGrabbing(true);
        try { stage.setPointerCapture(e.pointerId); } catch (err) {}
      }
    });

    stage.addEventListener("pointermove", (e) => {
      if (!dragLeft && !dragMid) return;
      stage.scrollLeft -= e.movementX;
      stage.scrollTop -= e.movementY;
    });

    function endDrag(e) {
      if (e.button !== 0 && e.button !== 1) return;
      if (e.button === 0) dragLeft = false;
      if (e.button === 1) dragMid = false;
      if (!dragLeft && !dragMid) setGrabbing(false);
      try { stage.releasePointerCapture(e.pointerId); } catch (err) {}
    }

    stage.addEventListener("pointerup", endDrag);
    stage.addEventListener("pointercancel", () => {
      dragLeft = false;
      dragMid = false;
      setGrabbing(false);
    });
  }

  installHandlersOnce();

  document.addEventListener("keydown", (e) => {
    const mod = e.ctrlKey || e.metaKey;
    if (!mod) return;
    const k = e.key;
    const plus = k === "+" || k === "=" || e.code === "Equal" || e.code === "NumpadAdd";
    const minus = k === "-" || k === "_" || e.code === "Minus" || e.code === "NumpadSubtract";
    const zero = k === "0" || e.code === "Digit0" || e.code === "Numpad0";
    if (plus) { e.preventDefault(); zoomAroundCenter(1.1); }
    else if (minus) { e.preventDefault(); zoomAroundCenter(1 / 1.1); }
    else if (zero) { e.preventDefault(); resetHundred(); }
  }, true);

  function clearNeutral() {
    const st = stageEl();
    if (st) st.innerHTML = "";
    const lane = document.getElementById("exportLane");
    if (lane) lane.innerHTML = "";
    installHandlersOnce();
  }

  function getFullSvgCloneXml() {
    const svg = document.querySelector("#panzoom svg");
    if (!svg) return "";
    const pad = 12;
    let bb = null;
    try { bb = svg.getBBox(); } catch (e) {}
    if (!bb || bb.width <= 0 || bb.height <= 0) {
      try {
        const vb = svg.viewBox && svg.viewBox.baseVal;
        if (vb && vb.width > 0 && vb.height > 0)
          bb = { x: vb.x, y: vb.y, width: vb.width, height: vb.height };
      } catch (e2) {}
    }
    if (!bb || bb.width <= 0 || bb.height <= 0)
      return (new XMLSerializer()).serializeToString(svg);

    const clone = svg.cloneNode(true);
    const vx = bb.x - pad;
    const vy = bb.y - pad;
    const vw = bb.width + pad * 2;
    const vh = bb.height + pad * 2;
    clone.setAttribute("viewBox", vx + " " + vy + " " + vw + " " + vh);
    clone.setAttribute("width", String(vw));
    clone.setAttribute("height", String(vh));
    clone.removeAttribute("style");
    if (!clone.getAttribute("xmlns"))
      clone.setAttribute("xmlns", "http://www.w3.org/2000/svg");
    return (new XMLSerializer()).serializeToString(clone);
  }

  async function render(text) {
    const local = ++seq;
    const raw = (text || "").trim();
    if (!raw) {
      if (local !== seq) return;
      clearNeutral();
      setStatus("Nessun diagramma.");
      return;
    }
    try {
      if (!window.mermaid) {
        stageEl().innerHTML = '<div class="error">Mermaid non inizializzato.</div>';
        setStatus("Errore: Mermaid non disponibile.");
        return;
      }
      mermaid.initialize({ startOnLoad: false, theme: "default" });
      setStatus("Rendering…");
      const id = "m" + local;
      const out = await mermaid.render(id, raw);
      if (local !== seq) return;
      stageEl().innerHTML = '<div id="panzoom">' + out.svg + "</div>";
      if (out.bindFunctions) out.bindFunctions(stageEl());
      installHandlersOnce();
      fitToStage();
      setStatus("OK");
    } catch (e) {
      const msg = (e && (e.stack || e.message)) ? (e.stack || e.message) : String(e);
      stageEl().innerHTML = '<div class="error">' + escapeHtml(msg) + '</div>';
      setStatus("Errore");
    }
  }

  window.__mermaidViewerRender = (payload) => render(payload?.mermaid ?? "");

  window.__mermaidViewerClear = () => {
    try {
      clearNeutral();
      setStatus("Pronto.");
    } catch (e) {}
  };

  window.__mermaidViewerFit = () => { try { fitToStage(); } catch (e) {} };
  window.__mermaidViewerZoomIn = () => zoomAroundCenter(1.1);
  window.__mermaidViewerZoomOut = () => zoomAroundCenter(1 / 1.1);
  window.__mermaidViewerZoomReset = () => { try { resetHundred(); } catch (e) {} };

  window.__mermaidViewerGetSvgXml = () => getFullSvgCloneXml();

  window.__mermaidViewerGetDiagramPngBase64 = async (scaleOpt) => {
    const scale = Math.max(1, Math.min(4, Number(scaleOpt) || 2));
    const xml = getFullSvgCloneXml();
    if (!xml) return "";
    const blob = new Blob([xml], { type: "image/svg+xml;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    try {
      const img = new Image();
      await new Promise((res, rej) => {
        img.onload = () => res(null);
        img.onerror = () => rej(new Error("svg-img"));
        img.src = url;
      });
      const w = img.naturalWidth || img.width;
      const h = img.naturalHeight || img.height;
      if (!w || !h) return "";
      const canvas = document.createElement("canvas");
      canvas.width = Math.max(1, Math.ceil(w * scale));
      canvas.height = Math.max(1, Math.ceil(h * scale));
      const ctx = canvas.getContext("2d");
      if (!ctx) return "";
      ctx.setTransform(scale, 0, 0, scale, 0, 0);
      ctx.fillStyle = "#ffffff";
      ctx.fillRect(0, 0, w, h);
      ctx.drawImage(img, 0, 0);
      const data = canvas.toDataURL("image/png");
      const i = data.indexOf(",");
      return i >= 0 ? data.slice(i + 1) : "";
    } finally {
      URL.revokeObjectURL(url);
    }
  };

  window.__mermaidViewerPreparePrintExport = () => {
    const lane = document.getElementById("exportLane");
    if (!lane) return { ok: false };
    lane.innerHTML = "";
    const xml = getFullSvgCloneXml();
    if (!xml) return { ok: false };
    lane.insertAdjacentHTML("afterbegin", xml);
    const inner = lane.querySelector("svg");
    if (!inner) return { ok: false };
    let w = parseFloat(inner.getAttribute("width") || "0");
    let h = parseFloat(inner.getAttribute("height") || "0");
    if (!w || !h) {
      try {
        const vb = inner.viewBox && inner.viewBox.baseVal;
        if (vb && vb.width > 0 && vb.height > 0) {
          w = vb.width;
          h = vb.height;
        }
      } catch (e) {}
    }
    if (!w || !h) return { ok: false };
    return { ok: true, widthPx: w, heightPx: h };
  };

  window.__mermaidViewerClearPrintExport = () => {
    const lane = document.getElementById("exportLane");
    if (lane) lane.innerHTML = "";
  };
})();
""";
}
