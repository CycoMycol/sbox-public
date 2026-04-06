namespace Editor.PluginBuilder;

/// <summary>
/// User-defined enum model — stores name, values, and flags info.
/// </summary>
public class CustomEnumDefinition
{
	public string Name { get; set; } = "NewEnum";
	public string Description { get; set; } = "";
	public bool IsFlags { get; set; }
	public List<EnumValueEntry> Values { get; set; } = new();
}

public class EnumValueEntry
{
	public string Name { get; set; } = "";
	public int IntValue { get; set; }
	public string Icon { get; set; } = "";
	public string Description { get; set; } = "";
}
