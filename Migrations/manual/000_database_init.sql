
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================================
-- 1. DATABASE CREATION (uses default data directory)
-- =============================================================
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'AssetMgmt')
BEGIN
    CREATE DATABASE AssetMgmt
    COLLATE SQL_Latin1_General_CP1_CI_AS;
END
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'AssetMgmt_Hangfire')
BEGIN
    CREATE DATABASE AssetMgmt_Hangfire
    COLLATE SQL_Latin1_General_CP1_CI_AS;
END
GO

-- =============================================================
-- 2. DATABASE OPTIONS
-- =============================================================
ALTER DATABASE AssetMgmt SET RECOVERY SIMPLE;
ALTER DATABASE AssetMgmt_Hangfire SET RECOVERY SIMPLE;
ALTER DATABASE AssetMgmt SET AUTO_SHRINK OFF;
ALTER DATABASE AssetMgmt SET AUTO_CREATE_STATISTICS ON;
ALTER DATABASE AssetMgmt SET AUTO_UPDATE_STATISTICS ON;
ALTER DATABASE AssetMgmt SET PAGE_VERIFY CHECKSUM;
ALTER DATABASE AssetMgmt SET COMPATIBILITY_LEVEL = 160;
ALTER DATABASE AssetMgmt_Hangfire SET COMPATIBILITY_LEVEL = 160;
GO

-- =============================================================
-- 3. SCHEMAS
-- =============================================================
USE AssetMgmt;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'asset')
    EXEC('CREATE SCHEMA asset');
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'audit')
    EXEC('CREATE SCHEMA audit');
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'ref')
    EXEC('CREATE SCHEMA ref');
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'integration')
    EXEC('CREATE SCHEMA integration');
GO

USE AssetMgmt_Hangfire;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'hangfire')
    EXEC('CREATE SCHEMA hangfire');
GO

-- =============================================================
-- 4. TABLES
-- =============================================================
USE AssetMgmt;
GO

-- -------------------------------------------------------------
-- 4.1 ref.departments
-- -------------------------------------------------------------
CREATE TABLE ref.departments (
    id              UNIQUEIDENTIFIER    NOT NULL    CONSTRAINT PK_departments PRIMARY KEY CLUSTERED
                    DEFAULT NEWSEQUENTIALID(),
    code            NVARCHAR(50)        NOT NULL,
    name            NVARCHAR(200)       NOT NULL,
    manager_id      UNIQUEIDENTIFIER    NULL,
    is_active       BIT                 NOT NULL    DEFAULT 1,
    created_at      DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    updated_at      DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    deleted_at      DATETIME2(3)        NULL,

    CONSTRAINT UQ_departments_code UNIQUE (code)
);
GO

CREATE NONCLUSTERED INDEX IX_departments_active
    ON ref.departments (is_active)
    INCLUDE (name)
    WHERE deleted_at IS NULL;
GO

-- -------------------------------------------------------------
-- 4.2 asset.users
-- -------------------------------------------------------------
CREATE TABLE asset.users (
    id                      UNIQUEIDENTIFIER    NOT NULL    CONSTRAINT PK_users PRIMARY KEY CLUSTERED
                            DEFAULT NEWSEQUENTIALID(),
    user_name               NVARCHAR(100)       NOT NULL,
    normalized_user_name    NVARCHAR(100)       NOT NULL,
    email                   NVARCHAR(255)       NOT NULL,
    normalized_email        NVARCHAR(255)       NOT NULL,
    email_confirmed         BIT                 NOT NULL    DEFAULT 0,
    password_hash           NVARCHAR(500)       NOT NULL,
    security_stamp          NVARCHAR(500)       NULL,
    phone_number            NVARCHAR(20)        NULL,
    phone_number_confirmed  BIT                 NOT NULL    DEFAULT 0,
    two_factor_enabled      BIT                 NOT NULL    DEFAULT 0,
    lockout_end             DATETIMEOFFSET      NULL,
    lockout_enabled         BIT                 NOT NULL    DEFAULT 1,
    access_failed_count     INT                 NOT NULL    DEFAULT 0,

    -- Custom fields
    employee_code           NVARCHAR(50)        NOT NULL,
    full_name               NVARCHAR(200)       NOT NULL,
    department_id           UNIQUEIDENTIFIER    NULL,
    role                    NVARCHAR(50)        NOT NULL,
    is_active               BIT                 NOT NULL    DEFAULT 1,
    last_login_at           DATETIME2(3)        NULL,

    created_at              DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    updated_at              DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    deleted_at              DATETIME2(3)        NULL,

    CONSTRAINT UQ_users_user_name UNIQUE (normalized_user_name),
    CONSTRAINT UQ_users_email UNIQUE (normalized_email),
    CONSTRAINT UQ_users_employee_code UNIQUE (employee_code),
    CONSTRAINT CK_users_role CHECK (role IN ('Employee', 'Manager', 'AdminIT'))
);
GO

ALTER TABLE asset.users
ADD CONSTRAINT FK_users_department
    FOREIGN KEY (department_id) REFERENCES ref.departments(id);
GO

CREATE NONCLUSTERED INDEX IX_users_department
    ON asset.users (department_id)
    WHERE department_id IS NOT NULL AND deleted_at IS NULL;

CREATE NONCLUSTERED INDEX IX_users_role_active
    ON asset.users (role)
    INCLUDE (full_name, employee_code)
    WHERE is_active = 1 AND deleted_at IS NULL;

CREATE NONCLUSTERED INDEX IX_users_full_name
    ON asset.users (full_name)
    WHERE deleted_at IS NULL;
GO

-- -------------------------------------------------------------
-- 4.3 asset.asset_models
-- -------------------------------------------------------------
CREATE TABLE asset.asset_models (
    id                              UNIQUEIDENTIFIER    NOT NULL    CONSTRAINT PK_asset_models PRIMARY KEY CLUSTERED
                                    DEFAULT NEWSEQUENTIALID(),
    name                            NVARCHAR(200)       NOT NULL,
    category                        NVARCHAR(100)       NOT NULL,
    manufacturer                    NVARCHAR(200)       NULL,
    model_number                    NVARCHAR(100)       NULL,
    specs                           NVARCHAR(MAX)       NULL,
    default_useful_life_months      INT                 NOT NULL    DEFAULT 36,
    default_depreciation_method     NVARCHAR(50)        NOT NULL    DEFAULT 'StraightLine',
    image_url                       NVARCHAR(500)       NULL,

    created_at                      DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    created_by                      UNIQUEIDENTIFIER    NULL,
    updated_at                      DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    updated_by                      UNIQUEIDENTIFIER    NULL,
    deleted_at                      DATETIME2(3)        NULL,

    CONSTRAINT CK_asset_models_category CHECK (category IN (
        'Laptop', 'Monitor', 'Phone', 'Tablet', 'Peripheral', 'Printer', 'NetworkDevice', 'Other'
    )),
    CONSTRAINT CK_asset_models_method CHECK (default_depreciation_method IN (
        'StraightLine', 'DecliningBalance'
    )),
    CONSTRAINT CK_asset_models_useful_life CHECK (default_useful_life_months > 0)
);
GO

