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
      failed: 'Fehlgeschlagen',
      notscanned: 'Nicht gescannt',
      scanning: 'Scan läuft',
      clean: 'Sicher',
      malicious: 'Unsicher'
    };
    const label = labels[normalized] || status || 'Unbekannt';
    const extra = (normalized === 'completed' || normalized === 'clean' || normalized === 'malicious') && completedAt
      ? `<span class="status-muted">${formatDateTime(completedAt)}</span>`
      : '';
    return `<span class="status-badge status-${normalized || 'unknown'}">${label}</span>${extra}`;
  }

  function needsRefresh(status, error) {
    const normalized = (status || '').toLowerCase();
    if (!normalized) return false;
    if (error && String(error).trim()) return false;
    return normalized === 'pending' || normalized === 'processing' || normalized === 'scanning';
  }

  function canVirusScan(status) {
    const normalized = (status || '').toLowerCase();
    return normalized !== 'clean' && normalized !== 'malicious' && normalized !== 'scanning';
  }

  function getVirusScanLabel(status) {
    const normalized = (status || '').toLowerCase();
    return normalized === 'scanning' ? 'Scan läuft...' : 'Virenscan';
  }

  async function readErrorMessage(res) {
    const contentType = res.headers.get('content-type') || '';
    if (contentType.includes('application/json') || contentType.includes('application/problem+json')) {
      try {
        const data = await res.json();
        return data.detail || data.title || JSON.stringify(data);
      } catch {
        return 'Unbekannter Fehler';
      }
    }
    return res.text().catch(() => 'Unbekannter Fehler');
  }

  function setupActionMenus(container) {
    const portalRoot = document.body;
    const openPanels = new Set();

    const getPanel = (menu) => {
      if (!menu) return null;
      let panel = menu.__menuPanel;
      if (!panel) {
        panel = menu.querySelector('.menu-panel');
        if (panel) menu.__menuPanel = panel;
      }
      if (panel) panel.__menuOwner = menu;
      if (panel && !panel.__menuHome) {
        panel.__menuHome = { parent: panel.parentElement, next: panel.nextSibling };
      }
      return panel || null;
    };

    const restorePanel = (menu) => {
      const panel = getPanel(menu);
      if (!panel) return;
      panel.classList.remove('is-open');
      panel.style.position = '';
      panel.style.zIndex = '';
      const home = panel.__menuHome;
      const parent = home && home.parent ? home.parent : menu;
      if (!parent || !parent.isConnected) {
        panel.remove();
        openPanels.delete(panel);
        return;
      }
      if (panel.parentElement !== parent) {
        if (home && home.next && home.next.parentNode === parent) {
          parent.insertBefore(panel, home.next);
        } else {
          parent.appendChild(panel);
        }
      }
      panel.style.left = '';
      panel.style.top = '';
      openPanels.delete(panel);
    };

    const closeMenu = (menu) => {
      menu.classList.remove('open');
      const toggle = menu.querySelector('.menu-toggle');
      if (toggle) toggle.setAttribute('aria-expanded', 'false');
      restorePanel(menu);
    };

    const getAllOpenPanels = () => {
      const panels = new Set(openPanels);
      document.querySelectorAll('.menu-panel.is-open').forEach(panel => panels.add(panel));
      return Array.from(panels);
    };

    const closeAll = (exceptMenu = null) => {
      const panels = getAllOpenPanels();
      panels.forEach(panel => {
        const owner = panel.__menuOwner;
        if (owner && owner.isConnected && owner !== exceptMenu) {
          closeMenu(owner);
          return;
        }
        panel.classList.remove('is-open');
        panel.style.position = '';
        panel.style.zIndex = '';
        panel.remove();
        openPanels.delete(panel);
      });
    };

    const positionMenu = (menu, toggle, panel) => {
      const menuPanel = panel || getPanel(menu);
      if (!menuPanel) return;
      const rect = toggle.getBoundingClientRect();
      const panelWidth = menuPanel.offsetWidth || 180;
      const panelHeight = menuPanel.offsetHeight || 0;
      const margin = 12;
      let left = rect.right - panelWidth;
      if (left < margin) left = margin;
      const maxLeft = window.innerWidth - panelWidth - margin;
      if (left > maxLeft) left = Math.max(margin, maxLeft);
      let top = rect.bottom + 8;
      if (top + panelHeight > window.innerHeight - margin) {
        top = rect.top - panelHeight - 8;
        if (top < margin) top = margin;
      }
      menuPanel.style.left = `${left}px`;
      menuPanel.style.top = `${top}px`;
    };

    const openMenu = (menu, toggle) => {
      closeAll(menu);
      menu.classList.add('open');
      toggle.setAttribute('aria-expanded', 'true');
      const panel = getPanel(menu);
      if (panel) {
        if (panel.parentElement !== portalRoot) {
          portalRoot.appendChild(panel);
        }
        panel.classList.add('is-open');
        panel.style.position = 'fixed';
        panel.style.zIndex = '1000';
        openPanels.add(panel);
        positionMenu(menu, toggle, panel);
      }
    };

    const onDocumentClick = (ev) => {
      const toggle = ev.target.closest('.menu-toggle');
      if (toggle && container.contains(toggle)) {
        const menu = toggle.closest('.action-menu');
        if (!menu) return;
        if (menu.classList.contains('open')) {
          closeMenu(menu);
        } else {
          openMenu(menu, toggle);
        }
        return;
      }
      closeAll();
    };

    document.addEventListener('click', onDocumentClick);
    const onWindowChange = () => closeAll();
    window.addEventListener('resize', onWindowChange);
    window.addEventListener('scroll', onWindowChange, true);
    const cleanup = () => {
      closeAll();
      document.removeEventListener('click', onDocumentClick);
      window.removeEventListener('resize', onWindowChange);
      window.removeEventListener('scroll', onWindowChange, true);
    };
    cleanup.closeAll = closeAll;
    return cleanup;
  }

  const virusScanPolls = new Map();
  const VIRUS_SCAN_POLL_MS = 5000;

  function startVirusScanPoll(id, { onUpdate, onDone, onError } = {}) {
    if (!id || virusScanPolls.has(id)) return;

    let stopped = false;
    const stop = () => {
      stopped = true;
      virusScanPolls.delete(id);
    };

    virusScanPolls.set(id, stop);

    const poll = async () => {
      if (stopped) return;
      try {
        const r = await fetch(`/api/documents/${encodeURIComponent(id)}/virus-scan`, { method: 'POST' });
        if (!r.ok) {
          const msg = await readErrorMessage(r);
          throw new Error(msg || 'Virenscan fehlgeschlagen');
        }
        const data = await r.json().catch(() => ({}));
        if (onUpdate) onUpdate(data);
        const status = (data.status || '').toLowerCase();
        if (status === 'scanning') {
          setTimeout(poll, VIRUS_SCAN_POLL_MS);
          return;
        }
        stop();
        if (onDone) onDone(data);
      } catch (e) {
        stop();
        if (onError) onError(e);
      }
    };

    poll();
  }

  function renderList() {
    setTitle('list');
    root.innerHTML = `
      <div class="glass-card list-card">
        <nav class="top-actions title-bar">
          <a class="link" href="./index.html">Start</a>
          <div class="page-title" id="listTitle">Dokumentenliste</div>
          <a class="link" href="./new.html">Neu</a>
        </nav>
        <div class="search-row">
          <div class="field">
            <label class="lbl" for="searchInput">Suche</label>
            <input id="searchInput" type="text" placeholder="Titel, Dateiname, OCR-Text..." />
          </div>
          <div class="search-actions">
            <button id="clearBtn" class="btn ghost">Zurücksetzen</button>
          </div>
        </div>
        <div id="searchHint" class="search-meta" role="status" aria-live="polite"></div>
        <p id="listMsg" class="msg" aria-live="polite"></p>
        <div id="list" class="scroll-area" role="region" aria-label="Dokumentenliste"></div>
      </div>
    `;

    const listEl = root.querySelector('#list');
    const listMsg = root.querySelector('#listMsg');
    const searchInput = root.querySelector('#searchInput');
    const clearBtn = root.querySelector('#clearBtn');
    const searchHint = root.querySelector('#searchHint');
    let isActive = true;
    let refreshHandle;
    let debounceHandle;
    let currentQuery = '';

    const cleanupMenus = setupActionMenus(root);
    const closeMenus = () => cleanupMenus.closeAll && cleanupMenus.closeAll();
    listEl.__closeMenus = closeMenus;

    const refresh = async (silent = false) => {
      closeMenus();
      const shouldRefresh = await loadDocs(listEl, currentQuery, searchHint, listMsg, silent, () => isActive);
      if (refreshHandle) {
        clearTimeout(refreshHandle);
        refreshHandle = null;
      }
      if (shouldRefresh) {
        refreshHandle = setTimeout(() => refresh(true), 6000);
      }
    };
    refresh();

    const runSearch = () => {
      currentQuery = (searchInput.value || '').trim();
      refresh();
    };

    clearBtn.addEventListener('click', () => {
      clearTimeout(debounceHandle);
      const hadQuery = currentQuery.trim() !== '';
      searchInput.value = '';
      currentQuery = '';
      if (searchHint) searchHint.textContent = '';
      if (hadQuery) {
        refresh(true);
      }
      searchInput.focus();
    });
    searchInput.addEventListener('keydown', (ev) => {
      if (ev.key === 'Enter') runSearch();
    });
    searchInput.addEventListener('input', () => {
      clearTimeout(debounceHandle);
      debounceHandle = setTimeout(runSearch, 350);
    });

    const onResize = () => requestAnimationFrame(() => syncScrollAreaOverflow(listEl));
    window.addEventListener('resize', onResize);
    const resizeObserver = window.ResizeObserver
      ? new ResizeObserver(() => requestAnimationFrame(() => syncScrollAreaOverflow(listEl)))
      : null;
    if (resizeObserver) resizeObserver.observe(listEl);

    return () => {
      isActive = false;
      clearTimeout(refreshHandle);
      clearTimeout(debounceHandle);
      window.removeEventListener('resize', onResize);
      if (resizeObserver) resizeObserver.disconnect();
      delete listEl.__closeMenus;
      cleanupMenus();
    };
  }

  function formatBytes(bytes) {
    if (!bytes && bytes !== 0) return '';
    const units = ['B', 'KB', 'MB', 'GB'];
    let i = 0; let v = bytes;
    while (v >= 1024 && i < units.length - 1) { v /= 1024; i++; }
    return `${v.toFixed(v < 10 && i > 0 ? 1 : 0)} ${units[i]}`;
  }

  function formatDateTime(value) {
    if (!value) return '—';
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) return '—';
    const pad = (num) => String(num).padStart(2, '0');
    const day = pad(date.getDate());
    const month = pad(date.getMonth() + 1);
    const year = String(date.getFullYear()).slice(-2);
    const hours = pad(date.getHours());
    const minutes = pad(date.getMinutes());
    return `${day}.${month}.${year}, ${hours}:${minutes}`;
  }

  function truncateText(text, maxLen) {
    const value = (text ?? '').toString();
    if (!maxLen || value.length <= maxLen) return value;
    if (maxLen <= 3) return value.slice(0, maxLen);
    return `${value.slice(0, maxLen - 3)}...`;
  }

  function formatContentType(type) {
    const normalized = (type || '').toLowerCase();
    if (!normalized) return '—';
    if (normalized === 'application/pdf' || normalized.endsWith('/pdf')) return 'PDF-Datei';
    if (normalized === 'text/plain') return 'Textdatei';
    return type;
  }

  function getDocsSignature(docs) {
    if (!Array.isArray(docs)) return '';
    return JSON.stringify(docs.map(d => ([
      d.id || '',
      d.name || '',
      d.sizeBytes || 0,
      d.contentType || '',
      d.uploadTime || '',
      d.ocrStatus || '',
      d.ocrCompletedAt || '',
      d.summaryStatus || '',
      d.summaryCompletedAt || '',
      d.virusScanStatus || '',
      d.virusScanCompletedAt || ''
    ])));
  }

  function getDetailsSignature(d) {
    if (!d) return '';
    return JSON.stringify({
      id: d.id || '',
      name: d.name || '',
      originalFileName: d.originalFileName || '',
      contentType: d.contentType || '',
      sizeBytes: d.sizeBytes || 0,
      uploadTime: d.uploadTime || '',
      ocrStatus: d.ocrStatus || '',
      ocrCompletedAt: d.ocrCompletedAt || '',
      ocrError: d.ocrError || '',
      ocrText: d.ocrText || '',
      summaryStatus: d.summaryStatus || '',
      summaryCompletedAt: d.summaryCompletedAt || '',
      summaryError: d.summaryError || '',
      summaryText: d.summaryText || '',
      virusScanStatus: d.virusScanStatus || '',
      virusScanCompletedAt: d.virusScanCompletedAt || '',
      virusScanError: d.virusScanError || ''
    });
  }

  function syncScrollAreaOverflow(el) {
    if (!el) return;
    const threshold = 24;
    const diffY = Math.ceil(el.scrollHeight - el.clientHeight);
    const diffX = Math.ceil(el.scrollWidth - el.clientWidth);
    const needsY = diffY > threshold;
    const needsX = diffX > threshold;
    el.style.overflowY = needsY ? 'auto' : 'hidden';
    el.style.overflowX = needsX ? 'auto' : 'hidden';
    el.classList.toggle('no-scrollbar', !needsY && !needsX);
    if (!needsY) el.scrollTop = 0;
    if (!needsX) el.scrollLeft = 0;
  }

  async function loadDocs(listEl, query, hintEl, msgEl, silent = false, isActive = () => true) {
    if (!silent) listEl.innerHTML = '<div style="padding:12px;">Lädt...</div>';
    if (!silent && msgEl) {
      msgEl.textContent = '';
      msgEl.className = 'msg';
    }
    try {
      const url = query
        ? `/api/documents/search?q=${encodeURIComponent(query)}`
        : '/api/documents';
      const res = await fetch(url, { cache: 'no-store' });
      if (!res.ok) throw new Error('Fehler beim Laden');

      const docs = await res.json();
      if (!Array.isArray(docs) || docs.length === 0) {
        const signature = `empty:${query || ''}`;
        if (silent && listEl.dataset.signature === signature && listEl.dataset.hasError !== '1') return false;
        listEl.dataset.signature = signature;
        delete listEl.dataset.hasError;
        if (listEl.__closeMenus) listEl.__closeMenus();
        listEl.innerHTML = query
          ? '<div style="padding:12px;"><em>Keine Treffer.</em></div>'
          : '<div style="padding:12px;"><em>Keine Dokumente vorhanden.</em></div>';
        requestAnimationFrame(() => syncScrollAreaOverflow(listEl));
        if (hintEl) hintEl.textContent = query ? '0 Treffer' : '';
        return false;
      }
      if (hintEl) {
        hintEl.textContent = query ? `${docs.length} Treffer` : '';
      }

      const signature = getDocsSignature(docs);
      const shouldRefresh = docs.some(d =>
        needsRefresh(d.ocrStatus, d.ocrError) ||
        needsRefresh(d.summaryStatus, d.summaryError)
      );
      if (silent && listEl.dataset.signature === signature && listEl.dataset.hasError !== '1') {
        return shouldRefresh;
      }

      if (listEl.__closeMenus) listEl.__closeMenus();
      const table = document.createElement('table');
      table.className = 'table';
      table.innerHTML = `
        <thead>
          <tr>
            <th scope="col">Name</th>
            <th scope="col">Größe</th> 
            <th scope="col">Typ</th>
            <th scope="col">OCR</th>
            <th scope="col">Summary</th>
            <th scope="col">Virenscan</th>
            <th scope="col">Upload</th>
            <th scope="col">Aktion</th>
          </tr>
        </thead>
        <tbody>
          ${docs.map(d => {
            const uploaded = formatDateTime(d.uploadTime);
            const nameRaw = d.name ?? '(ohne Name)';
            const nameShort = truncateText(nameRaw, 50);
            const safeName = escapeHtml(nameRaw);
            const safeNameShort = escapeHtml(nameShort);
            const id = d.id;
            const safeId = escapeHtml(id || '—');
            const details = id ? `./details.html?id=${encodeURIComponent(id)}` : '#';
            const dl = id ? `/api/documents/${encodeURIComponent(id)}/download` : '#';
            const scanState = (d.virusScanStatus || '').toLowerCase();
            const scanDisabled = !canVirusScan(d.virusScanStatus);
            const scanLabel = getVirusScanLabel(d.virusScanStatus);
            const scanBtn = id
              ? `<button class="menu-item" data-scan="${id}" data-scan-state="${scanState}" ${scanDisabled ? 'disabled' : ''}>${scanLabel}</button>`
              : '';
            return `
              <tr>
                <td>
                  <a class="doc-name" href="${details}" title="${safeName}">${safeNameShort}</a>
                  <div class="doc-id">${safeId}</div>
                </td>
                <td class="cell-muted">${formatBytes(d.sizeBytes)}</td>
                <td class="cell-muted">${formatContentType(d.contentType)}</td>
                <td>${renderStatusBadge(d.ocrStatus, d.ocrCompletedAt)}</td>
                <td>${renderStatusBadge(d.summaryStatus, d.summaryCompletedAt)}</td>
                <td>${renderStatusBadge(d.virusScanStatus, d.virusScanCompletedAt)}</td>
                <td class="cell-muted">${uploaded}</td>
                <td>
                  ${id ? `
                    <div class="action-menu" data-doc="${id}">
                      <button class="btn icon menu-toggle" aria-haspopup="true" aria-expanded="false" aria-label="Aktionen">⋯</button>
                      <div class="menu-panel" role="menu">
                        ${scanBtn}
                        <div class="menu-sep" role="separator"></div>
                        <a class="menu-item" href="${dl}" rel="external" target="_blank">Herunterladen</a>
                        <button class="menu-item danger" data-del="${id}">Löschen</button>
                      </div>
                    </div>
                  ` : ''}
                </td>
              </tr>
            `;
          }).join('')}
        </tbody>
      `;

      listEl.innerHTML = '';
      listEl.appendChild(table);
      listEl.dataset.signature = signature;
      delete listEl.dataset.hasError;
      requestAnimationFrame(() => syncScrollAreaOverflow(listEl));

      listEl.querySelectorAll('button[data-del]').forEach(btn => {
        btn.addEventListener('click', async () => {
          if (!isActive()) return;
          const id = btn.getAttribute('data-del');
          if (!id) return;
          if (!confirm('Wirklich löschen?')) return;
          const r = await fetch(`/api/documents/${encodeURIComponent(id)}`, { method: 'DELETE' });
          if (r.status === 204) {
            loadDocs(listEl, query, hintEl, msgEl, false, isActive);
          } else {
            const msg = await readErrorMessage(r);
            if (msgEl) {
              msgEl.textContent = msg || 'Löschen fehlgeschlagen';
              msgEl.className = 'msg err';
            }
          }
        });
      });

      listEl.querySelectorAll('button[data-scan]').forEach(btn => {
        btn.addEventListener('click', async () => {
          if (!isActive()) return;
          const id = btn.getAttribute('data-scan');
          if (!id) return;
          const scanState = btn.getAttribute('data-scan-state') || '';
          if (scanState !== 'scanning') {
            if (!confirm('Datei wirklich an VirusTotal senden und scannen?')) return;
          }

          btn.disabled = true;
          const prevText = btn.textContent;
          btn.textContent = 'Scan läuft...';
          startVirusScanPoll(id, {
            onUpdate: () => {
              if (!isActive()) return;
              loadDocs(listEl, query, hintEl, msgEl, true, isActive);
            },
            onDone: () => {
              if (!isActive()) return;
              loadDocs(listEl, query, hintEl, msgEl, true, isActive);
            },
            onError: (e) => {
              if (!isActive()) return;
              btn.disabled = false;
              btn.textContent = prevText || 'Virenscan';
              if (msgEl) {
                msgEl.textContent = e.message || 'Virenscan fehlgeschlagen';
                msgEl.className = 'msg err';
              }
            }
          });
        });
      });

      const scanningDocs = docs.filter(d => (d.virusScanStatus || '').toLowerCase() === 'scanning');
      scanningDocs.forEach(d => {
        startVirusScanPoll(d.id, {
          onUpdate: () => {
            if (!isActive()) return;
            loadDocs(listEl, query, hintEl, msgEl, true, isActive);
          },
          onDone: () => {
            if (!isActive()) return;
            loadDocs(listEl, query, hintEl, msgEl, true, isActive);
          }
        });
      });

      return shouldRefresh;
    } catch (e) {
      if (listEl.__closeMenus) listEl.__closeMenus();
      listEl.innerHTML = `<div style="padding:12px;color:#ff9b9b;">${e.message || 'Unbekannter Fehler'}</div>`;
      listEl.dataset.hasError = '1';
      listEl.dataset.signature = '';
      requestAnimationFrame(() => syncScrollAreaOverflow(listEl));
      if (hintEl) hintEl.textContent = '';
      if (msgEl) {
        msgEl.textContent = e.message || 'Unbekannter Fehler';
        msgEl.className = 'msg err';
      }
      return false;
    }
  }

  function renderNew() {
    setTitle('new');
    root.innerHTML = `
      <div class="glass-card">
        <nav class="top-actions title-bar">
          <a class="link" href="./index.html">Start</a>
          <div class="page-title">Neues Dokument</div>
          <a class="link" href="./list.html">Liste</a>
        </nav>

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
        <nav class="top-actions title-bar">
          <a class="link" href="./list.html">Liste</a>
          <div class="page-title" id="detailsTitle">...</div>
          <a class="link" href="./index.html">Start</a>
        </nav>
        <section id="details"></section>
      </div>
    `;

    const out = root.querySelector('#details');
    const cleanupMenus = setupActionMenus(root);
    let isActive = true;

    if (!id) {
      out.innerHTML = '<p style="color:#ff9b9b;">Fehlende ID.</p>';
      return () => cleanupMenus();
    }

    let refreshHandle;
    let lastDetailsSignature = null;

    const runVirusScanPoll = () => {
      startVirusScanPoll(id, {
        onUpdate: () => {
          if (!isActive) return;
          fetchDetails();
        },
        onDone: () => {
          if (!isActive) return;
          fetchDetails();
        },
        onError: (e) => {
          if (!isActive) return;
          const errMsg = e?.message || 'Virenscan fehlgeschlagen';
          const msgEl = root.querySelector('#detailsMsg');
          if (msgEl) {
            msgEl.textContent = errMsg;
            msgEl.className = 'msg err';
          }
          const scanMsgEl = root.querySelector('#virusScanMsg');
          if (scanMsgEl) {
            scanMsgEl.textContent = errMsg;
            scanMsgEl.className = 'msg err';
          }
          const scanBtnEl = root.querySelector('#virusScanBtn');
          if (scanBtnEl) {
            scanBtnEl.disabled = false;
            scanBtnEl.textContent = 'Virenscan';
          }
        }
      });
    };

    const scheduleRefresh = (shouldRefresh) => {
      if (refreshHandle) {
        clearTimeout(refreshHandle);
        refreshHandle = null;
      }
      if (shouldRefresh) {
        refreshHandle = setTimeout(() => fetchDetails(), 5000);
      }
    };

    const fetchDetails = async () => {
      if (!isActive) return;
      try {
        const res = await fetch(`/api/documents/${encodeURIComponent(id)}`, { cache: 'no-store' });
        if (res.status === 404) {
          out.innerHTML = '<p><em>Nicht gefunden.</em></p>';
          return;
        }
        if (!res.ok) throw new Error('Ladefehler');

        const d = await res.json();
        const uploaded = formatDateTime(d.uploadTime);
        const dlUrl = `/api/documents/${encodeURIComponent(id)}/download`;
        const inlineUrl = `${dlUrl}?inline=true`;
        const displayName = d.name ?? '(ohne Name)';
        const safeDisplayName = escapeHtml(displayName);
        const safeOriginal = escapeHtml(d.originalFileName ?? '—');
        const safeType = escapeHtml(formatContentType(d.contentType));
        const safeSize = escapeHtml(formatBytes(d.sizeBytes));
        const safeUpload = escapeHtml(uploaded);
        const divider = '<div class="section-divider" aria-hidden="true"></div>';
        const ocrPreview = d.ocrText
          ? `<div class="ocr-preview"><div class="lbl"><strong>OCR-Vorschau:</strong></div><pre>${escapeHtml(d.ocrText)}</pre></div>${divider}`
          : '';
        const summaryPreview = d.summaryText
          ? `<div class="ocr-preview"><div class="lbl"><strong>KI-Zusammenfassung:</strong></div><pre>${escapeHtml(d.summaryText)}</pre></div>${divider}`
          : '';
        const typeLower = (d.contentType || '').toLowerCase();
        let watermarkLabel = '';
        if (typeLower.includes('pdf')) {
          watermarkLabel = 'PDF';
        } else if (typeLower.startsWith('text/')) {
          watermarkLabel = 'TXT';
        }
        const watermark = watermarkLabel
          ? `<div class="filetype-watermark" aria-hidden="true">${watermarkLabel}</div>`
          : '';
        const detailsTitle = root.querySelector('#detailsTitle');
        if (detailsTitle) {
          detailsTitle.textContent = displayName;
          detailsTitle.title = displayName;
        }

        const scanStatus = (d.virusScanStatus || '').toLowerCase();
        const scanDisabled = !canVirusScan(d.virusScanStatus);
        const scanLabel = getVirusScanLabel(d.virusScanStatus);
        const signature = getDetailsSignature(d);
        const shouldRefresh = needsRefresh(d.ocrStatus, d.ocrError) || needsRefresh(d.summaryStatus, d.summaryError);

        if (signature === lastDetailsSignature) {
          if (scanStatus === 'scanning') {
            runVirusScanPoll();
          }
          scheduleRefresh(shouldRefresh);
          return;
        }
        lastDetailsSignature = signature;
        const scanStatusText = scanStatus === 'scanning'
          ? 'Scan läuft...'
          : '';

        out.innerHTML = `
          <div class="details-toolbar">
            ${watermark}
            <div class="actions" style="margin:0 0 0 auto;flex:none;flex-wrap:wrap;">
              <div class="action-menu info-menu">
                <button class="btn icon menu-toggle info-toggle" aria-haspopup="true" aria-expanded="false" aria-label="Dateiinfos">i</button>
                <div class="menu-panel info-panel" role="dialog" aria-label="Dateiinfos">
                  <div class="meta-block info-meta">
                    <p><strong>Name:</strong> ${safeDisplayName}</p>
                    <p><strong>Original-Dateiname:</strong> ${safeOriginal}</p>
                    <p><strong>Typ:</strong> ${safeType}</p>
                    <p><strong>Größe:</strong> ${safeSize}</p>
                    <p><strong>Upload:</strong> ${safeUpload}</p>
                  </div>
                </div>
              </div>
              <div class="action-menu">
                <button class="btn icon menu-toggle" aria-haspopup="true" aria-expanded="false" aria-label="Aktionen">⋯</button>
                <div class="menu-panel" role="menu">
                  <button id="virusScanBtn" class="menu-item" data-scan-state="${scanStatus}" ${scanDisabled ? 'disabled' : ''}>${scanLabel}</button>
                  <div class="menu-sep" role="separator"></div>
                  <a class="menu-item" href="${dlUrl}" rel="external" target="_blank">Herunterladen</a>
                  <button id="del" class="menu-item danger">Löschen</button>
                </div>
              </div>
              <a class="btn icon plus" href="./new.html" title="Weiteres hinzufügen" aria-label="Weiteres hinzufügen">+</a>
            </div>
          </div>
          <p id="detailsMsg" class="msg" aria-live="polite"></p>
          <p><strong>OCR-Status:</strong> ${renderStatusBadge(d.ocrStatus, d.ocrCompletedAt)} ${d.ocrError ? `<span class="status-muted">${escapeHtml(d.ocrError)}</span>` : ''}</p>
          ${ocrPreview}
          <p><strong>Summary-Status:</strong> ${renderStatusBadge(d.summaryStatus, d.summaryCompletedAt)} ${d.summaryError ? `<span class="status-muted">${escapeHtml(d.summaryError)}</span>` : ''}</p>
          ${summaryPreview}
          <p><strong>Virenscan-Status:</strong> ${renderStatusBadge(d.virusScanStatus, d.virusScanCompletedAt)} ${d.virusScanError ? `<span class="status-muted">${escapeHtml(d.virusScanError)}</span>` : ''}</p>
          <p id="virusScanMsg" class="status-muted">${scanStatusText}</p>
          ${divider}
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

        const detailsMsg = root.querySelector('#detailsMsg');
        root.querySelector('#del').addEventListener('click', async () => {
          if (!confirm('Wirklich löschen?')) return;
          const del = await fetch(`/api/documents/${encodeURIComponent(id)}`, { method: 'DELETE' });
          if (del.status === 204) {
            navigate('./list.html');
          } else {
            const msg = await readErrorMessage(del);
            if (detailsMsg) {
              detailsMsg.textContent = msg || 'Löschen fehlgeschlagen';
              detailsMsg.className = 'msg err';
            }
          }
        });

        const scanBtn = root.querySelector('#virusScanBtn');
        const scanMsg = root.querySelector('#virusScanMsg');
        if (scanStatus === 'scanning') {
          runVirusScanPoll();
        }
        if (scanBtn && !scanBtn.disabled) {
          scanBtn.addEventListener('click', async () => {
            if (!confirm('Datei wirklich an VirusTotal senden und scannen?')) return;
            scanBtn.disabled = true;
            if (scanMsg) {
              scanMsg.textContent = 'Scan läuft...';
              scanMsg.className = 'status-muted';
            }
            runVirusScanPoll();
          });
        }

        scheduleRefresh(shouldRefresh);
      } catch (e) {
        out.innerHTML = `<p style="color:#ff9b9b;">${e.message || 'Fehler'}</p>`;
        lastDetailsSignature = null;
      }
    };

    fetchDetails();
    return () => {
      isActive = false;
      clearTimeout(refreshHandle);
      cleanupMenus();
    };
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