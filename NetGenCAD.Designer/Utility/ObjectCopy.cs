using Dapper;
using Microsoft.Data.Sqlite;
using NetGenCAD.Designer.Functions;
using NetGenCAD.Designer.Repositories;

namespace NetGenCAD.Designer.Utility
{
    public static class ObjectCopy
    {
        public sealed class ObjectNameAlreadyExistsException(string name)
            : InvalidOperationException($"Object '{name}' already exists.");

        public static Task<string> CopyAsync(SqliteConnection connection, string sourceName) =>
            CopyCoreAsync(connection, sourceName, null);

        public static Task<string> CopyAsync(SqliteConnection connection, string sourceName, string targetName)
        {
            if (string.IsNullOrWhiteSpace(targetName))
                throw new ArgumentException("A target object name is required.", nameof(targetName));
            return CopyCoreAsync(connection, sourceName, targetName);
        }

        private static async Task<string> CopyCoreAsync(SqliteConnection connection, string sourceName, string? targetName)
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

            var copyName = targetName ?? sourceName + "_copy";
            for (var index = 2; await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM ObjectDimensions WHERE ObjectName = @copyName", new { copyName }, transaction) != 0; index++)
            {
                if (targetName != null)
                    throw new ObjectNameAlreadyExistsException(targetName);
                copyName = $"{sourceName}_copy_{index}";
            }
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
                        ["CreatedAt"] = DateTime.UtcNow
                    });
            }

            var copiedSolids = (await connection.QueryAsync<SolidDimensions>(
                "SELECT * FROM SolidDimensions WHERE ObjectDimensionsId = @newId ORDER BY Id", new { newId }, transaction)).ToList();
            var copiedModules = (await connection.QueryAsync<ModuleDimensions>(
                "SELECT * FROM ModuleDimensions WHERE ObjectDimensionsId = @newId ORDER BY Id", new { newId }, transaction)).ToList();
            foreach (var solid in copiedSolids.Where(s => s.OperationType is "Difference" or "Intersection"))
            {
                if (!copiedSolids.Any(s => s.OperationType == "Union" && s.Layer == solid.Layer))
                    throw new InvalidOperationException($"Cannot rebuild layer {solid.Layer} without a union solid.");
            }
            foreach (var module in copiedModules.Where(m => m.SolidType == "Object"))
            {
                if (!copiedSolids.Any(s => s.OperationType == module.ModuleType && s.Layer == module.Layer))
                    throw new InvalidOperationException($"Cannot rebuild object module {module.Id} without matching solids.");
            }

            await ObjectScadFunctions.CreateUnionModuleAsync(copyName, copiedSolids, copiedModules, connection, false, transaction);
            copiedModules = (await connection.QueryAsync<ModuleDimensions>(
                "SELECT * FROM ModuleDimensions WHERE ObjectDimensionsId = @newId ORDER BY Id", new { newId }, transaction)).ToList();
            await ObjectScadFunctions.CreateDifferenceModuleAsync(copyName, copiedSolids, copiedModules, connection, false, transaction);
            await ObjectScadFunctions.CreateIntersectionModuleAsync(copyName, copiedSolids, copiedModules, connection, false, transaction);
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
