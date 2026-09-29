-- Stand-ins for the Node-owned tables this service reads.
--
-- Test and smoke environments only. The real tables are created and owned by the Node
-- application; these carry just the columns the projections map, so the service can be booted
-- against a database that has never seen the monolith.

-- Marks this database as a throwaway carrying stand-ins rather than the Node application's own
-- tables. The dev-only stand-in seeding endpoint refuses to write without it, so a service
-- pointed at a real database cannot touch public.* even if it were started in Development.
CREATE TABLE IF NOT EXISTS public.__standin_marker (created_at timestamptz NOT NULL DEFAULT now());

CREATE TABLE IF NOT EXISTS public.company (
    uid                   varchar(128) PRIMARY KEY,
    external_id           uuid,
    company_name          varchar(512),
    status                varchar(64)  NOT NULL DEFAULT 'approved',
    is_account_completed  boolean      NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS public.employee (
    uid        varchar(128) PRIMARY KEY,
    username   varchar(256),
    name       varchar(256),
    surname    varchar(256),
    email      varchar(320),
    phone      varchar(64),
    photo_url  varchar(1000),
    province   varchar(256),
    town       varchar(256)
);

CREATE TABLE IF NOT EXISTS public.company_employee (
    company_uid   varchar(128) NOT NULL,
    employee_uid  varchar(128) NOT NULL,
    position      varchar(256),
    status        varchar(64)  NOT NULL DEFAULT 'approved',
    is_active     boolean      NOT NULL DEFAULT true,
    PRIMARY KEY (company_uid, employee_uid)
);

CREATE TABLE IF NOT EXISTS public.company_job (
    uid         varchar(128) PRIMARY KEY,
    company_uid varchar(128),
    title       text         NOT NULL DEFAULT '',
    department  varchar(256) NOT NULL DEFAULT '',
    position    varchar(256) NOT NULL DEFAULT '',
    province    varchar(256) NOT NULL DEFAULT '',
    town        varchar(256) NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS public.job_application (
    uid                varchar(128) PRIMARY KEY,
    job_uid            varchar(128) NOT NULL,
    applicant_uid      varchar(128) NOT NULL,
    resume_id          integer,
    application_status varchar(64)  NOT NULL DEFAULT 'pending',
    applied_at         timestamptz  NOT NULL DEFAULT now()
);
