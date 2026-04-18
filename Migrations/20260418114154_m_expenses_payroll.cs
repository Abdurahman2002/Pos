using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsApp2.Migrations
{
    /// <inheritdoc />
    public partial class m_expenses_payroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.ExpenseEntries', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ExpenseEntries](
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_ExpenseEntries_Id] DEFAULT NEWID() CONSTRAINT [PK_ExpenseEntries] PRIMARY KEY,
        [ExpenseDate] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [ExpenseKind] nvarchar(20) NOT NULL,
        [Category] nvarchar(100) NOT NULL,
        [EmployeeId] uniqueidentifier NULL,
        [PaymentMethod] nvarchar(20) NOT NULL,
        [ReferenceNo] nvarchar(50) NULL,
        [Note] nvarchar(500) NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [CreatedByUserName] nvarchar(256) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL
    );
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_ExpenseEntries_ExpenseDate_ExpenseKind'
      AND [object_id] = OBJECT_ID(N'dbo.ExpenseEntries'))
BEGIN
    CREATE INDEX [IX_ExpenseEntries_ExpenseDate_ExpenseKind] ON [dbo].[ExpenseEntries]([ExpenseDate], [ExpenseKind]);
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_ExpenseEntries_EmployeeId_ExpenseDate'
      AND [object_id] = OBJECT_ID(N'dbo.ExpenseEntries'))
BEGIN
    CREATE INDEX [IX_ExpenseEntries_EmployeeId_ExpenseDate] ON [dbo].[ExpenseEntries]([EmployeeId], [ExpenseDate]);
END

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = N'FK_ExpenseEntries_Employees_EmployeeId')
BEGIN
    ALTER TABLE [dbo].[ExpenseEntries]
    ADD CONSTRAINT [FK_ExpenseEntries_Employees_EmployeeId]
        FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employees]([Id])
        ON DELETE NO ACTION;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.ExpenseEntries', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[ExpenseEntries];
END
");
        }
    }
}
