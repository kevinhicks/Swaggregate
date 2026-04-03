/* global state */
let allServices = [];
let searchTerm = '';
let proxyBase = '';   // set once on load

const $ = id => document.getElementById(id);

/* ============================================================
   Boot
   ============================================================ */
document.addEventListener('DOMContentLoaded', () => {
  proxyBase = window.location.pathname.replace(/\/[^/]*$/, '');
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
    section.appendChild(renderEndpointCard(ep, svc, query));
  }

  return section;
}

function renderEndpointCard(ep, svc, query) {
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

  // Schema view
  const schemaView = document.createElement('div');
  schemaView.className = 'schema-view';
  schemaView.innerHTML = buildDetailsHtml(ep);

  // Try it out panel
  const tryPanel = buildTryItOutPanel(ep, svc);

  details.appendChild(schemaView);
  details.appendChild(tryPanel);

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
   Try It Out
   ============================================================ */
const HAS_BODY = new Set(['POST', 'PUT', 'PATCH']);

function buildTryItOutPanel(ep, svc) {
  const wrap = document.createElement('div');
  wrap.className = 'try-wrap';

  const canTry = !!svc.baseUrl;

  // Toggle button row
  const toggleRow = document.createElement('div');
  toggleRow.className = 'try-toggle-row';
  const btn = document.createElement('button');
  btn.className = 'try-toggle-btn';
  btn.textContent = 'Try it out';
  btn.disabled = !canTry;
  if (!canTry) btn.title = 'Base URL could not be determined for this service';
  toggleRow.appendChild(btn);
  wrap.appendChild(toggleRow);

  if (!canTry) return wrap;

  // Panel (hidden until toggled)
  const panel = document.createElement('div');
  panel.className = 'try-panel';

  // Build the raw path (strip pathPrefix for actual requests)
  const rawPath = svc.pathPrefix && ep.path.startsWith(svc.pathPrefix)
    ? ep.path.slice(svc.pathPrefix.length) || '/'
    : ep.path;

  // Path params
  const pathParams = (ep.parameters || []).filter(p => p.in === 'path');
  const queryParams = (ep.parameters || []).filter(p => p.in === 'query');
  const headerParams = (ep.parameters || []).filter(p => p.in === 'header');

  if (pathParams.length || queryParams.length || headerParams.length) {
    const paramSection = document.createElement('div');
    paramSection.className = 'try-section';
    const label = document.createElement('div');
    label.className = 'try-section-label';
    label.textContent = 'Parameters';
    paramSection.appendChild(label);

    [...pathParams, ...queryParams, ...headerParams].forEach(p => {
      const row = document.createElement('div');
      row.className = 'try-param-row';
      row.innerHTML = `
        <label class="try-param-label">
          <span class="param-name">${escapeHtml(p.name)}</span>
          <span class="param-in">${escapeHtml(p.in)}</span>
          ${p.required ? '<span class="param-required" title="required">*</span>' : ''}
        </label>
        <input class="try-param-input" type="text"
          data-param-name="${escapeHtml(p.name)}"
          data-param-in="${escapeHtml(p.in)}"
          placeholder="${escapeHtml(p.description || p.type || '')}" />`;
      paramSection.appendChild(row);
    });
    panel.appendChild(paramSection);
  }

  // Request body
  let bodyEditor = null;
  if (HAS_BODY.has(ep.method)) {
    const bodySection = document.createElement('div');
    bodySection.className = 'try-section';
    const bodyLabel = document.createElement('div');
    bodyLabel.className = 'try-section-label';
    bodyLabel.textContent = 'Request Body';
    bodySection.appendChild(bodyLabel);

    // Pre-populate from schema if available
    let placeholder = '{}';
    if (ep.requestBody?.content) {
      const schema = Object.values(ep.requestBody.content)[0]?.rawSchema;
      if (schema) placeholder = JSON.stringify(schema, null, 2);
    }
    bodyEditor = document.createElement('textarea');
    bodyEditor.className = 'try-body-editor';
    bodyEditor.placeholder = placeholder;
    bodyEditor.rows = 6;
    bodySection.appendChild(bodyEditor);
    panel.appendChild(bodySection);
  }

  // Execute button
  const execRow = document.createElement('div');
  execRow.className = 'try-exec-row';
  const execBtn = document.createElement('button');
  execBtn.className = 'try-execute-btn';
  execBtn.innerHTML = '<span class="try-exec-label">Execute</span>';
  execRow.appendChild(execBtn);
  panel.appendChild(execRow);

  // Response section
  const responseSection = document.createElement('div');
  responseSection.className = 'try-response';
  responseSection.style.display = 'none';
  panel.appendChild(responseSection);

  // Execute handler
  execBtn.addEventListener('click', async () => {
    // Build URL
    let url = svc.baseUrl + rawPath;
    const inputs = panel.querySelectorAll('.try-param-input');
    const queryParts = [];
    const reqHeaders = { 'Accept': 'application/json' };

    inputs.forEach(input => {
      const name = input.dataset.paramName;
      const inVal = input.dataset.paramIn;
      const val = input.value.trim();
      if (!val) return;
      if (inVal === 'path') url = url.replace(`{${name}}`, encodeURIComponent(val));
      else if (inVal === 'query') queryParts.push(`${encodeURIComponent(name)}=${encodeURIComponent(val)}`);
      else if (inVal === 'header') reqHeaders[name] = val;
    });
    if (queryParts.length) url += '?' + queryParts.join('&');

    const body = bodyEditor ? (bodyEditor.value.trim() || null) : null;
    if (body) reqHeaders['Content-Type'] = 'application/json';

    // Show loading
    responseSection.style.display = 'block';
    responseSection.innerHTML = '<div class="try-loading"><div class="spinner" style="width:20px;height:20px;border-width:2px"></div><span>Executing…</span></div>';
    execBtn.disabled = true;

    try {
      const res = await fetch(`${proxyBase}/proxy`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ url, method: ep.method, headers: reqHeaders, body })
      });
      const data = await res.json();

      if (!res.ok && !data.status) throw new Error(data.error || `Proxy error ${res.status}`);

      const statusClass = data.status >= 500 ? 'status-5xx'
        : data.status >= 400 ? 'status-4xx'
        : data.status >= 300 ? 'status-3xx'
        : 'status-2xx';

      let prettyBody = data.body || '';
      const ct = (data.headers?.['content-type'] || '');
      if (ct.includes('json')) {
        try { prettyBody = JSON.stringify(JSON.parse(data.body), null, 2); } catch {}
      }

      const headersId = `try-hdrs-${Math.random().toString(36).slice(2)}`;
      const hdrsHtml = Object.entries(data.headers || {})
        .map(([k, v]) => `<div><span class="try-hdr-key">${escapeHtml(k)}</span>: <span class="try-hdr-val">${escapeHtml(v)}</span></div>`)
        .join('');

      responseSection.innerHTML = `
        <div class="try-response-header">
          <span class="status-badge ${statusClass}">${data.status}</span>
          <span class="try-status-text">${escapeHtml(data.statusText || '')}</span>
          <button class="try-hdrs-toggle" onclick="toggleTryHeaders('${headersId}')">Headers</button>
          <span class="try-request-url">${escapeHtml(url)}</span>
        </div>
        <div class="try-response-headers" id="${headersId}">${hdrsHtml}</div>
        <pre class="try-response-body">${escapeHtml(prettyBody)}</pre>`;
    } catch (err) {
      responseSection.innerHTML = `<div class="try-error">⚠ ${escapeHtml(err.message)}</div>`;
    } finally {
      execBtn.disabled = false;
    }
  });

  // Toggle panel visibility
  btn.addEventListener('click', () => {
    const open = panel.classList.toggle('open');
    btn.classList.toggle('active', open);
    btn.textContent = open ? 'Cancel' : 'Try it out';
  });

  wrap.appendChild(panel);
  return wrap;
}

function toggleTryHeaders(id) {
  const el = document.getElementById(id);
  if (el) el.classList.toggle('open');
}
window.toggleTryHeaders = toggleTryHeaders;



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
