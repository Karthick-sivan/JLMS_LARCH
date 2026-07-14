/* =================================customer aster===========================
   JLMS API Client
   ============================================================
   Set API_BASE_URL to match where your ASP.NET Core API is
   running. Visual Studio's default launch profile in this
   project uses http://localhost:5080 — change this if your
   port differs (check the console window or launchSettings.json
   when you run the API).
   ============================================================ */

const API_BASE_URL = "http://localhost:5080/api";

class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.status = status;
  }
}

async function apiRequest(path, { method = "GET", body = null } = {}) {
  let response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method,
      headers: body ? { "Content-Type": "application/json" } : {},
      body: body ? JSON.stringify(body) : undefined
    });
  } catch (networkErr) {
    throw new ApiError(
      `Could not reach the API at ${API_BASE_URL}. Is the ASP.NET Core project running? (${networkErr.message})`,
      0
    );
  }

  let data = null;
  const text = await response.text();
  if (text) {
    try { data = JSON.parse(text); } catch { data = text; }
  }

  if (!response.ok) {
    const message = (data && (data.title || data.message || data.detail)) || data || `Request failed (${response.status})`;
    throw new ApiError(typeof message === "string" ? message : JSON.stringify(message), response.status);
  }
  return data;
}

const Api = {
  // ---- Auth ----
  login: (username, password, branchId) =>
    apiRequest("/auth/login", { method: "POST", body: { username, password, branchId } }),

  // ---- Customers ----
  searchCustomers: (params = {}) => {
    const qs = new URLSearchParams(params).toString();
    return apiRequest(`/customers${qs ? "?" + qs : ""}`);
  },
  getCustomer: (id) => apiRequest(`/customers/${id}`),
 createCustomer: async (formData) => {

    const response = await fetch(`${API_BASE_URL}/customers`, {
        method: "POST",
        body: formData
    });

    const text = await response.text();

    let data = null;

    if (text) {
        try {
            data = JSON.parse(text);
        } catch {
            data = text;
        }
    }

    if (!response.ok) {

        const message =
            (data && (data.title || data.message || data.detail))
            || data
            || "Unable to save customer.";

        throw new ApiError(message, response.status);
    }

    return data;
},
  updateCustomer: (id, dto) => apiRequest(`/customers/${id}`, { method: "PUT", body: dto }),

  // ---- Masters ----
  getJewelTypes: (activeOnly = true) => apiRequest(`/jewel-types?activeOnly=${activeOnly}`),
  createJewelType: (dto) => apiRequest("/jewel-types", { method: "POST", body: dto }),

  getTodayGoldRate: () => apiRequest("/gold-rates/today"),
  getGoldRateHistory: (days = 30) => apiRequest(`/gold-rates/history?days=${days}`),
    setTodayGoldRate: (dto) => apiRequest("/gold-rates", { method: "POST", body: dto }),

    // ---- User Master ----
    getUsers: () => apiRequest("/user-master"),
    getUserMasterBranches: () => apiRequest("/user-master/branches"),
    getUserMasterRoles: () => apiRequest("/user-master/roles"),
    createUser: (dto) => apiRequest("/user-master", { method: "POST", body: dto }),
    updateUser: (id, dto) => apiRequest(`/user-master/${id}`, { method: "PUT", body: dto }),
    toggleUserStatus: (id) => apiRequest(`/user-master/${id}/toggle-status`, { method: "PATCH" }),

  //getLoanSchemes: (activeOnly = true) => apiRequest(`/loan-schemes?activeOnly=${activeOnly}`),
  //createLoanScheme: (dto) => apiRequest("/loan-schemes", { method: "POST", body: dto }),



    getLoanSchemes: (activeOnly = false) => apiRequest(`/loan-schemes?activeOnly=${activeOnly}`),
    createLoanScheme: (dto) => apiRequest("/loan-schemes", { method: "POST", body: dto }),
    updateLoanScheme: (id, dto) => apiRequest(`/loan-schemes/${id}`, { method: "PUT", body: dto }),

  // ---- Jewel Appraisal ----
  calculateAppraisal: (dto) => apiRequest("/jewel-appraisal/calculate", { method: "POST", body: dto }),

  // ---- Loans ----
  createLoan: (dto) => apiRequest("/loans", { method: "POST", body: dto }),
  getLoans: (status) => apiRequest(`/loans${status ? "?status=" + status : ""}`),
  getLoanById: (id) => apiRequest(`/loans/${id}`),
  getLoanByNumber: (loanNumber) => apiRequest(`/loans/by-number/${encodeURIComponent(loanNumber)}`),
  approveLoan: (id, dto) => apiRequest(`/loans/${id}/approve`, { method: "POST", body: dto }),
  disburseLoan: (id, dto) => apiRequest(`/loans/${id}/disburse`, { method: "POST", body: dto }),
  submitForApproval: (id, dto) => apiRequest(`/loans/${id}/submit-for-approval`, { method: "POST", body: dto }),

  // ---- Collections ----
  getOutstanding: (loanId) => apiRequest(`/loans/${loanId}/outstanding`),
  collectInterest: (loanId, dto) => apiRequest(`/loans/${loanId}/collect-interest`, { method: "POST", body: dto }),
  collectPrincipal: (loanId, dto) => apiRequest(`/loans/${loanId}/collect-principal`, { method: "POST", body: dto }),

  // ---- Closure / Renewal / Release ----
  getClosureCalculation: (loanId) => apiRequest(`/loans/${loanId}/closure-calculation`),
  closeLoan: (loanId, dto) => apiRequest(`/loans/${loanId}/close`, { method: "POST", body: dto }),
  renewLoan: (loanId, dto) => apiRequest(`/loans/${loanId}/renew`, { method: "POST", body: dto }),
  releaseJewel: (loanId, userId) => apiRequest(`/loans/${loanId}/release-jewel`, { method: "POST", body: userId }),

  // ---- Dashboard ----
  getDashboardSummary: () => apiRequest("/dashboard/summary"),
  getCollectionsToday: () => apiRequest("/dashboard/collections-today"),
  getLoansDueToday: () => apiRequest("/dashboard/loans-due-today"),
  getCollectionTrend: (days = 14) => apiRequest(`/dashboard/collection-trend?days=${days}`),
    // ---- Outstanding Reports ----
  getOutstandingReport: (params = {}) => {
    const qs = new URLSearchParams(
      Object.fromEntries(Object.entries(params).filter(([, v]) => v !== null && v !== undefined && v !== ""))
    ).toString();
    return apiRequest(`/outstanding-reports${qs ? "?" + qs : ""}`);
  },
  searchCustomersForReport: (q) => apiRequest(`/outstanding-reports/customer-search?q=${encodeURIComponent(q)}`),

// ---- Collection Reports ----
getCollectionReport: (params = {}) => {
    const qs = new URLSearchParams(
        Object.fromEntries(Object.entries(params).filter(([, v]) => v !== null && v !== undefined && v !== ""))
    ).toString();
    return apiRequest(`/collection-reports${qs ? "?" + qs : ""}`);
},
    searchCustomersForReport: (q) => apiRequest(`/collection-reports/customer-search?q=${encodeURIComponent(q)}`),
    getLoansByCustomer: (customerId) => apiRequest(`/collection-reports/loans-by-customer?customerId=${customerId}`),
  
    /* ============================================================
   Add these methods INSIDE the existing `Api = { ... }` object
   in api-client.js (e.g. right after the "Collections" section).
   Nothing existing in Api is modified — this is purely additive.
   ============================================================ */



    // ---- Active Loans Report ----
    getActiveLoansReport: (params = {}) => {
        const qs = new URLSearchParams(
            Object.fromEntries(Object.entries(params).filter(([, v]) => v !== null && v !== undefined && v !== ""))
        ).toString();
        return apiRequest(`/active-loans-report${qs ? "?" + qs : ""}`);
    },
    searchCustomersForActiveReport: (q) => apiRequest(`/active-loans-report/customer-search?q=${encodeURIComponent(q)}`),

    // ---- Closed Loans Report ----
    getClosedLoansReport: (params = {}) => {
        const qs = new URLSearchParams(
            Object.fromEntries(Object.entries(params).filter(([, v]) => v !== null && v !== undefined && v !== ""))
        ).toString();
        return apiRequest(`/closed-loans-report${qs ? "?" + qs : ""}`);
    },
    searchCustomersForClosedReport: (q) => apiRequest(`/closed-loans-report/customer-search?q=${encodeURIComponent(q)}`),

    // ---- Branches (used by report filter dropdowns — adjust path if your actual
    // branches endpoint differs; I couldn't see a BranchesController in what you shared) ----
    getBranches: () => apiRequest(`/branches`),


// ---- Loan Operations (new, independent merged page) ----
getLoanOperationsGrid: (params = {}) => {
  const qs = new URLSearchParams(
    Object.fromEntries(Object.entries(params).filter(([, v]) => v !== null && v !== undefined && v !== ""))
  ).toString();
  return apiRequest(`/loan-operations/grid${qs ? "?" + qs : ""}`);
},

getLoanOperationsPaymentDetails: (loanId, asOfDate) =>
  apiRequest(`/loan-operations/${loanId}/payment-details${asOfDate ? "?asOfDate=" + asOfDate : ""}`),

getLoanOperationsInterestPreview: (loanId, asOfDate) =>
  apiRequest(`/loan-operations/${loanId}/interest-preview${asOfDate ? "?asOfDate=" + asOfDate : ""}`),

saveLoanOperationsPayment: (loanId, dto) =>
  apiRequest(`/loan-operations/${loanId}/payment`, { method: "POST", body: dto }),

getLoanOperationsClosureDetails: (loanId) =>
  apiRequest(`/loan-operations/${loanId}/closure-details`),

closeLoanOperations: (loanId, dto) =>
  apiRequest(`/loan-operations/${loanId}/close`, { method: "POST", body: dto }),

closeLoanOperationsWithPhoto: async (loanId, formData) => {
  const response = await fetch(`
${API_BASE_URL}/loan-operations/${loanId}/close`, {
    method: "POST",
    body: formData   // multipart — browser sets Content-Type + boundary automatically
  });
  const text = await response.text();
  let data = null;
  if (text) { try { data = JSON.parse(text); } catch { data = text; } }
  if (!response.ok) {
    const message = (data && (data.title || data.message || data.detail)) || data || "Unable to close loan.";
    throw new ApiError(typeof message === "string" ? message : JSON.stringify(message), response.status);
  }
  return data;
},

// Payment receipt — POST receipt data, get PDF blob back
async downloadPaymentReceiptPdf(receiptData) {
  return this._fetchBlob('/loan-operations/payment-receipt-pdf', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(receiptData)
  });
},