CREATE NONCLUSTERED INDEX IX_asset_models_category
    ON asset.asset_models (category, name)
    WHERE deleted_at IS NULL;

CREATE NONCLUSTERED INDEX IX_asset_models_manufacturer
    ON asset.asset_models (manufacturer)
    WHERE deleted_at IS NULL AND manufacturer IS NOT NULL;
GO

-- -------------------------------------------------------------
-- 4.4 asset.asset_instances
-- -------------------------------------------------------------
CREATE TABLE asset.asset_instances (
    id                  UNIQUEIDENTIFIER    NOT NULL    CONSTRAINT PK_asset_instances PRIMARY KEY CLUSTERED
                        DEFAULT NEWSEQUENTIALID(),
    asset_code          NVARCHAR(50)        NOT NULL,
    serial              NVARCHAR(100)       NOT NULL,
    model_id            UNIQUEIDENTIFIER    NOT NULL,
    status              NVARCHAR(50)        NOT NULL    DEFAULT 'InStock',
    current_holder_id   UNIQUEIDENTIFIER    NULL,
    acquisition_cost    DECIMAL(18,2)       NOT NULL,
    acquisition_date    DATE                NOT NULL,
    salvage_value       DECIMAL(18,2)       NOT NULL    DEFAULT 0,
    location            NVARCHAR(200)       NULL,
    warranty_expires_at DATE                NULL,
    qr_code_path        NVARCHAR(500)       NULL,
    notes               NVARCHAR(MAX)       NULL,

    -- Locking fields
    lock_expires_at     DATETIME2(3)        NULL,
    lock_token          NVARCHAR(100)       NULL,
    lock_holder_user_id UNIQUEIDENTIFIER    NULL,

    -- Optimistic concurrency
    version             INT                 NOT NULL    DEFAULT 1,

    created_at          DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    created_by          UNIQUEIDENTIFIER    NULL,
    updated_at          DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    updated_by          UNIQUEIDENTIFIER    NULL,
    deleted_at          DATETIME2(3)        NULL,

    CONSTRAINT UQ_asset_instances_asset_code UNIQUE (asset_code),
    CONSTRAINT UQ_asset_instances_serial UNIQUE (serial),
    CONSTRAINT CK_asset_instances_status CHECK (status IN (
        'InStock', 'LockedTemp', 'Allocated', 'Maintenance', 'Retired', 'Lost', 'Disposed'
    )),
    CONSTRAINT CK_asset_instances_cost CHECK (acquisition_cost >= 0),
    CONSTRAINT CK_asset_instances_salvage CHECK (salvage_value >= 0),
    CONSTRAINT CK_asset_instances_holder_status CHECK (
        (status = 'InStock' AND current_holder_id IS NULL) OR
        (status IN ('Allocated', 'LockedTemp', 'Maintenance') AND current_holder_id IS NOT NULL) OR
        (status IN ('Retired', 'Lost', 'Disposed'))
    )
);
GO

ALTER TABLE asset.asset_instances
ADD CONSTRAINT FK_asset_instances_model
    FOREIGN KEY (model_id) REFERENCES asset.asset_models(id);

ALTER TABLE asset.asset_instances
ADD CONSTRAINT FK_asset_instances_holder
    FOREIGN KEY (current_holder_id) REFERENCES asset.users(id);
GO

CREATE NONCLUSTERED INDEX IX_asset_instances_status
    ON asset.asset_instances (status, model_id)
    INCLUDE (asset_code, serial, acquisition_date)
    WHERE deleted_at IS NULL;

CREATE NONCLUSTERED INDEX IX_asset_instances_holder
    ON asset.asset_instances (current_holder_id)
    INCLUDE (asset_code, serial, status, acquisition_date)
    WHERE current_holder_id IS NOT NULL AND deleted_at IS NULL;

CREATE NONCLUSTERED INDEX IX_asset_instances_model
    ON asset.asset_instances (model_id, status)
    WHERE deleted_at IS NULL;

CREATE NONCLUSTERED INDEX IX_asset_instances_lock_expiry
    ON asset.asset_instances (lock_expires_at)
    INCLUDE (id, status, lock_token)
    WHERE lock_expires_at IS NOT NULL;

CREATE NONCLUSTERED INDEX IX_asset_instances_location
    ON asset.asset_instances (location)
    WHERE location IS NOT NULL AND deleted_at IS NULL;
GO

-- -------------------------------------------------------------
-- 4.5 asset.allocation_requests
-- -------------------------------------------------------------
CREATE TABLE asset.allocation_requests (
    id                          UNIQUEIDENTIFIER    NOT NULL    CONSTRAINT PK_allocation_requests PRIMARY KEY CLUSTERED
                                DEFAULT NEWSEQUENTIALID(),
    requester_id                UNIQUEIDENTIFIER    NOT NULL,
    asset_instance_id           UNIQUEIDENTIFIER    NOT NULL,
    status                      NVARCHAR(50)        NOT NULL    DEFAULT 'Pending',
    reason                      NVARCHAR(MAX)       NULL,
    expected_duration_months    INT                 NULL,
    idempotency_key             NVARCHAR(100)       NOT NULL,
    lock_token                  NVARCHAR(100)       NULL,
    lock_expires_at             DATETIME2(3)        NULL,
    approver_id                 UNIQUEIDENTIFIER    NULL,
    approved_at                 DATETIME2(3)        NULL,
    rejected_reason             NVARCHAR(MAX)       NULL,
    expired_at                  DATETIME2(3)        NULL,
    cancelled_at                DATETIME2(3)        NULL,
    cancellation_reason         NVARCHAR(MAX)       NULL,

    created_at                  DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    updated_at                  DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),

    CONSTRAINT UQ_allocation_requests_idempotency UNIQUE (idempotency_key),
    CONSTRAINT CK_allocation_requests_status CHECK (status IN (
        'Pending', 'Locked', 'Approved', 'Rejected', 'Expired', 'Cancelled'
    )),
    CONSTRAINT CK_allocation_requests_duration CHECK (
        expected_duration_months IS NULL OR expected_duration_months > 0
    )
);
GO

ALTER TABLE asset.allocation_requests
ADD CONSTRAINT FK_allocation_requests_requester
    FOREIGN KEY (requester_id) REFERENCES asset.users(id);

ALTER TABLE asset.allocation_requests
ADD CONSTRAINT FK_allocation_requests_asset
    FOREIGN KEY (asset_instance_id) REFERENCES asset.asset_instances(id);

ALTER TABLE asset.allocation_requests
ADD CONSTRAINT FK_allocation_requests_approver
    FOREIGN KEY (approver_id) REFERENCES asset.users(id);
