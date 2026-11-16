using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Arena.Api.Setup;

/// <summary>`--export-schema path`: writes the relational model (tables, keys, indexes, checks) as JSON.</summary>
public static class SchemaExporter
{
    public static void Write(ArenaDbContext db, string path)
    {
        var model = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        var tables = model.Tables.Select(table => new
        {
            table.Name,
            Columns = table.Columns.Select(column => new
            {
                column.Name, column.StoreType, column.IsNullable,
                PrimaryKey = table.PrimaryKey?.Columns.Contains(column) ?? false
            }).ToArray(),
            ForeignKeys = table.ForeignKeyConstraints.Select(fk => new
            {
                fk.Name, Columns = fk.Columns.Select(x => x.Name).ToArray(), ReferencedTable = fk.PrincipalTable.Name,
                ReferencedColumns = fk.PrincipalColumns.Select(x => x.Name).ToArray(), DeleteAction = fk.OnDeleteAction.ToString()
            }).ToArray(),
            UniqueConstraints = table.UniqueConstraints.Select(x => new { x.Name, Columns = x.Columns.Select(c => c.Name).ToArray() }).ToArray(),
            Indexes = table.Indexes.Select(x => new { x.Name, x.IsUnique, x.Filter, Columns = x.Columns.Select(c => c.Name).ToArray() }).ToArray(),
            Checks = table.CheckConstraints.Select(x => new { x.Name, x.Sql }).ToArray()
        }).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(tables, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Exported {tables.Length} SQL Server tables to {path}.");
    }
}
