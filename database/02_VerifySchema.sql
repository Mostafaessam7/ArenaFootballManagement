-- Run after applying the migration to ArenaSports in SQL Server.
USE [ArenaSports];
GO
SET NOCOUNT ON;
SELECT [MigrationId], [ProductVersion] FROM [__EFMigrationsHistory];
SELECT t.name AS TableName, COUNT(c.column_id) AS ColumnCount
FROM sys.tables t JOIN sys.columns c ON c.object_id = t.object_id
WHERE t.name <> '__EFMigrationsHistory'
GROUP BY t.name ORDER BY t.name;
SELECT name, is_disabled, is_not_trusted FROM sys.foreign_keys ORDER BY name;
SELECT name, is_disabled, is_not_trusted FROM sys.check_constraints ORDER BY name;
SELECT OBJECT_NAME(object_id) AS TableName, name, is_unique, filter_definition
FROM sys.indexes WHERE is_hypothetical = 0 AND OBJECTPROPERTY(object_id,'IsUserTable') = 1
ORDER BY TableName, name;
