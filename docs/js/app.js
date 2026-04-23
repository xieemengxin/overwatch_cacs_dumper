// Minimal utilities for the showcase pages. No framework — vanilla DOM only.

async function loadJSON(path) {
  const r = await fetch(path);
  if (!r.ok) throw new Error(`HTTP ${r.status}: ${path}`);
  return r.json();
}

async function loadText(path) {
  const r = await fetch(path);
  if (!r.ok) throw new Error(`HTTP ${r.status}: ${path}`);
  return r.text();
}

function $(sel, root = document) { return root.querySelector(sel); }
function $$(sel, root = document) { return [...root.querySelectorAll(sel)]; }

function el(tag, attrs = {}, ...children) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs || {})) {
    if (k === 'class') node.className = v;
    else if (k === 'html') node.innerHTML = v;
    else if (k.startsWith('on') && typeof v === 'function') node.addEventListener(k.slice(2), v);
    else if (v !== null && v !== undefined) node.setAttribute(k, v);
  }
  for (const c of children.flat()) {
    if (c === null || c === undefined || c === false) continue;
    node.append(c instanceof Node ? c : document.createTextNode(String(c)));
  }
  return node;
}

// Sortable table helper.
// headers = [{key, label, render?, sortVal?}]
// rows = array of row objects
function makeTable(headers, rows, opts = {}) {
  const tbl = el('table', { class: 'sticky-head' });
  const thead = el('thead');
  const trHead = el('tr');
  headers.forEach((h, i) => {
    const th = el('th', { 'data-col': i }, h.label);
    th.addEventListener('click', () => sortBy(i));
    trHead.append(th);
  });
  thead.append(trHead);
  const tbody = el('tbody');
  tbl.append(thead, tbody);

  let currentSort = { col: -1, dir: 1 };
  let currentRows = [...rows];

  function render() {
    tbody.innerHTML = '';
    const frag = document.createDocumentFragment();
    currentRows.forEach(r => {
      const tr = el('tr');
      headers.forEach(h => {
        const td = el('td');
        const v = h.render ? h.render(r) : (r[h.key] ?? '');
        if (v instanceof Node) td.append(v);
        else if (typeof v === 'string' && v.includes('<')) td.innerHTML = v;
        else td.textContent = v;
        tr.append(td);
      });
      frag.append(tr);
    });
    tbody.append(frag);
  }

  function sortBy(i) {
    const h = headers[i];
    if (!h) return;
    if (currentSort.col === i) currentSort.dir *= -1;
    else { currentSort = { col: i, dir: 1 }; }
    const get = h.sortVal || (r => r[h.key]);
    currentRows.sort((a, b) => {
      const va = get(a), vb = get(b);
      if (va == null && vb == null) return 0;
      if (va == null) return 1;
      if (vb == null) return -1;
      if (typeof va === 'number' && typeof vb === 'number') return (va - vb) * currentSort.dir;
      return String(va).localeCompare(String(vb)) * currentSort.dir;
    });
    $$('th', thead).forEach((th, idx) => {
      th.classList.remove('sort-asc', 'sort-desc');
      if (idx === i) th.classList.add(currentSort.dir > 0 ? 'sort-asc' : 'sort-desc');
    });
    render();
  }

  tbl.setRows = (newRows) => {
    currentRows = [...newRows];
    if (currentSort.col >= 0) sortBy(currentSort.col);  // re-sort
    else render();
    return tbl;
  };

  render();
  if (opts.initialSort != null) sortBy(opts.initialSort);
  return tbl;
}

// Simple hex-to-int for sorting
function hexSort(s) {
  if (typeof s === 'number') return s;
  if (!s) return 0;
  return parseInt(String(s).replace(/^0x/i, ''), 16) || 0;
}

// Text filter helper — matches if any field contains query (case-insensitive, supports 0x hex prefix alignment)
function filterRows(rows, query, fields) {
  if (!query) return rows;
  const q = query.trim().toLowerCase();
  return rows.filter(r => fields.some(f => {
    const v = r[f];
    if (v == null) return false;
    return String(v).toLowerCase().includes(q);
  }));
}

// Simple .hpp syntax highlighting — keyword + string + number + comment + chinese.
function highlightHpp(src) {
  return src
    .replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/(\/\/[^\n]*)/g, '<span class="c">$1</span>')
    .replace(/\/\*[\s\S]*?\*\//g, m => `<span class="c">${m}</span>`)
    .replace(/\b(inline|constexpr|namespace|struct|enum|class|return|const|auto|void|noexcept|switch|case|default|if|else|break|static|uint32_t|uint16_t|uint8_t|int16_t|uint64_t|int|bool|std|size_t|template|typename|true|false|nullptr|for|while)\b/g, '<span class="k">$1</span>')
    .replace(/\b(StateVar|Ability|HeroInfo|EntityInfo|WeaponEntry|HeroKit|HeroKitGraph|HeroWeaponEntry|AbilitySlot|AbilityCategory|StateVarKind|StateVarDomain|EntityType|BallisticType)\b/g, '<span class="t">$1</span>')
    .replace(/"([^"\\]|\\.)*"/g, m => {
      // Detect if contains CJK
      if (/[一-鿿]/.test(m)) return `<span class="zh">${m}</span>`;
      return `<span class="s">${m}</span>`;
    })
    .replace(/\b(0x[0-9A-Fa-f]+u?|\d+u?)\b/g, '<span class="n">$1</span>');
}

// Build standard nav (called in each page's head)
function renderNav(activeId, buildTag) {
  const nav = el('nav', { class: 'navbar' });
  nav.append(
    el('div', { class: 'brand' }, 'OW CASC Dumper ', el('small', {}, 'showcase')),
    el('a', { href: 'index.html', class: activeId === 'home' ? 'active' : '' }, 'Overview'),
    el('a', { href: 'heroes.html', class: activeId === 'heroes' ? 'active' : '' }, 'Heroes'),
    el('a', { href: 'entities.html', class: activeId === 'entities' ? 'active' : '' }, 'Entities'),
    el('a', { href: 'statevars.html', class: activeId === 'statevars' ? 'active' : '' }, 'State Vars'),
    el('a', { href: 'sdk.html', class: activeId === 'sdk' ? 'active' : '' }, 'SDK Headers'),
    el('a', { href: 'overrides.html', class: activeId === 'overrides' ? 'active' : '' }, 'Overrides'),
    el('div', { class: 'spacer' }),
    el('span', { class: 'build-tag' }, buildTag || 'Build —'),
  );
  return nav;
}

function renderFooter() {
  return el('footer', {},
    'Overwatch CASC Dumper showcase · data extracted from CASC, no memory reads · ',
    el('a', { href: 'https://github.com/', target: '_blank' }, 'source')
  );
}
