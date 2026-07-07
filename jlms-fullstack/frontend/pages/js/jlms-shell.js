/* ============================================================
   JLMS Shared Shell — Sidebar + Topbar
   Injects on every page. Set window.JLMS_ACTIVE before including.
   ============================================================ */

const JLMS_MENU = [
  { section: "", items: [
    { id: "dashboard", icon: "bi-speedometer2", label: "Dashboard", href: "dashboard.html" }
  ]},
  { section: "Masters", items: [
    { id: "customer-master", icon: "bi-person-vcard", label: "Customer Master", href: "customer-registration.html" },
    { id: "jewel-type-master", icon: "bi-gem", label: "Jewel Type Master", href: "jewel-type-master.html" },
    { id: "gold-rate-master", icon: "bi-graph-up-arrow", label: "Gold Rate Master", href: "gold-rate-master.html" },
    { id: "loan-scheme-master", icon: "bi-journal-text", label: "Loan Scheme Master", href: "loan-scheme-master.html" },
    { id: "user-master", icon: "bi-people", label: "User Master", href: "user-master.html" }
  ]},
  { section: "Transactions", items: [
    { id: "customer-registration", icon: "bi-person-plus", label: "Customer Registration", href: "customer-registration.html" },
    { id: "jewel-appraisal", icon: "bi-search", label: "Jewel Appraisal", href: "jewel-appraisal.html" },
    { id: "new-loan", icon: "bi-file-earmark-plus", label: "New Loan", href: "new-loan.html" },
    { id: "loan-approval", icon: "bi-check2-square", label: "Loan Approval", href: "loan-approval.html" },
    { id: "loan-disbursement", icon: "bi-cash-coin", label: "Loan Disbursement", href: "loan-disbursement.html" },
    { id: "interest-collection", icon: "bi-percent", label: "Interest Collection", href: "interest-collection.html" },
    { id: "principal-collection", icon: "bi-wallet2", label: "Principal Collection", href: "principal-collection.html" },
    { id: "loan-renewal", icon: "bi-arrow-repeat", label: "Loan Renewal", href: "loan-renewal.html" },
    { id: "loan-closure", icon: "bi-x-circle", label: "Loan Closure", href: "loan-closure.html" },
    { id: "jewel-release", icon: "bi-unlock", label: "Jewel Release", href: "jewel-release.html" },
    { id: "auction-management", icon: "bi-hammer", label: "Auction Management", href: "auction-management.html" }
  ]},
  { section: "Reports", items: [
    { id: "report-center", icon: "bi-bar-chart-line", label: "Report Center", href: "report-center.html" }
  ]},
  { section: "Administration", items: [
    { id: "roles", icon: "bi-shield-lock", label: "Roles", href: "roles.html" },
    { id: "permissions", icon: "bi-key", label: "Permissions", href: "permissions.html" },
    { id: "system-settings", icon: "bi-gear", label: "System Settings", href: "system-settings.html" },
    { id: "audit-logs", icon: "bi-clipboard-data", label: "Audit Logs", href: "audit-logs.html" }
  ]}
];

function jlmsBuildSidebar() {
  const active = window.JLMS_ACTIVE || "dashboard";
  let html = `
    <aside class="jlms-sidebar" id="jlmsSidebar">
      <div class="sidebar-brand">
        <div class="mark">JL</div>
        <div class="brand-text">
          <div class="name">JLMS</div>
          <div class="sub">Jewel Loan Management</div>
        </div>
      </div>
      <nav class="sidebar-nav">`;
  JLMS_MENU.forEach(group => {
    if (group.section) html += `<div class="nav-section-label">${group.section}</div>`;
    group.items.forEach(item => {
      html += `<a class="nav-item ${item.id === active ? 'active' : ''}" href="${item.href}" title="${item.label}">
        <span class="icon"><i class="bi ${item.icon}"></i></span>
        <span class="label">${item.label}</span>
      </a>`;
    });
  });
  html += `
      </nav>
      <div class="sidebar-foot" id="sidebarToggleBtn">
        <i class="bi bi-chevron-left" id="sidebarToggleIcon"></i>
      </div>
    </aside>`;
  return html;
}

function jlmsBuildTopbar(pageTitle, breadcrumbs) {
  let crumbHtml = `<a href="dashboard.html">Home</a>`;
  (breadcrumbs || []).forEach((b, i) => {
    crumbHtml += `<span class="sep">/</span>`;
    if (i === breadcrumbs.length - 1) crumbHtml += `<span class="current">${b}</span>`;
    else crumbHtml += `<a href="#">${b}</a>`;
  });

  return `
    <header class="jlms-topbar">
      <div class="topbar-left">
        <div class="sidebar-toggle-btn" id="mobileSidebarToggle"><i class="bi bi-list"></i></div>
        <div class="global-search">
          <i class="bi bi-search"></i>
          <input type="text" placeholder="Search customer, loan no, mobile...">
          <span class="kbd-hint">Ctrl K</span>
        </div>
      </div>
      <div class="topbar-right">
        <div class="branch-pill"><i class="bi bi-building"></i> Madurai Main Branch</div>
        <div class="icon-btn"><i class="bi bi-bell"></i><span class="dot"></span></div>
        <div class="icon-btn"><i class="bi bi-question-circle"></i></div>
        <div class="user-chip">
          <div class="user-avatar">RK</div>
          <div class="user-meta">
            <div class="uname">Ravi Kumar</div>
            <div class="urole">Branch Manager</div>
          </div>
          <i class="bi bi-chevron-down" style="font-size:10px;color:var(--n-500);"></i>
        </div>
      </div>
    </header>`;
}

function jlmsInitShell(pageTitle, breadcrumbs) {
  document.addEventListener('DOMContentLoaded', () => {
    const sidebarMount = document.getElementById('jlmsSidebarMount');
    const topbarMount = document.getElementById('jlmsTopbarMount');
    if (sidebarMount) sidebarMount.outerHTML = jlmsBuildSidebar();
    if (topbarMount) topbarMount.outerHTML = jlmsBuildTopbar(pageTitle, breadcrumbs);

    const toggleBtn = document.getElementById('sidebarToggleBtn');
    const sidebar = document.getElementById('jlmsSidebar');
    const icon = document.getElementById('sidebarToggleIcon');
    if (toggleBtn) {
      toggleBtn.addEventListener('click', () => {
        sidebar.classList.toggle('collapsed');
        document.body.classList.toggle('sidebar-collapsed');
        icon.classList.toggle('bi-chevron-left');
        icon.classList.toggle('bi-chevron-right');
      });
    }
    const mobileToggle = document.getElementById('mobileSidebarToggle');
    if (mobileToggle) {
      mobileToggle.addEventListener('click', () => {
        sidebar.classList.toggle('mobile-open');
      });
    }
  });
}
