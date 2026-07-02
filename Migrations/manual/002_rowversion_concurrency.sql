IF COL_LENGTH('asset.asset_instances', 'row_version') IS NULL
BEGIN
    ALTER TABLE asset.asset_instances
        ADD row_version ROWVERSION NOT NULL;
END;
GO

IF COL_LENGTH('asset.allocation_requests', 'row_version') IS NULL
BEGIN
    ALTER TABLE asset.allocation_requests
        ADD row_version ROWVERSION NOT NULL;
END;
GO
