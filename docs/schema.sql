-- kariyer-recruiting-service — the recruiting schema, as DDL.
--
-- GENERATED. Regenerate with
--   head -20 docs/schema.sql > /tmp/header.sql
--   dotnet ef migrations script --project src/Kariyer.Recruiting.Api --idempotent -o /tmp/body.sql
--   cat /tmp/header.sql /tmp/body.sql | perl -CSD -pe 's/\x{FEFF}//g' > docs/schema.sql
-- The BOM strip is not optional: `dotnet ef` writes one, and psql reads it as part of the first
-- statement — `ERROR: syntax error at or near "DO"` on an otherwise valid script.
-- CI regenerates it the same way and fails if the committed file differs.
--
-- Idempotent: every statement is guarded on the migration-history table.
--
-- What is NOT here, deliberately: public.job_application, public.company_job, public.employee,
-- public.company and public.company_employee. This service reads them through keyless
-- projections and never creates, alters or writes them — they belong to the Node application.
--
-- The migration-history table lands in `recruiting`, not in `public`: every .NET service
-- migrates into the same database, and a shared history table would have each one seeing the
-- others' migrations as unknown.

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'recruiting') THEN
        CREATE SCHEMA recruiting;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS recruiting."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'recruiting') THEN
            CREATE SCHEMA recruiting;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE OR REPLACE FUNCTION recruiting.kz_fold(value text)
    RETURNS text
    LANGUAGE sql
    IMMUTABLE
    PARALLEL SAFE
    RETURNS NULL ON NULL INPUT
    AS $$
        SELECT lower(translate(value,
            'İIıŞşĞğÜüÖöÇçÂâÎîÛû',
            'iiissgguuooccaaiiuu'));
    $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE TABLE recruiting.activity_log (
        id bigint GENERATED ALWAYS AS IDENTITY,
        application_uid character varying(128) NOT NULL,
        job_uid character varying(128) NOT NULL,
        company_uid character varying(128) NOT NULL,
        type character varying(48) NOT NULL,
        actor_uid character varying(128),
        actor_name character varying(256),
        metadata jsonb NOT NULL,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_activity_log" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE TABLE recruiting.application_note (
        application_uid character varying(128) NOT NULL,
        job_uid character varying(128) NOT NULL,
        company_uid character varying(128) NOT NULL,
        candidate_uid character varying(128) NOT NULL,
        body character varying(2000) NOT NULL,
        author_uid character varying(128) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_application_note" PRIMARY KEY (application_uid)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE TABLE recruiting.application_pipeline (
        application_uid character varying(128) NOT NULL,
        job_uid character varying(128) NOT NULL,
        company_uid character varying(128) NOT NULL,
        candidate_uid character varying(128) NOT NULL,
        stage character varying(32) NOT NULL,
        reason character varying(1000),
        changed_by character varying(128),
        changed_at timestamp with time zone NOT NULL,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_application_pipeline" PRIMARY KEY (application_uid)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE TABLE recruiting.interview (
        uid character varying(128) NOT NULL,
        application_uid character varying(128) NOT NULL,
        job_uid character varying(128) NOT NULL,
        company_uid character varying(128) NOT NULL,
        candidate_uid character varying(128) NOT NULL,
        type character varying(32) NOT NULL,
        starts_at timestamp with time zone NOT NULL,
        duration_minutes integer NOT NULL,
        timezone character varying(64) NOT NULL,
        video_url character varying(1000),
        location character varying(1000),
        candidate_message character varying(500),
        internal_note character varying(2000),
        status character varying(32) NOT NULL,
        confirmation_status character varying(32) NOT NULL,
        result character varying(32),
        created_by character varying(128) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_interview" PRIMARY KEY (uid)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE TABLE recruiting.saved_filter (
        id uuid NOT NULL,
        company_uid character varying(128) NOT NULL,
        user_uid character varying(128) NOT NULL,
        name character varying(120) NOT NULL,
        query jsonb NOT NULL,
        is_default boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_saved_filter" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE TABLE recruiting.interview_participant (
        id bigint GENERATED BY DEFAULT AS IDENTITY,
        email character varying(320) NOT NULL,
        role character varying(32) NOT NULL,
        name character varying(256),
        interview_uid character varying(512) NOT NULL,
        CONSTRAINT "PK_interview_participant" PRIMARY KEY (id),
        CONSTRAINT "FK_interview_participant_interview_interview_uid" FOREIGN KEY (interview_uid) REFERENCES recruiting.interview (uid) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE INDEX ix_activity_application ON recruiting.activity_log (application_uid, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE INDEX ix_activity_company ON recruiting.activity_log (company_uid, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE INDEX ix_note_job ON recruiting.application_note (job_uid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE INDEX ix_pipeline_candidate ON recruiting.application_pipeline (candidate_uid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE INDEX ix_pipeline_company_changed ON recruiting.application_pipeline (company_uid, changed_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE INDEX ix_pipeline_job_stage ON recruiting.application_pipeline (job_uid, stage);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE INDEX ix_interview_application_status ON recruiting.interview (application_uid, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE INDEX ix_interview_candidate ON recruiting.interview (candidate_uid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE INDEX ix_interview_job_starts ON recruiting.interview (job_uid, starts_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE INDEX "IX_interview_participant_interview_uid" ON recruiting.interview_participant (interview_uid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    CREATE INDEX ix_saved_filter_owner ON recruiting.saved_filter (company_uid, user_uid);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924131411_InitialRecruitingSchema') THEN
    INSERT INTO recruiting."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924131411_InitialRecruitingSchema', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924132206_InterviewerOnInterview') THEN
    ALTER TABLE recruiting.interview ADD interviewer_uid character varying(128) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924132206_InterviewerOnInterview') THEN
    INSERT INTO recruiting."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924132206_InterviewerOnInterview', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924133158_LegacyStageFunction') THEN
    CREATE OR REPLACE FUNCTION recruiting.kz_stage_from_legacy(value text)
    RETURNS text
    LANGUAGE sql
    IMMUTABLE
    PARALLEL SAFE
    AS $$
        SELECT CASE lower(coalesce(value, ''))
            WHEN 'pending'      THEN 'NEW'
            WHEN 'under_review' THEN 'REVIEWING'
            WHEN 'accepted'     THEN 'HIRED'
            WHEN 'rejected'     THEN 'REJECTED'
            WHEN 'withdrawn'    THEN 'WITHDRAWN'
            ELSE 'NEW'
        END;
    $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924133158_LegacyStageFunction') THEN
    INSERT INTO recruiting."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924133158_LegacyStageFunction', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924144250_TransactionalOutbox') THEN
    CREATE TABLE recruiting."InboxState" (
        "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
        "MessageId" uuid NOT NULL,
        "ConsumerId" uuid NOT NULL,
        "LockId" uuid NOT NULL,
        "RowVersion" bytea,
        "Received" timestamp with time zone NOT NULL,
        "ReceiveCount" integer NOT NULL,
        "ExpirationTime" timestamp with time zone,
        "Consumed" timestamp with time zone,
        "Delivered" timestamp with time zone,
        "LastSequenceNumber" bigint,
        CONSTRAINT "PK_InboxState" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_InboxState_MessageId_ConsumerId" UNIQUE ("MessageId", "ConsumerId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924144250_TransactionalOutbox') THEN
    CREATE TABLE recruiting."OutboxState" (
        "OutboxId" uuid NOT NULL,
        "LockId" uuid NOT NULL,
        "RowVersion" bytea,
        "Created" timestamp with time zone NOT NULL,
        "Delivered" timestamp with time zone,
        "LastSequenceNumber" bigint,
        CONSTRAINT "PK_OutboxState" PRIMARY KEY ("OutboxId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924144250_TransactionalOutbox') THEN
    CREATE TABLE recruiting."OutboxMessage" (
        "SequenceNumber" bigint GENERATED BY DEFAULT AS IDENTITY,
        "EnqueueTime" timestamp with time zone,
        "SentTime" timestamp with time zone NOT NULL,
        "Headers" text,
        "Properties" text,
        "InboxMessageId" uuid,
        "InboxConsumerId" uuid,
        "OutboxId" uuid,
        "MessageId" uuid NOT NULL,
        "ContentType" character varying(256) NOT NULL,
        "MessageType" text NOT NULL,
        "Body" text NOT NULL,
        "ConversationId" uuid,
        "CorrelationId" uuid,
        "InitiatorId" uuid,
        "RequestId" uuid,
        "SourceAddress" character varying(256),
        "DestinationAddress" character varying(256),
        "ResponseAddress" character varying(256),
        "FaultAddress" character varying(256),
        "ExpirationTime" timestamp with time zone,
        CONSTRAINT "PK_OutboxMessage" PRIMARY KEY ("SequenceNumber"),
        CONSTRAINT "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId" FOREIGN KEY ("InboxMessageId", "InboxConsumerId") REFERENCES recruiting."InboxState" ("MessageId", "ConsumerId"),
        CONSTRAINT "FK_OutboxMessage_OutboxState_OutboxId" FOREIGN KEY ("OutboxId") REFERENCES recruiting."OutboxState" ("OutboxId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924144250_TransactionalOutbox') THEN
    CREATE INDEX "IX_InboxState_Delivered" ON recruiting."InboxState" ("Delivered");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924144250_TransactionalOutbox') THEN
    CREATE INDEX "IX_OutboxMessage_EnqueueTime" ON recruiting."OutboxMessage" ("EnqueueTime");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924144250_TransactionalOutbox') THEN
    CREATE INDEX "IX_OutboxMessage_ExpirationTime" ON recruiting."OutboxMessage" ("ExpirationTime");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924144250_TransactionalOutbox') THEN
    CREATE UNIQUE INDEX "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber" ON recruiting."OutboxMessage" ("InboxMessageId", "InboxConsumerId", "SequenceNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924144250_TransactionalOutbox') THEN
    CREATE UNIQUE INDEX "IX_OutboxMessage_OutboxId_SequenceNumber" ON recruiting."OutboxMessage" ("OutboxId", "SequenceNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924144250_TransactionalOutbox') THEN
    CREATE INDEX "IX_OutboxState_Created" ON recruiting."OutboxState" ("Created");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260924144250_TransactionalOutbox') THEN
    INSERT INTO recruiting."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924144250_TransactionalOutbox', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260929114002_FixLegacyStageFunctionEnum') THEN
    CREATE OR REPLACE FUNCTION recruiting.kz_stage_from_legacy(value text)
    RETURNS text
    LANGUAGE sql
    IMMUTABLE
    PARALLEL SAFE
    AS $$
        SELECT CASE lower(coalesce(value, ''))
            WHEN 'pending'      THEN 'NEW'
            WHEN 'under_review' THEN 'REVIEWING'
            WHEN 'accepted'     THEN 'HIRED'
            WHEN 'rejected'     THEN 'REJECTED'
            WHEN 'withdrawn'    THEN 'WITHDRAWN'
            ELSE 'NEW'
        END;
    $$;

    CREATE OR REPLACE FUNCTION recruiting.kz_stage_from_legacy(value anyelement)
    RETURNS text
    LANGUAGE sql
    IMMUTABLE
    PARALLEL SAFE
    AS $$
        SELECT recruiting.kz_stage_from_legacy(value::text);
    $$;

    DO $$
    DECLARE
        v_schema text;
    BEGIN
        SELECT n.nspname INTO v_schema
        FROM pg_type t
        JOIN pg_namespace n ON n.oid = t.typnamespace
        WHERE t.typname = 'enum_job_application_application_status'
        LIMIT 1;

        IF v_schema IS NOT NULL THEN
            EXECUTE format('
                CREATE OR REPLACE FUNCTION recruiting.kz_stage_from_legacy(value %I.enum_job_application_application_status)
                RETURNS text
                LANGUAGE sql
                IMMUTABLE
                PARALLEL SAFE
                AS $func$
                    SELECT recruiting.kz_stage_from_legacy(value::text);
                $func$;
            ', v_schema);
        END IF;
    END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20260929114002_FixLegacyStageFunctionEnum') THEN
    INSERT INTO recruiting."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260929114002_FixLegacyStageFunctionEnum', '10.0.4');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20261002105529_CandidateMessages') THEN
    CREATE TABLE recruiting.candidate_message (
        uid character varying(128) NOT NULL,
        job_uid character varying(128) NOT NULL,
        company_uid character varying(128) NOT NULL,
        subject character varying(150),
        body character varying(2000) NOT NULL,
        sent_by character varying(128) NOT NULL,
        sent_by_name character varying(256),
        recipient_count integer NOT NULL,
        created_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_candidate_message" PRIMARY KEY (uid)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20261002105529_CandidateMessages') THEN
    CREATE INDEX ix_message_job_created ON recruiting.candidate_message (job_uid, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM recruiting."__EFMigrationsHistory" WHERE "MigrationId" = '20261002105529_CandidateMessages') THEN
    INSERT INTO recruiting."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261002105529_CandidateMessages', '10.0.4');
    END IF;
END $EF$;
COMMIT;

