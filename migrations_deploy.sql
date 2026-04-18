IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929181130_m1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250929181130_m1', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929181440_m2'
)
BEGIN
    CREATE TABLE [Contact] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(max) NOT NULL,
        [Phone] nvarchar(max) NOT NULL,
        [Facebook] nvarchar(max) NOT NULL,
        [Twitter] nvarchar(max) NOT NULL,
        [Instagram] nvarchar(max) NOT NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_Contact] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929181440_m2'
)
BEGIN
    CREATE TABLE [Sections] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_Sections] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929181440_m2'
)
BEGIN
    CREATE TABLE [SiteInfo] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Activity] nvarchar(max) NOT NULL,
        [About] nvarchar(max) NOT NULL,
        [LogoUrl] nvarchar(max) NULL,
        [CoverImageUrl] nvarchar(max) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_SiteInfo] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929181440_m2'
)
BEGIN
    CREATE TABLE [SiteState] (
        [Id] uniqueidentifier NOT NULL,
        [State] bit NOT NULL,
        [ClosingMessage] nvarchar(max) NOT NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_SiteState] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929181440_m2'
)
BEGIN
    CREATE TABLE [News] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Details] nvarchar(max) NOT NULL,
        [ImageUrl] nvarchar(max) NULL,
        [State] bit NOT NULL,
        [SectionsId] uniqueidentifier NOT NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_News] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_News_Sections_SectionsId] FOREIGN KEY ([SectionsId]) REFERENCES [Sections] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929181440_m2'
)
BEGIN
    CREATE INDEX [IX_News_SectionsId] ON [News] ([SectionsId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250929181440_m2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250929181440_m2', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930180236_m3'
)
BEGIN
    ALTER TABLE [News] DROP CONSTRAINT [FK_News_Sections_SectionsId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930180236_m3'
)
BEGIN
    EXEC sp_rename N'[News].[SectionsId]', N'SectionId', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930180236_m3'
)
BEGIN
    EXEC sp_rename N'[News].[IX_News_SectionsId]', N'IX_News_SectionId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930180236_m3'
)
BEGIN
    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SiteState]') AND [c].[name] = N'Id');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [SiteState] DROP CONSTRAINT [' + @var + '];');
    ALTER TABLE [SiteState] ADD DEFAULT (NEWID()) FOR [Id];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930180236_m3'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SiteInfo]') AND [c].[name] = N'Id');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [SiteInfo] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [SiteInfo] ADD DEFAULT (NEWID()) FOR [Id];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930180236_m3'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sections]') AND [c].[name] = N'Id');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Sections] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Sections] ADD DEFAULT (NEWID()) FOR [Id];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930180236_m3'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[News]') AND [c].[name] = N'Id');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [News] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [News] ADD DEFAULT (NEWID()) FOR [Id];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930180236_m3'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Contact]') AND [c].[name] = N'Id');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Contact] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [Contact] ADD DEFAULT (NEWID()) FOR [Id];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930180236_m3'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Created', N'Email', N'Facebook', N'Instagram', N'Modified', N'Phone', N'Twitter') AND [object_id] = OBJECT_ID(N'[Contact]'))
        SET IDENTITY_INSERT [Contact] ON;
    EXEC(N'INSERT INTO [Contact] ([Id], [Created], [Email], [Facebook], [Instagram], [Modified], [Phone], [Twitter])
    VALUES (''b27d3d62-8d55-4c2e-8b9c-34f6a6f2f1e2'', ''2025-09-30T00:00:00.0000000'', N''W.Wide@Gmail.com'', N''Worldwide Facebook'', N''Worldwide Instagram'', NULL, N''00218951234567'', N''Worldwide Twitter'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Created', N'Email', N'Facebook', N'Instagram', N'Modified', N'Phone', N'Twitter') AND [object_id] = OBJECT_ID(N'[Contact]'))
        SET IDENTITY_INSERT [Contact] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930180236_m3'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'About', N'Activity', N'CoverImageUrl', N'Created', N'LogoUrl', N'Modified', N'Name') AND [object_id] = OBJECT_ID(N'[SiteInfo]'))
        SET IDENTITY_INSERT [SiteInfo] ON;
    EXEC(N'INSERT INTO [SiteInfo] ([Id], [About], [Activity], [CoverImageUrl], [Created], [LogoUrl], [Modified], [Name])
    VALUES (''3f2504e0-4f89-11d3-9a0c-0305e82c3301'', N''We are a specialized news website covering political, sports, and economic events, along with various other sections of general interest to readers. We always strive to provide distinguished and reliable content that reflects the ongoing developments on both the local and global stages.'', N''News site'', NULL, ''2025-09-30T00:00:00.0000000'', NULL, NULL, N''Worldwide'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'About', N'Activity', N'CoverImageUrl', N'Created', N'LogoUrl', N'Modified', N'Name') AND [object_id] = OBJECT_ID(N'[SiteInfo]'))
        SET IDENTITY_INSERT [SiteInfo] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930180236_m3'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClosingMessage', N'Created', N'Modified', N'State') AND [object_id] = OBJECT_ID(N'[SiteState]'))
        SET IDENTITY_INSERT [SiteState] ON;
    EXEC(N'INSERT INTO [SiteState] ([Id], [ClosingMessage], [Created], [Modified], [State])
    VALUES (''6fa459ea-ee8a-3ca4-894e-db77e160355e'', N''The site is temporarily closed for development'', ''2025-09-30T00:00:00.0000000'', NULL, CAST(1 AS bit))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ClosingMessage', N'Created', N'Modified', N'State') AND [object_id] = OBJECT_ID(N'[SiteState]'))
        SET IDENTITY_INSERT [SiteState] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930180236_m3'
)
BEGIN
    ALTER TABLE [News] ADD CONSTRAINT [FK_News_Sections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [Sections] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930180236_m3'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250930180236_m3', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930182342_m5'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SiteInfo]') AND [c].[name] = N'Name');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [SiteInfo] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [SiteInfo] ALTER COLUMN [Name] nvarchar(100) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930182342_m5'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SiteInfo]') AND [c].[name] = N'Activity');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [SiteInfo] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [SiteInfo] ALTER COLUMN [Activity] nvarchar(500) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930182342_m5'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SiteInfo]') AND [c].[name] = N'About');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [SiteInfo] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [SiteInfo] ALTER COLUMN [About] nvarchar(2000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250930182342_m5'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250930182342_m5', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SiteInfo]') AND [c].[name] = N'Name');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [SiteInfo] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [SiteInfo] ALTER COLUMN [Name] nvarchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SiteInfo]') AND [c].[name] = N'Activity');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [SiteInfo] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [SiteInfo] ALTER COLUMN [Activity] nvarchar(200) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sections]') AND [c].[name] = N'Name');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Sections] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [Sections] ALTER COLUMN [Name] nvarchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[News]') AND [c].[name] = N'Title');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [News] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [News] ALTER COLUMN [Title] nvarchar(1000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [Discriminator] nvarchar(21) NOT NULL,
        [Age] int NULL,
        [LastAccessTime] datetime2 NULL,
        [CreatedDate] datetime2 NULL,
        [ModifiedDate] datetime2 NULL,
        [Approval] bit NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    CREATE TABLE [Employees] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Name] nvarchar(100) NOT NULL,
        [WorkPhone] nvarchar(20) NULL,
        [Address] nvarchar(200) NOT NULL,
        [YearsOfExperience] int NOT NULL,
        [Specialization] nvarchar(100) NOT NULL,
        [Bio] nvarchar(500) NOT NULL,
        [UserId] nvarchar(450) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Employees_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    CREATE TABLE [UserProfiles] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [DisplayName] nvarchar(max) NOT NULL,
        [ImageUrl] nvarchar(max) NULL,
        [Gender] bit NOT NULL,
        [UserId] nvarchar(450) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_UserProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserProfiles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Employees_UserId] ON [Employees] ([UserId]) WHERE [UserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UserProfiles_UserId] ON [UserProfiles] ([UserId]) WHERE [UserId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251119181323_id1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251119181323_id1', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251120171803_id2_seed'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
        SET IDENTITY_INSERT [AspNetRoles] ON;
    EXEC(N'INSERT INTO [AspNetRoles] ([Id], [ConcurrencyStamp], [Name], [NormalizedName])
    VALUES (N''2cdc7bbd-449b-4fa5-87c7-4d8cf0683bea'', N''a1404a92-7520-4989-8c46-5922377cb2d0'', N''Prog'', N''PROG'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
        SET IDENTITY_INSERT [AspNetRoles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251120171803_id2_seed'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccessFailedCount', N'Age', N'Approval', N'ConcurrencyStamp', N'CreatedDate', N'Discriminator', N'Email', N'EmailConfirmed', N'LastAccessTime', N'LockoutEnabled', N'LockoutEnd', N'ModifiedDate', N'NormalizedEmail', N'NormalizedUserName', N'PasswordHash', N'PhoneNumber', N'PhoneNumberConfirmed', N'SecurityStamp', N'TwoFactorEnabled', N'UserName') AND [object_id] = OBJECT_ID(N'[AspNetUsers]'))
        SET IDENTITY_INSERT [AspNetUsers] ON;
    EXEC(N'INSERT INTO [AspNetUsers] ([Id], [AccessFailedCount], [Age], [Approval], [ConcurrencyStamp], [CreatedDate], [Discriminator], [Email], [EmailConfirmed], [LastAccessTime], [LockoutEnabled], [LockoutEnd], [ModifiedDate], [NormalizedEmail], [NormalizedUserName], [PasswordHash], [PhoneNumber], [PhoneNumberConfirmed], [SecurityStamp], [TwoFactorEnabled], [UserName])
    VALUES (N''a1b495e6-dcb6-4763-9994-a4d74b93105c'', 0, 0, NULL, N''f67b0714-171b-4b5f-a459-817a40f3041d'', ''2025-01-01T00:00:00.0000000'', N''ApplicationUser'', N''Programmer@Gmail.com'', CAST(1 AS bit), NULL, CAST(1 AS bit), NULL, NULL, N''PROGRAMMER@GMAIL.COM'', N''PROGRAMMER@GMAIL.COM'', N''AQEQJwAAEAAAAAAAAAAAAAAAAAAAAAAAAACmiAWO3zXD/7BDkHUlLJb0ykidmrXY4DLxq+WrUGiUPQ=='', NULL, CAST(1 AS bit), N''1d4afc4a-ef8a-4e5e-a09a-7ee026455e41'', CAST(0 AS bit), N''Programmer@Gmail.com'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccessFailedCount', N'Age', N'Approval', N'ConcurrencyStamp', N'CreatedDate', N'Discriminator', N'Email', N'EmailConfirmed', N'LastAccessTime', N'LockoutEnabled', N'LockoutEnd', N'ModifiedDate', N'NormalizedEmail', N'NormalizedUserName', N'PasswordHash', N'PhoneNumber', N'PhoneNumberConfirmed', N'SecurityStamp', N'TwoFactorEnabled', N'UserName') AND [object_id] = OBJECT_ID(N'[AspNetUsers]'))
        SET IDENTITY_INSERT [AspNetUsers] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251120171803_id2_seed'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'UserId') AND [object_id] = OBJECT_ID(N'[AspNetUserRoles]'))
        SET IDENTITY_INSERT [AspNetUserRoles] ON;
    EXEC(N'INSERT INTO [AspNetUserRoles] ([RoleId], [UserId])
    VALUES (N''2cdc7bbd-449b-4fa5-87c7-4d8cf0683bea'', N''a1b495e6-dcb6-4763-9994-a4d74b93105c'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'UserId') AND [object_id] = OBJECT_ID(N'[AspNetUserRoles]'))
        SET IDENTITY_INSERT [AspNetUserRoles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251120171803_id2_seed'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251120171803_id2_seed', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE TABLE [Items] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Uom] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_Items] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE TABLE [Warehouses] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Name] nvarchar(100) NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_Warehouses] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE TABLE [Issues] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Number] nvarchar(50) NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [IssueDate] datetime2 NOT NULL,
        [Note] nvarchar(500) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_Issues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Issues_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE TABLE [Receipts] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Number] nvarchar(50) NULL,
        [WarehouseId] uniqueidentifier NOT NULL,
        [ReceiptDate] datetime2 NOT NULL,
        [Note] nvarchar(500) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_Receipts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Receipts_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE TABLE [StockBalances] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [WarehouseId] uniqueidentifier NOT NULL,
        [ItemId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,2) NOT NULL,
        [AllowNegative] bit NOT NULL,
        [RowVersion] rowversion NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_StockBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockBalances_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_StockBalances_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE TABLE [StockLedgers] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [WarehouseId] uniqueidentifier NOT NULL,
        [ItemId] uniqueidentifier NOT NULL,
        [ReferenceType] nvarchar(30) NOT NULL,
        [ReferenceId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,2) NOT NULL,
        [Note] nvarchar(500) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_StockLedgers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockLedgers_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_StockLedgers_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE TABLE [Transfers] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Number] nvarchar(50) NULL,
        [FromWarehouseId] uniqueidentifier NOT NULL,
        [ToWarehouseId] uniqueidentifier NOT NULL,
        [TransferDate] datetime2 NOT NULL,
        [Note] nvarchar(500) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_Transfers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Transfers_Warehouses_FromWarehouseId] FOREIGN KEY ([FromWarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Transfers_Warehouses_ToWarehouseId] FOREIGN KEY ([ToWarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE TABLE [IssueLines] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [IssueId] uniqueidentifier NOT NULL,
        [ItemId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,2) NOT NULL,
        [Note] nvarchar(500) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_IssueLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IssueLines_Issues_IssueId] FOREIGN KEY ([IssueId]) REFERENCES [Issues] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_IssueLines_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE TABLE [ReceiptLines] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [ReceiptId] uniqueidentifier NOT NULL,
        [ItemId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,2) NOT NULL,
        [Note] nvarchar(500) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_ReceiptLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReceiptLines_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ReceiptLines_Receipts_ReceiptId] FOREIGN KEY ([ReceiptId]) REFERENCES [Receipts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE TABLE [TransferLines] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [TransferId] uniqueidentifier NOT NULL,
        [ItemId] uniqueidentifier NOT NULL,
        [Quantity] decimal(18,2) NOT NULL,
        [Note] nvarchar(500) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_TransferLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TransferLines_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TransferLines_Transfers_TransferId] FOREIGN KEY ([TransferId]) REFERENCES [Transfers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
        SET IDENTITY_INSERT [AspNetRoles] ON;
    EXEC(N'INSERT INTO [AspNetRoles] ([Id], [ConcurrencyStamp], [Name], [NormalizedName])
    VALUES (N''24a53d0e-b1f6-4c57-94d7-0d5b7f2b6122'', N''5a6a1d5d-0b70-4a83-85fb-4ddf7b2b9c64'', N''InventoryAdmin'', N''INVENTORYADMIN''),
    (N''d2f8be95-9b13-4e8c-bc95-6f57a08b56f1'', N''9e07e5dd-fd8b-4a1a-8e45-8c7b2d3dfe7c'', N''InventoryClerk'', N''INVENTORYCLERK''),
    (N''e8e8f5c6-6f0f-4db1-8f65-0a3f6e798f2b'', N''ba0eb6a3-6f87-4a1e-9a4e-8bf084e40b85'', N''InventoryViewer'', N''INVENTORYVIEWER'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
        SET IDENTITY_INSERT [AspNetRoles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    EXEC(N'UPDATE [AspNetUsers] SET [PasswordHash] = N''AQAAAAIAAYagAAAAENggG9+6Z01XNeB9YmF/XmQybN3d/MpCrkCkJ58k03l2udJQx0IaIujsHHxHRpulzQ==''
    WHERE [Id] = N''a1b495e6-dcb6-4763-9994-a4d74b93105c'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE INDEX [IX_IssueLines_IssueId] ON [IssueLines] ([IssueId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE INDEX [IX_IssueLines_ItemId] ON [IssueLines] ([ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE INDEX [IX_Issues_WarehouseId] ON [Issues] ([WarehouseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Items_Code] ON [Items] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE INDEX [IX_ReceiptLines_ItemId] ON [ReceiptLines] ([ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE INDEX [IX_ReceiptLines_ReceiptId] ON [ReceiptLines] ([ReceiptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE INDEX [IX_Receipts_WarehouseId] ON [Receipts] ([WarehouseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE INDEX [IX_StockBalances_ItemId] ON [StockBalances] ([ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockBalances_WarehouseId_ItemId] ON [StockBalances] ([WarehouseId], [ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE INDEX [IX_StockLedgers_ItemId] ON [StockLedgers] ([ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE INDEX [IX_StockLedgers_WarehouseId] ON [StockLedgers] ([WarehouseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE INDEX [IX_TransferLines_ItemId] ON [TransferLines] ([ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE INDEX [IX_TransferLines_TransferId] ON [TransferLines] ([TransferId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE INDEX [IX_Transfers_FromWarehouseId] ON [Transfers] ([FromWarehouseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE INDEX [IX_Transfers_ToWarehouseId] ON [Transfers] ([ToWarehouseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Warehouses_Code] ON [Warehouses] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260117123523_m11'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260117123523_m11', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119173333_m12'
)
BEGIN
    ALTER TABLE [Items] ADD [CategoryId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119173333_m12'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [IsActive] bit NOT NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119173333_m12'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'Created', N'IsActive', N'Modified', N'Name') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] ON;
    EXEC(N'INSERT INTO [Categories] ([Id], [Code], [Created], [IsActive], [Modified], [Name])
    VALUES (''1a8af6e8-6016-4d81-9d3e-3b7e0e73f8b1'', N''CAT-RAW'', ''2026-01-01T00:00:00.0000000'', CAST(1 AS bit), NULL, N''Raw Materials''),
    (''2a9fbe41-1b63-46dc-8f29-975f2bb87c5d'', N''CAT-FIN'', ''2026-01-01T00:00:00.0000000'', CAST(1 AS bit), NULL, N''Finished Goods'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'Created', N'IsActive', N'Modified', N'Name') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119173333_m12'
)
BEGIN
    CREATE INDEX [IX_Items_CategoryId] ON [Items] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119173333_m12'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Code] ON [Categories] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119173333_m12'
)
BEGIN
    ALTER TABLE [Items] ADD CONSTRAINT [FK_Items_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260119173333_m12'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260119173333_m12', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260120110013_m13'
)
BEGIN
    ALTER TABLE [Items] ADD [ReorderLevel] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260120110013_m13'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260120110013_m13', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123161924_m14'
)
BEGIN
    ALTER TABLE [Issues] ADD [PatientName] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123161924_m14'
)
BEGIN
    ALTER TABLE [Employees] ADD [WarehouseId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123161924_m14'
)
BEGIN
    CREATE INDEX [IX_Employees_WarehouseId] ON [Employees] ([WarehouseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123161924_m14'
)
BEGIN
    ALTER TABLE [Employees] ADD CONSTRAINT [FK_Employees_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260123161924_m14'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260123161924_m14', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260125204041_m15'
)
BEGIN
    DROP TABLE [UserProfiles];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260125204041_m15'
)
BEGIN
    EXEC(N'DELETE FROM [Categories]
    WHERE [Id] = ''1a8af6e8-6016-4d81-9d3e-3b7e0e73f8b1'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260125204041_m15'
)
BEGIN
    EXEC(N'DELETE FROM [Categories]
    WHERE [Id] = ''2a9fbe41-1b63-46dc-8f29-975f2bb87c5d'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260125204041_m15'
)
BEGIN
    DECLARE @var12 sysname;
    SELECT @var12 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Employees]') AND [c].[name] = N'Address');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [Employees] DROP CONSTRAINT [' + @var12 + '];');
    ALTER TABLE [Employees] DROP COLUMN [Address];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260125204041_m15'
)
BEGIN
    DECLARE @var13 sysname;
    SELECT @var13 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Employees]') AND [c].[name] = N'Bio');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [Employees] DROP CONSTRAINT [' + @var13 + '];');
    ALTER TABLE [Employees] DROP COLUMN [Bio];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260125204041_m15'
)
BEGIN
    DECLARE @var14 sysname;
    SELECT @var14 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Employees]') AND [c].[name] = N'Specialization');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [Employees] DROP CONSTRAINT [' + @var14 + '];');
    ALTER TABLE [Employees] DROP COLUMN [Specialization];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260125204041_m15'
)
BEGIN
    DECLARE @var15 sysname;
    SELECT @var15 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Employees]') AND [c].[name] = N'WorkPhone');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [Employees] DROP CONSTRAINT [' + @var15 + '];');
    ALTER TABLE [Employees] DROP COLUMN [WorkPhone];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260125204041_m15'
)
BEGIN
    DECLARE @var16 sysname;
    SELECT @var16 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Employees]') AND [c].[name] = N'YearsOfExperience');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [Employees] DROP CONSTRAINT [' + @var16 + '];');
    ALTER TABLE [Employees] DROP COLUMN [YearsOfExperience];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260125204041_m15'
)
BEGIN
    DECLARE @var17 sysname;
    SELECT @var17 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'Age');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var17 + '];');
    ALTER TABLE [AspNetUsers] DROP COLUMN [Age];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260125204041_m15'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260125204041_m15', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127172246_m16'
)
BEGIN
    ALTER TABLE [Transfers] ADD [CreatedByUserId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127172246_m16'
)
BEGIN
    ALTER TABLE [Transfers] ADD [CreatedByUserName] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127172246_m16'
)
BEGIN
    ALTER TABLE [Receipts] ADD [CreatedByUserId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127172246_m16'
)
BEGIN
    ALTER TABLE [Receipts] ADD [CreatedByUserName] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127172246_m16'
)
BEGIN
    ALTER TABLE [Issues] ADD [CreatedByUserId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127172246_m16'
)
BEGIN
    ALTER TABLE [Issues] ADD [CreatedByUserName] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127172246_m16'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260127172246_m16', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127175953_m17'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] uniqueidentifier NULL,
        [EntityNumber] nvarchar(50) NULL,
        [Description] nvarchar(500) NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [CreatedByUserName] nvarchar(256) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127175953_m17'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260127175953_m17', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127221837_18'
)
BEGIN
    ALTER TABLE [Transfers] ADD [IsReversal] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127221837_18'
)
BEGIN
    ALTER TABLE [Transfers] ADD [ReversalOfId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127221837_18'
)
BEGIN
    ALTER TABLE [Receipts] ADD [IsReversal] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127221837_18'
)
BEGIN
    ALTER TABLE [Receipts] ADD [ReversalOfId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127221837_18'
)
BEGIN
    ALTER TABLE [Issues] ADD [IsReversal] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127221837_18'
)
BEGIN
    ALTER TABLE [Issues] ADD [ReversalOfId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127221837_18'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260127221837_18', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260128132254_19'
)
BEGIN
    ALTER TABLE [IssueLines] DROP CONSTRAINT [FK_IssueLines_Items_ItemId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260128132254_19'
)
BEGIN
    ALTER TABLE [ReceiptLines] DROP CONSTRAINT [FK_ReceiptLines_Items_ItemId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260128132254_19'
)
BEGIN
    ALTER TABLE [StockBalances] DROP CONSTRAINT [FK_StockBalances_Items_ItemId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260128132254_19'
)
BEGIN
    ALTER TABLE [StockLedgers] DROP CONSTRAINT [FK_StockLedgers_Items_ItemId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260128132254_19'
)
BEGIN
    ALTER TABLE [TransferLines] DROP CONSTRAINT [FK_TransferLines_Items_ItemId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260128132254_19'
)
BEGIN
    ALTER TABLE [IssueLines] ADD CONSTRAINT [FK_IssueLines_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260128132254_19'
)
BEGIN
    ALTER TABLE [ReceiptLines] ADD CONSTRAINT [FK_ReceiptLines_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260128132254_19'
)
BEGIN
    ALTER TABLE [StockBalances] ADD CONSTRAINT [FK_StockBalances_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260128132254_19'
)
BEGIN
    ALTER TABLE [StockLedgers] ADD CONSTRAINT [FK_StockLedgers_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260128132254_19'
)
BEGIN
    ALTER TABLE [TransferLines] ADD CONSTRAINT [FK_TransferLines_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260128132254_19'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260128132254_19', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    DROP INDEX [IX_Warehouses_Code] ON [Warehouses];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    DROP INDEX [IX_Items_Code] ON [Items];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    DROP INDEX [IX_Categories_Code] ON [Categories];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    DECLARE @var18 sysname;
    SELECT @var18 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Warehouses]') AND [c].[name] = N'Code');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [Warehouses] DROP CONSTRAINT [' + @var18 + '];');
    ALTER TABLE [Warehouses] DROP COLUMN [Code];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    DECLARE @var19 sysname;
    SELECT @var19 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Warehouses]') AND [c].[name] = N'IsActive');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [Warehouses] DROP CONSTRAINT [' + @var19 + '];');
    ALTER TABLE [Warehouses] DROP COLUMN [IsActive];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    DECLARE @var20 sysname;
    SELECT @var20 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Items]') AND [c].[name] = N'Code');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [Items] DROP CONSTRAINT [' + @var20 + '];');
    ALTER TABLE [Items] DROP COLUMN [Code];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    DECLARE @var21 sysname;
    SELECT @var21 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Items]') AND [c].[name] = N'Uom');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [Items] DROP CONSTRAINT [' + @var21 + '];');
    ALTER TABLE [Items] DROP COLUMN [Uom];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    DECLARE @var22 sysname;
    SELECT @var22 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'Code');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var22 + '];');
    ALTER TABLE [Categories] DROP COLUMN [Code];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    EXEC sp_rename N'[Items].[IsActive]', N'IsDeleted', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    EXEC sp_rename N'[Categories].[IsActive]', N'IsDeleted', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    ALTER TABLE [TransferLines] ADD [ExpiryDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    ALTER TABLE [ReceiptLines] ADD [ExpiryDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    ALTER TABLE [Items] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    ALTER TABLE [IssueLines] ADD [ExpiryDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    ALTER TABLE [Categories] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    CREATE TABLE [StockBatches] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [WarehouseId] uniqueidentifier NOT NULL,
        [ItemId] uniqueidentifier NOT NULL,
        [ExpiryDate] datetime2 NOT NULL,
        [Quantity] decimal(18,2) NOT NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_StockBatches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StockBatches_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_StockBatches_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Warehouses_Name] ON [Warehouses] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Items_Name] ON [Items] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Name] ON [Categories] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    CREATE INDEX [IX_StockBatches_ItemId] ON [StockBatches] ([ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockBatches_WarehouseId_ItemId_ExpiryDate] ON [StockBatches] ([WarehouseId], [ItemId], [ExpiryDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203154406_Exp'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260203154406_Exp', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    DROP INDEX [IX_StockBatches_WarehouseId_ItemId_ExpiryDate] ON [StockBatches];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    DECLARE @var23 sysname;
    SELECT @var23 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'Discriminator');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var23 + '];');
    ALTER TABLE [AspNetUsers] DROP COLUMN [Discriminator];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    EXEC sp_rename N'[StockBatches].[Quantity]', N'QtyOnHand', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    DECLARE @var24 sysname;
    SELECT @var24 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TransferLines]') AND [c].[name] = N'ExpiryDate');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [TransferLines] DROP CONSTRAINT [' + @var24 + '];');
    EXEC(N'UPDATE [TransferLines] SET [ExpiryDate] = ''0001-01-01T00:00:00.0000000'' WHERE [ExpiryDate] IS NULL');
    ALTER TABLE [TransferLines] ALTER COLUMN [ExpiryDate] datetime2 NOT NULL;
    ALTER TABLE [TransferLines] ADD DEFAULT '0001-01-01T00:00:00.0000000' FOR [ExpiryDate];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    ALTER TABLE [TransferLines] ADD [BatchId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    ALTER TABLE [TransferLines] ADD [BatchNo] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    ALTER TABLE [StockLedgers] ADD [BatchId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    ALTER TABLE [StockLedgers] ADD [BatchNo] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    ALTER TABLE [StockLedgers] ADD [ExpiryDate] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    ALTER TABLE [StockBatches] ADD [BatchNo] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    ALTER TABLE [StockBatches] ADD [RowVersion] rowversion NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    DECLARE @var25 sysname;
    SELECT @var25 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ReceiptLines]') AND [c].[name] = N'ExpiryDate');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [ReceiptLines] DROP CONSTRAINT [' + @var25 + '];');
    EXEC(N'UPDATE [ReceiptLines] SET [ExpiryDate] = ''0001-01-01T00:00:00.0000000'' WHERE [ExpiryDate] IS NULL');
    ALTER TABLE [ReceiptLines] ALTER COLUMN [ExpiryDate] datetime2 NOT NULL;
    ALTER TABLE [ReceiptLines] ADD DEFAULT '0001-01-01T00:00:00.0000000' FOR [ExpiryDate];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    ALTER TABLE [ReceiptLines] ADD [BatchId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    ALTER TABLE [ReceiptLines] ADD [BatchNo] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    DECLARE @var26 sysname;
    SELECT @var26 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[IssueLines]') AND [c].[name] = N'ExpiryDate');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [IssueLines] DROP CONSTRAINT [' + @var26 + '];');
    EXEC(N'UPDATE [IssueLines] SET [ExpiryDate] = ''0001-01-01T00:00:00.0000000'' WHERE [ExpiryDate] IS NULL');
    ALTER TABLE [IssueLines] ALTER COLUMN [ExpiryDate] datetime2 NOT NULL;
    ALTER TABLE [IssueLines] ADD DEFAULT '0001-01-01T00:00:00.0000000' FOR [ExpiryDate];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    ALTER TABLE [IssueLines] ADD [BatchId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    ALTER TABLE [IssueLines] ADD [BatchNo] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    DECLARE @var27 sysname;
    SELECT @var27 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AspNetUsers]') AND [c].[name] = N'CreatedDate');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [AspNetUsers] DROP CONSTRAINT [' + @var27 + '];');
    EXEC(N'UPDATE [AspNetUsers] SET [CreatedDate] = ''0001-01-01T00:00:00.0000000'' WHERE [CreatedDate] IS NULL');
    ALTER TABLE [AspNetUsers] ALTER COLUMN [CreatedDate] datetime2 NOT NULL;
    ALTER TABLE [AspNetUsers] ADD DEFAULT '0001-01-01T00:00:00.0000000' FOR [CreatedDate];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN

    UPDATE [StockBatches] SET [BatchNo] = 'LEGACY' WHERE [BatchNo] IS NULL OR [BatchNo] = '';
    UPDATE [ReceiptLines] SET [BatchNo] = 'LEGACY' WHERE [BatchNo] IS NULL OR [BatchNo] = '';
    UPDATE [IssueLines] SET [BatchNo] = 'LEGACY' WHERE [BatchNo] IS NULL OR [BatchNo] = '';
    UPDATE [TransferLines] SET [BatchNo] = 'LEGACY' WHERE [BatchNo] IS NULL OR [BatchNo] = '';
    UPDATE [StockLedgers] SET [BatchNo] = 'LEGACY' WHERE [BatchNo] IS NULL OR [BatchNo] = '';

    INSERT INTO [StockBatches] ([Id],[WarehouseId],[ItemId],[BatchNo],[ExpiryDate],[QtyOnHand],[Created],[Modified])
    SELECT DISTINCT NEWID(), r.[WarehouseId], rl.[ItemId], rl.[BatchNo], rl.[ExpiryDate], 0, GETUTCDATE(), NULL
    FROM [ReceiptLines] rl
    JOIN [Receipts] r ON r.[Id] = rl.[ReceiptId]
    LEFT JOIN [StockBatches] b ON b.[WarehouseId]=r.[WarehouseId] AND b.[ItemId]=rl.[ItemId] AND b.[BatchNo]=rl.[BatchNo] AND b.[ExpiryDate]=rl.[ExpiryDate]
    WHERE b.[Id] IS NULL;

    INSERT INTO [StockBatches] ([Id],[WarehouseId],[ItemId],[BatchNo],[ExpiryDate],[QtyOnHand],[Created],[Modified])
    SELECT DISTINCT NEWID(), i.[WarehouseId], il.[ItemId], il.[BatchNo], il.[ExpiryDate], 0, GETUTCDATE(), NULL
    FROM [IssueLines] il
    JOIN [Issues] i ON i.[Id] = il.[IssueId]
    LEFT JOIN [StockBatches] b ON b.[WarehouseId]=i.[WarehouseId] AND b.[ItemId]=il.[ItemId] AND b.[BatchNo]=il.[BatchNo] AND b.[ExpiryDate]=il.[ExpiryDate]
    WHERE b.[Id] IS NULL;

    INSERT INTO [StockBatches] ([Id],[WarehouseId],[ItemId],[BatchNo],[ExpiryDate],[QtyOnHand],[Created],[Modified])
    SELECT DISTINCT NEWID(), t.[FromWarehouseId], tl.[ItemId], tl.[BatchNo], tl.[ExpiryDate], 0, GETUTCDATE(), NULL
    FROM [TransferLines] tl
    JOIN [Transfers] t ON t.[Id] = tl.[TransferId]
    LEFT JOIN [StockBatches] b ON b.[WarehouseId]=t.[FromWarehouseId] AND b.[ItemId]=tl.[ItemId] AND b.[BatchNo]=tl.[BatchNo] AND b.[ExpiryDate]=tl.[ExpiryDate]
    WHERE b.[Id] IS NULL;

    INSERT INTO [StockBatches] ([Id],[WarehouseId],[ItemId],[BatchNo],[ExpiryDate],[QtyOnHand],[Created],[Modified])
    SELECT DISTINCT NEWID(), l.[WarehouseId], l.[ItemId], l.[BatchNo], l.[ExpiryDate], 0, GETUTCDATE(), NULL
    FROM [StockLedgers] l
    LEFT JOIN [StockBatches] b ON b.[WarehouseId]=l.[WarehouseId] AND b.[ItemId]=l.[ItemId] AND b.[BatchNo]=l.[BatchNo] AND b.[ExpiryDate]=l.[ExpiryDate]
    WHERE b.[Id] IS NULL;

    UPDATE rl
    SET rl.[BatchId] = b.[Id]
    FROM [ReceiptLines] rl
    JOIN [Receipts] r ON r.[Id] = rl.[ReceiptId]
    JOIN [StockBatches] b ON b.[WarehouseId]=r.[WarehouseId] AND b.[ItemId]=rl.[ItemId] AND b.[BatchNo]=rl.[BatchNo] AND b.[ExpiryDate]=rl.[ExpiryDate];

    UPDATE il
    SET il.[BatchId] = b.[Id]
    FROM [IssueLines] il
    JOIN [Issues] i ON i.[Id] = il.[IssueId]
    JOIN [StockBatches] b ON b.[WarehouseId]=i.[WarehouseId] AND b.[ItemId]=il.[ItemId] AND b.[BatchNo]=il.[BatchNo] AND b.[ExpiryDate]=il.[ExpiryDate];

    UPDATE tl
    SET tl.[BatchId] = b.[Id]
    FROM [TransferLines] tl
    JOIN [Transfers] t ON t.[Id] = tl.[TransferId]
    JOIN [StockBatches] b ON b.[WarehouseId]=t.[FromWarehouseId] AND b.[ItemId]=tl.[ItemId] AND b.[BatchNo]=tl.[BatchNo] AND b.[ExpiryDate]=tl.[ExpiryDate];

    UPDATE l
    SET l.[BatchId] = b.[Id]
    FROM [StockLedgers] l
    JOIN [StockBatches] b ON b.[WarehouseId]=l.[WarehouseId] AND b.[ItemId]=l.[ItemId] AND b.[BatchNo]=l.[BatchNo] AND b.[ExpiryDate]=l.[ExpiryDate];

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    CREATE INDEX [IX_TransferLines_BatchId] ON [TransferLines] ([BatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    CREATE INDEX [IX_StockLedgers_BatchId] ON [StockLedgers] ([BatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StockBatches_WarehouseId_ItemId_BatchNo_ExpiryDate] ON [StockBatches] ([WarehouseId], [ItemId], [BatchNo], [ExpiryDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    CREATE INDEX [IX_StockBatches_WarehouseId_ItemId_ExpiryDate] ON [StockBatches] ([WarehouseId], [ItemId], [ExpiryDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    EXEC(N'ALTER TABLE [StockBatches] ADD CONSTRAINT [CK_StockBatch_QtyOnHand_NonNegative] CHECK ([QtyOnHand] >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    CREATE INDEX [IX_ReceiptLines_BatchId] ON [ReceiptLines] ([BatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    CREATE INDEX [IX_IssueLines_BatchId] ON [IssueLines] ([BatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    ALTER TABLE [IssueLines] ADD CONSTRAINT [FK_IssueLines_StockBatches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [StockBatches] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    ALTER TABLE [ReceiptLines] ADD CONSTRAINT [FK_ReceiptLines_StockBatches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [StockBatches] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    ALTER TABLE [StockLedgers] ADD CONSTRAINT [FK_StockLedgers_StockBatches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [StockBatches] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    ALTER TABLE [TransferLines] ADD CONSTRAINT [FK_TransferLines_StockBatches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [StockBatches] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210172700_exp1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260210172700_exp1', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210205251_exp2'
)
BEGIN
    DECLARE @var28 sysname;
    SELECT @var28 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Transfers]') AND [c].[name] = N'TransferDate');
    IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [Transfers] DROP CONSTRAINT [' + @var28 + '];');
    ALTER TABLE [Transfers] ALTER COLUMN [TransferDate] date NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210205251_exp2'
)
BEGIN
    DECLARE @var29 sysname;
    SELECT @var29 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TransferLines]') AND [c].[name] = N'ExpiryDate');
    IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [TransferLines] DROP CONSTRAINT [' + @var29 + '];');
    ALTER TABLE [TransferLines] ALTER COLUMN [ExpiryDate] date NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210205251_exp2'
)
BEGIN
    DECLARE @var30 sysname;
    SELECT @var30 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StockLedgers]') AND [c].[name] = N'ExpiryDate');
    IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [StockLedgers] DROP CONSTRAINT [' + @var30 + '];');
    ALTER TABLE [StockLedgers] ALTER COLUMN [ExpiryDate] date NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210205251_exp2'
)
BEGIN
    DROP INDEX [IX_StockBatches_WarehouseId_ItemId_BatchNo_ExpiryDate] ON [StockBatches];
    DROP INDEX [IX_StockBatches_WarehouseId_ItemId_ExpiryDate] ON [StockBatches];
    DECLARE @var31 sysname;
    SELECT @var31 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StockBatches]') AND [c].[name] = N'ExpiryDate');
    IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [StockBatches] DROP CONSTRAINT [' + @var31 + '];');
    ALTER TABLE [StockBatches] ALTER COLUMN [ExpiryDate] date NOT NULL;
    CREATE UNIQUE INDEX [IX_StockBatches_WarehouseId_ItemId_BatchNo_ExpiryDate] ON [StockBatches] ([WarehouseId], [ItemId], [BatchNo], [ExpiryDate]);
    CREATE INDEX [IX_StockBatches_WarehouseId_ItemId_ExpiryDate] ON [StockBatches] ([WarehouseId], [ItemId], [ExpiryDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210205251_exp2'
)
BEGIN
    DECLARE @var32 sysname;
    SELECT @var32 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Receipts]') AND [c].[name] = N'ReceiptDate');
    IF @var32 IS NOT NULL EXEC(N'ALTER TABLE [Receipts] DROP CONSTRAINT [' + @var32 + '];');
    ALTER TABLE [Receipts] ALTER COLUMN [ReceiptDate] date NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210205251_exp2'
)
BEGIN
    DECLARE @var33 sysname;
    SELECT @var33 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ReceiptLines]') AND [c].[name] = N'ExpiryDate');
    IF @var33 IS NOT NULL EXEC(N'ALTER TABLE [ReceiptLines] DROP CONSTRAINT [' + @var33 + '];');
    ALTER TABLE [ReceiptLines] ALTER COLUMN [ExpiryDate] date NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210205251_exp2'
)
BEGIN
    DECLARE @var34 sysname;
    SELECT @var34 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Issues]') AND [c].[name] = N'IssueDate');
    IF @var34 IS NOT NULL EXEC(N'ALTER TABLE [Issues] DROP CONSTRAINT [' + @var34 + '];');
    ALTER TABLE [Issues] ALTER COLUMN [IssueDate] date NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210205251_exp2'
)
BEGIN
    DECLARE @var35 sysname;
    SELECT @var35 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[IssueLines]') AND [c].[name] = N'ExpiryDate');
    IF @var35 IS NOT NULL EXEC(N'ALTER TABLE [IssueLines] DROP CONSTRAINT [' + @var35 + '];');
    ALTER TABLE [IssueLines] ALTER COLUMN [ExpiryDate] date NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260210205251_exp2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260210205251_exp2', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260216194752_gs'
)
BEGIN
    CREATE TABLE [BarcodeMappings] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Code] nvarchar(200) NOT NULL,
        [CodeType] nvarchar(10) NOT NULL,
        [ItemId] uniqueidentifier NOT NULL,
        [Note] nvarchar(500) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_BarcodeMappings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BarcodeMappings_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260216194752_gs'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BarcodeMappings_Code_CodeType] ON [BarcodeMappings] ([Code], [CodeType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260216194752_gs'
)
BEGIN
    CREATE INDEX [IX_BarcodeMappings_ItemId] ON [BarcodeMappings] ([ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260216194752_gs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260216194752_gs', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    ALTER TABLE [Items] ADD [Barcode] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    ALTER TABLE [Items] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    ALTER TABLE [Items] ADD [Sku] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    ALTER TABLE [Items] ADD [Unit] nvarchar(30) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE TABLE [InventorySettings] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [IsSingleWarehouseMode] bit NOT NULL,
        [BaseCurrencyCode] nvarchar(3) NOT NULL,
        [DinarCurrencyCode] nvarchar(20) NOT NULL,
        [SingleWarehouseName] nvarchar(200) NULL,
        [DefaultRateSource] nvarchar(50) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_InventorySettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE TABLE [InvStockBalances] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [ItemId] uniqueidentifier NOT NULL,
        [QuantityOnHand] decimal(18,2) NOT NULL,
        [RowVersion] rowversion NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_InvStockBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_InvStockBalance_QtyOnHand_NonNegative] CHECK ([QuantityOnHand] >= 0),
        CONSTRAINT [FK_InvStockBalances_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE TABLE [InvStockLedgers] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [ItemId] uniqueidentifier NOT NULL,
        [MovementType] nvarchar(30) NOT NULL,
        [ReferenceType] nvarchar(30) NOT NULL,
        [ReferenceId] uniqueidentifier NOT NULL,
        [QuantityChange] decimal(18,2) NOT NULL,
        [BalanceAfter] decimal(18,2) NOT NULL,
        [Note] nvarchar(500) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_InvStockLedgers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InvStockLedgers_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE TABLE [ItemImages] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [ItemId] uniqueidentifier NOT NULL,
        [RelativePath] nvarchar(500) NOT NULL,
        [OriginalFileName] nvarchar(255) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [IsPrimary] bit NOT NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_ItemImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ItemImages_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE TABLE [PurchaseInvoices] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Number] nvarchar(50) NOT NULL,
        [InvoiceDate] date NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [EurToDinarRateSnapshot] decimal(18,6) NOT NULL,
        [RateTimestamp] datetime2 NOT NULL,
        [RateSource] nvarchar(50) NULL,
        [TotalEur] decimal(18,2) NOT NULL,
        [TotalDinar] decimal(18,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Note] nvarchar(500) NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [CreatedByUserName] nvarchar(256) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_PurchaseInvoices] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE TABLE [SalesInvoices] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [Number] nvarchar(50) NOT NULL,
        [InvoiceDate] date NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [EurToDinarRateSnapshot] decimal(18,6) NOT NULL,
        [RateTimestamp] datetime2 NOT NULL,
        [RateSource] nvarchar(50) NULL,
        [TotalEur] decimal(18,2) NOT NULL,
        [TotalDinar] decimal(18,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Note] nvarchar(500) NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [CreatedByUserName] nvarchar(256) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_SalesInvoices] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE TABLE [PurchaseLines] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [PurchaseInvoiceId] uniqueidentifier NOT NULL,
        [ItemId] uniqueidentifier NOT NULL,
        [Qty] decimal(18,2) NOT NULL,
        [UnitPriceEur] decimal(18,2) NOT NULL,
        [LineTotalEur] decimal(18,2) NOT NULL,
        [LineTotalDinar] decimal(18,2) NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [ExchangeRateSnapshot] decimal(18,6) NOT NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_PurchaseLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PurchaseLines_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PurchaseLines_PurchaseInvoices_PurchaseInvoiceId] FOREIGN KEY ([PurchaseInvoiceId]) REFERENCES [PurchaseInvoices] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE TABLE [SalesLines] (
        [Id] uniqueidentifier NOT NULL DEFAULT (NEWID()),
        [SalesInvoiceId] uniqueidentifier NOT NULL,
        [ItemId] uniqueidentifier NOT NULL,
        [Qty] decimal(18,2) NOT NULL,
        [UnitPriceEur] decimal(18,2) NOT NULL,
        [LineTotalEur] decimal(18,2) NOT NULL,
        [LineTotalDinar] decimal(18,2) NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [ExchangeRateSnapshot] decimal(18,6) NOT NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL,
        CONSTRAINT [PK_SalesLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SalesLines_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SalesLines_SalesInvoices_SalesInvoiceId] FOREIGN KEY ([SalesInvoiceId]) REFERENCES [SalesInvoices] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
        SET IDENTITY_INSERT [AspNetRoles] ON;
    EXEC(N'INSERT INTO [AspNetRoles] ([Id], [ConcurrencyStamp], [Name], [NormalizedName])
    VALUES (N''1b8e70e8-1dc6-4ff9-bd93-5a11d5c11a77'', N''58ab0c6c-10e6-4e30-a28c-f2f43e8de879'', N''Admin'', N''ADMIN'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'ConcurrencyStamp', N'Name', N'NormalizedName') AND [object_id] = OBJECT_ID(N'[AspNetRoles]'))
        SET IDENTITY_INSERT [AspNetRoles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccessFailedCount', N'Approval', N'ConcurrencyStamp', N'CreatedDate', N'Email', N'EmailConfirmed', N'LastAccessTime', N'LockoutEnabled', N'LockoutEnd', N'ModifiedDate', N'NormalizedEmail', N'NormalizedUserName', N'PasswordHash', N'PhoneNumber', N'PhoneNumberConfirmed', N'SecurityStamp', N'TwoFactorEnabled', N'UserName') AND [object_id] = OBJECT_ID(N'[AspNetUsers]'))
        SET IDENTITY_INSERT [AspNetUsers] ON;
    EXEC(N'INSERT INTO [AspNetUsers] ([Id], [AccessFailedCount], [Approval], [ConcurrencyStamp], [CreatedDate], [Email], [EmailConfirmed], [LastAccessTime], [LockoutEnabled], [LockoutEnd], [ModifiedDate], [NormalizedEmail], [NormalizedUserName], [PasswordHash], [PhoneNumber], [PhoneNumberConfirmed], [SecurityStamp], [TwoFactorEnabled], [UserName])
    VALUES (N''7e5d8740-fcb7-47c8-bdad-6f22072fe37f'', 0, CAST(1 AS bit), N''ce470a13-3765-46f4-989f-afb5e3341fe9'', ''2026-01-01T00:00:00.0000000'', N''admin@newsapp2.local'', CAST(1 AS bit), NULL, CAST(1 AS bit), NULL, NULL, N''ADMIN@NEWSAPP2.LOCAL'', N''ADMIN@NEWSAPP2.LOCAL'', N''AQAAAAIAAYagAAAAENggG9+6Z01XNeB9YmF/XmQybN3d/MpCrkCkJ58k03l2udJQx0IaIujsHHxHRpulzQ=='', NULL, CAST(1 AS bit), N''f3eeac15-69d9-4f95-a5e5-9c30d4f08157'', CAST(0 AS bit), N''admin@newsapp2.local'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccessFailedCount', N'Approval', N'ConcurrencyStamp', N'CreatedDate', N'Email', N'EmailConfirmed', N'LastAccessTime', N'LockoutEnabled', N'LockoutEnd', N'ModifiedDate', N'NormalizedEmail', N'NormalizedUserName', N'PasswordHash', N'PhoneNumber', N'PhoneNumberConfirmed', N'SecurityStamp', N'TwoFactorEnabled', N'UserName') AND [object_id] = OBJECT_ID(N'[AspNetUsers]'))
        SET IDENTITY_INSERT [AspNetUsers] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BaseCurrencyCode', N'Created', N'DefaultRateSource', N'DinarCurrencyCode', N'IsSingleWarehouseMode', N'Modified', N'SingleWarehouseName') AND [object_id] = OBJECT_ID(N'[InventorySettings]'))
        SET IDENTITY_INSERT [InventorySettings] ON;
    EXEC(N'INSERT INTO [InventorySettings] ([Id], [BaseCurrencyCode], [Created], [DefaultRateSource], [DinarCurrencyCode], [IsSingleWarehouseMode], [Modified], [SingleWarehouseName])
    VALUES (''d8ec402f-4f11-4f5e-97ed-c9dc2a58a232'', N''EUR'', ''2026-01-01T00:00:00.0000000'', N''Manual'', N''LYD'', CAST(1 AS bit), NULL, N''Main Warehouse'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BaseCurrencyCode', N'Created', N'DefaultRateSource', N'DinarCurrencyCode', N'IsSingleWarehouseMode', N'Modified', N'SingleWarehouseName') AND [object_id] = OBJECT_ID(N'[InventorySettings]'))
        SET IDENTITY_INSERT [InventorySettings] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'UserId') AND [object_id] = OBJECT_ID(N'[AspNetUserRoles]'))
        SET IDENTITY_INSERT [AspNetUserRoles] ON;
    EXEC(N'INSERT INTO [AspNetUserRoles] ([RoleId], [UserId])
    VALUES (N''1b8e70e8-1dc6-4ff9-bd93-5a11d5c11a77'', N''7e5d8740-fcb7-47c8-bdad-6f22072fe37f'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'UserId') AND [object_id] = OBJECT_ID(N'[AspNetUserRoles]'))
        SET IDENTITY_INSERT [AspNetUserRoles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Items_Barcode] ON [Items] ([Barcode]) WHERE [Barcode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Items_Sku] ON [Items] ([Sku]) WHERE [Sku] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InvStockBalances_ItemId] ON [InvStockBalances] ([ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE INDEX [IX_InvStockLedgers_ItemId] ON [InvStockLedgers] ([ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE INDEX [IX_ItemImages_ItemId_IsPrimary] ON [ItemImages] ([ItemId], [IsPrimary]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PurchaseInvoices_Number] ON [PurchaseInvoices] ([Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE INDEX [IX_PurchaseLines_ItemId] ON [PurchaseLines] ([ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE INDEX [IX_PurchaseLines_PurchaseInvoiceId] ON [PurchaseLines] ([PurchaseInvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SalesInvoices_Number] ON [SalesInvoices] ([Number]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE INDEX [IX_SalesLines_ItemId] ON [SalesLines] ([ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    CREATE INDEX [IX_SalesLines_SalesInvoiceId] ON [SalesLines] ([SalesInvoiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260219002731_Sprint1_FoundationSingleWarehouseEur'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260219002731_Sprint1_FoundationSingleWarehouseEur', N'9.0.9');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220141605_ModernCleanup2_LegacyDrop'
)
BEGIN
    DECLARE @var36 sysname;
    SELECT @var36 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SalesInvoices]') AND [c].[name] = N'RateSource');
    IF @var36 IS NOT NULL EXEC(N'ALTER TABLE [SalesInvoices] DROP CONSTRAINT [' + @var36 + '];');
    ALTER TABLE [SalesInvoices] DROP COLUMN [RateSource];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220141605_ModernCleanup2_LegacyDrop'
)
BEGIN
    DECLARE @var37 sysname;
    SELECT @var37 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseInvoices]') AND [c].[name] = N'RateSource');
    IF @var37 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseInvoices] DROP CONSTRAINT [' + @var37 + '];');
    ALTER TABLE [PurchaseInvoices] DROP COLUMN [RateSource];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220141605_ModernCleanup2_LegacyDrop'
)
BEGIN
    DROP TABLE [IssueLines];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220141605_ModernCleanup2_LegacyDrop'
)
BEGIN
    DROP TABLE [ReceiptLines];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220141605_ModernCleanup2_LegacyDrop'
)
BEGIN
    DROP TABLE [StockBalances];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220141605_ModernCleanup2_LegacyDrop'
)
BEGIN
    DROP TABLE [StockLedgers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220141605_ModernCleanup2_LegacyDrop'
)
BEGIN
    DROP TABLE [TransferLines];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220141605_ModernCleanup2_LegacyDrop'
)
BEGIN
    DROP TABLE [Issues];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220141605_ModernCleanup2_LegacyDrop'
)
BEGIN
    DROP TABLE [Receipts];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220141605_ModernCleanup2_LegacyDrop'
)
BEGIN
    DROP TABLE [StockBatches];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220141605_ModernCleanup2_LegacyDrop'
)
BEGIN
    DROP TABLE [Transfers];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220141605_ModernCleanup2_LegacyDrop'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260220141605_ModernCleanup2_LegacyDrop', N'9.0.9');
END;

COMMIT;
GO