GO

CREATE NONCLUSTERED INDEX IX_allocation_requests_requester_status
    ON asset.allocation_requests (requester_id, status, created_at DESC)
    INCLUDE (asset_instance_id);

CREATE NONCLUSTERED INDEX IX_allocation_requests_status_lock
    ON asset.allocation_requests (status, lock_expires_at)
    WHERE status IN ('Pending', 'Locked');

CREATE NONCLUSTERED INDEX IX_allocation_requests_asset
    ON asset.allocation_requests (asset_instance_id, created_at DESC);

CREATE NONCLUSTERED INDEX IX_allocation_requests_approver
    ON asset.allocation_requests (approver_id, approved_at DESC)
    WHERE approver_id IS NOT NULL;
GO

-- -------------------------------------------------------------
-- 4.6 asset.allocations (append-only history)
-- -------------------------------------------------------------
CREATE TABLE asset.allocations (
    id                      UNIQUEIDENTIFIER    NOT NULL    CONSTRAINT PK_allocations PRIMARY KEY CLUSTERED
                            DEFAULT NEWSEQUENTIALID(),
    asset_instance_id       UNIQUEIDENTIFIER    NOT NULL,
    user_id                 UNIQUEIDENTIFIER    NOT NULL,
    event_type              NVARCHAR(50)        NOT NULL,
    start_date              DATE                NOT NULL,
    end_date                DATE                NULL,
    from_user_id            UNIQUEIDENTIFIER    NULL,
    to_user_id              UNIQUEIDENTIFIER    NULL,
    allocation_request_id   UNIQUEIDENTIFIER    NULL,
    handover_doc_id         UNIQUEIDENTIFIER    NULL,
    notes                   NVARCHAR(MAX)       NULL,

    created_at              DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    created_by              UNIQUEIDENTIFIER    NOT NULL,

    CONSTRAINT CK_allocations_event_type CHECK (event_type IN (
        'Allocated', 'Returned', 'Transferred'
    )),
    CONSTRAINT CK_allocations_dates CHECK (
        end_date IS NULL OR end_date >= start_date
    )
);
GO

ALTER TABLE asset.allocations
ADD CONSTRAINT FK_allocations_asset
    FOREIGN KEY (asset_instance_id) REFERENCES asset.asset_instances(id);

ALTER TABLE asset.allocations
ADD CONSTRAINT FK_allocations_user
    FOREIGN KEY (user_id) REFERENCES asset.users(id);

ALTER TABLE asset.allocations
ADD CONSTRAINT FK_allocations_from_user
    FOREIGN KEY (from_user_id) REFERENCES asset.users(id);

ALTER TABLE asset.allocations
ADD CONSTRAINT FK_allocations_to_user
    FOREIGN KEY (to_user_id) REFERENCES asset.users(id);

ALTER TABLE asset.allocations
ADD CONSTRAINT FK_allocations_request
    FOREIGN KEY (allocation_request_id) REFERENCES asset.allocation_requests(id);

ALTER TABLE asset.allocations
ADD CONSTRAINT FK_allocations_created_by
    FOREIGN KEY (created_by) REFERENCES asset.users(id);
GO

CREATE NONCLUSTERED INDEX IX_allocations_asset
    ON asset.allocations (asset_instance_id, start_date DESC)
    INCLUDE (user_id, event_type, end_date);

CREATE NONCLUSTERED INDEX IX_allocations_user
    ON asset.allocations (user_id, start_date DESC)
    INCLUDE (asset_instance_id, event_type, end_date)
    WHERE end_date IS NULL;

CREATE NONCLUSTERED INDEX IX_allocations_event_type
    ON asset.allocations (event_type, start_date DESC);
GO

-- -------------------------------------------------------------
-- 4.7 asset.handover_documents
-- -------------------------------------------------------------
CREATE TABLE asset.handover_documents (
    id                      UNIQUEIDENTIFIER    NOT NULL    CONSTRAINT PK_handover_documents PRIMARY KEY CLUSTERED
                            DEFAULT NEWSEQUENTIALID(),
    document_number         NVARCHAR(50)        NOT NULL,
    allocation_id           UNIQUEIDENTIFIER    NOT NULL,
    file_path               NVARCHAR(500)       NOT NULL,
    file_size_bytes         BIGINT              NULL,
    file_hash_sha256        NVARCHAR(64)        NULL,
    generated_at            DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    generated_by            UNIQUEIDENTIFIER    NOT NULL,
    signed_at               DATETIME2(3)        NULL,
    signed_by_employee      BIT                 NOT NULL    DEFAULT 0,
    signed_by_it            BIT                 NOT NULL    DEFAULT 0,
    employee_signature_path NVARCHAR(500)       NULL,
    it_signature_path       NVARCHAR(500)       NULL,

    created_at              DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),

    CONSTRAINT UQ_handover_documents_number UNIQUE (document_number)
);
GO

ALTER TABLE asset.handover_documents
ADD CONSTRAINT FK_handover_documents_allocation
    FOREIGN KEY (allocation_id) REFERENCES asset.allocations(id);

ALTER TABLE asset.handover_documents
ADD CONSTRAINT FK_handover_documents_generated_by
    FOREIGN KEY (generated_by) REFERENCES asset.users(id);
GO

ALTER TABLE asset.allocations
ADD CONSTRAINT FK_allocations_handover
    FOREIGN KEY (handover_doc_id) REFERENCES asset.handover_documents(id);
GO

CREATE NONCLUSTERED INDEX IX_handover_documents_allocation
    ON asset.handover_documents (allocation_id);

CREATE NONCLUSTERED INDEX IX_handover_documents_generated
    ON asset.handover_documents (generated_at DESC);
GO

-- -------------------------------------------------------------
-- 4.8 asset.depreciation_policies
-- -------------------------------------------------------------
CREATE TABLE asset.depreciation_policies (
    id                          UNIQUEIDENTIFIER    NOT NULL    CONSTRAINT PK_depreciation_policies PRIMARY KEY CLUSTERED
                                DEFAULT NEWSEQUENTIALID(),
    asset_model_id              UNIQUEIDENTIFIER    NOT NULL,
    method                      NVARCHAR(50)        NOT NULL,
    useful_life_months          INT                 NOT NULL,
    annual_decline_rate         DECIMAL(5,4)        NULL,
    salvage_value_percent       DECIMAL(5,2)        NOT NULL    DEFAULT 0,
    effective_from              DATE                NOT NULL,
    effective_to                DATE                NULL,
    created_at                  DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    updated_at                  DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),

    CONSTRAINT UQ_depreciation_policies_model UNIQUE (asset_model_id),
    CONSTRAINT CK_depreciation_policies_method CHECK (method IN (
        'StraightLine', 'DecliningBalance'
    )),
    CONSTRAINT CK_depreciation_policies_life CHECK (useful_life_months > 0),
    CONSTRAINT CK_depreciation_policies_rate CHECK (
        annual_decline_rate IS NULL OR (annual_decline_rate > 0 AND annual_decline_rate < 1)
    ),
    CONSTRAINT CK_depreciation_policies_salvage CHECK (
        salvage_value_percent >= 0 AND salvage_value_percent <= 100
    )
);
GO

