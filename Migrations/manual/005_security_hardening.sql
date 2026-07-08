SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE AssetMgmt;
GO

-- Repair databases left half-upgraded by an interrupted 004 migration.
IF COL_LENGTH('asset.allocation_requests', 'handover_due_at') IS NULL
    ALTER TABLE asset.allocation_requests ADD handover_due_at DATETIME2(3) NULL;
GO

EXEC sys.sp_executesql N'
    UPDATE asset.allocation_requests
    SET handover_due_at = DATEADD(HOUR, 24, created_at)
    WHERE handover_due_at IS NULL;
';
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('asset.allocation_requests')
      AND name = 'handover_due_at' AND is_nullable = 1
)
    EXEC sys.sp_executesql N'ALTER TABLE asset.allocation_requests ALTER COLUMN handover_due_at DATETIME2(3) NOT NULL;';
GO

-- Idempotency keys are unique per requester, never globally.
IF EXISTS (
    SELECT 1 FROM sys.key_constraints
    WHERE parent_object_id = OBJECT_ID('asset.allocation_requests')
      AND name = 'UQ_allocation_requests_idempotency'
)
    ALTER TABLE asset.allocation_requests DROP CONSTRAINT UQ_allocation_requests_idempotency;
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('asset.allocation_requests')
      AND name = 'IX_allocation_requests_idempotency_key'
)
    DROP INDEX IX_allocation_requests_idempotency_key ON asset.allocation_requests;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('asset.allocation_requests')
      AND name = 'UX_allocation_requests_requester_idempotency'
)
    CREATE UNIQUE INDEX UX_allocation_requests_requester_idempotency
        ON asset.allocation_requests(requester_id, idempotency_key);
GO

IF OBJECT_ID('asset.refresh_sessions', 'U') IS NULL
BEGIN
    CREATE TABLE asset.refresh_sessions
    (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_refresh_sessions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        user_id UNIQUEIDENTIFIER NOT NULL,
        family_id UNIQUEIDENTIFIER NOT NULL,
        token_jti_hash CHAR(64) NOT NULL,
        expires_at DATETIME2(3) NOT NULL,
        used_at DATETIME2(3) NULL,
        revoked_at DATETIME2(3) NULL,
        replaced_by_id UNIQUEIDENTIFIER NULL,
        created_by_ip NVARCHAR(64) NULL,
        created_at DATETIME2(3) NOT NULL CONSTRAINT DF_refresh_sessions_created_at DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_refresh_sessions_user FOREIGN KEY (user_id) REFERENCES asset.users(id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX UX_refresh_sessions_jti_hash ON asset.refresh_sessions(token_jti_hash);
    CREATE INDEX IX_refresh_sessions_user_family ON asset.refresh_sessions(user_id, family_id);
END;
GO

IF OBJECT_ID('ai.pending_actions', 'U') IS NULL
BEGIN
    CREATE TABLE ai.pending_actions
    (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ai_pending_actions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        user_id UNIQUEIDENTIFIER NOT NULL,
        conversation_id UNIQUEIDENTIFIER NOT NULL,
        tool_name NVARCHAR(100) NOT NULL,
        payload_json NVARCHAR(MAX) NOT NULL,
        summary NVARCHAR(500) NOT NULL,
        status NVARCHAR(20) NOT NULL CONSTRAINT DF_ai_pending_actions_status DEFAULT 'Pending',
        result_json NVARCHAR(MAX) NULL,
        expires_at DATETIME2(3) NOT NULL,
        executed_at DATETIME2(3) NULL,
        cancelled_at DATETIME2(3) NULL,
        created_at DATETIME2(3) NOT NULL CONSTRAINT DF_ai_pending_actions_created_at DEFAULT SYSUTCDATETIME(),
        row_version ROWVERSION NOT NULL,
        CONSTRAINT FK_ai_pending_actions_user FOREIGN KEY (user_id) REFERENCES asset.users(id),
        CONSTRAINT FK_ai_pending_actions_conversation FOREIGN KEY (conversation_id)
            REFERENCES ai.agent_conversations(id) ON DELETE CASCADE,
        CONSTRAINT CK_ai_pending_actions_status CHECK (status IN ('Pending', 'Executed', 'Cancelled', 'Expired'))
    );
    CREATE INDEX IX_ai_pending_actions_user_status_expiry
        ON ai.pending_actions(user_id, status, expires_at);
END;
GO
