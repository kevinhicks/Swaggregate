/* global state */
let allServices = [];
let searchTerm = '';

const $ = id => document.getElementById(id);

/* ============================================================
   Boot
   ============================================================ */
document.addEventListener('DOMContentLoaded', () => {
  initTheme();
  loadSpecs();
  $('search-input').addEventListener('input', onSearch);
  $('search-clear').addEventListener('click', clearSearch);
  $('theme-toggle').addEventListener('click', toggleTheme);
});

/* ============================================================
   Fetch specs from the middleware endpoint
   ============================================================ */
async function loadSpecs() {
  try {
    const base = window.location.pathname.replace(/\/[^/]*$/, '');
    const res = await fetch(`${base}/specs`);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const data = await res.json();

    $('portal-title').textContent = data.title || 'API Docs';
    document.title = data.title || 'API Documentation Portal';

    allServices = data.services || [];
    $('loading').style.display = 'none';
    $('endpoints-container').style.display = 'block';

    renderAll(allServices);
    buildNav(allServices);
  } catch (err) {
    $('loading').style.display = 'none';
    $('error-banner').style.display = 'block';
    $('error-banner').textContent = `Failed to load API specs: ${err.message}`;
  }
}

/* ============================================================
   Render all services + endpoints
   ============================================================ */
function renderAll(services, query = '') {
  const container = $('endpoints-container');
  container.innerHTML = '';

  let totalVisible = 0;

  for (const svc of services) {
    const section = renderService(svc, query);
    if (section) { container.appendChild(section); totalVisible++; }
  }

  if (totalVisible === 0) {
    container.innerHTML = `
      <div class="no-results">
        <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
          <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
        </svg>
        <p>No endpoints match "<strong>${escapeHtml(query)}</strong>"</p>
      </div>`;
  }

  updateSearchStats(query);
}

function renderService(svc, query) {
  const filteredEndpoints = query
    ? svc.endpoints.filter(ep => matchesSearch(ep, svc.name, query))
    : svc.endpoints;

  // Hide entire service section if no match
  if (query && filteredEndpoints.length === 0 && !matchesServiceHeader(svc, query)) return null;

  const section = document.createElement('section');
  section.className = 'service-section';
  section.dataset.service = svc.name;

  // Header
  const header = document.createElement('div');
  header.className = 'service-header';
  header.innerHTML = `
    <div>
      <div class="service-title">${highlight(escapeHtml(svc.name), query)}</div>
      <div class="service-meta">
        ${svc.version ? `<span class="service-version">v${escapeHtml(svc.version)}</span>` : ''}
        <a class="service-url" href="${escapeHtml(svc.sourceUrl)}" target="_blank" rel="noopener">${escapeHtml(svc.sourceUrl)}</a>
      </div>
      ${svc.description ? `<div class="service-description">${highlight(escapeHtml(svc.description), query)}</div>` : ''}
    </div>
    <span class="service-count">${filteredEndpoints.length} endpoint${filteredEndpoints.length !== 1 ? 's' : ''}</span>`;
  section.appendChild(header);

  if (svc.fetchError) {
    const err = document.createElement('div');
    err.className = 'service-error';
    err.textContent = `⚠ Could not load spec: ${svc.fetchErrorMessage || 'Unknown error'}`;
    section.appendChild(err);
    return section;
  }

  // Endpoint cards
  for (const ep of filteredEndpoints) {
    section.appendChild(renderEndpointCard(ep, query));
  }

  return section;
}

function renderEndpointCard(ep, query) {
  const card = document.createElement('div');
  card.className = `endpoint-card${ep.deprecated ? ' deprecated' : ''}`;

  const summaryEl = document.createElement('div');
  summaryEl.className = 'endpoint-summary';
  summaryEl.setAttribute('role', 'button');
  summaryEl.setAttribute('tabindex', '0');
  summaryEl.setAttribute('aria-expanded', 'false');
  summaryEl.innerHTML = `
    <span class="method-badge method-${ep.method}">${escapeHtml(ep.method)}</span>
    <span class="endpoint-path">${highlight(escapeHtml(ep.path), query)}</span>
    <span class="endpoint-summary-text">${highlight(escapeHtml(ep.summary || ''), query)}</span>
    ${ep.tags.length ? `<div class="endpoint-tags">${ep.tags.map(t => `<span class="endpoint-tag">${escapeHtml(t)}</span>`).join('')}</div>` : ''}
    ${ep.deprecated ? '<span class="deprecated-badge">Deprecated</span>' : ''}
    <span class="endpoint-chevron">▾</span>`;

  const details = document.createElement('div');
  details.className = 'endpoint-details';
  details.innerHTML = buildDetailsHtml(ep);

  // Toggle expand
  const toggle = () => {
    const open = details.classList.toggle('open');
    summaryEl.classList.toggle('open', open);
    summaryEl.setAttribute('aria-expanded', open);
    summaryEl.querySelector('.endpoint-chevron').classList.toggle('open', open);
  };
  summaryEl.addEventListener('click', toggle);
  summaryEl.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggle(); } });

  card.appendChild(summaryEl);
  card.appendChild(details);
  return card;
}

