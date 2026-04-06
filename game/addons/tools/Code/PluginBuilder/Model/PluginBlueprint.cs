using System.Text.Json.Serialization;

namespace Editor.PluginBuilder;

/// <summary>
/// Root data model representing an entire plugin blueprint design.
/// </summary>
public class PluginBlueprint
{
	public const int CurrentSchemaVersion = 1;

	public string Name { get; set; } = "New Blueprint";
	public BlueprintType Type { get; set; } = BlueprintType.Component;
	public string Description { get; set; } = "";
	public string AiInstructions { get; set; } = "";
	public string Icon { get; set; } = "";
	public string BaseClass { get; set; } = "Component";
	public int SchemaVersion { get; set; } = CurrentSchemaVersion;
	public string TargetAddon { get; set; } = "";
	public string LastGeneratedPath { get; set; } = "";
	public DateTime LastModified { get; set; } = DateTime.UtcNow;

	public List<BlueprintElement> Elements { get; set; } = new();
	public List<CustomEnumDefinition> CustomEnums { get; set; } = new();
	public List<CustomStructDefinition> CustomStructs { get; set; } = new();
	public List<ExportHistoryEntry> ExportHistory { get; set; } = new();

	public PluginBlueprint Clone()
	{
		var json = JsonSerializer.Serialize( this, BlueprintSerializer.Options );
		return JsonSerializer.Deserialize<PluginBlueprint>( json, BlueprintSerializer.Options );
	}
}

public enum BlueprintType
{
	Component,
	Addon,
	Tool,
	EditorWindow
}

public class ExportHistoryEntry
{
	public DateTime Timestamp { get; set; }
	public string TargetPath { get; set; } = "";
	public List<string> Formats { get; set; } = new();
	public int SchemaVersion { get; set; }
}

/// <summary>
/// Shared serializer options for blueprint JSON roundtrips.
/// </summary>
public static class BlueprintSerializer
{
	public static JsonSerializerOptions Options { get; } = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};
}
