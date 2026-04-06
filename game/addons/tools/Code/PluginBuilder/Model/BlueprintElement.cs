namespace Editor.PluginBuilder;

/// <summary>
/// Represents one UI element in the blueprint tree (property, group, header, etc.).
/// </summary>
public class BlueprintElement
{
	public string Id { get; set; } = Guid.NewGuid().ToString();
	public string Name { get; set; } = "";
	public ElementType ElementType { get; set; } = ElementType.Property;
	public PropertyType PropertyType { get; set; } = PropertyType.String;
	public int Order { get; set; }

	/// <summary>
	/// Maps attribute names (e.g. "Range", "Group", "ShowIf") to their parameter dictionaries.
	/// Each value is a Dictionary&lt;string, object&gt; of parameter name → value.
	/// </summary>
	public Dictionary<string, Dictionary<string, object>> Attributes { get; set; } = new();

	public List<BlueprintElement> Children { get; set; } = new();

	public DefaultValueContainer DefaultValue { get; set; }

	/// <summary>
	/// For CustomEnum/CustomStruct types — the name of the definition.
	/// </summary>
	public string CustomTypeName { get; set; } = "";

	/// <summary>
	/// Per-property AI hint — describes what this property should do in code.
	/// </summary>
	public string AiHint { get; set; } = "";

	/// <summary>
	/// Help text — maps to [Description] attribute, shown as tooltip in inspector.
	/// </summary>
	public string HelpText { get; set; } = "";

	/// <summary>
	/// Per-attribute positioning relative to the element (e.g. icon left, header above).
	/// Maps attribute name → position. Attributes not listed default to Above.
	/// </summary>
	public Dictionary<string, AttributePosition> AttributePositions { get; set; } = new();

	/// <summary>
	/// Whether this element is hidden in the preview (eyeball toggle).
	/// </summary>
	public bool Hidden { get; set; }

	/// <summary>
	/// For Space elements: direction of the spacer.
	/// </summary>
	public SpacerDirection SpacerDirection { get; set; } = SpacerDirection.Vertical;

	/// <summary>
	/// For Space elements: vertical space in pixels (height).
	/// </summary>
	public float SpacerSize { get; set; } = 8f;

	/// <summary>
	/// For Space elements: horizontal space in pixels (width).
	/// </summary>
	public float SpacerWidth { get; set; } = 0f;

	/// <summary>
	/// Nested attribute hierarchy. Maps parent attribute name → ordered list of child attribute names.
	/// Child attributes render within the parent attribute's visual area.
	/// </summary>
	public Dictionary<string, List<string>> AttributeChildren { get; set; } = new();

	public BlueprintElement Clone()
	{
		var json = JsonSerializer.Serialize( this, BlueprintSerializer.Options );
		var clone = JsonSerializer.Deserialize<BlueprintElement>( json, BlueprintSerializer.Options );
		clone.Id = Guid.NewGuid().ToString();
		return clone;
	}
}

public enum ElementType
{
	Property,
	Group,
	ToggleGroup,
	Feature,
	Header,
	Space,
	Button,
	Separator,
	InfoBox,
	Struct
}

public enum PropertyType
{
	String,
	Int,
	Float,
	Bool,
	Color,
	Vector3,
	Vector2,
	Angles,
	Enum,
	Model,
	Material,
	GameObject,
	PhysicsBody,
	CustomEnum,
	CustomStruct
}

public enum AttributePosition
{
	Above,
	Below,
	Left,
	Right,
	Hidden
}

public enum SpacerDirection
{
	Vertical,
	Horizontal
}
