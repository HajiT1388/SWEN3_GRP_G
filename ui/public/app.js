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
            <th>datei</th>
            <th>upload</th>
            <th>aktion</th>
          </tr>
        </thead>
        <tbody>
          ${docs.map(d => {
            const uploaded = d.uploadTime ? new Date(d.uploadTime).toLocaleString() : '—';
            const name = d.fileName ?? '(ohne Name)';
            const id = d.id;
            const details = id ? `./details.html?id=${encodeURIComponent(id)}` : '#';
            return `
              <tr>
                <td><a href="${details}">${name}</a><br/><span class="badge">${id ? id : '—'}</span></td>
                <td>${uploaded}</td>
                <td>
                  ${id ? `<button class="btn danger" data-del="${id}">löschen</button>` : ''}
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

        <form id="createForm">
          <div class="field">
            <label class="lbl" for="fileName">Dateiname</label>
            <input id="fileName" type="text" name="fileName" required placeholder="Dateipfad (TODO: Upload?)" />
          </div>

          <div class="field">
            <label class="lbl" for="fileContent">Inhalt</label>
            <textarea id="fileContent" name="fileContent" rows="8" placeholder="Inhalt?"></textarea>
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

    form.addEventListener('submit', async (ev) => {
      ev.preventDefault();
      msg.textContent = 'Wird gespeichert...';
      msg.className = 'msg';

      const fd = new FormData(form);
      const payload = {
        fileName: (fd.get('fileName') || '').toString().trim(),
        fileContent: (fd.get('fileContent') || '').toString()
      };

      if (!payload.fileName) {
        msg.textContent = 'Bitte Dateinamen angeben.';
        msg.className = 'msg err';
        return;
      }

      try {
        const res = await fetch('/api/documents', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(payload)
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

      out.innerHTML = `
        <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px;">
          <h2 class="section-title" style="margin:0;">Details</h2>
          <div class="actions" style="margin:0;">
            <button id="del" class="btn danger">löschen</button>
            <a class="btn ghost" href="./list.html">zur liste</a>
          </div>
        </div>
        <p><strong>Datei:</strong> ${d.fileName ?? '(ohne Name)'}</p>
        <p><strong>Id:</strong> <code>${d.id ?? '—'}</code></p>
        <p><strong>Upload:</strong> ${uploaded}</p>
        <div>
          <div class="lbl" style="margin-top:10px;">Inhalt</div>
          <pre>${d.fileContent ?? ''}</pre>
        </div>
      `;

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

  window.addEventListener('popstate', renderRoute);
  document.addEventListener('click', handleLinkClick);

  renderRoute();
})();