import { useEffect, useState } from "react";
import { api } from "./api";

const RESYNC_MS = 5 * 60 * 1000;

/**
 * The live clock on screen. It's COSMETIC (PRD §4.5): seeded from the server's trusted time
 * (/api/time/now), then advanced locally with performance.now(), which measures elapsed time and
 * not the time of day, so a wrong device clock doesn't matter. Re-synced every 5 minutes.
 * Punches never use this value: the server records its own time.
 */
export function useZurichClock(): { now: Date | null; error: boolean } {
  const [anchor, setAnchor] = useState<{ serverMs: number; perf: number } | null>(null);
  const [now, setNow] = useState<Date | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const sync = async () => {
      try {
        const t = await api.timeNow();
        if (!cancelled) { setAnchor({ serverMs: Date.parse(t.zurichTime), perf: performance.now() }); setError(false); }
      } catch {
        if (!cancelled) setError(true);
      }
    };
    void sync();
    const id = setInterval(sync, RESYNC_MS);
    return () => { cancelled = true; clearInterval(id); };
  }, []);

  useEffect(() => {
    if (!anchor) return;
    const tick = () => setNow(new Date(anchor.serverMs + (performance.now() - anchor.perf)));
    tick();
    const id = setInterval(tick, 1000);
    return () => clearInterval(id);
  }, [anchor]);

  return { now, error };
}
