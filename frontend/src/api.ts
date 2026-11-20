// Talks to the ASP.NET API. Every error comes back as ProblemDetails with a stable "code"
// (e.g. ALREADY_ON_SHIFT), which the screens turn into friendly messages.

import { clearSession, getSession } from "./session";

export type Employee = { id: number; userName: string; displayName: string; role: "Employee" | "Manager" };
export type LoginResponse = { token: string; expiresAtUtc: string; employee: Employee };

export type Shift = {
  id: number;
  zurichDate: string;
  clockIn: string;
  clockInSource: string;
  clockOut: string | null;
  clockOutSource: string | null;
  durationMinutes: number | null;
  flags: string[];
};
export type PunchStatus = { state: "OnShift" | "OffShift"; openShift: Shift | null };
export type DayTotal = { date: string; totalMinutes: number; shiftCount: number };
export type History = { from: string; to: string; totalMinutes: number; days: DayTotal[]; shifts: Shift[]; openShift: Shift | null };
export type TimeNow = { zurichTime: string; source: string };

export class ApiError extends Error {
  constructor(public status: number, public code: string | undefined, public title: string, public detail?: string) {
    super(detail ?? title);
  }
}

/** Called when the server says the login badge is no longer valid (expired, tampered, account deactivated). */
let onUnauthorized: () => void = () => {};
export function setUnauthorizedHandler(handler: () => void) {
  onUnauthorized = handler;
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const session = getSession();
  let response: Response;
  try {
    response = await fetch(path, {
      method,
      headers: {
        ...(body !== undefined ? { "Content-Type": "application/json" } : {}),
        ...(session ? { Authorization: `Bearer ${session.token}` } : {}),
      },
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch {
    throw new ApiError(0, "NETWORK", "Can't reach the server. Check your connection and try again.");
  }

  if (response.ok) return (await response.json()) as T;

  let problem: { code?: string; title?: string; detail?: string } = {};
  try { problem = await response.json(); } catch { /* not JSON (e.g. an empty 401) */ }

  if (response.status === 401 && path !== "/api/auth/login") {
    clearSession();
    onUnauthorized();
  }
  throw new ApiError(response.status, problem.code, problem.title ?? `Request failed (${response.status})`, problem.detail);
}

export const api = {
  login: (userName: string, password: string) => request<LoginResponse>("POST", "/api/auth/login", { userName, password }),
  timeNow: () => request<TimeNow>("GET", "/api/time/now"),
  status: () => request<PunchStatus>("GET", "/api/punches/status"),
  clockIn: () => request<Shift>("POST", "/api/punches/clock-in"),   // no body: the server takes the time (D-2)
  clockOut: () => request<Shift>("POST", "/api/punches/clock-out"),
  history: (from?: string, to?: string) =>
    request<History>("GET", from && to ? `/api/shifts/mine?from=${from}&to=${to}` : "/api/shifts/mine"),
};
