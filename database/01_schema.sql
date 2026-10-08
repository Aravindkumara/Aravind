-- =====================================================================
-- OEM Product Service Excellence - PostgreSQL schema
-- =====================================================================

-- ---------------------------------------------------------------------
-- Dealers & users
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS dealers (
    id              SERIAL PRIMARY KEY,
    code            VARCHAR(30)  NOT NULL UNIQUE,
    name            VARCHAR(200) NOT NULL,
    city            VARCHAR(100),
    state           VARCHAR(100),
    contact_phone   VARCHAR(30),
    email           VARCHAR(150),
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS users (
    id              SERIAL PRIMARY KEY,
    username        VARCHAR(60)  NOT NULL UNIQUE,
    password_hash   VARCHAR(200) NOT NULL,
    full_name       VARCHAR(150) NOT NULL,
    email           VARCHAR(150),
    role            VARCHAR(20)  NOT NULL CHECK (role IN ('Admin', 'Author', 'Approver', 'Dealer')),
    dealer_id       INT REFERENCES dealers(id),
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    last_login_at   TIMESTAMPTZ,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ,
    CONSTRAINT ck_users_dealer CHECK (role <> 'Dealer' OR dealer_id IS NOT NULL)
);

-- ---------------------------------------------------------------------
-- Product classification hierarchy
-- Division -> Model Category -> Model -> Variant -> Assembly (tree)
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS divisions (
    id              SERIAL PRIMARY KEY,
    code            VARCHAR(30)  NOT NULL UNIQUE,
    name            VARCHAR(150) NOT NULL,
    description     VARCHAR(500),
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS model_categories (
    id              SERIAL PRIMARY KEY,
    division_id     INT          NOT NULL REFERENCES divisions(id),
    code            VARCHAR(30)  NOT NULL UNIQUE,
    name            VARCHAR(150) NOT NULL,
    description     VARCHAR(500),
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS ix_model_categories_division ON model_categories(division_id);

CREATE TABLE IF NOT EXISTS models (
    id              SERIAL PRIMARY KEY,
    category_id     INT          NOT NULL REFERENCES model_categories(id),
    code            VARCHAR(30)  NOT NULL UNIQUE,
    name            VARCHAR(150) NOT NULL,
    description     VARCHAR(500),
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS ix_models_category ON models(category_id);

CREATE TABLE IF NOT EXISTS variants (
    id              SERIAL PRIMARY KEY,
    model_id        INT          NOT NULL REFERENCES models(id),
    code            VARCHAR(30)  NOT NULL UNIQUE,
    name            VARCHAR(150) NOT NULL,
    description     VARCHAR(500),
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS ix_variants_model ON variants(model_id);

-- Assembly level: 1 = Assembly, 2 = Sub-assembly, 3 = Component
CREATE TABLE IF NOT EXISTS assemblies (
    id              SERIAL PRIMARY KEY,
    variant_id      INT          NOT NULL REFERENCES variants(id),
    parent_id       INT          REFERENCES assemblies(id),
    level           SMALLINT     NOT NULL CHECK (level BETWEEN 1 AND 3),
    code            VARCHAR(40)  NOT NULL,
    name            VARCHAR(150) NOT NULL,
    description     VARCHAR(500),
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ,
    CONSTRAINT uq_assemblies_variant_code UNIQUE (variant_id, code),
    CONSTRAINT ck_assemblies_parent CHECK ((level = 1 AND parent_id IS NULL) OR (level > 1 AND parent_id IS NOT NULL))
);
CREATE INDEX IF NOT EXISTS ix_assemblies_variant ON assemblies(variant_id);
CREATE INDEX IF NOT EXISTS ix_assemblies_parent ON assemblies(parent_id);

-- ---------------------------------------------------------------------
-- Standard Operating Procedures
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sops (
    id                      SERIAL PRIMARY KEY,
    sop_code                VARCHAR(40)  NOT NULL,
    version                 INT          NOT NULL DEFAULT 1,
    title                   VARCHAR(250) NOT NULL,
    activity_type           VARCHAR(40)  NOT NULL CHECK (activity_type IN
                                ('PDI', 'PeriodicMaintenance', 'Repair', 'Inspection', 'Warranty', 'Campaign')),
    purpose                 TEXT,
    division_id             INT          NOT NULL REFERENCES divisions(id),
    category_id             INT          REFERENCES model_categories(id),
    model_id                INT          REFERENCES models(id),
    variant_id              INT          REFERENCES variants(id),
    assembly_id             INT          REFERENCES assemblies(id),
    standard_time_minutes   INT          NOT NULL DEFAULT 0 CHECK (standard_time_minutes >= 0),
    skill_level             VARCHAR(40),
    safety_notes            TEXT,
    status                  VARCHAR(20)  NOT NULL DEFAULT 'Draft' CHECK (status IN
                                ('Draft', 'UnderReview', 'Approved', 'Rejected', 'Published', 'Obsolete')),
    review_remarks          TEXT,
    previous_version_id     INT          REFERENCES sops(id),
    created_by              INT          NOT NULL REFERENCES users(id),
    created_at              TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_by              INT          REFERENCES users(id),
    updated_at              TIMESTAMPTZ,
    submitted_at            TIMESTAMPTZ,
    approved_by             INT          REFERENCES users(id),
    approved_at             TIMESTAMPTZ,
    published_at            TIMESTAMPTZ,
    CONSTRAINT uq_sops_code_version UNIQUE (sop_code, version)
);
CREATE INDEX IF NOT EXISTS ix_sops_status ON sops(status);
CREATE INDEX IF NOT EXISTS ix_sops_classification ON sops(division_id, category_id, model_id, variant_id, assembly_id);

CREATE TABLE IF NOT EXISTS sop_steps (
    id                  SERIAL PRIMARY KEY,
    sop_id              INT          NOT NULL REFERENCES sops(id) ON DELETE CASCADE,
    step_no             INT          NOT NULL,
    title               VARCHAR(200) NOT NULL,
    instruction         TEXT         NOT NULL,
    tools_required      VARCHAR(500),
    specification       VARCHAR(500),
    caution             VARCHAR(1000),
    estimated_minutes   INT          NOT NULL DEFAULT 0 CHECK (estimated_minutes >= 0)
);
CREATE INDEX IF NOT EXISTS ix_sop_steps_sop ON sop_steps(sop_id, step_no);

CREATE TABLE IF NOT EXISTS sop_step_attachments (
    id              SERIAL PRIMARY KEY,
    step_id         INT          NOT NULL REFERENCES sop_steps(id) ON DELETE CASCADE,
    file_name       VARCHAR(255) NOT NULL,
    stored_name     VARCHAR(255) NOT NULL,
    content_type    VARCHAR(100) NOT NULL,
    size_bytes      BIGINT       NOT NULL,
    uploaded_by     INT          NOT NULL REFERENCES users(id),
    uploaded_at     TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_sop_step_attachments_step ON sop_step_attachments(step_id);

-- Parts, special tools and consumables required by an SOP
CREATE TABLE IF NOT EXISTS sop_resources (
    id              SERIAL PRIMARY KEY,
    sop_id          INT           NOT NULL REFERENCES sops(id) ON DELETE CASCADE,
    resource_type   VARCHAR(20)   NOT NULL CHECK (resource_type IN ('Part', 'Tool', 'Consumable')),
    part_number     VARCHAR(60),
    description     VARCHAR(250)  NOT NULL,
    quantity        NUMERIC(10,2) NOT NULL DEFAULT 1,
    uom             VARCHAR(20)
);
CREATE INDEX IF NOT EXISTS ix_sop_resources_sop ON sop_resources(sop_id);

-- ---------------------------------------------------------------------
-- Audit trail
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS audit_logs (
    id              BIGSERIAL PRIMARY KEY,
    entity_type     VARCHAR(50)  NOT NULL,
    entity_id       INT          NOT NULL,
    action          VARCHAR(50)  NOT NULL,
    details         TEXT,
    user_id         INT          REFERENCES users(id),
    username        VARCHAR(60),
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_audit_logs_entity ON audit_logs(entity_type, entity_id);
CREATE INDEX IF NOT EXISTS ix_audit_logs_created ON audit_logs(created_at DESC);
