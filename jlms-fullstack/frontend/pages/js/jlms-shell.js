/* ============================================================
   JLMS Shared Shell – Sidebar + Topbar
   Injects on every page. Set window.JLMS_ACTIVE before including.
   ============================================================ */

const JLMS_MENU = [
  { section: "", items: [
    { id: "dashboard", icon: "bi-speedometer2", label: "Dashboard", href: "dashboard.html" }
  ]},

  { section: "Transactions", items: [
    { id: "customer-registration", icon: "bi-person-plus", label: "Customer Registration", href: "customer-registration.html" },
    { id: "new-loan", icon: "bi-file-earmark-plus", label: "New Loan", href: "new-loan.html" },
    { id: "loan-operations", icon: "bi-unlock", label: "Loan Operations", href: "loan-operations.html" }
  ]},
  { section: "Masters", items: [
    { id: "jewel-type-master", icon: "bi-gem", label: "Jewel Type Master", href: "jewel-type-master.html" },
    { id: "gold-rate-master", icon: "bi-graph-up-arrow", label: "Gold Rate Master", href: "gold-rate-master.html" },
    { id: "loan-scheme-master", icon: "bi-journal-text", label: "Loan Scheme Master", href: "loan-scheme-master.html" },
     { id: "financial-year", icon: "bi-calendar-range", label: "Financial Year", href: "financial-year.html" }
    // { id: "user-master", icon: "bi-people", label: "User Master", href: "user-master.html" }
  ]},
  { section: "Reports", items: [
    { id: "report-center", icon: "bi-bar-chart-line", label: "Report Center", href: "activeloan-report.html" }
  ]},
  // { section: "Administration", items: [
  //   { id: "roles", icon: "bi-shield-lock", label: "Roles", href: "roles.html" },
  //   { id: "permissions", icon: "bi-key", label: "Permissions", href: "permissions.html" },
  //   { id: "system-settings", icon: "bi-gear", label: "System Settings", href: "system-settings.html" },
  //   { id: "audit-logs", icon: "bi-clipboard-data", label: "Audit Logs", href: "audit-logs.html" }
  // ]}
];

function stringEqualsIgnoreCase(s1, s2) {
  return (s1 || "").toString().toLowerCase() === (s2 || "").toString().toLowerCase();
}

