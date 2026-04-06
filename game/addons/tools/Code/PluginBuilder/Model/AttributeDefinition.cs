namespace Editor.PluginBuilder;

/// <summary>
/// Metadata definition for a single S&Box inspector attribute.
/// Used by the attribute palette and validation system.
/// </summary>
public class AttributeDefinition
{
	public string Name { get; set; }
	public string Description { get; set; }
	public string Icon { get; set; }
	public AttributeCategory Category { get; set; }

	/// <summary>
	/// Which property types this attribute can be applied to. Empty = all types.
	/// </summary>
	public List<PropertyType> ApplicableTypes { get; set; } = new();

	/// <summary>
	/// Which element types this attribute applies to. Empty = Property only.
	/// </summary>
	public List<ElementType> ApplicableElementTypes { get; set; } = new();

	public List<AttributeParameter> Parameters { get; set; } = new();

	/// <summary>
	/// Example code snippet showing usage (e.g. [Range(0, 100)])
	/// </summary>
	public string CodeExample { get; set; } = "";
}

public class AttributeParameter
{
	public string Name { get; set; }
	public string ParamType { get; set; } = "string"; // string, int, float, bool
	public string DefaultValue { get; set; }
	public bool Required { get; set; }
	public string Description { get; set; } = "";
}

public enum AttributeCategory
{
	Layout,
	Display,
	Conditional,
	Numeric,
	String,
	Object,
	Enum,
	Interaction,
	Validation,
	Color,
	Network
}
