(() => {
  if (window.__spaBooted) return;
  window.__spaBooted = true;

  const root = document.getElementById('app');

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
    const path = location.pathname.replace(/\/+$/, '') || '/';
    const page = path.split('/').pop() || 'index.html';
    window.scrollTo(0, 0);

    switch (page) {
      case '':
      case 'index.html':
        return renderHome();
      case 'list.html':
        return renderList();
      case 'new.html':
        return renderNew();
      case 'details.html':
        return renderDetails();
      default:
        return renderHome();
    }
  }

  function renderHome() {
    setTitle('index');
    root.innerHTML = `
      <div class="glass-card nav-card">
        <div class="links">
          <a class="link" href="./new.html">Neues Dokument</a>
          <span class="sep">/</span>
          <a class="link" href="./list.html">Liste</a>
        </div>
      </div>
    `;
  }

  function renderList() {
    setTitle('list');
    root.innerHTML = `
      <div class="glass-card">
        <nav class="top-actions">
          <a class="link" href="./index.html">-- Start</a>
          <a class="link" href="./new.html">Neu --</a>
        </nav>

        <h2 class="section-title">Dokumentenliste</h2>
        <div id="list" class="scroll-area" role="region" aria-label="Dokumentenliste"></div>
      </div>
    `;

    const listEl = root.querySelector('#list');
    loadDocs(listEl);
  }

  function formatBytes(bytes) {
    if (!bytes && bytes !== 0) return '';
    const units = ['B', 'KB', 'MB', 'GB'];
    let i = 0; let v = bytes;
    while (v >= 1024 && i < units.length - 1) { v /= 1024; i++; }
    return `${v.toFixed(v < 10 && i > 0 ? 1 : 0)} ${units[i]}`;
  }

  async function loadDocs(listEl) {
    listEl.innerHTML = '<div style="padding:12px;">Lädt...</div>';
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
            <th>name</th>
            <th>größe</th>
            <th>typ</th>
            <th>upload</th>
            <th>aktion</th>
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
                <td>${uploaded}</td>
                <td>
                  ${id ? `
                    <a class="btn ghost" href="${dl}" rel="external" target="_blank">download</a>
                    <button class="btn danger" data-del="${id}">löschen</button>
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
          <a class="link" href="./index.html">-- Start</a>
          <a class="link" href="./list.html">Liste --</a>
        </nav>

        <h2 class="section-title">Neues Dokument hinzufügen</h2>

        <form id="createForm" enctype="multipart/form-data">
          <div class="field">
            <label class="lbl" for="docName">Name</label>
            <input id="docName" type="text" name="name" required placeholder="Dokumentname" />
          </div>

          <div class="field">
            <label class="lbl" for="file">Datei (.pdf oder .txt)</label>
            <input id="file" type="file" name="file" accept=".pdf,.txt" required />
          </div>

          <div class="actions">
            <a class="btn ghost" href="./list.html">Zur Liste</a>
            <button type="submit" class="btn primary">Speichern</button>
          </div>

          <p id="msg" class="msg" aria-live="polite"></p>
        </form>
      </div>
    `;

    const form = root.querySelector('#createForm');
    const msg = root.querySelector('#msg');
    const nameInput = root.querySelector('#docName');
    const fileInput = root.querySelector('#file');

    fileInput.addEventListener('change', () => {
      const file = fileInput.files && fileInput.files[0];
      if (file && !nameInput.value.trim()) {
        const n = file.name.replace(/\.[^.]+$/, '');
        nameInput.value = n;
      }
    });

    form.addEventListener('submit', async (ev) => {
      ev.preventDefault();
      msg.textContent = 'Wird gespeichert...';
      msg.className = 'msg';

      const file = fileInput.files && fileInput.files[0];
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

        const detailsLink = created?.id
          ? ` <a class="link" href="./details.html?id=${encodeURIComponent(created.id)}">Details ansehen</a>`
          : '';

        msg.innerHTML = `erstellt${detailsLink ? ' - ' + detailsLink : ''}`;
        msg.className = 'msg ok';
      } catch (e) {
        msg.textContent = e.message || 'Erstellen fehlgeschlagen';
        msg.className = 'msg err';
      }
    });
  }

  async function renderDetails() {
    setTitle('details');
    const id = new URL(location.href).searchParams.get('id');
    root.innerHTML = `
      <div class="glass-card">
        <nav class="top-actions">
          <a class="link" href="./list.html">-- liste</a>
          <a class="link" href="./index.html">start --</a>
        </nav>
        <section id="details"></section>
      </div>
    `;

    const out = root.querySelector('#details');

    if (!id) {
      out.innerHTML = '<p style="color:#ff9b9b;">Fehlende ID.</p>';
      return;
    }

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
        <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px;">
          <h2 class="section-title" style="margin:0;">Details</h2>
          <div class="actions" style="margin:0;">
            <a class="btn ghost" href="${dlUrl}" rel="external" target="_blank">Download</a>
            <button id="del" class="btn danger">löschen</button>
            <a class="btn ghost" href="./list.html">zur liste</a>
          </div>
        </div>
        <p><strong>Name:</strong> ${d.name ?? '(ohne Name)'}</p>
        <p><strong>Original-Dateiname:</strong> ${d.originalFileName ?? '—'}</p>
        <p><strong>Typ:</strong> ${d.contentType ?? '—'}</p>
        <p><strong>Größe:</strong> ${formatBytes(d.sizeBytes)}</p>
        <p><strong>Upload:</strong> ${uploaded}</p>
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
          <div class="lbl" style="margin-top:10px;">PDF Vorschau</div>
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
    } catch (e) {
      out.innerHTML = `<p style="color:#ff9b9b;">${e.message || 'Fehler'}</p>`;
    }
  }

  function escapeHtml(s) {
    return (s ?? '').toString()
      .replace(/&/g, '&amp;').replace(/</g, '&lt;')
      .replace(/>/g, '&gt;').replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  window.addEventListener('popstate', renderRoute);
  document.addEventListener('click', handleLinkClick);

  renderRoute();
})();