function jlmsBuildSidebar() {
  const active = window.JLMS_ACTIVE || "dashboard";
  const user = Session.get();
  const isAdmin = user && stringEqualsIgnoreCase(user.roleName, "Administrator");

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
    // Hide Administration section for non-admins
    if (group.section === "Administration" && !isAdmin) return;

    let hasItems = false;
    let itemsHtml = "";
    group.items.forEach(item => {
      // Hide User Master for non-admins
      if (item.id === "user-master" && !isAdmin) return;

      hasItems = true;
      itemsHtml += `<a class="nav-item ${item.id === active ? 'active' : ''}" href="${item.href}" title="${item.label}">
        <span class="icon"><i class="bi ${item.icon}"></i></span>
        <span class="label">${item.label}</span>
      </a>`;
    });

    if (hasItems) {
      if (group.section) html += `<div class="nav-section-label">${group.section}</div>`;
      html += itemsHtml;
    }
  });

  html += `
      </nav>
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

  const user = Session.get() || { fullName: "Guest", roleName: "Guest", branchName: "No Branch" };
  const initials = user.fullName ? user.fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase() : "G";

  return `
    <header class="jlms-topbar">
      <div class="topbar-left">
        <div class="sidebar-toggle-btn" id="mobileSidebarToggle"><i class="bi bi-list"></i></div>
      </div>
      <div class="topbar-right">
        <div class="branch-pill"><i class="bi bi-building"></i> ${user.branchName || 'No Branch'}</div>
        <div class="rate-pill" id="topbarGoldRatePill" style="display:flex;align-items:center;gap:10px;font-size:11.5px;font-weight:600;color:#7a5c00;background:#fefce8;border:1px solid #fde68a;border-radius:20px;padding:3px 12px;white-space:nowrap;">
          <span>Gold: <span id="topbarGold22K">—</span></span>
          <span style="color:#d7dce3;">|</span>
          <span>Silver: <span id="topbarSilver">—</span></span>
        </div>
        <div class="icon-btn"><i class="bi bi-bell"></i><span class="dot"></span></div>
        <div class="user-chip" id="userDropdownTrigger" style="position:relative; cursor:pointer;">
          <div class="user-avatar">${initials}</div>
          <div class="user-meta">
            <div class="uname">${user.fullName || 'Guest'}</div>
            <div class="urole">${user.roleName || 'Guest'}</div>
          </div>
          <i class="bi bi-chevron-down" style="font-size:10px;color:var(--n-500);"></i>
          <div class="user-dropdown-menu" id="userDropdownMenu" style="display:none; position:absolute; right:0; top:100%; margin-top:8px; background:#fff; border:1px solid var(--n-200); border-radius:var(--radius-md); box-shadow:var(--shadow-md); z-index:1000; width:150px; text-align:left;">
            <a href="#" id="logoutLink" style="display:block; padding:10px 14px; color:var(--red-600); font-size:12px; font-weight:600; text-decoration:none;"><i class="bi bi-box-arrow-right"></i> Sign Out</a>
          </div>
        </div>
      </div>
    </header>`;
}

function jlmsInitShell(pageTitle, breadcrumbs) {
    // Session require login check
    const path = window.location.pathname.toLowerCase();
    const isAuthPage = path.endsWith("login.html") || path.endsWith("forgot-password.html") || path.endsWith("index.html");
    if (!isAuthPage) {
        Session.requireLogin();
    }

    document.addEventListener('DOMContentLoaded', () => {
        const sidebarMount = document.getElementById('jlmsSidebarMount');
        const topbarMount = document.getElementById('jlmsTopbarMount');
        if (sidebarMount) sidebarMount.outerHTML = jlmsBuildSidebar();
        if (topbarMount) topbarMount.outerHTML = jlmsBuildTopbar(pageTitle, breadcrumbs);

        // User profile dropdown toggle
        const dropdownTrigger = document.getElementById('userDropdownTrigger');
        const dropdownMenu = document.getElementById('userDropdownMenu');
        if (dropdownTrigger && dropdownMenu) {
            dropdownTrigger.addEventListener('click', (e) => {
                e.stopPropagation();
                const isVisible = dropdownMenu.style.display === 'block';
                dropdownMenu.style.display = isVisible ? 'none' : 'block';
            });
            document.addEventListener('click', () => {
                dropdownMenu.style.display = 'none';
            });
        }

        (async () => {
          try {
            const rate = await Api.getTodayGoldRate();
            const g = document.getElementById('topbarGold22K');
            const s = document.getElementById('topbarSilver');
            if (g) g.textContent = rate.rate22K != null ? '₹' + Number(rate.rate22K).toLocaleString('en-IN', {minimumFractionDigits:2, maximumFractionDigits:2}) + '/g' : '—';
            if (s) s.textContent = rate.silverRate != null ? '₹' + Number(rate.silverRate).toLocaleString('en-IN', {minimumFractionDigits:2, maximumFractionDigits:2}) + '/g' : '—';
          } catch (e) {
            // silently fail — rate pill stays at —
          }
        })();
        // Logout click handler
        const logoutLink = document.getElementById('logoutLink');
        if (logoutLink) {
            logoutLink.addEventListener('click', (e) => {
                e.preventDefault();
                Session.clear();
                window.location.href = 'login.html';
            });
        }

        const sidebar = document.getElementById('jlmsSidebar');
        const topToggle = document.getElementById('mobileSidebarToggle');

        if (topToggle) {
            topToggle.addEventListener('click', () => {
                if (window.innerWidth <= 1024) {
                    sidebar.classList.toggle('mobile-open');
                } else {
                    sidebar.classList.toggle('collapsed');
                    document.body.classList.toggle('sidebar-collapsed');
                }
            });
        }
    });
}
