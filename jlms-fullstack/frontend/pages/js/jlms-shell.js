/* ============================================================
   JLMS Shared Shell – Sidebar + Topbar
   Injects on every page. Set window.JLMS_ACTIVE before including.
   ============================================================ */

// Menu — labelKey/sectionKey read from lang[] for translation, label/sectionLabel are English fallbacks
const JLMS_MENU = [
  { sectionKey: "", sectionLabel: "", items: [
    { id: "dashboard", icon: "bi-speedometer2", labelKey: "dashboard", label: "Dashboard", href: "dashboard.html" }
  ]},
  { sectionKey: "transactions", sectionLabel: "Transactions", items: [
    { id: "customer-registration", icon: "bi-person-plus",     labelKey: "customerRegistration", label: "Customer Registration", href: "customer-registration.html" },
    { id: "new-loan",             icon: "bi-file-earmark-plus", labelKey: "newLoan",             label: "New Loan",             href: "new-loan.html" },
    { id: "loan-operations",      icon: "bi-unlock",            labelKey: "loanOperations",      label: "Loan Operations",      href: "loan-operations.html" }
  ]},
  { sectionKey: "masters", sectionLabel: "Masters", items: [
    { id: "branch-master",      icon: "bi-building",      labelKey: "branchMaster",     label: "Branch Master",      href: "branch-master.html" },
    { id: "user-master",        icon: "bi-people",        labelKey: "users",            label: "User Master",        href: "user-master.html" },
    { id: "jewel-type-master",  icon: "bi-gem",           labelKey: "jewelTypeMaster",  label: "Jewel Type Master",  href: "jewel-type-master.html" },
    { id: "gold-rate-master",   icon: "bi-graph-up-arrow",labelKey: "goldRateMaster",   label: "Gold Rate Master",   href: "gold-rate-master.html" },
    { id: "loan-scheme-master", icon: "bi-journal-text",  labelKey: "loanSchemeMaster", label: "Loan Scheme Master", href: "loan-scheme-master.html" },
    { id: "financial-year",     icon: "bi-calendar-range",labelKey: "financialYear",    label: "Financial Year",     href: "financial-year.html" }
  ]},
  { sectionKey: "reports", sectionLabel: "Reports", items: [
    { id: "report-center", icon: "bi-bar-chart-line", labelKey: "reportCenter", label: "Report Center", href: "activeloan-report.html" }
  ]}
];

function stringEqualsIgnoreCase(s1, s2) {
  return (s1 || "").toString().toLowerCase() === (s2 || "").toString().toLowerCase();
}

// Get translated text from lang[], fall back to English
function jlmsLabel(key, fallback) {
  return (typeof lang !== "undefined" && lang && lang[key]) || fallback;
}

function jlmsBuildSidebar() {
  const active = window.JLMS_ACTIVE || "dashboard";
  const user = Session.get();
  const isSuperAdmin = user && user.roleId === 1002;
  const isAdmin = user && stringEqualsIgnoreCase(user.roleName, "Administrator");
    const isRestrictedBranch = user && Number(user.branchId) === 1; 

  let html = `
    <aside class="jlms-sidebar" id="jlmsSidebar">
      <div class="sidebar-brand">
        <div class="mark">JL</div>
        <div class="brand-text">
          <div class="name">JLMS</div>
          <div class="sub">${jlmsLabel("appName", "Jewel Loan Management")}</div>
        </div>
      </div>
      <nav class="sidebar-nav">`;

  if (isSuperAdmin) {
    const superAdminItems = [
      { id: "dashboard",            icon: "bi-speedometer2", labelKey: "dashboard",           label: "Dashboard",            href: "superadmin-dashboard.html" },
      { id: "create-administrator", icon: "bi-person-gear",  labelKey: "createAdministrator", label: "Create Administrator", href: "create-administrator.html" }
    ];
    superAdminItems.forEach(item => {
      const lbl = jlmsLabel(item.labelKey, item.label);
      html += `<a class="nav-item ${item.id === active ? 'active' : ''}" href="${item.href}" title="${lbl}">
        <span class="icon"><i class="bi ${item.icon}"></i></span>
        <span class="label">${lbl}</span>
      </a>`;
    });
  } else {
    JLMS_MENU.forEach(group => {
      if (group.sectionKey === "administration" && !isAdmin) return;

      let hasItems = false;
      let itemsHtml = "";
      group.items.forEach(item => {
        if (item.id === "user-master" && !isAdmin) return;
          if (item.id === "user-master" && isRestrictedBranch) return;      
        if (item.id === "financial-year" && isRestrictedBranch) return; 
        if (item.id === "branch-master") return;

        hasItems = true;
        const lbl = jlmsLabel(item.labelKey, item.label);
        // report-center matches all report pages
        const isActive = item.id === active || (item.id === "report-center" && (active === "active-loans-report" || active === "loandetails-report" || active === "collection-reports" || active === "closureloan-report" || active === "outstanding-reports"));
        itemsHtml += `<a class="nav-item ${isActive ? 'active' : ''}" href="${item.href}" title="${lbl}">
          <span class="icon"><i class="bi ${item.icon}"></i></span>
          <span class="label">${lbl}</span>
        </a>`;
      });

      if (hasItems) {
        if (group.sectionKey) {
          const secLbl = jlmsLabel(group.sectionKey, group.sectionLabel);
          html += `<div class="nav-section-label">${secLbl}</div>`;
        }
        html += itemsHtml;
      }
    });
  }

  html += `
      </nav>
    </aside>`;
  return html;
}

