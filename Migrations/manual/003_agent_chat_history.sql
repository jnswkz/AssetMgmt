IF SCHEMA_ID('ai') IS NULL
BEGIN
    EXEC('CREATE SCHEMA ai');
END;
GO

IF OBJECT_ID('ai.agent_conversations', 'U') IS NULL
BEGIN
    CREATE TABLE ai.agent_conversations
    (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_agent_conversations_id DEFAULT NEWSEQUENTIALID(),
        user_id UNIQUEIDENTIFIER NOT NULL,
        conversation_key NVARCHAR(100) NOT NULL,
        title NVARCHAR(200) NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_agent_conversations_created_at DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NOT NULL CONSTRAINT DF_agent_conversations_updated_at DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_agent_conversations PRIMARY KEY (id),
        CONSTRAINT FK_agent_conversations_users FOREIGN KEY (user_id)
            REFERENCES asset.users(id)
    );

    CREATE UNIQUE INDEX UX_agent_conversations_user_key
        ON ai.agent_conversations(user_id, conversation_key);
END;
GO

IF OBJECT_ID('ai.agent_messages', 'U') IS NULL
BEGIN
    CREATE TABLE ai.agent_messages
    (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_agent_messages_id DEFAULT NEWSEQUENTIALID(),
        conversation_id UNIQUEIDENTIFIER NOT NULL,
        role NVARCHAR(20) NOT NULL,
        content NVARCHAR(MAX) NOT NULL,
        intent NVARCHAR(80) NULL,
        tool_calls_json NVARCHAR(MAX) NULL,
        requires_confirmation BIT NOT NULL CONSTRAINT DF_agent_messages_requires_confirmation DEFAULT 0,
        pending_action_id NVARCHAR(100) NULL,
        created_at DATETIME2 NOT NULL CONSTRAINT DF_agent_messages_created_at DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_agent_messages PRIMARY KEY (id),
        CONSTRAINT FK_agent_messages_conversations FOREIGN KEY (conversation_id)
            REFERENCES ai.agent_conversations(id)
            ON DELETE CASCADE,
        CONSTRAINT CK_agent_messages_role CHECK (role IN ('user', 'assistant'))
    );

    CREATE INDEX IX_agent_messages_conversation_created
        ON ai.agent_messages(conversation_id, created_at);
END;
GO

CREATE OR ALTER TRIGGER ai.trg_agent_conversations_updated_at
ON ai.agent_conversations
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT UPDATE(updated_at)
    BEGIN
        UPDATE c
        SET updated_at = SYSUTCDATETIME()
        FROM ai.agent_conversations c
        INNER JOIN inserted i ON c.id = i.id;
    END
END;
GO
