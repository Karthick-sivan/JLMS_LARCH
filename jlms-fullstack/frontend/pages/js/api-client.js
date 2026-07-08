/* ============================================================
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
  updateJewelType: (id, dto) =>
    apiRequest(`/jewel-types/${id}`, {
        method: "PUT",
        body: dto
    }),

  getTodayGoldRate: () => apiRequest("/gold-rates/today"),
  getGoldRateHistory: (days = 30) => apiRequest(`/gold-rates/history?days=${days}`),
  setTodayGoldRate: (dto) => apiRequest("/gold-rates", { method: "POST", body: dto }),

getLoanSchemes: (activeOnly = false) => apiRequest(`/loan-schemes?activeOnly=${activeOnly}`),
  createLoanScheme: (dto) => apiRequest("/loan-schemes", { method: "POST", body: dto }),
  updateLoanScheme: (id, dto) => apiRequest(`/loan-schemes/${id}`, { method: "PUT", body: dto }),


    // ---- User Master ----
  getUsers: () => apiRequest("/user-master"),
  getUserMasterBranches: () => apiRequest("/user-master/branches"),
  getUserMasterRoles: () => apiRequest("/user-master/roles"),
  createUser: (dto) => apiRequest("/user-master", { method: "POST", body: dto }),
  updateUser: (id, dto) => apiRequest(`/user-master/${id}`, { method: "PUT", body: dto }),
  toggleUserStatus: (id) => apiRequest(`/user-master/${id}/toggle-status`, { method: "PATCH" }),

  // ---- Jewel Appraisal ----
  calculateAppraisal: (dto) => apiRequest("/jewel-appraisal/calculate", { method: "POST", body: dto }),

  // ---- Loans ----
  createLoan: (dto) => apiRequest("/loans", { method: "POST", body: dto }),
  getLoans: (status) => apiRequest(`/loans${status ? "?status=" + status : ""}`),
  getLoanById: (id) => apiRequest(`/loans/${id}`),
getReleaseDetails: (id) =>
    apiRequest(`/loans/${id}/release-details`),
  getLoanByNumber: (loanNumber) => apiRequest(`/loans/by-number/${encodeURIComponent(loanNumber)}`),
  approveLoan: (id, dto) => apiRequest(`/loans/${id}/approve`, { method: "POST", body: dto }),
  disburseLoan: (id, dto) => apiRequest(`/loans/${id}/disburse`, { method: "POST", body: dto }),

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
  getLoansDueToday: () => apiRequest("/dashboard/loans-due-today")
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