ALTER TABLE asset.depreciation_policies
ADD CONSTRAINT FK_depreciation_policies_model
    FOREIGN KEY (asset_model_id) REFERENCES asset.asset_models(id);
GO

-- -------------------------------------------------------------
-- 4.9 asset.depreciation_ledger
-- -------------------------------------------------------------
CREATE TABLE asset.depreciation_ledger (
    id                          UNIQUEIDENTIFIER    NOT NULL    CONSTRAINT PK_depreciation_ledger PRIMARY KEY CLUSTERED
                                DEFAULT NEWSEQUENTIALID(),
    asset_instance_id           UNIQUEIDENTIFIER    NOT NULL,
    period_date                 DATE                NOT NULL,
    opening_book_value          DECIMAL(18,2)       NOT NULL,
    period_depreciation         DECIMAL(18,2)       NOT NULL,
    accumulated_depreciation    DECIMAL(18,2)       NOT NULL,
    closing_book_value          DECIMAL(18,2)       NOT NULL,
    policy_id                   UNIQUEIDENTIFIER    NOT NULL,
    posted_at                   DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    posted_by                   UNIQUEIDENTIFIER    NULL,
    created_at                  DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),

    CONSTRAINT UQ_depreciation_ledger_asset_period UNIQUE (asset_instance_id, period_date),
    CONSTRAINT CK_depreciation_ledger_period_day CHECK (DAY(period_date) = 1)
);
GO

ALTER TABLE asset.depreciation_ledger
ADD CONSTRAINT FK_depreciation_ledger_asset
    FOREIGN KEY (asset_instance_id) REFERENCES asset.asset_instances(id);

ALTER TABLE asset.depreciation_ledger
ADD CONSTRAINT FK_depreciation_ledger_policy
    FOREIGN KEY (policy_id) REFERENCES asset.depreciation_policies(id);

ALTER TABLE asset.depreciation_ledger
ADD CONSTRAINT FK_depreciation_ledger_posted_by
    FOREIGN KEY (posted_by) REFERENCES asset.users(id);
GO

CREATE NONCLUSTERED INDEX IX_depreciation_ledger_period
    ON asset.depreciation_ledger (period_date DESC, asset_instance_id)
    INCLUDE (period_depreciation, closing_book_value);

CREATE NONCLUSTERED INDEX IX_depreciation_ledger_asset
    ON asset.depreciation_ledger (asset_instance_id, period_date DESC)
    INCLUDE (closing_book_value, accumulated_depreciation);
GO

-- -------------------------------------------------------------
-- 4.10 asset.maintenance_records
-- -------------------------------------------------------------
CREATE TABLE asset.maintenance_records (
    id                  UNIQUEIDENTIFIER    NOT NULL    CONSTRAINT PK_maintenance_records PRIMARY KEY CLUSTERED
                        DEFAULT NEWSEQUENTIALID(),
    asset_instance_id   UNIQUEIDENTIFIER    NOT NULL,
    maintenance_type    NVARCHAR(50)        NOT NULL,
    description         NVARCHAR(MAX)       NOT NULL,
    cost                DECIMAL(18,2)       NOT NULL    DEFAULT 0,
    vendor              NVARCHAR(200)       NULL,
    start_date          DATE                NOT NULL,
    end_date            DATE                NULL,
    status              NVARCHAR(50)        NOT NULL    DEFAULT 'InProgress',
    invoice_path        NVARCHAR(500)       NULL,
    notes               NVARCHAR(MAX)       NULL,
    created_at          DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    created_by          UNIQUEIDENTIFIER    NOT NULL,
    updated_at          DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),

    CONSTRAINT CK_maintenance_records_type CHECK (maintenance_type IN (
        'Repair', 'Upgrade', 'Inspection', 'WarrantyClaim', 'Cleaning'
    )),
    CONSTRAINT CK_maintenance_records_status CHECK (status IN (
        'InProgress', 'Completed', 'Cancelled'
    )),
    CONSTRAINT CK_maintenance_records_cost CHECK (cost >= 0),
    CONSTRAINT CK_maintenance_records_dates CHECK (
        end_date IS NULL OR end_date >= start_date
    )
);
GO

ALTER TABLE asset.maintenance_records
ADD CONSTRAINT FK_maintenance_records_asset
    FOREIGN KEY (asset_instance_id) REFERENCES asset.asset_instances(id);

ALTER TABLE asset.maintenance_records
ADD CONSTRAINT FK_maintenance_records_created_by
    FOREIGN KEY (created_by) REFERENCES asset.users(id);
GO

CREATE NONCLUSTERED INDEX IX_maintenance_records_asset
    ON asset.maintenance_records (asset_instance_id, start_date DESC)
    INCLUDE (maintenance_type, status, cost);

CREATE NONCLUSTERED INDEX IX_maintenance_records_status
    ON asset.maintenance_records (status, start_date DESC)
    WHERE status = 'InProgress';
GO

-- -------------------------------------------------------------
-- 4.11 audit.audit_logs
-- -------------------------------------------------------------
CREATE TABLE audit.audit_logs (
    id                  UNIQUEIDENTIFIER    NOT NULL    CONSTRAINT PK_audit_logs PRIMARY KEY CLUSTERED
                        DEFAULT NEWSEQUENTIALID(),
    user_id             UNIQUEIDENTIFIER    NULL,
    action              NVARCHAR(100)       NOT NULL,
    entity_type         NVARCHAR(100)       NULL,
    entity_id           UNIQUEIDENTIFIER    NULL,
    metadata            NVARCHAR(MAX)       NULL,
    ip_address          NVARCHAR(50)        NULL,
    user_agent          NVARCHAR(500)       NULL,
    correlation_id      UNIQUEIDENTIFIER    NULL,
    severity            NVARCHAR(20)        NOT NULL    DEFAULT 'Info',
    result              NVARCHAR(20)        NOT NULL    DEFAULT 'Success',
    error_message       NVARCHAR(MAX)       NULL,
    created_at          DATETIME2(3)        NOT NULL    DEFAULT SYSUTCDATETIME(),

    CONSTRAINT CK_audit_logs_severity CHECK (severity IN ('Debug', 'Info', 'Warning', 'Error', 'Critical')),
    CONSTRAINT CK_audit_logs_result CHECK (result IN ('Success', 'Failed'))
);
GO

ALTER TABLE audit.audit_logs
ADD CONSTRAINT FK_audit_logs_user
    FOREIGN KEY (user_id) REFERENCES asset.users(id);
