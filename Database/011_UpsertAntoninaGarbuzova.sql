DECLARE @AdminRoleId INT = (SELECT TOP (1) Id FROM dbo.Roles WHERE Code = N'Admin');

IF @AdminRoleId IS NULL
    THROW 50002, N'Роль Admin не найдена.', 1;

IF EXISTS (SELECT 1 FROM dbo.Users WHERE Login = N'antonina')
BEGIN
    UPDATE dbo.Users
    SET FullName = N'Антонина Гарбузова',
        PasswordHash = N'1111',
        RoleId = @AdminRoleId,
        BranchId = NULL,
        IsActive = 1,
        UpdatedAt = GETDATE()
    WHERE Login = N'antonina';
END
ELSE
BEGIN
    INSERT INTO dbo.Users (Login, PasswordHash, FullName, RoleId, BranchId, IsActive, CreatedAt, UpdatedAt)
    VALUES (N'antonina', N'1111', N'Антонина Гарбузова', @AdminRoleId, NULL, 1, GETDATE(), GETDATE());
END
