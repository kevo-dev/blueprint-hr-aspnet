IF DB_ID(N'BluePrintHr') IS NULL
BEGIN
    CREATE DATABASE [BluePrintHr];
END
GO

USE [BluePrintHr];
GO

IF OBJECT_ID(N'dbo.Tenants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tenants (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Tenants PRIMARY KEY,
        CompanyName nvarchar(240) NOT NULL,
        KraPin nvarchar(32) NULL,
        Email nvarchar(320) NULL,
        Phone nvarchar(64) NULL,
        Address nvarchar(500) NULL,
        Subdomain nvarchar(120) NULL,
        Status nvarchar(16) NOT NULL CONSTRAINT DF_Tenants_Status DEFAULT N'Active',
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_Tenants_CreatedAt DEFAULT SYSUTCDATETIME()
    );
    CREATE UNIQUE INDEX UX_Tenants_Subdomain ON dbo.Tenants(Subdomain) WHERE Subdomain IS NOT NULL;
END
GO

IF OBJECT_ID(N'dbo.Branches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Branches (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Branches PRIMARY KEY,
        TenantId int NOT NULL,
        Name nvarchar(160) NOT NULL,
        Code nvarchar(32) NULL,
        Location nvarchar(240) NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_Branches_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Branches_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
    );
    CREATE UNIQUE INDEX UX_Branches_Tenant_Name ON dbo.Branches(TenantId, Name);
END
GO

IF OBJECT_ID(N'dbo.Departments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Departments (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Departments PRIMARY KEY,
        TenantId int NOT NULL,
        BranchId int NULL,
        Name nvarchar(160) NOT NULL,
        Code nvarchar(32) NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_Departments_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Departments_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
        CONSTRAINT FK_Departments_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches(Id)
    );
    CREATE UNIQUE INDEX UX_Departments_Tenant_Name ON dbo.Departments(TenantId, Name);
END
GO

IF OBJECT_ID(N'dbo.Designations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Designations (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Designations PRIMARY KEY,
        TenantId int NOT NULL,
        Name nvarchar(160) NOT NULL,
        CONSTRAINT FK_Designations_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
    );
    CREATE UNIQUE INDEX UX_Designations_Tenant_Name ON dbo.Designations(TenantId, Name);
END
GO

