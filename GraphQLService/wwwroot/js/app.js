const STORAGE_KEY = "dbservices.graphql.connection";

const providerSelect = document.getElementById("providerSelect");
const connectionInput = document.getElementById("connectionInput");
const connectBtn = document.getElementById("connectBtn");
const statusBadge = document.getElementById("statusBadge");
const tablesTree = document.getElementById("tablesTree");
const queryTree = document.getElementById("queryTree");
const resultTree = document.getElementById("resultTree");
const tableCountLabel = document.getElementById("tableCountLabel");
const queryPanelTitle = document.getElementById("queryPanelTitle");
const queryEditor = document.getElementById("queryEditor");
const rawResult = document.getElementById("rawResult");
const footerMessage = document.getElementById("footerMessage");

let providers = [];
let lastRawJson = null;
let showRaw = false;
let selectedTableName = null;
let selectedTableMeta = null;
let lastProviderId = null;

async function api(path, options = {}) {
  const res = await fetch(path, {
    headers: { "Content-Type": "application/json", ...options.headers },
    ...options,
  });
  const text = await res.text();
  let data = null;
  try {
    data = text ? JSON.parse(text) : null;
  } catch {
    data = text;
  }
  if (!res.ok) {
    const msg = data?.message ?? (typeof data === "string" ? data : res.statusText);
    throw new Error(msg);
  }
  return data;
}

function setStatus(connected, provider, tableCount) {
  statusBadge.textContent = connected
    ? `已連線 · ${provider} · ${tableCount} 表`
    : "未連線";
  statusBadge.classList.toggle("connected", connected);
}

function setFooter(msg) {
  footerMessage.textContent = msg;
}

function saveConnection(provider, connectionString) {
  sessionStorage.setItem(
    STORAGE_KEY,
    JSON.stringify({ provider, connectionString }),
  );
}

