using Dapper;
using Microsoft.Data.Sqlite;
using NetGenCAD.Designer.Repositories;
using System.Text.RegularExpressions;

namespace NetGenCAD.Designer.Utility
{
    public static class ObjectCopy
    {
        public static async Task<string> CopyAsync(SqliteConnection connection, string sourceName)
        {
            using var transaction = connection.BeginTransaction();
            var source = await connection.QuerySingleOrDefaultAsync<ObjectDimensions>(
                "SELECT * FROM ObjectDimensions WHERE ObjectName = @sourceName", new { sourceName }, transaction)
                ?? throw new InvalidOperationException($"Object '{sourceName}' was not found.");
            var solids = (await connection.QueryAsync<SolidDimensions>(
                "SELECT * FROM SolidDimensions WHERE ObjectDimensionsId = @Id ORDER BY Id", new { source.Id }, transaction)).ToList();
            var modules = (await connection.QueryAsync<ModuleDimensions>(
                "SELECT * FROM ModuleDimensions WHERE ObjectDimensionsId = @Id ORDER BY Id", new { source.Id }, transaction)).ToList();
            if (solids.Count == 0 && modules.Count == 0)
                throw new InvalidOperationException($"Object '{sourceName}' has no solids or modules to copy.");

            var copyName = sourceName + "_copy";
            for (var index = 2; await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM ObjectDimensions WHERE ObjectName = @copyName", new { copyName }, transaction) != 0; index++)
                copyName = $"{sourceName}_copy_{index}";
            var suffix = copyName[sourceName.Length..];

            var identifiers = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var code in solids.Select(s => s.OSCADMethod).Concat(modules.Select(m => m.OSCADMethod)))
            {
                var match = Regex.Match(code ?? string.Empty, @"\bmodule\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(");
                if (match.Success)
                    identifiers[match.Groups[1].Value] = match.Groups[1].Value + suffix;
            }

            string RenameCode(string code) => Regex.Replace(code ?? string.Empty,
                "\"(?:\\\\.|[^\"\\\\])*\"|//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/|[A-Za-z_][A-Za-z0-9_]*",
                match => identifiers.TryGetValue(match.Value, out var replacement) ? replacement : match.Value);

            var newId = await CloneRowAsync(connection, transaction, "ObjectDimensions", source.Id,
                new Dictionary<string, object?> { ["ObjectName"] = copyName, ["CreatedAt"] = DateTime.UtcNow });
            var moduleIds = new Dictionary<int, int>();
            foreach (var module in modules)
            {
                var newModuleId = await CloneRowAsync(connection, transaction, "ModuleDimensions", module.Id,
                    new Dictionary<string, object?>
                    {
                        ["ObjectName"] = copyName,
                        ["ObjectDimensionsId"] = newId,
                        ["Name"] = RenameCode(module.Name),
                        ["OSCADMethod"] = RenameCode(module.OSCADMethod),
                        ["IncludeMethod"] = string.Empty,
                        ["CreatedAt"] = DateTime.UtcNow
                    });
                moduleIds.Add(module.Id, newModuleId);
            }
            foreach (var solid in solids)
            {
                if (solid.ModuleDimensionsId is int moduleId && !moduleIds.ContainsKey(moduleId))
                    throw new InvalidOperationException($"Solid {solid.Id} references a module outside object '{sourceName}'.");
                await CloneRowAsync(connection, transaction, "SolidDimensions", solid.Id,
                    new Dictionary<string, object?>
                    {
                        ["Name"] = copyName,
                        ["ObjectDimensionsId"] = newId,
                        ["ModuleDimensionsId"] = solid.ModuleDimensionsId is int id ? moduleIds[id] : null,
                        ["OSCADMethod"] = RenameCode(solid.OSCADMethod),
                        ["CreatedAt"] = DateTime.UtcNow
                    });
            }
            transaction.Commit();
            return copyName;
        }

        private static async Task<int> CloneRowAsync(SqliteConnection connection, SqliteTransaction transaction,
            string table, int id, Dictionary<string, object?> overrides)
        {
            var columns = (await connection.QueryAsync<string>($"SELECT name FROM pragma_table_info('{table}') WHERE name <> 'Id'", transaction: transaction)).ToArray();
            var parameters = new DynamicParameters();
            parameters.Add("SourceId", id);
            var expressions = new List<string>();
            foreach (var column in columns)
            {
                if (overrides.TryGetValue(column, out var value))
                {
                    parameters.Add(column, value);
                    expressions.Add("@" + column);
                }
                else
                    expressions.Add("\"" + column + "\"");
            }
            var names = string.Join(", ", columns.Select(column => "\"" + column + "\""));
            var sql = $"INSERT INTO {table} ({names}) SELECT {string.Join(", ", expressions)} FROM {table} WHERE Id = @SourceId; SELECT last_insert_rowid();";
            return await connection.ExecuteScalarAsync<int>(sql, parameters, transaction);
        }
    }
}