IF OBJECT_ID(N'dbo.Grades', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Grades (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Grades PRIMARY KEY,
        TenantId int NOT NULL,
        Name nvarchar(160) NOT NULL,
        Level nvarchar(64) NULL,
        MinSalary decimal(18,2) NOT NULL,
        MaxSalary decimal(18,2) NOT NULL,
        CONSTRAINT FK_Grades_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
    );
END
GO

IF OBJECT_ID(N'dbo.EmploymentTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmploymentTypes (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmploymentTypes PRIMARY KEY,
        TenantId int NOT NULL,
        Name nvarchar(160) NOT NULL,
        Description nvarchar(500) NULL,
        CONSTRAINT FK_EmploymentTypes_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
    );
    CREATE UNIQUE INDEX UX_EmploymentTypes_Tenant_Name ON dbo.EmploymentTypes(TenantId, Name);
END
GO

IF OBJECT_ID(N'dbo.Employees', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Employees (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Employees PRIMARY KEY,
        TenantId int NOT NULL,
        EmployeeNo nvarchar(64) NOT NULL,
        PayrollNo nvarchar(64) NULL,
        FirstName nvarchar(120) NOT NULL,
        MiddleName nvarchar(120) NULL,
        LastName nvarchar(120) NOT NULL,
        Gender nvarchar(32) NULL,
        DateOfBirth datetime2(7) NULL,
        IdNo nvarchar(64) NULL,
        KraPin nvarchar(32) NOT NULL,
        NssfNo nvarchar(64) NULL,
        ShifNo nvarchar(64) NULL,
        Phone nvarchar(64) NULL,
        Email nvarchar(320) NULL,
        BranchId int NULL,
        DepartmentId int NULL,
        DesignationId int NULL,
        GradeId int NULL,
        EmploymentTypeId int NULL,
        EmploymentDate datetime2(7) NULL,
        TerminationDate datetime2(7) NULL,
        EmploymentStatus nvarchar(32) NOT NULL CONSTRAINT DF_Employees_Status DEFAULT N'Active',
        BasicSalary decimal(18,2) NOT NULL,
        BankName nvarchar(160) NULL,
        BankBranch nvarchar(160) NULL,
        AccountNumber nvarchar(128) NULL,
        CONSTRAINT FK_Employees_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
        CONSTRAINT FK_Employees_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches(Id),
        CONSTRAINT FK_Employees_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments(Id),
        CONSTRAINT FK_Employees_Designations FOREIGN KEY (DesignationId) REFERENCES dbo.Designations(Id),
        CONSTRAINT FK_Employees_Grades FOREIGN KEY (GradeId) REFERENCES dbo.Grades(Id),
        CONSTRAINT FK_Employees_EmploymentTypes FOREIGN KEY (EmploymentTypeId) REFERENCES dbo.EmploymentTypes(Id)
    );
    CREATE UNIQUE INDEX UX_Employees_Tenant_EmployeeNo ON dbo.Employees(TenantId, EmployeeNo);
END
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        TenantId int NOT NULL,
        Name nvarchar(240) NOT NULL,
        Email nvarchar(320) NOT NULL,
        PasswordHash nvarchar(500) NOT NULL,
        Role nvarchar(32) NOT NULL CONSTRAINT DF_Users_Role DEFAULT N'Employee',
        EmployeeId int NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
        LastSignedIn datetime2(7) NULL,
        CONSTRAINT FK_Users_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
        CONSTRAINT FK_Users_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id) ON DELETE SET NULL
    );
    CREATE UNIQUE INDEX UX_Users_Tenant_Email ON dbo.Users(TenantId, Email);
END
GO

IF OBJECT_ID(N'dbo.PayrollPeriods', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PayrollPeriods (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PayrollPeriods PRIMARY KEY,
        TenantId int NOT NULL,
        Name nvarchar(160) NOT NULL,
        Month int NOT NULL,
        Year int NOT NULL,
        Status nvarchar(16) NOT NULL CONSTRAINT DF_PayrollPeriods_Status DEFAULT N'Open',
        ProcessedAt datetime2(7) NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_PayrollPeriods_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_PayrollPeriods_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
    );
    CREATE UNIQUE INDEX UX_PayrollPeriods_Tenant_Year_Month ON dbo.PayrollPeriods(TenantId, Year, Month);
END
GO

IF OBJECT_ID(N'dbo.PayrollTransactions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PayrollTransactions (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PayrollTransactions PRIMARY KEY,
        TenantId int NOT NULL,
        PayrollPeriodId int NOT NULL,
        EmployeeId int NOT NULL,
        BasicSalary decimal(18,2) NOT NULL,
        Allowances decimal(18,2) NOT NULL,
        GrossPay decimal(18,2) NOT NULL,
        TaxablePay decimal(18,2) NOT NULL,
        Paye decimal(18,2) NOT NULL,
        PersonalRelief decimal(18,2) NOT NULL,
        Nssf decimal(18,2) NOT NULL,
        Shif decimal(18,2) NOT NULL,
        HousingLevy decimal(18,2) NOT NULL,
        OtherDeductions decimal(18,2) NOT NULL,
        TotalDeductions decimal(18,2) NOT NULL,
        NetPay decimal(18,2) NOT NULL,
        Status nvarchar(32) NOT NULL CONSTRAINT DF_PayrollTransactions_Status DEFAULT N'Draft',
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_PayrollTransactions_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_PayrollTransactions_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
        CONSTRAINT FK_PayrollTransactions_Periods FOREIGN KEY (PayrollPeriodId) REFERENCES dbo.PayrollPeriods(Id) ON DELETE CASCADE,
        CONSTRAINT FK_PayrollTransactions_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id)
    );
    CREATE UNIQUE INDEX UX_PayrollTransactions_Tenant_Period_Employee ON dbo.PayrollTransactions(TenantId, PayrollPeriodId, EmployeeId);
