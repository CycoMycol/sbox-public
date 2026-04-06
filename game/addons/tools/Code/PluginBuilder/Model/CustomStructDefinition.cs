namespace Editor.PluginBuilder;

/// <summary>
/// User-defined struct model — stores name and child properties with KeyProperty support.
/// </summary>
public class CustomStructDefinition
{
	public string Name { get; set; } = "NewStruct";
	public string Description { get; set; } = "";
	public List<BlueprintElement> Fields { get; set; } = new();
}