GO

CREATE NONCLUSTERED INDEX IX_audit_logs_user_created
    ON audit.audit_logs (user_id, created_at DESC)
    INCLUDE (action, entity_type, result)
    WHERE user_id IS NOT NULL;

CREATE NONCLUSTERED INDEX IX_audit_logs_entity
    ON audit.audit_logs (entity_type, entity_id, created_at DESC)
    WHERE entity_id IS NOT NULL;

CREATE NONCLUSTERED INDEX IX_audit_logs_action
    ON audit.audit_logs (action, created_at DESC);

CREATE NONCLUSTERED INDEX IX_audit_logs_correlation
    ON audit.audit_logs (correlation_id)
    WHERE correlation_id IS NOT NULL;

CREATE NONCLUSTERED INDEX IX_audit_logs_severity_created
    ON audit.audit_logs (severity, created_at DESC)
    WHERE severity IN ('Error', 'Critical');
GO

-- Computed columns for JSON queries
ALTER TABLE audit.audit_logs
ADD metadata_action AS JSON_VALUE(metadata, '$.action');

ALTER TABLE audit.audit_logs
ADD metadata_asset_id AS JSON_VALUE(metadata, '$.asset_id');
GO

-- Index on computed column (no WHERE filter — filtered indexes cannot reference computed columns)
CREATE NONCLUSTERED INDEX IX_audit_logs_metadata_action
    ON audit.audit_logs (metadata_action, created_at DESC);
GO

-- -------------------------------------------------------------
-- 4.12 Circular FK: departments.manager_id -> users
-- -------------------------------------------------------------
ALTER TABLE ref.departments
ADD CONSTRAINT FK_departments_manager
    FOREIGN KEY (manager_id) REFERENCES asset.users(id);
GO

-- =============================================================
-- 5. FUNCTIONS
-- =============================================================

-- 5.1 Calculate book value
CREATE FUNCTION asset.fn_calculate_book_value(
    @acquisition_cost DECIMAL(18,2),
    @salvage_value DECIMAL(18,2),
    @acquisition_date DATE,
    @as_of_date DATE,
    @method NVARCHAR(50),
    @useful_life_months INT,
    @annual_decline_rate DECIMAL(5,4) = NULL
)
RETURNS DECIMAL(18,2)
AS
BEGIN
    DECLARE @result DECIMAL(18,2);
    DECLARE @months_elapsed INT;
    DECLARE @depreciable DECIMAL(18,2);
    DECLARE @monthly_depr DECIMAL(18,2);
    DECLARE @years DECIMAL(10,4);

    IF @as_of_date <= @acquisition_date RETURN @acquisition_cost;
    IF @acquisition_cost <= @salvage_value RETURN @salvage_value;

    SET @depreciable = @acquisition_cost - @salvage_value;

    IF @method = 'StraightLine'
    BEGIN
        SET @months_elapsed = DATEDIFF(MONTH, @acquisition_date, @as_of_date);
        IF @months_elapsed >= @useful_life_months
            SET @result = @salvage_value;
        ELSE
        BEGIN
            SET @monthly_depr = @depreciable / @useful_life_months;
            SET @result = @acquisition_cost - (@monthly_depr * @months_elapsed);
            IF @result < @salvage_value SET @result = @salvage_value;
        END
    END
    ELSE IF @method = 'DecliningBalance'
    BEGIN
        IF @annual_decline_rate IS NULL OR @annual_decline_rate <= 0
            SET @annual_decline_rate = 0.2;
        SET @years = CAST(DATEDIFF(DAY, @acquisition_date, @as_of_date) AS DECIMAL(10,4)) / 365.0;
        SET @result = @acquisition_cost * POWER(1 - @annual_decline_rate, @years);
        IF @result < @salvage_value SET @result = @salvage_value;
    END
    ELSE
        SET @result = @acquisition_cost;

    RETURN ROUND(@result, 2);
END
GO

-- 5.2 Generate asset code
-- Fixed: correct SUBSTRING offset to extract numeric part from 'IT-XX-0001' format
CREATE FUNCTION asset.fn_generate_asset_code(
    @category NVARCHAR(100)
)
RETURNS NVARCHAR(50)
AS
BEGIN
    DECLARE @prefix NVARCHAR(10);
    DECLARE @full_prefix NVARCHAR(20);
    DECLARE @next_number INT;
    DECLARE @result NVARCHAR(50);

    SET @prefix = CASE @category
        WHEN 'Laptop' THEN 'LT'
        WHEN 'Monitor' THEN 'MT'
        WHEN 'Phone' THEN 'PH'
        WHEN 'Tablet' THEN 'TB'
        WHEN 'Peripheral' THEN 'PR'
        WHEN 'Printer' THEN 'PRN'
        WHEN 'NetworkDevice' THEN 'NW'
        ELSE 'OTH'
    END;

    SET @full_prefix = 'IT-' + @prefix + '-';

    SELECT @next_number = ISNULL(MAX(TRY_CAST(
        SUBSTRING(asset_code, LEN(@full_prefix) + 1, 10) AS INT
    )), 0) + 1
    FROM asset.asset_instances
    WHERE asset_code LIKE @full_prefix + '%';

    SET @result = @full_prefix + RIGHT('0000' + CAST(@next_number AS NVARCHAR(10)), 4);
    RETURN @result;
END
GO

-- 5.3 Generate document number
CREATE FUNCTION asset.fn_generate_doc_number(@year INT)
RETURNS NVARCHAR(50)
AS
BEGIN
    DECLARE @next_number INT;
    DECLARE @prefix NVARCHAR(20);

    SET @prefix = 'BB-' + CAST(@year AS NVARCHAR(4)) + '-';

    SELECT @next_number = ISNULL(MAX(TRY_CAST(SUBSTRING(document_number, LEN(@prefix) + 1, 10) AS INT)), 0) + 1
    FROM asset.handover_documents
    WHERE document_number LIKE @prefix + '%';

    RETURN @prefix + RIGHT('0000' + CAST(@next_number AS NVARCHAR(10)), 4);
END
GO

-- =============================================================
-- 6. VIEWS
-- =============================================================

-- 6.1 Asset full info
CREATE VIEW asset.v_asset_full_info
AS
SELECT
    ai.id,
    ai.asset_code,
    ai.serial,
    ai.status,
    ai.acquisition_cost,
    ai.acquisition_date,
    ai.salvage_value,
    ai.location,
    ai.warranty_expires_at,
    ai.qr_code_path,
    ai.lock_expires_at,
    ai.version,
    ai.created_at,
    ai.updated_at,
    am.id AS model_id,
    am.name AS model_name,
    am.category,
    am.manufacturer,
    am.model_number,
    am.specs AS model_specs,
    u.id AS holder_id,
    u.employee_code AS holder_employee_code,
    u.full_name AS holder_full_name,
    u.email AS holder_email,
    d.id AS holder_department_id,
    d.name AS holder_department_name,
    (ai.acquisition_cost - ISNULL(ai.salvage_value, 0)) AS depreciable_amount,
    DATEDIFF(MONTH, ai.acquisition_date, GETUTCDATE()) AS age_months,
    CASE
        WHEN ai.status = 'Allocated' THEN 1
        WHEN ai.status = 'LockedTemp' THEN 1
        WHEN ai.status = 'Maintenance' THEN 1
        ELSE 0
    END AS is_in_use