END
GO

IF OBJECT_ID(N'dbo.LeaveTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeaveTypes (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LeaveTypes PRIMARY KEY,
        TenantId int NOT NULL,
        Name nvarchar(160) NOT NULL,
        DefaultDays int NOT NULL,
        Paid bit NOT NULL CONSTRAINT DF_LeaveTypes_Paid DEFAULT 1,
        Description nvarchar(500) NULL,
        CONSTRAINT FK_LeaveTypes_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
    );
    CREATE UNIQUE INDEX UX_LeaveTypes_Tenant_Name ON dbo.LeaveTypes(TenantId, Name);
END
GO

IF OBJECT_ID(N'dbo.LeaveBalances', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeaveBalances (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LeaveBalances PRIMARY KEY,
        TenantId int NOT NULL,
        EmployeeId int NOT NULL,
        LeaveTypeId int NOT NULL,
        Year int NOT NULL,
        AllocatedDays decimal(9,2) NOT NULL,
        UsedDays decimal(9,2) NOT NULL,
        CONSTRAINT FK_LeaveBalances_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
        CONSTRAINT FK_LeaveBalances_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id),
        CONSTRAINT FK_LeaveBalances_LeaveTypes FOREIGN KEY (LeaveTypeId) REFERENCES dbo.LeaveTypes(Id)
    );
    CREATE UNIQUE INDEX UX_LeaveBalances_Tenant_Employee_Type_Year ON dbo.LeaveBalances(TenantId, EmployeeId, LeaveTypeId, Year);
END
GO

IF OBJECT_ID(N'dbo.LeaveRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeaveRequests (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LeaveRequests PRIMARY KEY,
        TenantId int NOT NULL,
        EmployeeId int NOT NULL,
        LeaveTypeId int NOT NULL,
        StartDate datetime2(7) NOT NULL,
        EndDate datetime2(7) NOT NULL,
        DaysRequested decimal(9,2) NOT NULL,
        Reason nvarchar(1000) NULL,
        Status nvarchar(16) NOT NULL CONSTRAINT DF_LeaveRequests_Status DEFAULT N'Pending',
        ReviewedBy int NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_LeaveRequests_CreatedAt DEFAULT SYSUTCDATETIME(),
        ReviewedAt datetime2(7) NULL,
        CONSTRAINT FK_LeaveRequests_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
        CONSTRAINT FK_LeaveRequests_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id),
        CONSTRAINT FK_LeaveRequests_LeaveTypes FOREIGN KEY (LeaveTypeId) REFERENCES dbo.LeaveTypes(Id)
    );
END
GO

IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY,
        TenantId int NOT NULL,
        UserId int NULL,
        UserName nvarchar(240) NULL,
        Action nvarchar(64) NOT NULL,
        EntityType nvarchar(128) NOT NULL,
        EntityId int NULL,
        Details nvarchar(max) NULL,
        IpAddress nvarchar(64) NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_AuditLogs_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_AuditLogs_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
    );
    CREATE INDEX IX_AuditLogs_Tenant_CreatedAt ON dbo.AuditLogs(TenantId, CreatedAt DESC);
END
GO

IF OBJECT_ID(N'dbo.ReportDefinitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReportDefinitions (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReportDefinitions PRIMARY KEY,
        Name nvarchar(240) NOT NULL,
        Description nvarchar(1000) NOT NULL,
        ReportPath nvarchar(500) NOT NULL,
        Enabled bit NOT NULL CONSTRAINT DF_ReportDefinitions_Enabled DEFAULT 1
    );
    CREATE UNIQUE INDEX UX_ReportDefinitions_Name ON dbo.ReportDefinitions(Name);
END
GO
