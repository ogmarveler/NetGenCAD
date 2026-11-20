using Dapper;
using Microsoft.Data.Sqlite;
using static NetGenCAD.Core.Measurements.Conversion;
using static NetGenCAD.Core.Measurements.VolumeConverter;
namespace NetGenCAD.Designer.Repositories
{
    public class ObjectDimensions
    {
        public int Id { get; set; }
        public string ObjectName { get; set; } = string.Empty;
        public string? ObjectDescription { get; set; } = string.Empty; // Reference to Object Description
        public int? SetDimensionsId { get; set; }
        public int? AxisDimensionsId { get; set; }

        // Common transformation properties
        public double XOffset_MM { get; set; } // X-axis translation offset
        public double YOffset_MM { get; set; } // Y-axis translation offset
        public double ZOffset_MM { get; set; } // Z-axis translation offset
        // Imperial conversions - Common properties
        public double XOffset_IN => Math.Round(MillimeterToInches(XOffset_MM), OpenSCAD_DecimalPlaces);
        public double YOffset_IN => Math.Round(MillimeterToInches(YOffset_MM), OpenSCAD_DecimalPlaces);
        public double ZOffset_IN => Math.Round(MillimeterToInches(ZOffset_MM), OpenSCAD_DecimalPlaces);
        public double XRotate { get; set; } = 0;
        public double YRotate { get; set; } = 0;
        public double ZRotate { get; set; } = 0;

        // Scale properties (for transformation scaling)
        public double ScaleX { get; set; } = 1.0;
        public double ScaleY { get; set; } = 1.0;
        public double ScaleZ { get; set; } = 1.0;
        public double XMirror { get; set; } = 0.0;
        public double YMirror { get; set; } = 0.0;
        public double ZMirror { get; set; } = 0.0;
        public string Color { get; set; } = string.Empty;
        public double Alpha { get; set; } = 1;
        public int Layer { get; set; } = 0;
        public string IncludeMethod { get; set; } = string.Empty;
        public string OSCADMethod { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public const int OpenSCAD_DecimalPlaces = 6; // High precision for 3D printing
    }

    public static class ObjectDimensionsExtensions
    {
        public static async Task EnsureSchemaAsync(SqliteConnection connection)
        {
            await AxisDimensionsExtensions.CreateTable(connection);
            await SolidDimensionsExtensions.CreateTable(connection);
            await ModuleDimensionsExtensions.CreateTable(connection);
            await PolyhedronDimensionsExtensions.CreateTable(connection);
            await ShapeDimensionsExtensions.CreateTable(connection);
            await InitializeAsync(connection);
        }

        public static async Task InitializeAsync(SqliteConnection connection)
        {
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS ObjectDimensions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ObjectName TEXT NOT NULL UNIQUE CHECK(length(trim(ObjectName)) > 0),
                    ObjectDescription TEXT DEFAULT NULL,
                    SetDimensionsId INTEGER REFERENCES ShapeDimensions(Id) ON DELETE SET NULL,
                    AxisDimensionsId INTEGER REFERENCES AxisDimensions(Id) ON DELETE SET NULL,
                    XOffset_MM REAL NOT NULL DEFAULT 0, YOffset_MM REAL NOT NULL DEFAULT 0, ZOffset_MM REAL NOT NULL DEFAULT 0,
                    XOffset_IN REAL NOT NULL DEFAULT 0, YOffset_IN REAL NOT NULL DEFAULT 0, ZOffset_IN REAL NOT NULL DEFAULT 0,
                    XRotate REAL NOT NULL DEFAULT 0, YRotate REAL NOT NULL DEFAULT 0, ZRotate REAL NOT NULL DEFAULT 0,
                    XMirror REAL NOT NULL DEFAULT 0, YMirror REAL NOT NULL DEFAULT 0, ZMirror REAL NOT NULL DEFAULT 0,
                    ScaleX REAL NOT NULL DEFAULT 1, ScaleY REAL NOT NULL DEFAULT 1, ScaleZ REAL NOT NULL DEFAULT 1,
                    Color TEXT NOT NULL DEFAULT '', Alpha REAL NOT NULL DEFAULT 1,
                    Layer INTEGER NOT NULL DEFAULT 0, IncludeMethod TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')), OSCADMethod TEXT NOT NULL DEFAULT ''
                );");

            await AddReferenceColumnAsync(connection, "SolidDimensions", "ObjectDimensionsId", "ObjectDimensions", "RESTRICT");
            await AddReferenceColumnAsync(connection, "ModuleDimensions", "ObjectDimensionsId", "ObjectDimensions", "SET NULL");
            await AddReferenceColumnAsync(connection, "PolyhedronDimensions", "SetDimensionsId", "ShapeDimensions", "SET NULL");

