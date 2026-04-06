namespace Editor.PluginBuilder;

/// <summary>
/// Handles blueprint schema version migration on load.
/// When the blueprint format changes, add a migration function here.
/// </summary>
public static class SchemaMigrator
{
	/// <summary>
	/// Migrates raw JSON to the current schema version before deserialization.
	/// Returns the migrated JSON string.
	/// </summary>
	public static string Migrate( string json, out List<string> migrationLog )
	{
		migrationLog = new List<string>();

		using var doc = JsonDocument.Parse( json );
		var root = doc.RootElement;

		int version = 0;
		if ( root.TryGetProperty( "schemaVersion", out var sv ) || root.TryGetProperty( "SchemaVersion", out sv ) )
			version = sv.GetInt32();

		if ( version >= PluginBlueprint.CurrentSchemaVersion )
			return json;

		// Clone into a mutable dictionary for migration
		var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>( json );

		if ( version < 1 )
		{
			// v0 → v1: Add schemaVersion field if missing
			migrationLog.Add( "v0 → v1: Added schemaVersion field" );
		}

		// Future migrations go here:
		// if ( version < 2 ) { ... migrationLog.Add( "v1 → v2: ..." ); }

		// Update schema version
		dict["schemaVersion"] = JsonSerializer.SerializeToElement( PluginBlueprint.CurrentSchemaVersion );

		return JsonSerializer.Serialize( dict, new JsonSerializerOptions { WriteIndented = true } );
	}
}
