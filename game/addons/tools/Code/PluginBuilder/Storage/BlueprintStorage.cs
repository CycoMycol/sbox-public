using System.IO;

using System.Text.Json.Serialization;

namespace Editor.PluginBuilder;

/// <summary>
/// Full save/load system for blueprints.
/// Auto-save is inactivity-based (60s idle) with atomic file writes.
/// </summary>
public class BlueprintStorage
{
	private static readonly string BlueprintsDir = Path.Combine( "assets", "blueprints" );
	private static readonly string AutosaveDir = Path.Combine( BlueprintsDir, ".autosave" );

	/// <summary>
	/// Saves a blueprint to a JSON file at the specified path.
	/// Uses atomic write (.tmp → rename) to prevent corruption.
	/// </summary>
	public static void Save( PluginBlueprint blueprint, string filePath )
	{
		blueprint.LastModified = DateTime.UtcNow;

		var json = JsonSerializer.Serialize( blueprint, new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		} );

		// Atomic write: write to .tmp then rename
		var dir = Path.GetDirectoryName( filePath );
		if ( !string.IsNullOrEmpty( dir ) )
			Directory.CreateDirectory( dir );

		var tmpPath = filePath + ".tmp";
		File.WriteAllText( tmpPath, json );
		File.Move( tmpPath, filePath, overwrite: true );
	}

	/// <summary>
	/// Loads a blueprint from a JSON file, running schema migration if needed.
	/// </summary>
	public static PluginBlueprint Load( string filePath )
	{
		var json = File.ReadAllText( filePath );

		// Run migration
		json = SchemaMigrator.Migrate( json, out var migrationLog );

		if ( migrationLog.Count > 0 )
		{
			foreach ( var entry in migrationLog )
				Log.Info( $"Plugin Builder migration: {entry}" );
		}

		return JsonSerializer.Deserialize<PluginBlueprint>( json, new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		} );
	}

	/// <summary>
	/// Saves an autosave snapshot for the given blueprint.
	/// </summary>
	public static void AutoSave( PluginBlueprint blueprint, string basePath )
	{
		var dir = Path.Combine( basePath, AutosaveDir );
		Directory.CreateDirectory( dir );

		var sanitized = CodeGenerator.SanitizeName( blueprint.Name );
		var filePath = Path.Combine( dir, $"{sanitized}.autosave.json" );

		Save( blueprint, filePath );
	}

	/// <summary>
	/// Returns paths to all .pluginbuilder.json files in the blueprints directory.
	/// </summary>
	public static string[] GetSavedBlueprints( string basePath )
	{
		var dir = Path.Combine( basePath, BlueprintsDir );
		if ( !Directory.Exists( dir ) ) return Array.Empty<string>();

		return Directory.GetFiles( dir, "*.pluginbuilder.json", SearchOption.TopDirectoryOnly );
	}
}
