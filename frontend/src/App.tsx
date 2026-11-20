import { useEffect, useState } from "react";
import { setUnauthorizedHandler } from "./api";
import { clearSession, getSession, type Session } from "./session";
import LoginScreen from "./screens/LoginScreen";
import ClockScreen from "./screens/ClockScreen";
import WeekScreen from "./screens/WeekScreen";
import TeamScreen from "./screens/TeamScreen";

type Tab = "clock" | "week" | "team";

export default function App() {
  const [session, setSession] = useState<Session | null>(() => getSession());
  const [tab, setTab] = useState<Tab>("clock");
  const [notice, setNotice] = useState<string | null>(null);

  // If the server rejects the badge (expired after 60 min, or account deactivated), go back to login (EC-14).
  useEffect(() => {
    setUnauthorizedHandler(() => {
      setSession(null);
      setNotice("Your session has ended. Please log in again.");
    });
  }, []);

  if (!session) {
    return <LoginScreen notice={notice} onLoggedIn={(s) => { setNotice(null); setTab("clock"); setSession(s); }} />;
  }

  const logout = () => { clearSession(); setSession(null); };
  // The tab is only a convenience: the server refuses manager endpoints to non-managers anyway (403).
  const isManager = session.employee.role === "Manager";

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">⏱️ Attendance Clock</div>
        <nav className="tabs" aria-label="Main">
          <button className={tab === "clock" ? "tab active" : "tab"} onClick={() => setTab("clock")}>Clock</button>
          <button className={tab === "week" ? "tab active" : "tab"} onClick={() => setTab("week")}>My week</button>
          {isManager && <button className={tab === "team" ? "tab active" : "tab"} onClick={() => setTab("team")}>Team</button>}
        </nav>
        <div className="user">
          <span className="user-name">{session.employee.displayName}</span>
          <button className="link" onClick={logout}>Log out</button>
        </div>
      </header>
      <main className="content">
        {tab === "clock" ? <ClockScreen /> : tab === "week" ? <WeekScreen /> : <TeamScreen />}
      </main>
    </div>
  );
}