FROM asset.asset_instances ai
    INNER JOIN asset.asset_models am ON ai.model_id = am.id
    LEFT JOIN asset.users u ON ai.current_holder_id = u.id
    LEFT JOIN ref.departments d ON u.department_id = d.id
WHERE ai.deleted_at IS NULL;
GO

-- 6.2 Allocation history
CREATE VIEW asset.v_allocation_history
AS
SELECT
    al.id,
    al.event_type,
    al.start_date,
    al.end_date,
    al.notes,
    al.created_at,
    ai.id AS asset_id,
    ai.asset_code,
    ai.serial,
    am.name AS model_name,
    am.category,
    u.id AS user_id,
    u.employee_code,
    u.full_name,
    u.email,
    fu.full_name AS from_user_name,
    tu.full_name AS to_user_name,
    hd.document_number,
    hd.file_path AS handover_file_path,
    CASE
        WHEN al.end_date IS NULL THEN DATEDIFF(DAY, al.start_date, GETUTCDATE())
        ELSE DATEDIFF(DAY, al.start_date, al.end_date)
    END AS duration_days
FROM asset.allocations al
    INNER JOIN asset.asset_instances ai ON al.asset_instance_id = ai.id
    INNER JOIN asset.asset_models am ON ai.model_id = am.id
    INNER JOIN asset.users u ON al.user_id = u.id
    LEFT JOIN asset.users fu ON al.from_user_id = fu.id
    LEFT JOIN asset.users tu ON al.to_user_id = tu.id
    LEFT JOIN asset.handover_documents hd ON al.handover_doc_id = hd.id;
GO

-- 6.3 Dashboard stats
CREATE VIEW asset.v_dashboard_stats
AS
SELECT
    (SELECT COUNT(*) FROM asset.asset_instances WHERE status = 'InStock' AND deleted_at IS NULL) AS total_in_stock,
    (SELECT COUNT(*) FROM asset.asset_instances WHERE status = 'Allocated' AND deleted_at IS NULL) AS total_allocated,
    (SELECT COUNT(*) FROM asset.asset_instances WHERE status = 'LockedTemp' AND deleted_at IS NULL) AS total_locked,
    (SELECT COUNT(*) FROM asset.asset_instances WHERE status = 'Maintenance' AND deleted_at IS NULL) AS total_maintenance,
    (SELECT COUNT(*) FROM asset.asset_instances WHERE status = 'Retired' AND deleted_at IS NULL) AS total_retired,
    (SELECT COUNT(*) FROM asset.asset_instances WHERE deleted_at IS NULL) AS total_assets,
    (SELECT COUNT(*) FROM asset.allocation_requests WHERE status IN ('Pending', 'Locked')) AS pending_requests,
    (SELECT ISNULL(SUM(acquisition_cost), 0) FROM asset.asset_instances WHERE deleted_at IS NULL) AS total_acquisition_value,
    (SELECT ISNULL(SUM(dl.closing_book_value), 0)
     FROM asset.depreciation_ledger dl
     INNER JOIN (
         SELECT asset_instance_id, MAX(period_date) AS max_period
         FROM asset.depreciation_ledger
         GROUP BY asset_instance_id
     ) latest ON dl.asset_instance_id = latest.asset_instance_id AND dl.period_date = latest.max_period
    ) AS current_book_value;
GO

-- 6.4 Recent audit actions
CREATE VIEW audit.v_recent_actions
AS
SELECT TOP 1000
    al.id,
    al.created_at,
    al.action,
    al.entity_type,
    al.entity_id,
    al.ip_address,
    al.correlation_id,
    al.severity,
    al.result,
    u.id AS user_id,
    u.user_name,
    u.full_name,
    JSON_VALUE(al.metadata, '$.description') AS description
FROM audit.audit_logs al
    LEFT JOIN asset.users u ON al.user_id = u.id
ORDER BY al.created_at DESC;
GO

-- =============================================================
-- 7. TRIGGERS
-- =============================================================

-- 7.1 Auto-update updated_at on users
CREATE TRIGGER asset.trg_users_updated_at
ON asset.users
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(updated_at)
    BEGIN
        UPDATE u
        SET updated_at = SYSUTCDATETIME()
        FROM asset.users u
        INNER JOIN inserted i ON u.id = i.id;
    END
END
GO

-- 7.2 Auto-update updated_at on asset_instances
CREATE TRIGGER asset.trg_asset_instances_updated_at
ON asset.asset_instances
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(updated_at)
    BEGIN
        UPDATE ai
        SET updated_at = SYSUTCDATETIME()
        FROM asset.asset_instances ai
        INNER JOIN inserted i ON ai.id = i.id;
    END
END
GO

-- 7.3 Auto-update updated_at on asset_models
CREATE TRIGGER asset.trg_asset_models_updated_at
ON asset.asset_models
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(updated_at)
    BEGIN
        UPDATE am
        SET updated_at = SYSUTCDATETIME()
        FROM asset.asset_models am
        INNER JOIN inserted i ON am.id = i.id;
    END
END
GO

-- 7.4 Auto-update updated_at on depreciation_policies
CREATE TRIGGER asset.trg_depreciation_policies_updated_at
ON asset.depreciation_policies
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(updated_at)
    BEGIN
        UPDATE dp
        SET updated_at = SYSUTCDATETIME()
        FROM asset.depreciation_policies dp
        INNER JOIN inserted i ON dp.id = i.id;
    END
END
GO

-- 7.5 Auto-update updated_at on maintenance_records
CREATE TRIGGER asset.trg_maintenance_records_updated_at
ON asset.maintenance_records
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(updated_at)
    BEGIN
        UPDATE mr
        SET updated_at = SYSUTCDATETIME()
        FROM asset.maintenance_records mr
        INNER JOIN inserted i ON mr.id = i.id;
    END
END
GO

-- 7.6 Auto-update updated_at on allocation_requests
CREATE TRIGGER asset.trg_allocation_requests_updated_at
ON asset.allocation_requests
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(updated_at)
    BEGIN
        UPDATE ar
        SET updated_at = SYSUTCDATETIME()
        FROM asset.allocation_requests ar
        INNER JOIN inserted i ON ar.id = i.id;
    END
END
GO

