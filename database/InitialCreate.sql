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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [UserId] int NULL,
        [UserName] nvarchar(max) NULL,
        [Action] nvarchar(max) NOT NULL,
        [EntityType] nvarchar(max) NOT NULL,
        [EntityId] int NULL,
        [Details] nvarchar(max) NULL,
        [IpAddress] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE TABLE [Branches] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Code] nvarchar(max) NULL,
        [Location] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE TABLE [Departments] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [BranchId] int NULL,
        [Name] nvarchar(450) NOT NULL,
        [Code] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE TABLE [Designations] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_Designations] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE TABLE [EmploymentTypes] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Description] nvarchar(max) NULL,
        CONSTRAINT [PK_EmploymentTypes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE TABLE [Grades] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Level] nvarchar(max) NULL,
        [MinSalary] decimal(18,2) NOT NULL,
        [MaxSalary] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_Grades] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE TABLE [LeaveBalances] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [LeaveTypeId] int NOT NULL,
        [Year] int NOT NULL,
        [AllocatedDays] decimal(9,2) NOT NULL,
        [UsedDays] decimal(9,2) NOT NULL,
        CONSTRAINT [PK_LeaveBalances] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE TABLE [LeaveRequests] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [LeaveTypeId] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [DaysRequested] decimal(9,2) NOT NULL,
        [Reason] nvarchar(max) NULL,
        [Status] nvarchar(16) NOT NULL,
        [ReviewedBy] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ReviewedAt] datetime2 NULL,
        CONSTRAINT [PK_LeaveRequests] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE TABLE [LeaveTypes] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [DefaultDays] int NOT NULL,
        [Paid] bit NOT NULL DEFAULT CAST(1 AS bit),
        [Description] nvarchar(max) NULL,
        CONSTRAINT [PK_LeaveTypes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE TABLE [PayrollPeriods] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Month] int NOT NULL,
        [Year] int NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [ProcessedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PayrollPeriods] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE TABLE [ReportDefinitions] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(450) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [ReportPath] nvarchar(max) NOT NULL,
        [Enabled] bit NOT NULL,
        CONSTRAINT [PK_ReportDefinitions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] int NOT NULL IDENTITY,
        [CompanyName] nvarchar(max) NOT NULL,
        [KraPin] nvarchar(max) NULL,
        [Email] nvarchar(max) NULL,
        [Phone] nvarchar(max) NULL,
        [Address] nvarchar(max) NULL,
        [Subdomain] nvarchar(450) NULL,
        [Status] nvarchar(16) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE TABLE [Employees] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [EmployeeNo] nvarchar(450) NOT NULL,
        [PayrollNo] nvarchar(max) NULL,
        [FirstName] nvarchar(max) NOT NULL,
        [MiddleName] nvarchar(max) NULL,
        [LastName] nvarchar(max) NOT NULL,
        [Gender] nvarchar(max) NULL,
        [DateOfBirth] datetime2 NULL,
        [IdNo] nvarchar(max) NULL,
        [KraPin] nvarchar(max) NOT NULL,
        [NssfNo] nvarchar(max) NULL,
        [ShifNo] nvarchar(max) NULL,
        [Phone] nvarchar(max) NULL,
        [Email] nvarchar(max) NULL,
        [BranchId] int NULL,
        [DepartmentId] int NULL,
        [DesignationId] int NULL,
        [GradeId] int NULL,
        [EmploymentTypeId] int NULL,
        [EmploymentDate] datetime2 NULL,
        [TerminationDate] datetime2 NULL,
        [EmploymentStatus] nvarchar(max) NOT NULL,
        [BasicSalary] decimal(18,2) NOT NULL,
        [BankName] nvarchar(max) NULL,
        [BankBranch] nvarchar(max) NULL,
        [AccountNumber] nvarchar(max) NULL,
        CONSTRAINT [PK_Employees] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Employees_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE TABLE [PayrollTransactions] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [PayrollPeriodId] int NOT NULL,
        [EmployeeId] int NOT NULL,
        [BasicSalary] decimal(18,2) NOT NULL,
        [Allowances] decimal(18,2) NOT NULL,
        [GrossPay] decimal(18,2) NOT NULL,
        [TaxablePay] decimal(18,2) NOT NULL,
        [Paye] decimal(18,2) NOT NULL,
        [PersonalRelief] decimal(18,2) NOT NULL,
        [Nssf] decimal(18,2) NOT NULL,
        [Shif] decimal(18,2) NOT NULL,
        [HousingLevy] decimal(18,2) NOT NULL,
        [OtherDeductions] decimal(18,2) NOT NULL,
        [TotalDeductions] decimal(18,2) NOT NULL,
        [NetPay] decimal(18,2) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PayrollTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PayrollTransactions_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PayrollTransactions_PayrollPeriods_PayrollPeriodId] FOREIGN KEY ([PayrollPeriodId]) REFERENCES [PayrollPeriods] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [TenantId] int NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Email] nvarchar(450) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [Role] nvarchar(32) NOT NULL,
        [EmployeeId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastSignedIn] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Users_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_TenantId_CreatedAt] ON [AuditLogs] ([TenantId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Branches_TenantId_Name] ON [Branches] ([TenantId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Departments_TenantId_Name] ON [Departments] ([TenantId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Designations_TenantId_Name] ON [Designations] ([TenantId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Employees_TenantId_EmployeeNo] ON [Employees] ([TenantId], [EmployeeNo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EmploymentTypes_TenantId_Name] ON [EmploymentTypes] ([TenantId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LeaveBalances_TenantId_EmployeeId_LeaveTypeId_Year] ON [LeaveBalances] ([TenantId], [EmployeeId], [LeaveTypeId], [Year]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LeaveTypes_TenantId_Name] ON [LeaveTypes] ([TenantId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PayrollPeriods_TenantId_Year_Month] ON [PayrollPeriods] ([TenantId], [Year], [Month]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PayrollTransactions_EmployeeId] ON [PayrollTransactions] ([EmployeeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PayrollTransactions_PayrollPeriodId] ON [PayrollTransactions] ([PayrollPeriodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PayrollTransactions_TenantId_PayrollPeriodId_EmployeeId] ON [PayrollTransactions] ([TenantId], [PayrollPeriodId], [EmployeeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReportDefinitions_Name] ON [ReportDefinitions] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Tenants_Subdomain] ON [Tenants] ([Subdomain]) WHERE [Subdomain] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_EmployeeId] ON [Users] ([EmployeeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_TenantId_Email] ON [Users] ([TenantId], [Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818114617_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260818114617_InitialCreate', N'8.0.11');
END;
GO

COMMIT;
GO

