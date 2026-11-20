import { useState, type FormEvent } from "react";
import { api, ApiError } from "../api";
import { saveSession, type Session } from "../session";

export default function LoginScreen({ notice, onLoggedIn }: { notice: string | null; onLoggedIn: (s: Session) => void }) {
  const [userName, setUserName] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const res = await api.login(userName.trim(), password);
      const session: Session = { token: res.token, expiresAtUtc: res.expiresAtUtc, employee: res.employee };
      saveSession(session);
      onLoggedIn(session);
    } catch (err) {
      if (err instanceof ApiError && err.code === "INVALID_CREDENTIALS") setError("Invalid username or password.");
      else if (err instanceof ApiError && err.code === "TOO_MANY_ATTEMPTS") setError("Too many login attempts. Please wait a minute and try again.");
      else if (err instanceof ApiError) setError(err.message);
      else setError("Something went wrong. Please try again.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="login-page">
      <form className="card login-card" onSubmit={submit}>
        <h1>⏱️ Attendance Clock</h1>
        <p className="muted">Log in to clock in and out of your shift.</p>
        {notice && <div className="banner info">{notice}</div>}
        <label>
          Username
          <input value={userName} onChange={(e) => setUserName(e.target.value)} autoComplete="username" autoFocus required />
        </label>
        <label>
          Password
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required />
        </label>
        {error && <div className="banner error" role="alert">{error}</div>}
        <button className="primary" type="submit" disabled={busy}>{busy ? "Logging in…" : "Log in"}</button>
        <details className="demo">
          <summary>Demo accounts</summary>
          <p><code>dana</code> / <code>Employee123!</code> · <code>yossi</code> / <code>Employee123!</code> · <code>manager</code> / <code>Manager123!</code></p>
        </details>
      </form>
    </div>
  );
}
