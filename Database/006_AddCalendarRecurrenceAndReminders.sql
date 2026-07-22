IF COL_LENGTH(N'dbo.CalendarEvents', N'RecurrenceType') IS NULL
    ALTER TABLE dbo.CalendarEvents ADD RecurrenceType NVARCHAR(20) NOT NULL CONSTRAINT DF_CalendarEvents_RecurrenceType DEFAULT (N'None');

IF COL_LENGTH(N'dbo.CalendarEvents', N'RecursUntil') IS NULL
    ALTER TABLE dbo.CalendarEvents ADD RecursUntil DATETIME2 NULL;

IF COL_LENGTH(N'dbo.CalendarEvents', N'ReminderMinutes') IS NULL
    ALTER TABLE dbo.CalendarEvents ADD ReminderMinutes INT NULL;