// Closure receipt — POST closure data, get PDF blob back
async downloadClosureReceiptPdf(receiptData) {
  return this._fetchBlob('/loan-operations/closure-receipt-pdf', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(receiptData)
  });
},

// Ledger row receipt — GET by transactionId, server does DB lookup
async downloadTransactionReceiptPdf(transactionId) {
  return this._fetchBlob(
    `/loan-operations/transaction-receipt-pdf/${transactionId}`
  );
},

// Shared blob fetch helper — mirrors apiRequest's error handling but
// resolves to a Blob (PDF bytes) instead of parsed JSON.
async _fetchBlob(path, options = {}) {
  let res;
  try {
    res = await fetch(`${API_BASE_URL}${path}`, options);
  } catch (networkErr) {
    throw new ApiError(
      `Could not reach the API at ${API_BASE_URL}. Is the ASP.NET Core project running? (${networkErr.message})`,
      0
    );
  }
  if (!res.ok) {
    let message = `PDF download failed (${res.status})`;
    try {
      const text = await res.text();
      if (text) {
        try {
          const data = JSON.parse(text);
          message = (data && (data.title || data.message || data.detail)) || message;
        } catch { message = text; }
      }
    } catch { /* ignore — fall back to generic message */ }
    throw new ApiError(message, res.status);
  }
  return res.blob();
},


getLoanOperationsLedger: (loanId, page = 1, pageSize = 10) =>
  apiRequest(`/loan-operations/${loanId}/ledger?page=${page}&pageSize=${pageSize}`)



};




/* Simple session helper — stores the logged-in user in sessionStorage
   so other pages know who's "logged in" during this test session. */
const Session = {
  set(user) { sessionStorage.setItem("jlms_user", JSON.stringify(user)); },
  get() {
    const raw = sessionStorage.getItem("jlms_user");
    return raw ? JSON.parse(raw) : null;
  },
  clear() { sessionStorage.removeItem("jlms_user"); },
  requireLogin() {
    const user = this.get();
    if (!user) window.location.href = "login.html";
    return user;
  }
};

/* Tiny helper to show inline error banners without a UI framework */
function showApiError(err, containerSelector = "#apiErrorBanner") {
  const el = document.querySelector(containerSelector);
  if (!el) { alert(err.message); return; }
  el.textContent = err.message;
  el.style.display = "block";
}
function clearApiError(containerSelector = "#apiErrorBanner") {
  const el = document.querySelector(containerSelector);
  if (el) el.style.display = "none";
}