function loadSavedConnection() {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function renderTree(container, nodes, options = {}) {
  container.innerHTML = "";
  if (!nodes?.length) {
    container.innerHTML = '<p class="tree-empty">（無資料）</p>';
    return;
  }
  const root = document.createElement("div");
  root.className = "tree-root";
  for (const node of nodes) {
    root.appendChild(createTreeNode(node, options));
  }
  container.appendChild(root);
}

function createTreeNode(node, options) {
  const hasChildren = node.children?.length > 0;
  const expanded = node.expanded ?? options.defaultExpanded ?? false;

  const wrap = document.createElement("div");
  wrap.className = "tree-node";
  wrap.dataset.id = node.id;

  const row = document.createElement("div");
  row.className = "tree-row";
  row.setAttribute("role", "treeitem");
  row.setAttribute("aria-expanded", hasChildren ? String(expanded) : "false");

  const toggle = document.createElement("span");
  toggle.className = "tree-toggle" + (hasChildren ? "" : " empty");
  toggle.textContent = hasChildren ? (expanded ? "▼" : "▶") : "·";

  const label = document.createElement("span");
  label.className = "tree-label" + (node.kind ? ` kind-${node.kind}` : "");
  label.textContent = node.label;
  label.title = node.label;

  row.appendChild(toggle);
  row.appendChild(label);
  wrap.appendChild(row);

  let childContainer = null;
  if (hasChildren) {
    childContainer = document.createElement("div");
    childContainer.className = "tree-children" + (expanded ? "" : " collapsed");
    for (const child of node.children) {
      childContainer.appendChild(createTreeNode(child, options));
    }
    wrap.appendChild(childContainer);
  }

  row.addEventListener("click", (e) => {
    e.stopPropagation();
    if (hasChildren && childContainer) {
      const isCollapsed = childContainer.classList.toggle("collapsed");
      toggle.textContent = isCollapsed ? "▶" : "▼";
      row.setAttribute("aria-expanded", String(!isCollapsed));
    }
    const scope = options.selectionScope
      ? row.closest(options.selectionScope)
      : row.closest(".tree, .tree-root");
    scope?.querySelectorAll(".tree-row.selected").forEach((el) => {
      el.classList.remove("selected");
    });
    row.classList.add("selected");

    if (options.onSelect) options.onSelect(node);
  });

  return wrap;
}

function buildQueryFromNode(node) {
  if (node.kind === "querySnippet" && node.label?.includes("meta")) {
    return "query {\n  meta {\n    tables\n  }\n}";
  }

  if (node.kind === "table" && node.tableName) {
    const fieldBlock = collectFieldNames(node, node.graphQlTypeName ?? node.tableName);
    const fields = fieldBlock.length ? fieldBlock.join("\n    ") : "id";
    const listName = node.listFieldName ?? toCamelPlural(node.graphQlTypeName ?? node.tableName);
    return `query {\n  ${listName} {\n    ${fields}\n  }\n}`;
  }

  const table = node.tableName;
  const typeName = node.graphQlTypeName ?? table;
  if (!table && !typeName) return null;

  const fields = collectFieldNames(node, typeName);
  const fieldBlock = fields.length ? fields.join("\n    ") : "id";

  if (node.kind === "querySnippet" && node.singularFieldName) {
    return `query {\n  ${node.singularFieldName}(id: 1) {\n    ${fieldBlock}\n  }\n}`;
  }

  if (node.kind === "querySnippet" && node.listFieldName === node.label?.split("(")[0]) {
    const name = node.listFieldName ?? node.byFieldName;
    if (node.label?.includes("ByField") || name?.includes("ByField")) {
      const base = name?.replace("ByField", "") ?? "items";
      return `query {\n  ${name ?? base + "ByField"}(key: "Name", value: "test") {\n    ${fieldBlock}\n  }\n}`;
    }
  }

  if (node.kind === "mutationSnippet") {
    const mut = node.label?.split("(")[0] ?? `create${typeName}`;
    return `mutation {\n  ${mut}(input: { }) {\n    ${fieldBlock}\n  }\n}`;
  }

  if (node.listFieldName) {
    return `query {\n  ${node.listFieldName} {\n    ${fieldBlock}\n  }\n}`;
  }

  if (node.singularFieldName) {
    return `query {\n  ${node.singularFieldName}(id: 1) {\n    ${fieldBlock}\n  }\n}`;
  }

  return `query {\n  ${toCamelPlural(typeName)} {\n    ${fieldBlock}\n  }\n}`;
}

function toCamelPlural(name) {
  if (!name) return "items";
  const c = name.charAt(0).toLowerCase() + name.slice(1);
  return c.endsWith("s") ? c : c + "s";
}

function collectFieldNames(node, typeName) {
  const fields = [];
  const walk = (n) => {
    if (n.kind === "field" && n.fieldName) fields.push(n.fieldName);
    n.children?.forEach(walk);
  };
  if (node.kind === "table" || node.kind === "folder") {
    node.children?.forEach((c) => {
      if (c.label === "欄位") c.children?.forEach(walk);
      else walk(c);
    });
  } else if (node.fieldName) {
    fields.push(node.fieldName);
  }
  return [...new Set(fields)].slice(0, 12);
}

function insertFieldsFromNode(node) {
  const q = buildQueryFromNode(node);
  if (q) queryEditor.value = q;
}

async function loadProviders() {
  providers = await api("/api/providers");
  providerSelect.innerHTML = "";
  for (const p of providers) {
    const opt = document.createElement("option");
    opt.value = p.id;
    opt.textContent = p.displayName;
    opt.dataset.default = p.defaultConnectionString;
    opt.title = p.connectionHint ?? "";
    providerSelect.appendChild(opt);
  }
  providerSelect.addEventListener("change", onProviderChange);
  lastProviderId = null;
  onProviderChange();
}

function onProviderChange() {
  const providerId = providerSelect.value;
  const opt = providerSelect.selectedOptions[0];
  if (!opt) return;

  connectionInput.placeholder = opt.title || "連線字串";

  const saved = loadSavedConnection();
  const providerChanged =
    lastProviderId !== null && lastProviderId !== providerId;
  lastProviderId = providerId;

  // 若 session 中有此資料庫系統的連線紀錄，優先還原
  if (saved?.provider === providerId && saved.connectionString) {
    connectionInput.value = saved.connectionString;
    return;
  }

  // 切換資料庫系統時，一律帶入該系統的連線字串範本供使用者修改
  if (providerChanged || !connectionInput.value.trim()) {
    connectionInput.value = opt.dataset.default ?? "";
    connectionInput.focus();
    connectionInput.select();
  }
}

async function connect(silent = false) {
  const provider = providerSelect.value;
  const connectionString = connectionInput.value.trim();
  if (!connectionString) {
    if (!silent) alert("請輸入連線字串。");
    return false;
  }

  try {
    setFooter("連線中…");
    const status = await api("/api/connection/connect", {
      method: "POST",
      body: JSON.stringify({ provider, connectionString }),
    });
    saveConnection(provider, connectionString);
    setStatus(true, status.provider ?? provider, status.tableCount ?? 0);
    setFooter(`已連線。共 ${status.tableCount ?? 0} 張資料表。切換資料庫後請重新整理頁面以更新 GraphQL Schema。`);
    await loadTablesTree(status.tableCount ?? 0);
    return true;
  } catch (err) {
    setStatus(false);
    setFooter(err.message);
    if (!silent) alert(err.message);
    return false;
  }
}

async function loadTablesTree(tableCount) {
  const tables = await api("/api/schema/tables");
  tableCountLabel.textContent = tables.length
    ? `${tables.length} 張`
    : tableCount
      ? `${tableCount} 張`
      : "";

  if (!tables.length) {
    tablesTree.innerHTML = '<p class="tree-empty">（尚無資料表）</p>';
    queryTree.innerHTML = '<p class="tree-empty">請先連線資料庫</p>';
    queryPanelTitle.textContent = "查詢";
    return;
  }

  renderTree(tablesTree, tables, {
    selectionScope: ".tree-tables",
    onSelect: async (node) => {
      if (node.kind !== "table" || !node.tableName) return;
      selectedTableName = node.tableName;
      selectedTableMeta = node;
      await loadTableDetail(node.tableName);
    },
  });

  const first = tables[0];
  if (first?.tableName) {
    selectedTableName = first.tableName;
    selectedTableMeta = first;
    await loadTableDetail(first.tableName);
    const firstRow = tablesTree.querySelector(".tree-row");
    firstRow?.classList.add("selected");
  }
}

function extractFieldNamesFromDetail(detail) {
  const folder = detail.find((n) => n.label === "欄位");
  const names =
    folder?.children?.map((c) => c.fieldName).filter(Boolean) ?? [];
  return names.length ? names : ["id"];
}

async function loadTableDetail(tableName) {
  queryPanelTitle.textContent = `查詢 · ${tableName}`;
  const detail = await api(`/api/schema/table/${encodeURIComponent(tableName)}`);

  if (!detail.length) {
    queryTree.innerHTML = '<p class="tree-empty">（無欄位資訊）</p>';
    return;
  }

  renderTree(queryTree, detail, {
    defaultExpanded: true,
    onSelect: (node) => {
      if (
        node.kind === "querySnippet" ||
        node.kind === "mutationSnippet" ||
        node.kind === "field"
      ) {
        insertFieldsFromNode(node);
      }
    },
  });

  const listName =
    selectedTableMeta?.listFieldName ?? toCamelPlural(tableName);
  const fields = extractFieldNamesFromDetail(detail);
  queryEditor.value = `query {\n  ${listName} {\n    ${fields.join("\n    ")}\n  }\n}`;
}

async function executeQuery() {
  const query = queryEditor.value.trim();
  if (!query) return;

  setFooter("執行中…");
  try {
    const res = await fetch("/graphql", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ query }),
    });
    const json = await res.json();
    lastRawJson = json;
    rawResult.textContent = JSON.stringify(json, null, 2);

    const tree = await api("/api/graphql/result-tree", {
      method: "POST",
      body: JSON.stringify(json),
    });
    renderTree(resultTree, tree, { defaultExpanded: true });
    setFooter(json.errors?.length ? "執行完成（含錯誤）" : "執行成功");
  } catch (err) {
    setFooter(err.message);
    renderTree(resultTree, [
      {
        id: "error",
        label: err.message,
        kind: "error",
        expanded: true,
      },
    ]);
  }
}