function buildDetailsHtml(ep) {
  let html = '';

  if (ep.description) {
    html += `<p class="full-description">${escapeHtml(ep.description)}</p>`;
  }

  // Parameters
  if (ep.parameters && ep.parameters.length > 0) {
    html += `<div class="detail-section">
      <div class="detail-label">Parameters</div>
      <table class="params-table">
        <thead><tr><th>Name</th><th>In</th><th>Type</th><th>Required</th><th>Description</th></tr></thead>
        <tbody>
          ${ep.parameters.map(p => `
            <tr>
              <td><span class="param-name">${escapeHtml(p.name)}</span></td>
              <td><span class="param-in">${escapeHtml(p.in)}</span></td>
              <td><span class="param-type">${escapeHtml(p.type || (p.format ? `${p.type}(${p.format})` : '') || '—')}</span></td>
              <td>${p.required ? '<span class="param-required">●</span>' : '<span class="param-optional">○</span>'}</td>
              <td>${escapeHtml(p.description || '—')}</td>
            </tr>`).join('')}
        </tbody>
      </table>
    </div>`;
  }

  // Request body
  if (ep.requestBody) {
    const schemas = Object.entries(ep.requestBody.content || {});
    html += `<div class="detail-section">
      <div class="detail-label">Request Body${ep.requestBody.required ? ' <span style="color:#ef4444">*required</span>' : ''}</div>
      ${ep.requestBody.description ? `<p style="font-size:12px;color:var(--text-secondary);margin-bottom:8px">${escapeHtml(ep.requestBody.description)}</p>` : ''}
      ${schemas.map(([mt, s]) => `
        <div style="margin-bottom:6px"><span class="param-in">${escapeHtml(mt)}</span></div>
        ${s.rawSchema ? `<div class="schema-block">${escapeHtml(JSON.stringify(s.rawSchema, null, 2))}</div>` : ''}
      `).join('')}
    </div>`;
  }

  // Responses
  if (ep.responses && ep.responses.length > 0) {
    html += `<div class="detail-section">
      <div class="detail-label">Responses</div>
      <div class="response-list">
        ${ep.responses.map(r => {
          const statusClass = statusBadgeClass(r.statusCode);
          const schemas = Object.entries(r.content || {});
          const id = `resp-${Math.random().toString(36).slice(2)}`;
          return `
            <div class="response-item">
              <div class="response-header" onclick="toggleResponse('${id}')">
                <span class="status-badge ${statusClass}">${escapeHtml(r.statusCode)}</span>
                <span class="response-desc">${escapeHtml(r.description || '')}</span>
                ${schemas.length ? '<span style="margin-left:auto;font-size:11px;color:var(--text-secondary)">▾</span>' : ''}
              </div>
              ${schemas.length ? `
                <div class="response-body" id="${id}">
                  ${schemas.map(([mt, s]) => `
                    <div style="margin-bottom:6px"><span class="param-in">${escapeHtml(mt)}</span></div>
                    ${s.rawSchema ? `<div class="schema-block">${escapeHtml(JSON.stringify(s.rawSchema, null, 2))}</div>` : ''}
                  `).join('')}
                </div>` : ''}
            </div>`;
        }).join('')}
      </div>
    </div>`;
  }

  return html || '<p style="color:var(--text-secondary);font-size:13px">No additional details available.</p>';
}

function toggleResponse(id) {
  const el = document.getElementById(id);
  if (el) el.classList.toggle('open');
}
window.toggleResponse = toggleResponse;

/* ============================================================
   Sidebar nav
   ============================================================ */
