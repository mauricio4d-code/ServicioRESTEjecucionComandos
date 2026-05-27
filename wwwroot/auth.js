// ========================
//  Shared Authentication Utilities
// ========================

const API_BASE = window.location.origin;
const ACCESS_TOKEN_KEY = "auth_access_token";
const REFRESH_TOKEN_KEY = "auth_refresh_token";
const TOKEN_EXPIRY_KEY = "auth_token_expiry";

// How many seconds before expiry to trigger auto-refresh
const AUTO_REFRESH_THRESHOLD_SECONDS = 60;

let autoRefreshTimer = null;

// Guard to prevent concurrent refresh calls (infinite loop protection)
let refreshInProgress = false;
let refreshPromise = null;

// ========================
//  Token storage helpers
// ========================

function storeTokens(access, refresh, expiresIn) {
    localStorage.setItem(ACCESS_TOKEN_KEY, access);
    localStorage.setItem(REFRESH_TOKEN_KEY, refresh);
    const expiryMs = Date.now() + (expiresIn * 1000);
    localStorage.setItem(TOKEN_EXPIRY_KEY, expiryMs.toString());
}

function clearTokens() {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(TOKEN_EXPIRY_KEY);
}

function getAccessToken() {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
}

function getRefreshToken() {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
}

function getTokenExpiry() {
    const v = localStorage.getItem(TOKEN_EXPIRY_KEY);
    return v ? parseInt(v, 10) : 0;
}

function isTokenExpired() {
    const expiry = getTokenExpiry();
    return Date.now() >= expiry;
}

function timeUntilExpiry() {
    const expiry = getTokenExpiry();
    return Math.max(0, (expiry - Date.now()) / 1000);
}

// ========================
//  JWT payload decoder
// ========================

function decodeJwtPayload(token) {
    try {
        return JSON.parse(atob(token.split(".")[1]));
    } catch {
        return null;
    }
}

function extractUserName(payload, fallback) {
    return payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/given_name"]
        || payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"]
        || fallback;
}

// ========================
//  Auto-refresh scheduler
// ========================

function scheduleAutoRefresh() {
    if (autoRefreshTimer) {
        clearTimeout(autoRefreshTimer);
    }

    const secondsLeft = timeUntilExpiry();
    if (secondsLeft <= AUTO_REFRESH_THRESHOLD_SECONDS) {
        autoRefreshToken();
        return;
    }

    const triggerIn = (secondsLeft - AUTO_REFRESH_THRESHOLD_SECONDS) * 1000;
    autoRefreshTimer = setTimeout(async () => {
        await autoRefreshToken();
    }, triggerIn);
}

/**
 * Performs a single refresh-token rotation. Uses a promise guard so that
 * concurrent callers all wait for the SAME in-flight request instead of
 * each one hitting the server independently (which caused the infinite-loop bug).
 */
async function autoRefreshToken() {
    // If token is still valid, nothing to do
    if (!isTokenExpired()) {
        return true;
    }

    // Deduplication: if a refresh is already in flight, wait for it
    if (refreshInProgress) {
        return refreshPromise;
    }

    const refresh = getRefreshToken();
    if (!refresh) return false;

    refreshInProgress = true;

    // Ensure the promise is cleaned up even if an early return happens
    refreshPromise = _doRefresh()
        .finally(() => {
            refreshInProgress = false;
            refreshPromise = null;
        });

    try {
        const result = await refreshPromise;
        return result;
    } catch {
        return false;
    }
}

async function _doRefresh() {
    const refresh = getRefreshToken();
    if (!refresh) return false;

    try {
        const res = await fetch(`${API_BASE}/api/auth/refresh`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ token: refresh })
        });

        if (!res.ok) {
            console.warn("Auto-refresh failed, redirecting to login.");
            clearTokens();
            window.location.href = "login.html";
            return false;
        }

        const data = await res.json();
        storeTokens(data.accessToken, data.refreshToken, data.expiresIn);
        console.log("Token auto-refreshed successfully.");

        // Schedule next refresh
        scheduleAutoRefresh();
        return true;
    } catch (err) {
        console.error("Auto-refresh error:", err);
        return false;
    }
}

// ========================
//  Logout helper
// ========================

async function logoutAndRedirect() {
    const refresh = getRefreshToken();
    const access = getAccessToken();

    try {
        await fetch(`${API_BASE}/api/auth/logout`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Authorization": `Bearer ${access}`
            },
            body: JSON.stringify({ token: refresh })
        });
    } catch (err) {
        console.warn("Logout API call failed:", err);
    } finally {
        if (autoRefreshTimer) clearTimeout(autoRefreshTimer);
        clearTokens();
        window.location.href = "login.html";
    }
}

// ========================
//  Auth check - returns true if user has valid token
// ========================

function isAuthenticated() {
    const token = getAccessToken();
    return token && !isTokenExpired();
}

// ========================
//  Ensure authenticated - attempts refresh if token expired
// ========================

async function ensureAuthenticated() {
    if (isAuthenticated()) {
        return true;
    }

    // Token expired or missing - try refresh (uses deduplication guard)
    const refreshSuccess = await autoRefreshToken();
    if (!refreshSuccess) {
        clearTokens();
        return false;
    }
    return true;
}

// ========================
//  Authenticated fetch - auto-refreshes on 401
// ========================

async function authenticatedFetch(url, options = {}) {
    const token = getAccessToken();
    if (!options.headers) options.headers = {};
    options.headers["Authorization"] = `Bearer ${token}`;

    let response = await fetch(url, options);

    // If 401 and we haven't retried yet, try to refresh
    if (response.status === 401 && !options._retried) {
        const refreshSuccess = await autoRefreshToken();
        if (refreshSuccess) {
            const newToken = getAccessToken();
            options.headers["Authorization"] = `Bearer ${newToken}`;
            options._retried = true;
            response = await fetch(url, options);
        }
        // If refresh failed, autoRefreshToken already redirected to login
    }

    return response;
}