            var unnamedSolidCount = await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM SolidDimensions WHERE Name IS NULL OR length(trim(Name)) = 0");
            if (unnamedSolidCount != 0)
                throw new InvalidOperationException($"Cannot establish object ownership: {unnamedSolidCount} solid(s) have no object name.");
            var invalidAxisCount = await connection.ExecuteScalarAsync<long>(@"
                SELECT COUNT(*) FROM SolidDimensions s
                WHERE s.AxisDimensionsId IS NOT NULL AND NOT EXISTS
                    (SELECT 1 FROM AxisDimensions a WHERE a.Id = s.AxisDimensionsId)");
            if (invalidAxisCount != 0)
                throw new InvalidOperationException($"Cannot establish object axes: {invalidAxisCount} solid(s) reference a missing axis.");

            await connection.ExecuteAsync(@"
                INSERT OR IGNORE INTO ObjectDimensions (ObjectName)
                    SELECT Name FROM SolidDimensions WHERE Name IS NOT NULL AND length(trim(Name)) > 0
                    UNION SELECT ObjectName FROM ModuleDimensions WHERE ObjectName IS NOT NULL AND length(trim(ObjectName)) > 0;
                UPDATE SolidDimensions SET ObjectDimensionsId = (SELECT Id FROM ObjectDimensions WHERE ObjectName = SolidDimensions.Name)
                    WHERE ObjectDimensionsId IS NOT (SELECT Id FROM ObjectDimensions WHERE ObjectName = SolidDimensions.Name);
                UPDATE ModuleDimensions SET ObjectDimensionsId = (SELECT Id FROM ObjectDimensions WHERE ObjectName = ModuleDimensions.ObjectName)
                    WHERE ObjectDimensionsId IS NULL;
                UPDATE PolyhedronDimensions SET SetDimensionsId = (SELECT Id FROM ShapeDimensions WHERE Name = PolyhedronDimensions.Name)
                    WHERE SetDimensionsId IS NULL;

                UPDATE ObjectDimensions SET AxisDimensionsId = (
                    SELECT s.AxisDimensionsId FROM SolidDimensions s WHERE s.ObjectDimensionsId = ObjectDimensions.Id
                    ORDER BY s.CreatedAt DESC, s.Id DESC LIMIT 1)
                    WHERE EXISTS (SELECT 1 FROM SolidDimensions s WHERE s.ObjectDimensionsId = ObjectDimensions.Id)
                    AND AxisDimensionsId IS NOT (
                        SELECT s.AxisDimensionsId FROM SolidDimensions s WHERE s.ObjectDimensionsId = ObjectDimensions.Id
                        ORDER BY s.CreatedAt DESC, s.Id DESC LIMIT 1);
                UPDATE SolidDimensions SET AxisDimensionsId = (
                    SELECT o.AxisDimensionsId FROM ObjectDimensions o WHERE o.Id = SolidDimensions.ObjectDimensionsId)
                    WHERE AxisDimensionsId IS NOT (
                        SELECT o.AxisDimensionsId FROM ObjectDimensions o WHERE o.Id = SolidDimensions.ObjectDimensionsId);

                DROP TRIGGER IF EXISTS SolidDimensions_Object_Insert;
                DROP TRIGGER IF EXISTS SolidDimensions_Object_Update;
                DROP TRIGGER IF EXISTS SolidDimensions_Object_EmptyName;
                CREATE TRIGGER IF NOT EXISTS SolidDimensions_Object_Required_Insert BEFORE INSERT ON SolidDimensions
                WHEN NEW.Name IS NULL OR length(trim(NEW.Name)) = 0 OR NOT EXISTS
                    (SELECT 1 FROM ObjectDimensions WHERE Id = NEW.ObjectDimensionsId AND ObjectName = NEW.Name)
                BEGIN
                    SELECT RAISE(ABORT, 'Solid must reference an object with the same name');
                END;
                CREATE TRIGGER IF NOT EXISTS SolidDimensions_Object_Required_Update BEFORE UPDATE OF Name, ObjectDimensionsId ON SolidDimensions
                WHEN NEW.Name IS NULL OR length(trim(NEW.Name)) = 0 OR NOT EXISTS
                    (SELECT 1 FROM ObjectDimensions WHERE Id = NEW.ObjectDimensionsId AND ObjectName = NEW.Name)
                BEGIN
                    SELECT RAISE(ABORT, 'Solid must reference an object with the same name');
                END;
                CREATE TRIGGER IF NOT EXISTS ObjectDimensions_Solid_Restrict_Delete BEFORE DELETE ON ObjectDimensions
                WHEN EXISTS (SELECT 1 FROM SolidDimensions WHERE ObjectDimensionsId = OLD.Id)
                BEGIN
                    SELECT RAISE(ABORT, 'Cannot delete an object that has solids');
                END;
                CREATE TRIGGER IF NOT EXISTS ObjectDimensions_Solid_Restrict_Update BEFORE UPDATE OF Id, ObjectName ON ObjectDimensions
                WHEN (NEW.Id != OLD.Id OR NEW.ObjectName != OLD.ObjectName)
                    AND EXISTS (SELECT 1 FROM SolidDimensions WHERE ObjectDimensionsId = OLD.Id)
                BEGIN
                    SELECT RAISE(ABORT, 'Cannot change the identity of an object that has solids');
                END;
                CREATE TRIGGER IF NOT EXISTS SolidDimensions_Axis_Validate_Insert BEFORE INSERT ON SolidDimensions
                WHEN NEW.AxisDimensionsId IS NOT NULL AND NOT EXISTS
                    (SELECT 1 FROM AxisDimensions WHERE Id = NEW.AxisDimensionsId)
                BEGIN
                    SELECT RAISE(ABORT, 'Solid references a missing axis');
                END;
                CREATE TRIGGER IF NOT EXISTS SolidDimensions_Axis_Validate_Update BEFORE UPDATE OF AxisDimensionsId ON SolidDimensions
                WHEN NEW.AxisDimensionsId IS NOT NULL AND NOT EXISTS
                    (SELECT 1 FROM AxisDimensions WHERE Id = NEW.AxisDimensionsId)
                BEGIN
                    SELECT RAISE(ABORT, 'Solid references a missing axis');
                END;
                CREATE TRIGGER IF NOT EXISTS SolidDimensions_Axis_Insert AFTER INSERT ON SolidDimensions
                BEGIN
                    UPDATE ObjectDimensions SET AxisDimensionsId = NEW.AxisDimensionsId
                        WHERE Id = NEW.ObjectDimensionsId AND NEW.AxisDimensionsId IS NOT NULL
                        AND AxisDimensionsId IS NOT NEW.AxisDimensionsId;
                    UPDATE SolidDimensions SET AxisDimensionsId = (SELECT AxisDimensionsId FROM ObjectDimensions WHERE Id = NEW.ObjectDimensionsId)
                        WHERE Id = NEW.Id AND AxisDimensionsId IS NOT (SELECT AxisDimensionsId FROM ObjectDimensions WHERE Id = NEW.ObjectDimensionsId);
                END;
                CREATE TRIGGER IF NOT EXISTS SolidDimensions_Axis_Update AFTER UPDATE OF AxisDimensionsId, ObjectDimensionsId ON SolidDimensions
                WHEN OLD.AxisDimensionsId IS NOT NEW.AxisDimensionsId OR OLD.ObjectDimensionsId IS NOT NEW.ObjectDimensionsId
                BEGIN
                    UPDATE ObjectDimensions SET AxisDimensionsId = NEW.AxisDimensionsId
                        WHERE Id = NEW.ObjectDimensionsId AND AxisDimensionsId IS NOT NEW.AxisDimensionsId;
                END;
                CREATE TRIGGER IF NOT EXISTS ObjectDimensions_Axis_Validate_Insert BEFORE INSERT ON ObjectDimensions
                WHEN NEW.AxisDimensionsId IS NOT NULL AND NOT EXISTS
                    (SELECT 1 FROM AxisDimensions WHERE Id = NEW.AxisDimensionsId)
                BEGIN
                    SELECT RAISE(ABORT, 'Object references a missing axis');
                END;
                CREATE TRIGGER IF NOT EXISTS ObjectDimensions_Axis_Validate_Update BEFORE UPDATE OF AxisDimensionsId ON ObjectDimensions
                WHEN NEW.AxisDimensionsId IS NOT NULL AND NOT EXISTS
                    (SELECT 1 FROM AxisDimensions WHERE Id = NEW.AxisDimensionsId)
                BEGIN
                    SELECT RAISE(ABORT, 'Object references a missing axis');
                END;
                CREATE TRIGGER IF NOT EXISTS ObjectDimensions_Axis_Update AFTER UPDATE OF AxisDimensionsId ON ObjectDimensions
                WHEN OLD.AxisDimensionsId IS NOT NEW.AxisDimensionsId
                BEGIN
                    UPDATE SolidDimensions SET AxisDimensionsId = NEW.AxisDimensionsId
                        WHERE ObjectDimensionsId = NEW.Id AND AxisDimensionsId IS NOT NEW.AxisDimensionsId;
                END;
                CREATE TRIGGER IF NOT EXISTS AxisDimensions_Object_Clear BEFORE DELETE ON AxisDimensions
                BEGIN
                    UPDATE ObjectDimensions SET AxisDimensionsId = NULL WHERE AxisDimensionsId = OLD.Id;
                END;
                CREATE TRIGGER IF NOT EXISTS ModuleDimensions_Object_Insert AFTER INSERT ON ModuleDimensions
                WHEN NEW.ObjectName IS NOT NULL AND length(trim(NEW.ObjectName)) > 0
                BEGIN
                    INSERT OR IGNORE INTO ObjectDimensions (ObjectName) VALUES (NEW.ObjectName);
                    UPDATE ModuleDimensions SET ObjectDimensionsId = (SELECT Id FROM ObjectDimensions WHERE ObjectName = NEW.ObjectName) WHERE Id = NEW.Id;
                END;
                CREATE TRIGGER IF NOT EXISTS ModuleDimensions_Object_Update AFTER UPDATE OF ObjectName ON ModuleDimensions
                WHEN NEW.ObjectName IS NOT NULL AND length(trim(NEW.ObjectName)) > 0
                BEGIN
                    INSERT OR IGNORE INTO ObjectDimensions (ObjectName) VALUES (NEW.ObjectName);
                    UPDATE ModuleDimensions SET ObjectDimensionsId = (SELECT Id FROM ObjectDimensions WHERE ObjectName = NEW.ObjectName) WHERE Id = NEW.Id;
                END;
                CREATE TRIGGER IF NOT EXISTS PolyhedronDimensions_Shape_Insert AFTER INSERT ON PolyhedronDimensions
                BEGIN
                    UPDATE PolyhedronDimensions SET SetDimensionsId = (SELECT Id FROM ShapeDimensions WHERE Name = NEW.Name) WHERE Id = NEW.Id;
                END;
                CREATE TRIGGER IF NOT EXISTS PolyhedronDimensions_Shape_Update AFTER UPDATE OF Name ON PolyhedronDimensions
                BEGIN
                    UPDATE PolyhedronDimensions SET SetDimensionsId = (SELECT Id FROM ShapeDimensions WHERE Name = NEW.Name) WHERE Id = NEW.Id;
                END;
                CREATE TRIGGER IF NOT EXISTS ShapeDimensions_Polyhedron_Insert AFTER INSERT ON ShapeDimensions
                BEGIN
                    UPDATE PolyhedronDimensions SET SetDimensionsId = NEW.Id WHERE Name = NEW.Name;
                END;");
        }

        private static async Task AddReferenceColumnAsync(SqliteConnection connection, string table, string column, string parent, string onDelete)
        {
            var columns = await connection.QueryAsync<string>($"SELECT name FROM pragma_table_info('{table}')");
            if (!columns.Contains(column, StringComparer.OrdinalIgnoreCase))
                await connection.ExecuteAsync($"ALTER TABLE {table} ADD COLUMN {column} INTEGER REFERENCES {parent}(Id) ON DELETE {onDelete}");
        }

        public static async Task<int> GetOrCreateIdAsync(SqliteConnection connection, string name, SqliteTransaction transaction)
        {
            await connection.ExecuteAsync("INSERT INTO ObjectDimensions (ObjectName) VALUES (@Name) ON CONFLICT(ObjectName) DO NOTHING", new { Name = name }, transaction);
            return await connection.QuerySingleAsync<int>("SELECT Id FROM ObjectDimensions WHERE ObjectName = @Name", new { Name = name }, transaction);
        }

        public static Task<ObjectDimensions?> GetByIdAsync(this ObjectDimensions _, SqliteConnection connection, int id) =>
            connection.QuerySingleOrDefaultAsync<ObjectDimensions>("SELECT * FROM ObjectDimensions WHERE Id = @Id", new { Id = id });

        public static Task<ObjectDimensions?> GetByNameAsync(this ObjectDimensions _, SqliteConnection connection, string name) =>
            connection.QuerySingleOrDefaultAsync<ObjectDimensions>("SELECT * FROM ObjectDimensions WHERE ObjectName = @Name", new { Name = name });
    }
}
