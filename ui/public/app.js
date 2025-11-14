(() => {
  if (window.__spaBooted) return;
  window.__spaBooted = true;

  const root = document.getElementById('app');
  let cleanup = () => {};

  function createSketchSVG(width, height, radius) {
    const svg  = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    svg.setAttribute('viewBox', `0 0 ${width} ${height}`);
    rect.setAttribute('x', '0');
    rect.setAttribute('y', '0');
    rect.setAttribute('width', '100%');
    rect.setAttribute('height', '100%');
    rect.setAttribute('rx', String(radius));
    rect.setAttribute('ry', String(radius));
    rect.setAttribute('pathLength', '10');
    svg.appendChild(rect);
    return svg;
  }

  function enhanceBtn(btn) {
    if (!(btn instanceof Element)) return;
    if (btn.dataset.sketchified === '1') return;
    btn.dataset.sketchified = '1';

    const style = getComputedStyle(btn);
    const lines = document.createElement('div');
    lines.className = 'btn-lines';

    const gTop = document.createElement('div');
    const gBot = document.createElement('div');

    const radius = parseInt(style.borderRadius, 10) || 0;
    const svg = createSketchSVG(btn.offsetWidth, btn.offsetHeight, radius);

    gTop.appendChild(svg);
    gTop.appendChild(svg.cloneNode(true));
    gTop.appendChild(svg.cloneNode(true));
    gTop.appendChild(svg.cloneNode(true));

    gBot.appendChild(svg.cloneNode(true));
    gBot.appendChild(svg.cloneNode(true));
    gBot.appendChild(svg.cloneNode(true));
    gBot.appendChild(svg.cloneNode(true));

    lines.appendChild(gTop);
    lines.appendChild(gBot);
    btn.appendChild(lines);

    btn.addEventListener('pointerenter', () => {
      btn.classList.add('sketch-start');
    });

    svg.addEventListener('animationend', () => {
      btn.classList.remove('sketch-start');
    });
  }

  function enhanceAllButtons(container = document) {
    container.querySelectorAll('button.btn, a.btn').forEach(enhanceBtn);
  }

  function bootSketchOverlay() {
    enhanceAllButtons(root);
    const mo = new MutationObserver(muts => {
      for (const m of muts) {
        m.addedNodes && m.addedNodes.forEach(node => {
          if (!(node instanceof Element)) return;
          if (node.matches && node.matches('button.btn, a.btn')) enhanceBtn(node);
          if (node.querySelectorAll) {
            node.querySelectorAll('button.btn, a.btn').forEach(enhanceBtn);
          }
        });
      }
    });
    mo.observe(root, { childList: true, subtree: true });
  }

  function setTitle(suffix) {
    document.title = `DMSG3 (G3/${suffix})`;
  }

  function navigate(href, { replace = false } = {}) {
    const url = new URL(href, location.origin);
    if (replace) history.replaceState({}, '', url);
    else history.pushState({}, '', url);
    renderRoute();
  }

  function handleLinkClick(e) {
    const a = e.target.closest('a');
    if (!a) return;
    if (a.target && a.target !== '_self') return;
    if (a.hasAttribute('download') || a.rel === 'external' || a.getAttribute('rel') === 'nofollow') return;

    const url = new URL(a.href, location.origin);
    if (url.origin !== location.origin) return;
    if (!/\.html$/i.test(url.pathname)) return;

    e.preventDefault();
    navigate(url.href);
  }

  function renderRoute() {
    try { cleanup(); } catch {}
    cleanup = () => {};

    const path = location.pathname.replace(/\/+$/, '') || '/';
    const page = path.split('/').pop() || 'index.html';
    window.scrollTo(0, 0);

    let ret;
    switch (page) {
      case '':
      case 'index.html':
        ret = renderHome(); break;
      case 'list.html':
        ret = renderList(); break;
      case 'new.html':
        ret = renderNew(); break;
      case 'details.html':
        ret = renderDetails(); break;
      default:
        ret = renderHome(); break;
    }
    if (typeof ret === 'function') cleanup = ret;
  }

  function renderHome() {
    setTitle('index');
    root.innerHTML = `
      <div class="glass-card nav-card">
        <div class="home-actions">
          <a class="btn soft big" href="./new.html">Neues Dokument</a>
          <a class="btn soft light" href="./list.html">Liste</a>
        </div>
      </div>
    `;
  }

  function renderStatusBadge(status, completedAt) {
    const normalized = (status || '').toLowerCase();
    const labels = {
      pending: 'Ausstehend',
      processing: 'Wird verarbeitet',
      completed: 'Fertig',
      failed: 'Fehlgeschlagen'
    };
    const label = labels[normalized] || status || 'Unbekannt';
    const extra = normalized === 'completed' && completedAt
      ? `<span class="status-muted">${new Date(completedAt).toLocaleString()}</span>`
      : '';
    return `<span class="status-badge status-${normalized || 'unknown'}">${label}</span>${extra}`;
  }

  function renderList() {
    setTitle('list');
    root.innerHTML = `
      <div class="glass-card">
        <nav class="top-actions">
          <a class="link" href="./index.html">← Start</a>
          <a class="link" href="./new.html">Neu →</a>
        </nav>

        <h2 class="section-title">Dokumentenliste</h2>
        <div id="list" class="scroll-area" role="region" aria-label="Dokumentenliste"></div>
      </div>
    `;

    const listEl = root.querySelector('#list');
    let refreshHandle;

    const refresh = (silent = false) => loadDocs(listEl, silent);
    refresh();
    refreshHandle = setInterval(() => refresh(true), 6000);

    return () => clearInterval(refreshHandle);
  }

  function formatBytes(bytes) {
    if (!bytes && bytes !== 0) return '';
    const units = ['B', 'KB', 'MB', 'GB'];
    let i = 0; let v = bytes;
    while (v >= 1024 && i < units.length - 1) { v /= 1024; i++; }
    return `${v.toFixed(v < 10 && i > 0 ? 1 : 0)} ${units[i]}`;
  }

  async function loadDocs(listEl, silent = false) {
    if (!silent) listEl.innerHTML = '<div style="padding:12px;">Lädt...</div>';
    try {
      const res = await fetch('/api/documents', { cache: 'no-store' });
      if (!res.ok) throw new Error('Fehler beim Laden');

      const docs = await res.json();
      if (!Array.isArray(docs) || docs.length === 0) {
        listEl.innerHTML = '<div style="padding:12px;"><em>Keine Dokumente vorhanden.</em></div>';
        return;
      }

      const table = document.createElement('table');
      table.className = 'table';
      table.innerHTML = `
        <thead>
          <tr>
            <th scope="col">Name</th>
            <th scope="col">Größe</th> 
            <th scope="col">Typ</th>
            <th scope="col">OCR</th>
            <th scope="col">Upload</th>
            <th scope="col">Aktion</th>
          </tr>
        </thead>
        <tbody>
          ${docs.map(d => {
            const uploaded = d.uploadTime ? new Date(d.uploadTime).toLocaleString() : '—';
            const name = d.name ?? '(ohne Name)';
            const id = d.id;
            const details = id ? `./details.html?id=${encodeURIComponent(id)}` : '#';
            const dl = id ? `/api/documents/${encodeURIComponent(id)}/download` : '#';
            return `
              <tr>
                <td><a href="${details}">${name}</a><br/><span class="badge">${id || '—'}</span></td>
                <td>${formatBytes(d.sizeBytes)}</td>
                <td>${d.contentType || '—'}</td>
                <td>${renderStatusBadge(d.ocrStatus, d.ocrCompletedAt)}</td>
                <td>${uploaded}</td>
                <td>
                  ${id ? `
                    <a class="btn ghost" href="${dl}" rel="external" target="_blank">Herunterladen</a>
                    <button class="btn danger" data-del="${id}">Löschen</button>
                  ` : ''}
                </td>
              </tr>
            `;
          }).join('')}
        </tbody>
      `;

      listEl.innerHTML = '';
      listEl.appendChild(table);

      listEl.querySelectorAll('button[data-del]').forEach(btn => {
        btn.addEventListener('click', async () => {
          const id = btn.getAttribute('data-del');
          if (!id) return;
          if (!confirm('Wirklich löschen?')) return;
          const r = await fetch(`/api/documents/${encodeURIComponent(id)}`, { method: 'DELETE' });
          if (r.status === 204) {
            loadDocs(listEl);
          } else {
            alert('Löschen fehlgeschlagen');
          }
        });
      });
    } catch (e) {
      listEl.innerHTML = `<div style="padding:12px;color:#ff9b9b;">${e.message || 'Unbekannter Fehler'}</div>`;
    }
  }

  function renderNew() {
    setTitle('new');
    root.innerHTML = `
      <div class="glass-card">
        <nav class="top-actions">
          <a class="link" href="./index.html">← Start</a>
          <a class="link" href="./list.html">Liste →</a>
        </nav>

        <h2 class="section-title">Neues Dokument hinzufügen</h2>

        <form id="createForm" enctype="multipart/form-data">
          <div class="field">
            <label class="lbl" for="docName">Name</label>
            <input id="docName" type="text" name="name" required placeholder="Dokumentname" />
          </div>

          <div class="field">
            <label class="lbl" for="fileInput">Datei</label>
            <input id="fileInput" type="file" name="file" accept=".pdf,.txt" hidden />
            <label id="dropZone" for="fileInput" class="drop-zone" tabindex="0">
              <span class="dz-text">Datei hierher ziehen<br><small>oder zum Auswählen drücken</small></span>
            </label>
          </div>

          <div class="actions">
            <button type="submit" class="btn primary">Speichern</button>
          </div>

          <p id="msg" class="msg" aria-live="polite"></p>
        </form>
      </div>
    `;

    const form       = root.querySelector('#createForm');
    const msg        = root.querySelector('#msg');
    const nameInput  = root.querySelector('#docName');
    const fileInput  = root.querySelector('#fileInput');
    const dropZone   = root.querySelector('#dropZone');

    let pendingFile = null;
    let dragDepth   = 0;

    function updateDropZoneText(text, ok = false) {
      dropZone.querySelector('.dz-text').innerHTML = text;
      dropZone.dataset.state = ok ? 'ok' : '';
    }

    function setFile(file) {
      try {
        const dt = new DataTransfer();
        dt.items.add(file);
        fileInput.files = dt.files;
      } catch {
        pendingFile = file;
      }
      if (file && !nameInput.value.trim()) {
        nameInput.value = file.name.replace(/\.[^.]+$/, '');
      }
      if (file) {
        updateDropZoneText(`Hinzugefügt: <strong>${file.name}</strong>`, true);
      }
    }

    fileInput.addEventListener('change', () => {
      const file = fileInput.files && fileInput.files[0];
      if (file) setFile(file);
    });

    const onDragOver = (ev) => {
      ev.preventDefault();
    };
    const onDragEnter = (ev) => {
      ev.preventDefault();
      dragDepth++;
      dropZone.classList.add('is-dragover');
    };
    const onDragLeave = () => {
      dragDepth = Math.max(0, dragDepth - 1);
      if (dragDepth === 0) dropZone.classList.remove('is-dragover');
    };
    const onDrop = (ev) => {
      ev.preventDefault();
      dragDepth = 0;
      dropZone.classList.remove('is-dragover');

      const files = ev.dataTransfer && ev.dataTransfer.files;
      if (!files || !files.length) return;

      const file = files[0];
      const ext = (file.name.match(/\.[^.]+$/) || [''])[0].toLowerCase();
      if (!['.pdf', '.txt'].includes(ext)) {
        msg.textContent = 'Nur .pdf und .txt erlaubt.';
        msg.className = 'msg err';
        return;
      }
      setFile(file);
      msg.textContent = '';
      msg.className = 'msg';
    };

    ['dragover', 'dragenter', 'dragleave', 'drop'].forEach(evt => {
      dropZone.addEventListener(evt, (e) => {
        switch (evt) {
          case 'dragover':  onDragOver(e);  break;
          case 'dragenter': onDragEnter(e); break;
          case 'dragleave': onDragLeave(e); break;
          case 'drop':      onDrop(e);      break;
        }
      });
    });

    form.addEventListener('submit', async (ev) => {
      ev.preventDefault();
      msg.textContent = 'Wird gespeichert...';
      msg.className = 'msg';

      let file = fileInput.files && fileInput.files[0];
      if (!file && pendingFile) file = pendingFile;

      if (!file) {
        msg.textContent = 'Bitte eine Datei auswählen (.pdf oder .txt).';
        msg.className = 'msg err';
        return;
      }
      const ext = (file.name.match(/\.[^.]+$/) || [''])[0].toLowerCase();
      if (!['.pdf', '.txt'].includes(ext)) {
        msg.textContent = 'Es sind nur .pdf und .txt erlaubt.';
        msg.className = 'msg err';
        return;
      }

      const fd = new FormData();
      fd.append('name', nameInput.value.trim());
      fd.append('file', file);

      try {
        const res = await fetch('/api/documents', {
          method: 'POST',
          body: fd
        });

        if (!res.ok) {
          const text = await res.text().catch(() => '');
          throw new Error(text || 'Erstellen fehlgeschlagen');
        }

        const created = await res.json().catch(() => null);
        form.reset();
        pendingFile = null;
        updateDropZoneText('Datei(en) hierher ziehen<br><small>oder zum Auswählen drücken</small>');

        const detailsLink = created?.id
          ? ` <a class="link" href="./details.html?id=${encodeURIComponent(created.id)}">Details ansehen</a>`
          : '';

        msg.innerHTML = `Erstellt${detailsLink ? ' - ' + detailsLink : ''}`;
        msg.className = 'msg ok';
      } catch (e) {
        msg.textContent = e.message || 'Erstellen fehlgeschlagen';
        msg.className = 'msg err';
      }
    });
  }

  function renderDetails() {
    setTitle('details');
    const id = new URL(location.href).searchParams.get('id');
    root.innerHTML = `
      <div class="glass-card">
        <nav class="top-actions">
          <a class="link" href="./list.html">← Liste</a>
          <a class="link" href="./index.html">Start →</a>
        </nav>
        <section id="details"></section>
      </div>
    `;

    const out = root.querySelector('#details');

    if (!id) {
      out.innerHTML = '<p style="color:#ff9b9b;">Fehlende ID.</p>';
      return;
    }

    let refreshHandle;

    const fetchDetails = async () => {
      try {
        const res = await fetch(`/api/documents/${encodeURIComponent(id)}`, { cache: 'no-store' });
        if (res.status === 404) {
          out.innerHTML = '<p><em>Nicht gefunden.</em></p>';
          return;
        }
        if (!res.ok) throw new Error('Ladefehler');

        const d = await res.json();
        const uploaded = d.uploadTime ? new Date(d.uploadTime).toLocaleString() : '—';
        const dlUrl = `/api/documents/${encodeURIComponent(id)}/download`;
        const inlineUrl = `${dlUrl}?inline=true`;

        out.innerHTML = `
          <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px;gap:10px;flex-wrap:wrap;">
            <h2 class="section-title" style="margin:0;flex:1 1 auto;">Details</h2>
            <div class="actions" style="margin:0;flex:none;">
              <a class="btn ghost" href="${dlUrl}" rel="external" target="_blank">Herunterladen</a>
              <button id="del" class="btn danger">Löschen</button>
              <a class="btn soft" href="./new.html">Weiteres hinzufügen...</a>
            </div>
          </div>
          <div class="meta-block">
            <p><strong>Name:</strong> ${d.name ?? '(ohne Name)'}</p>
            <p><strong>Original-Dateiname:</strong> ${d.originalFileName ?? '—'}</p>
            <p><strong>Typ:</strong> ${d.contentType ?? '—'}</p>
            <p><strong>Größe:</strong> ${formatBytes(d.sizeBytes)}</p>
            <p><strong>Upload:</strong> ${uploaded}</p>
          </div>
          <p><strong>OCR-Status:</strong> ${renderStatusBadge(d.ocrStatus, d.ocrCompletedAt)} ${d.ocrError ? `<span class="status-muted">${escapeHtml(d.ocrError)}</span>` : ''}</p>
          ${d.ocrText ? `<div class="ocr-preview"><div class="lbl"><strong>OCR-Vorschau:</strong></div><pre>${escapeHtml(d.ocrText)}</pre></div>` : ''}
          <div id="preview" style="margin-top:12px;"></div>
        `;

        const previewEl = root.querySelector('#preview');
        if (d.contentType && d.contentType.startsWith('text/plain')) {
          try {
            const r2 = await fetch(dlUrl);
            if (r2.ok) {
              const text = await r2.text();
              previewEl.innerHTML = `
                <div class="lbl" style="margin-top:10px;">Inhalt (Vorschau)</div>
                <pre>${escapeHtml(text)}</pre>
              `;
            }
          } catch {}
        } else if (d.contentType && d.contentType.includes('pdf')) {
          previewEl.innerHTML = `
            <div class="lbl" style="margin-top:10px;"><strong>PDF-Vorschau:</strong></div>
            <iframe src="${inlineUrl}" style="width:100%;height:70vh;border:1px solid rgba(255,255,255,0.1);border-radius:6px;"></iframe>
          `;
        }

        root.querySelector('#del').addEventListener('click', async () => {
          if (!confirm('Wirklich löschen?')) return;
          const del = await fetch(`/api/documents/${encodeURIComponent(id)}`, { method: 'DELETE' });
          if (del.status === 204) {
            navigate('./list.html');
          } else {
            alert('Löschen fehlgeschlagen');
          }
        });

        if (refreshHandle) {
          clearTimeout(refreshHandle);
          refreshHandle = null;
        }

        if (d.ocrStatus && !['completed', 'failed'].includes((d.ocrStatus || '').toLowerCase())) {
          refreshHandle = setTimeout(() => fetchDetails(), 5000);
        }
      } catch (e) {
        out.innerHTML = `<p style="color:#ff9b9b;">${e.message || 'Fehler'}</p>`;
      }
    };

    fetchDetails();
    return () => clearTimeout(refreshHandle);
  }

  function escapeHtml(s) {
    return (s ?? '').toString()
      .replace(/&/g, '&amp;').replace(/</g, '&lt;')
      .replace(/>/g, '&gt;').replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  window.addEventListener('popstate', renderRoute);
  document.addEventListener('click', handleLinkClick);

  bootSketchOverlay();

  renderRoute();
})();
