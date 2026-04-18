using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsApp2.Migrations
{
    /// <inheritdoc />
    public partial class paymentMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.SalesInvoices', N'PaymentMethod') IS NULL
BEGIN
    ALTER TABLE [dbo].[SalesInvoices] ADD [PaymentMethod] NVARCHAR(20) NOT NULL CONSTRAINT [DF_SalesInvoices_PaymentMethod] DEFAULT (N'');
END

IF COL_LENGTH(N'dbo.SalesInvoices', N'PosShiftId') IS NULL
BEGIN
    ALTER TABLE [dbo].[SalesInvoices] ADD [PosShiftId] UNIQUEIDENTIFIER NULL;
END

IF COL_LENGTH(N'dbo.Items', N'DefaultSalePriceLyd') IS NULL
BEGIN
    ALTER TABLE [dbo].[Items] ADD [DefaultSalePriceLyd] DECIMAL(18,2) NULL;
END

IF COL_LENGTH(N'dbo.InventorySettings', N'MaxCashierDiscountPercent') IS NULL
BEGIN
    ALTER TABLE [dbo].[InventorySettings] ADD [MaxCashierDiscountPercent] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_InventorySettings_MaxCashierDiscountPercent] DEFAULT (0);
END

IF OBJECT_ID(N'dbo.PosShifts', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PosShifts](
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_PosShifts_Id] DEFAULT NEWID() CONSTRAINT [PK_PosShifts] PRIMARY KEY,
        [OpenedByUserId] NVARCHAR(450) NOT NULL,
        [OpenedByUserName] NVARCHAR(200) NULL,
        [OpeningCashLyd] DECIMAL(18,2) NOT NULL,
        [OpenedAtUtc] DATETIME2 NOT NULL,
        [ClosedAtUtc] DATETIME2 NULL,
        [ClosingCashLyd] DECIMAL(18,2) NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [Note] NVARCHAR(500) NULL,
        [Created] DATETIME2 NOT NULL,
        [Modified] DATETIME2 NULL
    );
END

IF NOT EXISTS (
    SELECT 1 FROM [dbo].[AspNetRoles]
    WHERE [Id] = N'6c99e53a-1163-4504-9b68-15e9b9ec48ea'
       OR [NormalizedName] = N'CASHIER')
BEGIN
    INSERT INTO [dbo].[AspNetRoles] ([Id], [ConcurrencyStamp], [Name], [NormalizedName])
    VALUES (N'6c99e53a-1163-4504-9b68-15e9b9ec48ea', N'5d0655d1-6b89-4557-b9a7-7f07666a5568', N'Cashier', N'CASHIER');
END

IF NOT EXISTS (
    SELECT 1 FROM [dbo].[Customers]
    WHERE [Id] = '7e2efb6c-0cb2-430f-92af-6e0ad720f105' OR [Name] = N'مبيعات يومية')
BEGIN
    INSERT INTO [dbo].[Customers] ([Id], [Created], [Modified], [Name], [Note], [Phone])
    VALUES ('7e2efb6c-0cb2-430f-92af-6e0ad720f105', '2026-01-01T00:00:00.0000000', NULL, N'مبيعات يومية', N'عميل افتراضي لمبيعات الكاش اليومية', NULL);
END

UPDATE [dbo].[InventorySettings]
SET [MaxCashierDiscountPercent] = 10.0
WHERE [Id] = 'd8ec402f-4f11-4f5e-97ed-c9dc2a58a232';

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_SalesInvoices_PosShiftId'
      AND [object_id] = OBJECT_ID(N'dbo.SalesInvoices'))
BEGIN
    CREATE INDEX [IX_SalesInvoices_PosShiftId] ON [dbo].[SalesInvoices]([PosShiftId]);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_PosShifts_OpenedByUserId_Status'
      AND [object_id] = OBJECT_ID(N'dbo.PosShifts'))
BEGIN
    CREATE INDEX [IX_PosShifts_OpenedByUserId_Status] ON [dbo].[PosShifts]([OpenedByUserId], [Status]);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = N'FK_SalesInvoices_PosShifts_PosShiftId')
BEGIN
    ALTER TABLE [dbo].[SalesInvoices]
    ADD CONSTRAINT [FK_SalesInvoices_PosShifts_PosShiftId]
        FOREIGN KEY ([PosShiftId]) REFERENCES [dbo].[PosShifts]([Id])
        ON DELETE SET NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = N'FK_SalesInvoices_PosShifts_PosShiftId')
BEGIN
    ALTER TABLE [dbo].[SalesInvoices] DROP CONSTRAINT [FK_SalesInvoices_PosShifts_PosShiftId];
END

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_SalesInvoices_PosShiftId'
      AND [object_id] = OBJECT_ID(N'dbo.SalesInvoices'))
BEGIN
    DROP INDEX [IX_SalesInvoices_PosShiftId] ON [dbo].[SalesInvoices];
END

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_PosShifts_OpenedByUserId_Status'
      AND [object_id] = OBJECT_ID(N'dbo.PosShifts'))
BEGIN
    DROP INDEX [IX_PosShifts_OpenedByUserId_Status] ON [dbo].[PosShifts];
END

IF OBJECT_ID(N'dbo.PosShifts', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[PosShifts];
END

DELETE FROM [dbo].[AspNetRoles]
WHERE [Id] = N'6c99e53a-1163-4504-9b68-15e9b9ec48ea';

DELETE FROM [dbo].[Customers]
WHERE [Id] = '7e2efb6c-0cb2-430f-92af-6e0ad720f105';

IF COL_LENGTH(N'dbo.SalesInvoices', N'PaymentMethod') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[SalesInvoices] DROP COLUMN [PaymentMethod];
END

IF COL_LENGTH(N'dbo.SalesInvoices', N'PosShiftId') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[SalesInvoices] DROP COLUMN [PosShiftId];
END

IF COL_LENGTH(N'dbo.Items', N'DefaultSalePriceLyd') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Items] DROP COLUMN [DefaultSalePriceLyd];
END

IF COL_LENGTH(N'dbo.InventorySettings', N'MaxCashierDiscountPercent') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[InventorySettings] DROP COLUMN [MaxCashierDiscountPercent];
END
");
        }
    }
}
