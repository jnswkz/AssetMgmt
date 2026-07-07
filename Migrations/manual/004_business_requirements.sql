SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE AssetMgmt;
GO

IF COL_LENGTH('asset.allocation_requests', 'handover_due_at') IS NULL
BEGIN
    ALTER TABLE asset.allocation_requests ADD handover_due_at DATETIME2(3) NULL;
END;
GO

-- Dynamic SQL avoids same-batch name resolution when the column was just added.
EXEC sys.sp_executesql N'
    UPDATE asset.allocation_requests
    SET handover_due_at = DATEADD(HOUR, 24, created_at)
    WHERE handover_due_at IS NULL;
';
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('asset.allocation_requests')
      AND name = 'handover_due_at'
      AND is_nullable = 1
)
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE asset.allocation_requests
        ALTER COLUMN handover_due_at DATETIME2(3) NOT NULL;
    ';
END;
GO

IF COL_LENGTH('asset.allocations', 'expected_return_at') IS NULL
    ALTER TABLE asset.allocations ADD expected_return_at DATETIME2(3) NULL;
GO

IF OBJECT_ID('asset.return_obligations', 'U') IS NULL
BEGIN
    CREATE TABLE asset.return_obligations (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_return_obligations PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        user_id UNIQUEIDENTIFIER NOT NULL,
        asset_instance_id UNIQUEIDENTIFIER NOT NULL,
        reason NVARCHAR(50) NOT NULL,
        due_at DATETIME2(3) NOT NULL,
        resolved_at DATETIME2(3) NULL,
        resolved_by UNIQUEIDENTIFIER NULL,
        resolution_notes NVARCHAR(MAX) NULL,
        created_at DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_return_obligations_user FOREIGN KEY (user_id) REFERENCES asset.users(id),
        CONSTRAINT FK_return_obligations_asset FOREIGN KEY (asset_instance_id) REFERENCES asset.asset_instances(id),
        CONSTRAINT CK_return_obligations_reason CHECK (reason IN ('DepartmentChanged', 'UserDeactivated'))
    );
    CREATE UNIQUE INDEX UX_return_obligations_open ON asset.return_obligations(user_id, asset_instance_id) WHERE resolved_at IS NULL;
END;
GO

IF OBJECT_ID('asset.inventory_scans', 'U') IS NULL
BEGIN
    CREATE TABLE asset.inventory_scans (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_inventory_scans PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        department_id UNIQUEIDENTIFIER NULL,
        status NVARCHAR(20) NOT NULL DEFAULT 'Open',
        started_at DATETIME2(3) NOT NULL,
        closed_at DATETIME2(3) NULL,
        created_by UNIQUEIDENTIFIER NOT NULL,
        created_at DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_inventory_scans_department FOREIGN KEY (department_id) REFERENCES ref.departments(id),
        CONSTRAINT CK_inventory_scans_status CHECK (status IN ('Open', 'Closed'))
    );
    CREATE TABLE asset.inventory_scan_items (
        id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_inventory_scan_items PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        inventory_scan_id UNIQUEIDENTIFIER NOT NULL,
        asset_instance_id UNIQUEIDENTIFIER NULL,
        asset_code NVARCHAR(50) NOT NULL,
        result NVARCHAR(20) NOT NULL,
        scanned_at DATETIME2(3) NOT NULL,
        CONSTRAINT FK_inventory_scan_items_scan FOREIGN KEY (inventory_scan_id) REFERENCES asset.inventory_scans(id) ON DELETE CASCADE,
        CONSTRAINT FK_inventory_scan_items_asset FOREIGN KEY (asset_instance_id) REFERENCES asset.asset_instances(id),
        CONSTRAINT CK_inventory_scan_items_result CHECK (result IN ('Found', 'Missing', 'Unexpected')),
        CONSTRAINT UX_inventory_scan_items_code UNIQUE (inventory_scan_id, asset_code)
    );
END;
GO
