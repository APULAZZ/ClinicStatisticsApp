/* Creates/updates the requested test accounts. Temporary password: 1111. */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Code = N'Statistics')
    INSERT INTO dbo.Roles (Code, Name) VALUES (N'Statistics', N'Статистика');

DECLARE @CallCenterRoleId int = (SELECT Id FROM dbo.Roles WHERE Code = N'CallCenter');
DECLARE @AdminRoleId int = (SELECT Id FROM dbo.Roles WHERE Code = N'Admin');
DECLARE @StatisticsRoleId int = (SELECT Id FROM dbo.Roles WHERE Code = N'Statistics');

IF EXISTS (SELECT 1 FROM dbo.Users WHERE Login = N'zoya' OR FullName = N'Зоя Ершова')
    UPDATE dbo.Users
    SET Login = N'zoya', FullName = N'Зоя Ершова', PasswordHash = N'1111', RoleId = @CallCenterRoleId, BranchId = NULL, IsActive = 1, UpdatedAt = GETDATE()
    WHERE Login = N'zoya' OR FullName = N'Зоя Ершова';
ELSE
    INSERT INTO dbo.Users (Login, PasswordHash, FullName, RoleId, BranchId, IsActive, CreatedAt, UpdatedAt)
    VALUES (N'zoya', N'1111', N'Зоя Ершова', @CallCenterRoleId, NULL, 1, GETDATE(), GETDATE());

IF EXISTS (SELECT 1 FROM dbo.Users WHERE Login = N'sergey' OR FullName = N'Сергей Елисеенко')
    UPDATE dbo.Users
    SET Login = N'sergey', FullName = N'Сергей Елисеенко', PasswordHash = N'1111', RoleId = @AdminRoleId, BranchId = NULL, IsActive = 1, UpdatedAt = GETDATE()
    WHERE Login = N'sergey' OR FullName = N'Сергей Елисеенко';
ELSE
    INSERT INTO dbo.Users (Login, PasswordHash, FullName, RoleId, BranchId, IsActive, CreatedAt, UpdatedAt)
    VALUES (N'sergey', N'1111', N'Сергей Елисеенко', @AdminRoleId, NULL, 1, GETDATE(), GETDATE());

IF EXISTS (SELECT 1 FROM dbo.Users WHERE Login = N'lisa' OR FullName IN (N'Мерзликина Елизавета', N'Елизавета Мерзликина'))
    UPDATE dbo.Users
    SET Login = N'lisa', FullName = N'Елизавета Мерзликина', PasswordHash = N'1111', RoleId = @StatisticsRoleId, BranchId = NULL, IsActive = 1, UpdatedAt = GETDATE()
    WHERE Login = N'lisa' OR FullName IN (N'Мерзликина Елизавета', N'Елизавета Мерзликина');
ELSE
    INSERT INTO dbo.Users (Login, PasswordHash, FullName, RoleId, BranchId, IsActive, CreatedAt, UpdatedAt)
    VALUES (N'lisa', N'1111', N'Елизавета Мерзликина', @StatisticsRoleId, NULL, 1, GETDATE(), GETDATE());

COMMIT TRANSACTION;
