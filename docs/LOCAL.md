# Running it locally

Brings the service up against a seeded Postgres and points the employer portal at it, without a
Supabase project and without touching the Node backend.

## 1. Dependencies

```bash
cd kariyer-recruiting-service
docker compose -f docker-compose.dev.yml up -d
```

Postgres on **5433** (seeded on first boot from `deploy/local/`) and Garnet on **6380** — ports
offset so they cannot collide with anything already running. The seed creates the `public`
stand-ins, a company (`mock-company`, approved), three staff members, 40 candidates, 12 postings
and ~109 applications. **The posting uids match `kariyer-basvuru-web`'s mock jobs**, so the
portal's İlanlar list links straight into real service data.

Reseed from scratch:

```bash
docker compose -f docker-compose.dev.yml down -v && docker compose -f docker-compose.dev.yml up -d
```

## 2. The service

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_HTTP_PORTS=5340 \
  dotnet run --project src/Kariyer.Recruiting.Api
```

`appsettings.Development.json` turns on `Auth:DevIdentity`, which accepts every request as the
seeded company — there is no Supabase project locally. It is refused outside Development, and
migrations run on startup in this environment only.

Check it:

```bash
curl -s localhost:5340/health/ready
curl -s 'localhost:5340/api/recruiting/jobs/00000000-mock-job/applications?limit=2' | jq
curl -s -X PATCH localhost:5340/api/recruiting/applications/00000000-mock-job-1-app/status \
  -H 'Content-Type: application/json' -d '{"status":"REVIEWING"}' | jq
```

## 3. Events (optional)

Messaging is off in `appsettings.Development.json`, so the ATS screens work with no broker. To
watch the outbox publish for real:

```bash
ConnectionStrings__RabbitMQ='amqp://guest:guest@localhost:5673' Messaging__Enabled=true \
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_HTTP_PORTS=5340 \
  dotnet run --project src/Kariyer.Recruiting.Api
```

Invite someone to an interview and three messages land: the two stage moves the walk performs
(`NEW → REVIEWING → INTERVIEW`) and the invitation itself, on `recruiting.application.stage_changed`
and `recruiting.interview.invited`. Management UI on <http://localhost:15673> (guest/guest).

The invitation carries `acceptUrl` / `declineUrl` pointing back at this service
(`Recruiting:PublicApiUrl`). Opening one in a browser shows the candidate's confirmation page, and
the button on it records the answer — that is the whole path the e-mail exercises, testable
without the mail service.

## 4. Working against the real backend

The seed above is fictional data. To develop the ATS screens against the **real** postings and
applicants the portal is showing, copy them into the stand-ins — this service authorises every
read against `public.job_application` / `company_job` in its own database, so a real uid it has
never seen reads as "not found" and the portal hides notes, interviews and the activity trail.

With the portal open and signed in, in the browser console:

```js
await __kzSyncAts()
// → { jobs: 3, candidates: 7, applications: 9, signedInAs: "dev-company" }
```

It reads what the portal already reads (the company, its postings, their applications) from the
Node API and posts them to `POST /dev/stand-ins` here. Nothing is written to the backend. The
synced company takes over the dev identity's `external_id`, so this service signs you in as it —
no config change, no restart. Then set `VITE_RECRUITING_LIVE=true` in the portal's `.env`,
restart its dev server, and the pipeline, notes, interviews and activity run on real rows while
the writes stay in your local `recruiting` schema.

Re-run the sync whenever new applications arrive. That endpoint is the only thing here that
writes outside `recruiting`, and it is fenced in three ways: not mapped outside Development,
re-checked in the handler, and refused unless the database carries the `public.__standin_marker`
table that only `deploy/local` and `deploy/smoke` create.

To exercise the real Supabase token path instead of the dev identity, start the service with
`Auth__DevIdentity__Enabled=false Auth__ExternalProviderUrl=https://<project>.supabase.co`; the
company is then resolved from the token's `sub` against `company.external_id`, exactly as in
production.

## 5. The portal

In `kariyer-basvuru-web/.env`:

```
VITE_RECRUITING_LIVE=true
VITE_RECRUITING_API_URL=http://localhost:5340/api/recruiting
```

`VITE_RECRUITING_LIVE` runs the ATS screens (pipeline, interviews, notes, activity) against this
service while jobs, companies, candidates and CVs stay on the mock layer — those live in the Node
backend and would need a Supabase token. Restart `pnpm dev` after changing `.env`.

Then open `http://localhost:5199/ilanlar/00000000-mock-job`: the applicant list, stage moves,
interview invitations and notes are all real reads and writes against Postgres.

`Recruiting:AllowedOrigins` in `appsettings.Development.json` allows `localhost:5199`; add your
port there if you run the portal elsewhere.

## 6. Watching the data

```bash
docker exec kz-recruiting-postgres psql -U kariyer -d kariyer \
  -c 'select application_uid, stage, changed_by from recruiting.application_pipeline;' \
  -c 'select uid, type, status, starts_at from recruiting.interview;' \
  -c 'select type, actor_name, created_at from recruiting.activity_log order by created_at desc limit 10;'
```

## Notes

- RabbitMQ is off (`Messaging:Enabled=false`) unless you follow §3; the ATS screens do not need it.
- Garnet may be skipped entirely (`Garnet:Enabled=false`); everything works, reads just hit
  Postgres every time.