function jlmsBuildTopbar(pageTitle, breadcrumbs) {
  const user = Session.get() || { fullName: "Guest", roleName: "Guest", branchName: "No Branch" };
  const isSuperAdmin = user && user.roleId === 1002;
  let homeHref = isSuperAdmin ? "superadmin-dashboard.html" : "dashboard.html";

  let crumbHtml = `<a href="${homeHref}">Home</a>`;
  (breadcrumbs || []).forEach((b, i) => {
    crumbHtml += `<span class="sep">/</span>`;
    if (i === breadcrumbs.length - 1) crumbHtml += `<span class="current">${b}</span>`;
    else crumbHtml += `<a href="#">${b}</a>`;
  });

  const initials = user.fullName ? user.fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase() : "G";

  return `
    <header class="jlms-topbar">
      <div class="topbar-left">
        <div class="sidebar-toggle-btn" id="mobileSidebarToggle"><i class="bi bi-list"></i></div>
      </div>
      <div class="topbar-right">
        ${!isSuperAdmin ? `<div class="branch-pill"><i class="bi bi-building"></i> ${user.branchName || 'No Branch'}</div>` : ''}
        <div class="rate-pill" id="topbarGoldRatePill" style="display:flex;align-items:center;gap:10px;font-size:11.5px;font-weight:600;color:#7a5c00;background:#fefce8;border:1px solid #fde68a;border-radius:20px;padding:3px 12px;white-space:nowrap;">
          <span>Gold: <span id="topbarGold22K">—</span></span>
          <span style="color:#d7dce3;">|</span>
          <span>Silver: <span id="topbarSilver">—</span></span>
        </div>
        <div class="icon-btn"><i class="bi bi-bell"></i><span class="dot"></span></div>
        <div class="language-selector" style="position:relative; margin-right:12px;">
          <select id="languageSwitcher" onchange="switchLanguage(this.value)" style="padding:6px 10px; border:1px solid var(--n-200); border-radius:var(--radius-sm); font-size:12px; background:#fff; cursor:pointer;">
            <option value="en">English</option>
            <option value="ta">தமிழ்</option>
          </select>
        </div>
        <div class="user-chip" id="userDropdownTrigger" style="position:relative; cursor:pointer;">
          <div class="user-avatar">${initials}</div>
          <div class="user-meta">
            <div class="uname">${user.fullName || 'Guest'}</div>
            <div class="urole">${user.roleName || 'Guest'}</div>
          </div>
          <i class="bi bi-chevron-down" style="font-size:10px;color:var(--n-500);"></i>
          <div class="user-dropdown-menu" id="userDropdownMenu" style="display:none; position:absolute; right:0; top:100%; margin-top:8px; background:#fff; border:1px solid var(--n-200); border-radius:var(--radius-md); box-shadow:var(--shadow-md); z-index:1000; width:180px; text-align:left;">
            <a href="profile-settings.html" id="profileSettingsLink" style="display:block; padding:10px 14px; color:var(--n-700); font-size:12px; font-weight:600; text-decoration:none;"><i class="bi bi-person-gear"></i> Profile Settings</a>
            <div style="border-top:1px solid var(--n-200); margin:2px 0;"></div>
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
        
        // Initialize language switcher value after topbar is injected
        const languageSwitcher = document.getElementById('languageSwitcher');
        if (languageSwitcher) {
            const savedLang = localStorage.getItem('jlmsLanguage') || 'ta';
            languageSwitcher.value = savedLang;
        }

        // Rebuild sidebar + sync dropdown every time language changes
        if (typeof registerLanguageCallback === 'function') {
            registerLanguageCallback(() => {
                // Sync the dropdown to current language
                const sw = document.getElementById('languageSwitcher');
                if (sw) sw.value = localStorage.getItem('jlmsLanguage') || 'ta';
                // Rebuild sidebar with translated labels
                const sb = document.getElementById('jlmsSidebar');
                if (sb) {
                    sb.outerHTML = jlmsBuildSidebar();
                    const newSb = document.getElementById('jlmsSidebar');
                    const tog = document.getElementById('mobileSidebarToggle');
                    if (tog && newSb) {
                        tog.addEventListener('click', () => {
                            if (window.innerWidth <= 1024) newSb.classList.toggle('mobile-open');
                            else { newSb.classList.toggle('collapsed'); document.body.classList.toggle('sidebar-collapsed'); }
                        });
                    }
                }
            });
        }
        
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