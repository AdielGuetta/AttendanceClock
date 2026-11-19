# Plan 04: Choose the external time providers (PRD task T0)

**Status:** done ✅ · **Branch:** `feature/time-providers` · **PRD refs:** §4, D-4, D-5, EC-8, EC-9

## Goal
Pick a **primary** and a **backup** external time API for Europe/Zurich, based on measurements rather than guesses.
If no reliable backup exists, run with one provider and record that as a known risk (decision from PRD review, item 4).

## Selection criteria
| # | Criterion | Why |
|---|---|---|
| 1 | Free, **no API key needed** | Reviewers can run the project without signing up for anything. (The assignment requires *an external API*; "no key" is our own convenience criterion.) |
| 2 | HTTPS | Nobody on the network can tamper with the time we receive |
| 3 | Supports `Europe/Zurich` by IANA name | Explicit requirement |
| 4 | Returns the **UTC instant** and/or the **offset**, not only a local string | Without these, a local time is ambiguous during the DST "fall back" hour (EC-7) |
| 5 | Fast and consistent (low latency, few failures) | A punch waits for this call; the budget is ~2.5 s per provider (D-5) |
| 6 | Independent of the other provider | A backup on the same infrastructure doesn't help when the first one fails |

## Method
1. Shortlist candidates (well-known, keyless where possible).
2. From **inside the API container** (the same network path the real app will use), call each one several times. Record:
   HTTP status, latency, returned Zurich time, offset/UTC fields, and response shape.
3. Cross-check the providers' times against each other. They should agree within about 1 second.
4. Score them against the criteria → choose primary and backup → update PRD D-4.

## Out of scope for this step
Writing the C# code (`ITrustedClock`, provider clients, fallback, validation). That's **Plan 05**, which builds on this decision.

## Results (2026-11-19, ~20:15 Zurich, called from inside the API container)

### Shortlist: first call
| Candidate | Key? | Result | What it returns |
|---|---|---|---|
| timeapi.io `/api/time/current/zone` | no | ✅ 200 | Zurich local time + `dstActive` flag (no explicit offset) |
| timeapi.io `/api/timezone/zone` | no | ✅ 200 | Zurich local time + **`currentUtcOffset`** + DST start/end dates |
| worldtimeapi.org | no | ❌ TLS connection error | (well known, but frequently down) |
| time.now `/developer/api/timezone/…` | no | ✅ 200 | Local time **with offset**, `utc_datetime`, `unixtime`, `dst` (worldtimeapi-compatible format) |
| timeapi.world | no | ❌ domain doesn't resolve | (dead) |
| aisenseapi.com | no | ✅ 200 | UTC only. No time zone, so we'd have to convert to Zurich ourselves |

### Reliability: 10 calls each, 0.3 s apart (5 s timeout)
| Provider | OK / 10 | Typical latency | Notes |
|---|---|---|---|
| timeapi.io `/timezone/zone` | **10** | ~0.27 s | 4 of 10 calls took ~1.27 s (occasional slow responses) |
| timeapi.io `/time/current/zone` | **10** | ~0.27 s | 2 of 10 calls took ~1.26 s |
| time.now | **7** | ~0.25 s | 3 connection failures; a second run of 10 had 2 more. Not rate limiting (no 429): intermittent connection drops |
| worldtimeapi.org (https and http) | **0** | — | Down for the whole test |

### Cross-check
Same moment, both providers: timeapi.io `20:17:24.63 (+01:00)` vs time.now `20:17:24.94+01:00`. The ~0.3 s gap is
the time between the two sequential calls, so **the providers agree**. Both report CET (+01:00), which is correct for November.

### Decision (approved by the user, 2026-11-19)
- **Primary: timeapi.io `/api/timezone/zone`.** 100% success, fast, keyless, HTTPS. It returns the explicit
  current UTC offset, so the instant is unambiguous even in the DST "fall back" hour (EC-7).
- **Backup: time.now.** Keyless, HTTPS, an independent operator, and the richest format (offset + UTC + unix time,
  easy to cross-validate). **Known risk:** ~20–30% connection failures in our test. As a backup it's only called when
  the primary fails, so both failing together should be rare, but it's not guaranteed. Recorded in DECISIONS.md.
- **Rejected:** worldtimeapi.org (down), timeapi.world (dead), aisenseapi.com (UTC only, no Zurich zone),
  keyed providers such as TimeZoneDB or API Ninjas (would force reviewers to sign up; criterion 1).
- **Implication for Plan 05:** use a short per-attempt timeout (~2.5 s) so a slow primary call falls through to the backup
  within the overall budget (D-5).
