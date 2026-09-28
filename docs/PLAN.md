# kariyer-recruiting-service — PLAN

The başvuru yönetimi (application management) backend: the hiring pipeline, interviews,
recruiter notes, the audit trail and saved filters. It is the service behind the İlan detayı and
Adaylar screens in `kariyer-basvuru-web`.

Source of truth for scope: **Kariyer_Zamani_Basvuru_Yonetim_Teknik_Dokuman.pdf v1.0**. Section
references below (§4, §5.1, §9 …) point at that document. Where this service deviates, the
deviation is written down here with its reason — an undocumented deviation is a bug.

---

## 1. Boundary — what this service owns

Decided 2026-09-24.

| Owns (writes, migrates, is authoritative for) | Reads only (owned by the Node monolith) |
|---|---|
| `recruiting.application_pipeline` — the stage of every application + who moved it | `public.job_application` |
| `recruiting.interview` + `recruiting.interview_participant` | `public.company_job` |
| `recruiting.application_note` — one note per application | `public.employee`, `public.resume` |
| `recruiting.activity_log` | `public.company`, `public.company_employee` |
| `recruiting.saved_filter` | |

**It does not own applications.** Creating an application is still the candidate-facing flow in
the Node backend (`POST /jobs/:jobUid/apply`), and the planned `kariyer-job-service` is where
`job_application` eventually moves (see that repo's `JOB_MICROSERVICE_MIGRATION_BLUEPRINT.md`).
This service is the recruiting layer on top: it decides how an application moves and records
what was done to it. When applications do move to the job service, nothing here changes except
where the read-only projections point.

### 1.1 Nothing outside `recruiting` is ever written

The Node backend's code, models and tables are off limits: this service creates and migrates the
`recruiting` schema only, and reads `public.*` through keyless projections. No write-through, no
triggers, no enum widening.

The consequence, stated plainly: `public.job_application.application_status` keeps its five
legacy values and stops being the employer-side truth. The pipeline stage in
`recruiting.application_pipeline` is what the recruiting screens read and write, and it is
richer than the legacy column can express. The candidate-facing site continues to read its own
column and therefore does not see CONTACT / OFFER / HOLD at all.

Bridging that is the Node side's decision, not this service's: every move publishes
`ApplicationStageChangedEvent` (contracts 1.3.0) carrying both stages, so whenever the monolith
— or the future `kariyer-job-service` — wants the legacy column to follow, it consumes the event
and updates its own table with its own code.

## 2. Status vocabulary — 8 stages, one vocabulary

Decided 2026-09-24: the document's eight stages become the vocabulary **everywhere**, including
`public.job_application.application_status`, rather than this service mapping down to the legacy
five. One enum, no translation layer, no "which of the two statuses is real" question.

`Domain/Pipeline/ApplicationStage.cs` + `StageTransitions.cs` are the only definition; both this
service and (after the migration below) the Node backend speak it.

| Stage | Label | From here you may go to |
|---|---|---|
| `NEW` | Yeni | REVIEWING, CONTACT, REJECTED, HOLD |
| `REVIEWING` | İnceleniyor | CONTACT, INTERVIEW, REJECTED, HOLD |
| `CONTACT` | İletişim | INTERVIEW, REJECTED, HOLD |
| `INTERVIEW` | Mülakat | OFFER, REJECTED, HOLD |
| `OFFER` | Teklif | HIRED, REJECTED, HOLD |
| `HIRED` | İşe alındı | — terminal |
| `HOLD` | Karar verilmedi | NEW, REVIEWING, CONTACT, INTERVIEW, REJECTED |
| `REJECTED` | Reddedildi | — terminal |
| `WITHDRAWN` | Geri çekildi | — terminal, **candidate-only** |

**`WITHDRAWN` is this service's one addition to the document.** The doc's eight stages have no
place for a candidate who withdraws, but the product has always allowed it and rows already
exist. It is terminal, and `StageTransitions` makes it unreachable from every company action —
a recruiter must not be able to make an application look as though the candidate walked away.

A no-op move (`X -> X`) is rejected like any other illegal transition, so a double-submitted
button cannot write two `STAGE_CHANGED` entries claiming the same decision twice.

### 2.1 Relationship to the legacy five

No migration of `job_application.application_status` is performed or required by this service.
The stage starts at `NEW` for an application the pipeline has not seen before, derived once from
the legacy value on first touch:

| Legacy value | Initial stage |
|---|---|
| `pending` | `NEW` |
| `under_review` | `REVIEWING` |
| `accepted` | `HIRED` |
| `rejected` | `REJECTED` |
| `withdrawn` | `WITHDRAWN` |

`accepted → HIRED` is the judgement call: today's "accepted" is the end of the company's
process. Applications accepted but not hired cannot be told apart in the data, and inventing an
`OFFER` for them would put people in a stage nobody put them in. The derivation happens once,
lazily, and after that the pipeline row is authoritative.

## 3. Architecture

Same shape as `kariyer-cms-service` — that service is the reference for every cross-cutting
decision here, and deviating from it needs a reason.

- **net10.0**, central package management, versions level with the CMS service (EF Core 10.0.4,
  Npgsql 10.0.3, MassTransit 8.5.5).
- **Vertical slices**: `Features/<Area>/<UseCase>/<UseCase>Endpoint.cs`, discovered by assembly
  scan through `IEndpoint`. No controllers, no central route registry to merge-conflict over.
- **Domain project** holds the rules that must not depend on ASP.NET or EF: the stage machine,
  interview validation, note rules, Turkish search folding.
- **Same Postgres, own `recruiting` schema**, migration history inside it (a shared history
  table in `public` would make each service try to apply the others' migrations).
- **Supabase JWT bearer**, `MapInboundClaims = false`, issuer + audience validated, zero clock
  skew — copied from the CMS service, which copied it from the file service.
- **MassTransit + RabbitMQ with the EF outbox** for the events other services consume. They
  live in `Kariyer.Messaging.Contracts` **1.3.0**, `Recruiting/` folder, consumed from GitHub
  Packages like every other service — never as a ProjectReference to the local checkout, because
  the servers restore from the feed and a project link would let this build against types that
  are not on the registry.

  | Event | Published when | Consumers |
  |---|---|---|
  | `ApplicationStageChangedEvent` | every pipeline move, in the same commit as the pipeline row and the activity entry | mail (only for INTERVIEW/OFFER/HIRED/REJECTED — the publisher does not decide what is worth an e-mail), analytics |
  | `InterviewInvitedEvent` | an invitation is created | mail + in-app notification |
  | `InterviewRescheduledEvent` | a change the candidate can see (time, duration, type, place) | mail + calendar update |
  | `InterviewCancelledEvent` | the company calls a meeting off; a NO_SHOW publishes nothing | mail to candidate + participants |

  The invitation is announced once: the stage move to `INTERVIEW` also publishes
  `ApplicationStageChangedEvent`, so the mail service treats that stage as silent and reacts to
  `InterviewInvitedEvent` instead — otherwise one invitation sends two e-mails.
- **Serilog + OTLP**, `/health/live`, `/health/ready`, Prometheus metrics, and the health-check
  command that re-enters the binary for the container `HEALTHCHECK`.


### 3.2 Caching (Garnet)

Same client and connection policy as `kariyer-seo-service`: `StackExchange.Redis` against Garnet,
`AbortOnConnectFail = false`, bounded timeouts, and a no-op implementation when it is switched
off — a cache that is down degrades latency, never correctness. Every read falls through to
Postgres on a miss and every failure is logged, not thrown.

| Key | Holds | TTL |
|---|---|---|
| `recruiting:job:{jobUid}:apps:{queryHash}` | one page of the application list, filters included in the hash | 60 s |
| `recruiting:job:{jobUid}:stats` | pipeline chip counts | 120 s |
| `recruiting:job:{jobUid}:interviews` | the job's interview board | 60 s |
| `recruiting:company:{companyUid}:members` | interviewer directory | 15 min |
| `recruiting:identity:{sub}` | resolved company context for a token | 10 min |

Invalidation runs through `CacheInvalidator`, so a new cached read is registered in one place:
a stage change or a note save drops `recruiting:job:{jobUid}*` entirely, an interview write drops
the board and the list pages. Prefix deletes use `SCAN`, never `KEYS`.

### 3.1 Projections of Node-owned tables

Keyless, read-only EF projections over `public.job_application`, `public.company_job`,
`public.employee`, `public.company_employee`, mapped to only the columns this service reads.
They are excluded from migrations: this service must never create or alter a table it does not
own. Test and smoke environments get stand-ins under `deploy/smoke/`, the way the CMS service
does for `public.admin`.

---

## 4. Endpoints

Base path `/api/recruiting`. **The surface is driven by the screens in `kariyer-basvuru-web`, not
by the technical document's endpoint list.** Every route below has a caller; the document's
saved filters, CSV export, `source` / `minScore` / `location` filters and bulk actions have none
in the current design and are not implemented. The company is always resolved from the token —
no route accepts a company identifier.

| Method | Path | Screen |
|---|---|---|
| GET | `/jobs/{jobUid}/applications` | İlan detayı → Başvuranlar: status filter, `q` search, sort (`appliedAt:desc\|asc`, `stage:asc`), paging. Cached 60 s per filter set. |
| GET | `/jobs/{jobUid}/application-stats` | İlan detayı header: stage counts + interview totals. Cached 120 s. |
| GET | `/jobs/{jobUid}/interviews` | İlan detayı → Mülakatlar: ongoing / upcoming / past, each with interviewer and **davet eden**. |
| GET | `/jobs/{jobUid}/notes` | The note of every applicant on the posting, for the inline note editor. |
| GET | `/applications/{uid}/note` | That one application's note, for the Aday screen; 204 when there is none. |
| PUT | `/applications/{uid}/note` | Note upsert; an empty body clears it. |
| PATCH | `/applications/{uid}/status` | Stage move; 409 `INVALID_STATUS_TRANSITION` on an illegal one. |
| GET | `/applications/{uid}/activity` | Audit trail for one application. |
| POST | `/applications/{uid}/interviews` | Mülakata davet et: creates the interview, moves the stage to INTERVIEW, writes two activity rows, publishes `InterviewInvitedEvent`. |
| PATCH | `/interviews/{uid}` | Reschedule, complete with a result, mark no-show. |
| DELETE | `/interviews/{uid}` | Cancel; publishes `InterviewCancelledEvent`. |
| GET | `/candidates/{candidateUid}/interviews` | Aday detayı: that candidate's interviews **with this company only**. |
| GET | `/company/applications` | Adaylar: every application across the company's postings, filterable by `jobUid`, `candidateUid`, `status`, `q`; the screen folds them per candidate. |
| GET | `/company/members` | Interviewer picker; the signed-in user is always included. |
| GET | `/interviews/{uid}/confirmation/{accept\|decline}` | **Anonymous.** The page the candidate lands on from the invitation e-mail. Shows what is being answered and writes nothing — mail scanners follow links. |
| POST | `/interviews/{uid}/confirmation/{accept\|decline}` | **Anonymous.** Records the answer, from the button on that page or from an API client sending `Accept: application/json`. |

The two anonymous routes are the exception to "the company is always resolved from the token":
the candidate has no account here, so an HMAC token in the link — binding the interview, the
answer and an expiry — is the authorisation. `Recruiting:ConfirmationSigningKey` signs them and
is required outside Development.

Error bodies: `{ error: { code, message, details } }` with `VALIDATION_ERROR`, `UNAUTHORIZED`,
`FORBIDDEN`, `NOT_FOUND`, `INVALID_STATUS_TRANSITION`, `INTERNAL_ERROR`.

**Match scores are not available here.** `applicant_match` is computed per request by the Node
compatibility engine (`jobMatchService.getBulkMatchesForApplicants`) and stored nowhere, so no
projection can read it. `ApplicationListRow.Score` stays in the contract and is always `null`
until the matching engine is reachable as a service — the portal renders "% uyum" only when a
score is present, and offers no sort by it.

`recruiting.saved_filter` exists in the schema but has no endpoints: the table was cheap to
migrate now and the screen that would use it does not exist yet.

## 5. Deviations from the technical document

The implementation follows the current product design in `kariyer-basvuru-web`; the document is
input, not specification. Beyond the omissions listed above:

Each of these is a product decision already taken, recorded so the next reader does not "fix"
the code back to the document.

1. **Notes are one editable note per application, not an append-only list** (§8 says a list with
   author and createdAt per entry). The product decision is a notepad, not a comment thread. The
   audit the document wants is kept by `activity_log`, which records every save with its author
   and timestamp — so "who wrote what, when" is still answerable; what is not kept is every
   intermediate revision of the text.
2. **`WITHDRAWN` exists** (§2 above).
3. **`NO_SHOW` is an interview status** the document does not list. "Aday gelmedi" is not the
   same event as "şirket iptal etti", and collapsing them destroys the only no-show signal.
4. **A 5-minute grace on the "geçmiş tarih" rule.** The document says a start time may not be in
   the past; taken literally, a recruiter who fills the form for 15:00 and submits at 15:00:02
   is refused. A genuinely past meeting is recorded by creating and completing it.
5. **An invitation implies the review.** Inviting an application that is still `NEW` walks it
   `NEW → REVIEWING → INTERVIEW` in one transaction, with both moves logged, instead of
   returning 409 because `NEW → INTERVIEW` is not in the table. Inviting someone IS the decision
   to review them; refusing it would make the recruiter click twice to express one intent.
6. **Interview results** (`POSITIVE / NEGATIVE / UNDECIDED`) are kept alongside the document's
   `confirmationStatus`; the doc models only whether the candidate accepted, not how it went.

---

## 6. Milestones

| # | Scope | State |
|---|---|---|
| M0 | Solution, Domain (stage machine, interview + note rules, Turkish folding), 46 domain tests | **done** |
| M1 | Persistence: `recruiting` schema, entities, projections, first migration, `schema.sql` | **done** |
| M2 | Auth + company/actor resolution, error contract, health, telemetry, Garnet cache + invalidation | **done** |
| M3 | Applications: list, company-wide list, stats, stage change, note upsert, activity | **done** |
| M4 | Interviews: invite, reschedule, complete, no-show, cancel, job board, candidate board, member directory | **done** |
| M5 | Contracts 1.3.0 published; mail service consumers and analytics events | **done** — `kariyer-mail-service` consumes the three interview events; stage-change mail needs a recipient address on the contract (below) |
| M6 | Integration tests (Testcontainers), Dockerfile, compose, ARCHITECTURE/DEPLOYMENT docs | **done** |
| M7 | `kariyer-basvuru-web` switch-over (mock layer → this service, 8-stage vocabulary) | **done** |
| M8 | Node enum migration — a separate job, on its own terms | later |
| M9 | Candidate-facing mail on stage moves (OFFER / HIRED / REJECTED) | blocked: `ApplicationStageChangedEvent` carries `CandidateUid` but no address or name, so the mail service cannot send on it. Adding `CandidateEmail` / `CandidateName` is an additive contracts 1.4.0 release. |

Mutation rate limiting (§11) **is** implemented — see `ARCHITECTURE.md` — with the limits tunable
through `RateLimiting__*`.

### Roles are not coming

The document's role matrix (§11: Owner/Admin, Recruiter, Hiring Manager, Viewer) is **out of
scope, permanently** — not deferred. The platform's authentication has nothing to map it onto:
a Supabase account is an employee, a company, a community or an admin, and a company's staff are
rows in `company_employee` carrying a free-text `position`. There is no role claim, no role
table and no assignment UI anywhere in the estate, so a matrix here would be a second, invented
definition of "who may do what" that no other service could honour or enforce.

Access control is therefore company-level and complete at that level: `CompanyContextResolver`
resolves the token to one approved, completed company, and every read and write is scoped to it
— the same boundary the Node backend enforces. Every authenticated user of a company sees and
acts on that company's own applications.

What this closes, so it is not re-filed as a gap: per-role field visibility (aday e-posta /
telefon), CV-download gating (AC-07), export permission, note-delete ownership, and job-level or
hiring-team assignment. If the platform ever grows a real role model, this is the place to
revisit — until then these are answered, not open.
