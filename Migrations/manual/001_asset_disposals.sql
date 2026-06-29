-- =============================================================
-- 001 — asset.asset_disposals
-- Run manually on the cloud DB (DB-first model). Tracks end-of-life
-- disposal of assets: Sold (to an employee), Scrapped, Donated, Lost.
-- =============================================================

USE AssetMgmt;
GO

CREATE TABLE asset.asset_disposals (
    id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_asset_disposals PRIMARY KEY CLUSTERED DEFAULT NEWSEQUENTIALID(),
    asset_instance_id UNIQUEIDENTIFIER NOT NULL,
    disposal_type     NVARCHAR(50)     NOT NULL,         -- Sold | Scrapped | Donated | Lost
    sold_to_user_id   UNIQUEIDENTIFIER NULL,
    sale_price        DECIMAL(18,2)    NULL,
    reason            NVARCHAR(MAX)    NULL,
    disposed_at       DATE             NOT NULL,
    created_at        DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    created_by        UNIQUEIDENTIFIER NOT NULL,

    CONSTRAINT CK_asset_disposals_type CHECK (disposal_type IN ('Sold','Scrapped','Donated','Lost')),
    CONSTRAINT CK_asset_disposals_sold CHECK (
        (disposal_type = 'Sold' AND sold_to_user_id IS NOT NULL AND sale_price IS NOT NULL AND sale_price >= 0)
        OR (disposal_type <> 'Sold')
    ),
    CONSTRAINT FK_asset_disposals_asset   FOREIGN KEY (asset_instance_id) REFERENCES asset.asset_instances(id),
    CONSTRAINT FK_asset_disposals_buyer   FOREIGN KEY (sold_to_user_id)   REFERENCES asset.users(id),
    CONSTRAINT FK_asset_disposals_creator FOREIGN KEY (created_by)         REFERENCES asset.users(id)
);
GO

CREATE NONCLUSTERED INDEX IX_asset_disposals_asset
    ON asset.asset_disposals (asset_instance_id);
GO
