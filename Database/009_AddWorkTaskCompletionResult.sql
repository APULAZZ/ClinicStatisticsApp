IF COL_LENGTH(N'dbo.WorkTasks', N'CompletionResult') IS NULL
    ALTER TABLE dbo.WorkTasks ADD CompletionResult NVARCHAR(2000) NULL;
