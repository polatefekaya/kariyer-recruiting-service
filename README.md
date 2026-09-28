# kariyer-recruiting-service

Başvuru yönetimi backend for the employer portal: hiring pipeline, interviews, recruiter notes
and the activity trail behind the İlan detayı and Adaylar screens in `kariyer-basvuru-web`.

- **Owns** the `recruiting` schema only. `job_application`, `company_job`, `employee`, `company`
  and `company_employee` are read through keyless projections and never written.
- **Publishes** `Kariyer.Messaging.Contracts` ≥ 1.3.0 `Recruiting/` events over the MassTransit
  EF outbox.
- **Caches** reads in Garnet with prefix invalidation on every write.
- **Serves** the candidate's accept/decline page itself, anonymously and signed — the only
  route here that does not belong to a signed-in company (`docs/ARCHITECTURE.md`).

**Running it locally: `docs/LOCAL.md`.** See `docs/PLAN.md` for scope, boundary and decisions,
`docs/ARCHITECTURE.md` for how it is put together, and `docs/schema.sql` for the DDL.

```bash
dotnet build
dotnet test
```

`docs/schema.sql` is generated; its header carries the exact recipe, and CI fails if the
committed file has drifted from the migrations.