function setTreeExpand(container, expand) {
  container.querySelectorAll(".tree-children").forEach((el) => {
    el.classList.toggle("collapsed", !expand);
  });
  container.querySelectorAll(".tree-toggle").forEach((t) => {
    if (!t.classList.contains("empty")) t.textContent = expand ? "▼" : "▶";
  });
}

function formatQuery() {
  const q = queryEditor.value.trim();
  if (!q) return;
  queryEditor.value = q
    .replace(/\s*{\s*/g, " {\n  ")
    .replace(/;\s*/g, "\n")
    .replace(/\s*}\s*/g, "\n}\n")
    .replace(/\n\s*\n/g, "\n")
    .trim();
}

document.getElementById("expandQueryTreeBtn").addEventListener("click", () =>
  setTreeExpand(queryTree, true),
);
document.getElementById("collapseQueryTreeBtn").addEventListener("click", () =>
  setTreeExpand(queryTree, false),
);
document.getElementById("expandResultTreeBtn").addEventListener("click", () =>
  setTreeExpand(resultTree, true),
);
document.getElementById("collapseResultTreeBtn").addEventListener("click", () =>
  setTreeExpand(resultTree, false),
);

document.getElementById("viewRawBtn").addEventListener("click", () => {
  showRaw = !showRaw;
  rawResult.classList.toggle("hidden", !showRaw);
  resultTree.classList.toggle("hidden", showRaw);
  document.getElementById("viewRawBtn").textContent = showRaw
    ? "樹狀檢視"
    : "原始 JSON";
});

connectBtn.addEventListener("click", () => connect(false));
document.getElementById("executeBtn").addEventListener("click", executeQuery);
document.getElementById("formatBtn").addEventListener("click", formatQuery);

queryEditor.addEventListener("keydown", (e) => {
  if (e.ctrlKey && e.key === "Enter") {
    e.preventDefault();
    executeQuery();
  }
});

async function init() {
  await loadProviders();
  const saved = loadSavedConnection();
  if (saved) {
    providerSelect.value = saved.provider;
    connectionInput.value = saved.connectionString;
    lastProviderId = saved.provider;
  }

  try {
    const status = await api("/api/connection/status");
    if (status.connected) {
      setStatus(true, status.provider, status.tableCount);
      await loadTablesTree(status.tableCount);
      setFooter(`已連線。共 ${status.tableCount} 張資料表。`);
    } else if (saved) {
      await connect(true);
    }
  } catch (err) {
    setFooter(err.message);
  }
}

init();
