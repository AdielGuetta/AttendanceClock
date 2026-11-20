import { describe, expect, it } from "vitest";
import { addDays, formatElapsed, formatHM, formatRecordedTime, formatZurichNow, parseServerTime, weekDates } from "./time";

describe("recorded times from the server", () => {
  it("reads the Zurich wall-clock time straight from the string (winter = CET)", () => {
    expect(formatRecordedTime("2026-11-20T08:02:11.9160071+01:00")).toBe("08:02:11 CET");
  });

  it("labels summer time CEST", () => {
    expect(parseServerTime("2026-07-15T14:30:00+02:00")).toMatchObject({ date: "2026-07-15", hm: "14:30", label: "CEST" });
  });

  it("doesn't depend on the device's time zone (no Date conversion involved)", () => {
    // 23:30 in Zurich stays 23:30 even where it's already the next day.
    expect(parseServerTime("2026-11-20T23:30:00+01:00")).toMatchObject({ date: "2026-11-20", hm: "23:30" });
  });
});

describe("live clock formatting", () => {
  it("shows a UTC instant as Zurich winter time", () => {
    expect(formatZurichNow(new Date("2026-11-20T07:00:05Z"))).toMatchObject({ time: "08:00:05", label: "CET", dateIso: "2026-11-20" });
  });

  it("shows a UTC instant as Zurich summer time", () => {
    expect(formatZurichNow(new Date("2026-07-15T12:30:00Z"))).toMatchObject({ time: "14:30:00", label: "CEST" });
  });

  it("switches label exactly at the DST change (29 Mar 2026, 01:00 UTC)", () => {
    expect(formatZurichNow(new Date("2026-03-29T00:59:59Z"))).toMatchObject({ time: "01:59:59", label: "CET" });
    expect(formatZurichNow(new Date("2026-03-29T01:00:00Z"))).toMatchObject({ time: "03:00:00", label: "CEST" });
  });
});

describe("durations", () => {
  it("formats hours:minutes", () => {
    expect(formatHM(510)).toBe("8:30");
    expect(formatHM(5)).toBe("0:05");
    expect(formatHM(0)).toBe("0:00");
  });

  it("formats elapsed time for the status line", () => {
    expect(formatElapsed(192)).toBe("3 h 12 min");
    expect(formatElapsed(45)).toBe("45 min");
  });
});

describe("calendar dates", () => {
  it("moves across month and DST boundaries without shifting", () => {
    expect(addDays("2026-11-30", 1)).toBe("2026-12-01");
    expect(addDays("2026-03-28", 1)).toBe("2026-03-29");
    expect(addDays("2026-11-23", -7)).toBe("2026-11-16");
  });

  it("lists the 7 days of a week", () => {
    expect(weekDates("2026-11-16")).toEqual([
      "2026-11-16", "2026-11-17", "2026-11-18", "2026-11-19", "2026-11-20", "2026-11-21", "2026-11-22",
    ]);
  });
});
