/* Run this file with sqlcmd -f 65001. Corrects only CRM-side display names. */
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

UPDATE dbo.ClinicDataSources
SET Name = CASE Code
    WHEN N'CENTRAL_COPY_TEST' THEN N'Копия ЦК (тест)'
    WHEN N'CENTRAL_PRODUCTION' THEN N'ЦК — рабочая база'
    WHEN N'BRANCH_02' THEN N'Комфорт — рабочая база'
    WHEN N'BRANCH_03' THEN N'Баграмяна — рабочая база'
    WHEN N'BRANCH_04' THEN N'Детство — рабочая база'
    WHEN N'BRANCH_05' THEN N'Генделя — рабочая база'
    WHEN N'BRANCH_06' THEN N'Виктория — рабочая база'
    WHEN N'BRANCH_07' THEN N'Альфа — рабочая база'
    WHEN N'BRANCH_08' THEN N'Регион — рабочая база'
    WHEN N'BRANCH_09' THEN N'Артиллерийская — рабочая база'
    WHEN N'BRANCH_10' THEN N'Сельма — рабочая база'
    ELSE Name
END,
UpdatedAt = SYSUTCDATETIME();

COMMIT TRANSACTION;