-- 7.7 Prevent UPDATE/DELETE on allocations (append-only)
CREATE TRIGGER asset.trg_allocations_no_modify
ON asset.allocations
FOR UPDATE, DELETE
AS
BEGIN
    ROLLBACK;
    THROW 50000, 'allocations is append-only. UPDATE/DELETE is not allowed.', 1;
END
GO

-- =============================================================
-- 8. SECURITY — Application users
-- =============================================================

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'asset_app')
BEGIN
    CREATE LOGIN asset_app WITH PASSWORD = 'AppStrong!Passw0rd!2026';
END
GO

USE AssetMgmt;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'asset_app')
BEGIN
    CREATE USER asset_app FOR LOGIN asset_app;
END
GO

ALTER ROLE db_datareader ADD MEMBER asset_app;
ALTER ROLE db_datawriter ADD MEMBER asset_app;

DENY DELETE ON audit.audit_logs TO asset_app;
DENY UPDATE ON audit.audit_logs TO asset_app;
DENY UPDATE ON asset.allocations TO asset_app;
DENY DELETE ON asset.allocations TO asset_app;
DENY UPDATE ON asset.depreciation_ledger TO asset_app;
DENY DELETE ON asset.depreciation_ledger TO asset_app;
GO

-- Hangfire user
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'asset_hangfire')
BEGIN
    CREATE LOGIN asset_hangfire WITH PASSWORD = 'HangfireStrong!Passw0rd!2026';
END
GO

USE AssetMgmt_Hangfire;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'asset_hangfire')
BEGIN
    CREATE USER asset_hangfire FOR LOGIN asset_hangfire;
END
GO

ALTER ROLE db_owner ADD MEMBER asset_hangfire;
GO

-- =============================================================
-- 9. SEED DATA
-- =============================================================
USE AssetMgmt;
GO

-- 9.1 Departments
INSERT INTO ref.departments (code, name, is_active) VALUES
    ('IT',      N'Phòng Công nghệ Thông tin',  1),
    ('HR',      N'Phòng Nhân sự',             1),
    ('ACC',     N'Phòng Kế toán',             1),
    ('SALE',    N'Phòng Kinh doanh',          1),
    ('MKT',     N'Phòng Marketing',           1),
    ('OPS',     N'Phòng Vận hành',            1),
    ('LEGAL',   N'Phòng Pháp chế',            1),
    ('CEO',     N'Ban Giám đốc',              1);
GO

-- 9.2 Users
DECLARE @dept_it UNIQUEIDENTIFIER = (SELECT id FROM ref.departments WHERE code = 'IT');
DECLARE @dept_hr UNIQUEIDENTIFIER = (SELECT id FROM ref.departments WHERE code = 'HR');
DECLARE @dept_acc UNIQUEIDENTIFIER = (SELECT id FROM ref.departments WHERE code = 'ACC');
DECLARE @dept_sale UNIQUEIDENTIFIER = (SELECT id FROM ref.departments WHERE code = 'SALE');

INSERT INTO asset.users (
    user_name, normalized_user_name, email, normalized_email,
    password_hash, security_stamp,
    employee_code, full_name, department_id, role, is_active
) VALUES
    ('admin',     'ADMIN',     'admin@company.vn',    'ADMIN@COMPANY.VN',
     'PLACEHOLDER_HASH_WILL_BE_REPLACED_BY_APP', NEWID(),
     'EMP001', N'Nguyễn Văn Admin',     @dept_it,   'AdminIT',  1),

    ('manager1',  'MANAGER1',  'manager.it@company.vn',  'MANAGER.IT@COMPANY.VN',
     'PLACEHOLDER_HASH_WILL_BE_REPLACED_BY_APP', NEWID(),
     'EMP010', N'Trần Thị ManagerIT',  @dept_it,   'Manager',   1),

    ('manager2',  'MANAGER2',  'manager.hr@company.vn',  'MANAGER.HR@COMPANY.VN',
     'PLACEHOLDER_HASH_WILL_BE_REPLACED_BY_APP', NEWID(),
     'EMP011', N'Lê Văn ManagerHR',   @dept_hr,   'Manager',   1),

    ('emp1',      'EMP1',      'emp1@company.vn',     'EMP1@COMPANY.VN',
     'PLACEHOLDER_HASH_WILL_BE_REPLACED_BY_APP', NEWID(),
     'EMP101', N'Phạm Thị Employee1', @dept_acc,  'Employee', 1),

    ('emp2',      'EMP2',      'emp2@company.vn',     'EMP2@COMPANY.VN',
     'PLACEHOLDER_HASH_WILL_BE_REPLACED_BY_APP', NEWID(),
     'EMP102', N'Hoàng Văn Employee2', @dept_sale, 'Employee', 1),

    ('emp3',      'EMP3',      'emp3@company.vn',     'EMP3@COMPANY.VN',
     'PLACEHOLDER_HASH_WILL_BE_REPLACED_BY_APP', NEWID(),
     'EMP103', N'Vũ Thị Employee3',    @dept_sale, 'Employee', 1);
GO

-- Update department managers
UPDATE ref.departments SET manager_id = (
    SELECT id FROM asset.users WHERE user_name = 'manager1'
) WHERE code = 'IT';

UPDATE ref.departments SET manager_id = (
    SELECT id FROM asset.users WHERE user_name = 'manager2'
) WHERE code = 'HR';
GO

-- 9.3 Asset Models
INSERT INTO asset.asset_models (name, category, manufacturer, model_number, default_useful_life_months, default_depreciation_method, specs) VALUES
    (N'Dell XPS 13 9340',           'Laptop',  'Dell',     'XPS13-9340', 36, 'StraightLine',
     N'{"cpu":"Intel Core Ultra 7","ram":"16GB","storage":"512GB SSD","display":"13.4 FHD+"}'),
    (N'MacBook Pro 14 M3',          'Laptop',  'Apple',    'MBP14-M3',   36, 'StraightLine',
     N'{"cpu":"Apple M3 Pro","ram":"18GB","storage":"512GB SSD","display":"14.2 Liquid Retina XDR"}'),
    (N'Lenovo ThinkPad X1 Carbon',  'Laptop',  'Lenovo',   'X1C-G11',    36, 'StraightLine',
     N'{"cpu":"Intel Core i7","ram":"16GB","storage":"1TB SSD","display":"14 WUXGA"}'),
    (N'HP EliteBook 840',           'Laptop',  'HP',       'EB840-G10',  36, 'StraightLine',
     N'{"cpu":"Intel Core i5","ram":"16GB","storage":"512GB SSD","display":"14 FHD"}'),

    (N'Dell UltraSharp U2723QE',    'Monitor', 'Dell',     'U2723QE',    60, 'StraightLine',
     N'{"size":"27in","resolution":"4K UHD","panel":"IPS Black","ports":"USB-C, HDMI, DP"}'),
    (N'LG 27UP850-W',               'Monitor', 'LG',       '27UP850',    60, 'StraightLine',
     N'{"size":"27in","resolution":"4K UHD","panel":"IPS","ports":"USB-C, HDMI"}'),
    (N'Samsung ViewFinity S8',      'Monitor', 'Samsung',  'LS32D800U',  60, 'StraightLine',
     N'{"size":"32in","resolution":"4K UHD","panel":"IPS"}'),

    (N'iPhone 15 Pro',              'Phone',   'Apple',    'IP15-PRO',   24, 'DecliningBalance',
     N'{"storage":"256GB","color":"Natural Titanium"}'),
    (N'Samsung Galaxy S24',         'Phone',   'Samsung',  'SM-S928B',   24, 'DecliningBalance',
     N'{"storage":"256GB","color":"Onyx Black"}'),

    (N'Logitech MX Master 3S',      'Peripheral', 'Logitech', 'MXM3S',    36, 'StraightLine',
     N'{"type":"Mouse","connectivity":"Bluetooth + USB-C"}'),
    (N'Apple Magic Keyboard',       'Peripheral', 'Apple',    'MK293LL/A', 36, 'StraightLine',
     N'{"type":"Keyboard","layout":"US English","connectivity":"Bluetooth"}');
