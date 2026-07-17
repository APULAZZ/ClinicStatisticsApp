/* Adds the two module roles without changing existing users or permissions. */
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Code = N'CallCenter')
    INSERT INTO dbo.Roles (Code, Name) VALUES (N'CallCenter', N'Коллцентр');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Code = N'CallCenterAdmin')
    INSERT INTO dbo.Roles (Code, Name) VALUES (N'CallCenterAdmin', N'Администратор коллцентра');