function buildNav(services) {
  const nav = $('service-nav');
  nav.innerHTML = '';

  for (const svc of services) {
    const sect = document.createElement('div');
    sect.className = 'nav-section';

    const hdr = document.createElement('div');
    hdr.className = 'nav-section-header';
    hdr.innerHTML = `<span class="chevron">▾</span>${escapeHtml(svc.name)}<span class="nav-count">${svc.endpoints.length}</span>`;
    hdr.addEventListener('click', () => {
      const endpointList = sect.querySelector('.nav-endpoints');
      const collapsed = endpointList.classList.toggle('hidden');
      hdr.classList.toggle('collapsed', collapsed);
    });

    const list = document.createElement('div');
    list.className = 'nav-endpoints';

    for (const ep of svc.endpoints) {
      const item = document.createElement('div');
      item.className = 'nav-endpoint';
      item.innerHTML = `<span class="nav-method" style="color:var(--method-${ep.method.toLowerCase()},var(--text-secondary))">${escapeHtml(ep.method)}</span><span class="nav-path">${escapeHtml(ep.path)}</span>`;
      item.addEventListener('click', () => scrollToEndpoint(svc.name, ep));
      list.appendChild(item);
    }

    sect.appendChild(hdr);
    sect.appendChild(list);
    nav.appendChild(sect);
  }
}

function scrollToEndpoint(serviceName, ep) {
  // Find matching card in DOM
  const sections = document.querySelectorAll('.service-section');
  for (const sect of sections) {
    if (sect.dataset.service === serviceName) {
      const cards = sect.querySelectorAll('.endpoint-card');
      for (const card of cards) {
        const path = card.querySelector('.endpoint-path');
        const method = card.querySelector('.method-badge');
        if (path && method && path.textContent === ep.path && method.textContent === ep.method) {
          card.scrollIntoView({ behavior: 'smooth', block: 'start' });
          // Auto-expand if not already open
          const details = card.querySelector('.endpoint-details');
          if (!details.classList.contains('open')) {
            card.querySelector('.endpoint-summary').click();
          }
          return;
        }
      }
    }
  }
}

/* ============================================================
   Search
   ============================================================ */
function onSearch(e) {
  searchTerm = e.target.value.trim();
  $('search-clear').style.display = searchTerm ? 'block' : 'none';
  renderAll(allServices, searchTerm);
  updateNavHighlight(searchTerm);
}

function clearSearch() {
  $('search-input').value = '';
  searchTerm = '';
  $('search-clear').style.display = 'none';
  renderAll(allServices, '');
  updateNavHighlight('');
}

function matchesSearch(ep, serviceName, query) {
  const q = query.toLowerCase();
  return (
    ep.path.toLowerCase().includes(q) ||
    (ep.summary || '').toLowerCase().includes(q) ||
    (ep.description || '').toLowerCase().includes(q) ||
    ep.method.toLowerCase().includes(q) ||
    ep.tags.some(t => t.toLowerCase().includes(q)) ||
    serviceName.toLowerCase().includes(q)
  );
}

function matchesServiceHeader(svc, query) {
  const q = query.toLowerCase();
  return svc.name.toLowerCase().includes(q) || (svc.description || '').toLowerCase().includes(q);
}

function updateSearchStats(query) {
  if (!query) { $('search-stats').textContent = ''; return; }
  let total = 0;
  for (const svc of allServices)
    total += svc.endpoints.filter(ep => matchesSearch(ep, svc.name, query)).length;
  $('search-stats').textContent = `${total} result${total !== 1 ? 's' : ''}`;
}

function updateNavHighlight(query) {
  const items = document.querySelectorAll('.nav-endpoint');
  for (const item of items) {
    const path = item.querySelector('.nav-path')?.textContent || '';
    const method = item.querySelector('.nav-method')?.textContent || '';
    const matches = !query || path.toLowerCase().includes(query.toLowerCase()) || method.toLowerCase().includes(query.toLowerCase());
    item.style.opacity = matches ? '1' : '0.3';
  }
}

/* ============================================================
   Theme
   ============================================================ */
function initTheme() {
  const saved = localStorage.getItem('swagger-agg-theme');
  const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
  const dark = saved ? saved === 'dark' : prefersDark;
  applyTheme(dark);
}

function toggleTheme() {
  const dark = document.documentElement.dataset.theme !== 'dark';
  applyTheme(dark);
  localStorage.setItem('swagger-agg-theme', dark ? 'dark' : 'light');
}

function applyTheme(dark) {
  document.documentElement.dataset.theme = dark ? 'dark' : '';
  $('icon-sun').style.display = dark ? 'none' : 'block';
  $('icon-moon').style.display = dark ? 'block' : 'none';
}

/* ============================================================
   Helpers
   ============================================================ */
function escapeHtml(str) {
  return String(str ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function highlight(text, query) {
  if (!query) return text;
  const escaped = query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  return text.replace(new RegExp(`(${escaped})`, 'gi'), '<mark>$1</mark>');
}

function statusBadgeClass(code) {
  if (code.startsWith('2')) return 'status-2xx';
  if (code.startsWith('3')) return 'status-3xx';
  if (code.startsWith('4')) return 'status-4xx';
  if (code.startsWith('5')) return 'status-5xx';
  return 'status-default';
}