GO

-- 9.4 Asset Instances (50 demo devices using direct INSERT instead of function in loop)
DECLARE @models TABLE (id UNIQUEIDENTIFIER, category NVARCHAR(100), prefix NVARCHAR(10));
INSERT INTO @models (id, category, prefix)
SELECT id, category,
    CASE category
        WHEN 'Laptop' THEN 'LT'
        WHEN 'Monitor' THEN 'MT'
        WHEN 'Phone' THEN 'PH'
        WHEN 'Tablet' THEN 'TB'
        WHEN 'Peripheral' THEN 'PR'
        WHEN 'Printer' THEN 'PRN'
        WHEN 'NetworkDevice' THEN 'NW'
        ELSE 'OTH'
    END
FROM asset.asset_models;

DECLARE @counter TABLE (prefix NVARCHAR(10), cnt INT);
INSERT INTO @counter VALUES ('LT',0),('MT',0),('PH',0),('TB',0),('PR',0),('PRN',0),('NW',0),('OTH',0);

DECLARE @i INT = 1;
DECLARE @model_id UNIQUEIDENTIFIER;
DECLARE @category NVARCHAR(100);
DECLARE @prefix NVARCHAR(10);
DECLARE @acq_cost DECIMAL(18,2);
DECLARE @asset_code NVARCHAR(50);
DECLARE @serial NVARCHAR(100);
DECLARE @location NVARCHAR(200);
DECLARE @num INT;

WHILE @i <= 50
BEGIN
    SELECT TOP 1
        @model_id = id,
        @category = category,
        @prefix = prefix
    FROM @models
    ORDER BY NEWID();

    SET @acq_cost = CASE @category
        WHEN 'Laptop' THEN 25000000 + ABS(CHECKSUM(NEWID())) % 15000000
        WHEN 'Monitor' THEN 8000000 + ABS(CHECKSUM(NEWID())) % 7000000
        WHEN 'Phone' THEN 15000000 + ABS(CHECKSUM(NEWID())) % 10000000
        WHEN 'Peripheral' THEN 2000000 + ABS(CHECKSUM(NEWID())) % 3000000
        ELSE 5000000
    END;

    UPDATE @counter SET cnt = cnt + 1 WHERE prefix = @prefix;
    SELECT @num = cnt FROM @counter WHERE prefix = @prefix;

    SET @asset_code = 'IT-' + @prefix + '-' + RIGHT('0000' + CAST(@num AS NVARCHAR(10)), 4);
    SET @serial = 'SN-' + CAST(@i AS NVARCHAR(10)) + '-' + RIGHT('000000' + CAST(ABS(CHECKSUM(NEWID())) % 1000000 AS NVARCHAR(10)), 6);
    SET @location = CASE ABS(CHECKSUM(NEWID())) % 4
        WHEN 0 THEN N'Kho IT - Tầng 5'
        WHEN 1 THEN N'Kho IT - Tầng 7'
        WHEN 2 THEN N'Kho Backup - Tầng 1'
        ELSE N'Văn phòng chi nhánh'
    END;

    INSERT INTO asset.asset_instances (
        asset_code, serial, model_id, status,
        acquisition_cost, acquisition_date, salvage_value,
        location, version
    ) VALUES (
        @asset_code, @serial, @model_id, 'InStock',
        @acq_cost,
        DATEADD(DAY, -ABS(CHECKSUM(NEWID())) % 720, GETUTCDATE()),
        @acq_cost * 0.05,
        @location, 1
    );

    SET @i = @i + 1;
END
GO

-- 9.5 Depreciation policies for each model
INSERT INTO asset.depreciation_policies (asset_model_id, method, useful_life_months, annual_decline_rate, salvage_value_percent, effective_from)
SELECT
    id,
    default_depreciation_method,
    default_useful_life_months,
    CASE WHEN default_depreciation_method = 'DecliningBalance' THEN 0.2000 ELSE NULL END,
    5.00,
    '2024-01-01'
FROM asset.asset_models;
GO

-- 9.6 Allocation Requests demo (5 pending requests)
DECLARE @emp_id UNIQUEIDENTIFIER;
DECLARE @asset_id UNIQUEIDENTIFIER;
DECLARE @j INT = 1;

WHILE @j <= 5
BEGIN
    SELECT TOP 1 @emp_id = id FROM asset.users WHERE role = 'Employee' ORDER BY NEWID();
    SELECT TOP 1 @asset_id = id FROM asset.asset_instances WHERE status = 'InStock' AND deleted_at IS NULL ORDER BY NEWID();

    INSERT INTO asset.allocation_requests (
        requester_id, asset_instance_id, status, reason,
        expected_duration_months, idempotency_key, created_at
    ) VALUES (
        @emp_id, @asset_id, 'Pending',
        N'Cần thiết bị để làm việc dự án mới',
        12,
        NEWID(),
        DATEADD(HOUR, -ABS(CHECKSUM(NEWID())) % 48, SYSUTCDATETIME())
    );

    SET @j = @j + 1;
END
GO

-- =============================================================
-- VERIFICATION
-- =============================================================
SELECT 'departments' AS table_name, COUNT(*) AS row_count FROM ref.departments
UNION ALL
SELECT 'users', COUNT(*) FROM asset.users
UNION ALL
SELECT 'asset_models', COUNT(*) FROM asset.asset_models
UNION ALL
SELECT 'asset_instances', COUNT(*) FROM asset.asset_instances
UNION ALL
SELECT 'depreciation_policies', COUNT(*) FROM asset.depreciation_policies
UNION ALL
SELECT 'allocation_requests', COUNT(*) FROM asset.allocation_requests;
GO

PRINT '====================================';
PRINT ' Database initialization complete!';
PRINT '====================================';
GO
