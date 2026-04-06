namespace Editor.PluginBuilder;

/// <summary>
/// Static catalog of all S&Box inspector attributes with their metadata.
/// </summary>
public static class AttributeCatalog
{
	private static List<AttributeDefinition> _definitions;

	public static IReadOnlyList<AttributeDefinition> All
	{
		get
		{
			_definitions ??= BuildCatalog();
			return _definitions;
		}
	}

	public static IEnumerable<AttributeDefinition> GetByCategory( AttributeCategory category )
		=> All.Where( a => a.Category == category );

	public static IEnumerable<AttributeDefinition> GetApplicable( PropertyType propType, ElementType elemType )
		=> All.Where( a =>
			(a.ApplicableTypes.Count == 0 || a.ApplicableTypes.Contains( propType )) &&
			(a.ApplicableElementTypes.Count == 0 || a.ApplicableElementTypes.Contains( elemType ))
		);

	public static AttributeDefinition Get( string name )
		=> All.FirstOrDefault( a => a.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) );

	private static List<AttributeDefinition> BuildCatalog()
	{
		var allPropertyTypes = System.Enum.GetValues<PropertyType>().ToList();
		var numericTypes = new List<PropertyType> { PropertyType.Int, PropertyType.Float };
		var stringTypes = new List<PropertyType> { PropertyType.String };
		var enumTypes = new List<PropertyType> { PropertyType.Enum, PropertyType.CustomEnum };
		var colorTypes = new List<PropertyType> { PropertyType.Color };

		return new List<AttributeDefinition>
		{
			// Layout
			new()
			{
				Name = "Group", Category = AttributeCategory.Layout, Icon = "folder",
				Description = "Groups properties under a collapsible header.",
				ApplicableElementTypes = new() { ElementType.Property },
				Parameters = new() { new() { Name = "name", ParamType = "string", Required = true, Description = "Group display name" } },
				CodeExample = "[Group( \"Vitals\" )]"
			},
			new()
			{
				Name = "ToggleGroup", Category = AttributeCategory.Layout, Icon = "toggle_on",
				Description = "A group with a checkbox that enables/disables its children.",
				ApplicableTypes = new() { PropertyType.Bool },
				Parameters = new() { new() { Name = "name", ParamType = "string", Required = true, Description = "Group name" } },
				CodeExample = "[ToggleGroup( \"Shields\" )]"
			},
			new()
			{
				Name = "Feature", Category = AttributeCategory.Layout, Icon = "tab",
				Description = "Creates a tabbed feature section.",
				ApplicableElementTypes = new() { ElementType.Property },
				Parameters = new() { new() { Name = "name", ParamType = "string", Required = true, Description = "Feature tab name" } },
				CodeExample = "[Feature( \"Advanced\" )]"
			},
			new()
			{
				Name = "FeatureEnabled", Category = AttributeCategory.Layout, Icon = "check_circle",
				Description = "Bool that controls whether a Feature tab is shown.",
				ApplicableTypes = new() { PropertyType.Bool },
				Parameters = new() { new() { Name = "name", ParamType = "string", Required = true, Description = "Feature name to control" } },
				CodeExample = "[FeatureEnabled( \"Advanced\" )]"
			},
			new()
			{
				Name = "Header", Category = AttributeCategory.Layout, Icon = "title",
				Description = "Adds a bold header label above the property.",
				Parameters = new() { new() { Name = "text", ParamType = "string", Required = true } },
				CodeExample = "[Header( \"Combat Settings\" )]"
			},
			new()
			{
				Name = "Space", Category = AttributeCategory.Layout, Icon = "space_bar",
				Description = "Adds space before the property.",
				Parameters = new()
				{
					new() { Name = "height", ParamType = "float", Required = false, DefaultValue = "8", Description = "Vertical space in pixels" },
					new() { Name = "width", ParamType = "float", Required = false, DefaultValue = "0", Description = "Horizontal space in pixels" }
				},
				CodeExample = "[Space( 16 )]"
			},
			new()
			{
				Name = "Order", Category = AttributeCategory.Layout, Icon = "sort",
				Description = "Controls the display order of the property in the inspector.",
				Parameters = new() { new() { Name = "order", ParamType = "int", Required = true } },
				CodeExample = "[Order( 10 )]"
			},
			new()
			{
				Name = "WideMode", Category = AttributeCategory.Layout, Icon = "width_wide",
				Description = "Property value stretches full width below its label.",
				Parameters = new(),
				CodeExample = "[WideMode]"
			},

			// Display
			new()
			{
				Name = "Title", Category = AttributeCategory.Display, Icon = "label",
				Description = "Sets a custom display name for the property.",
				Parameters = new() { new() { Name = "text", ParamType = "string", Required = true } },
				CodeExample = "[Title( \"Hit Points\" )]"
			},
			new()
			{
				Name = "Description", Category = AttributeCategory.Display, Icon = "info",
				Description = "Tooltip text shown when hovering the property.",
				Parameters = new() { new() { Name = "text", ParamType = "string", Required = true } },
				CodeExample = "[Description( \"Maximum health\" )]"
			},
			new()
			{
				Name = "Icon", Category = AttributeCategory.Display, Icon = "image",
				Description = "Sets an icon for the property or component.",
				Parameters = new() { new() { Name = "name", ParamType = "string", Required = true, Description = "Material icon name" } },
				CodeExample = "[Icon( \"favorite\" )]"
			},
			new()
			{
				Name = "ReadOnly", Category = AttributeCategory.Display, Icon = "lock",
				Description = "Displays the property as non-editable.",
				Parameters = new(),
				CodeExample = "[ReadOnly]"
			},
			new()
			{
				Name = "Hide", Category = AttributeCategory.Display, Icon = "visibility_off",
				Description = "Hides the property from the inspector entirely.",
				Parameters = new(),
				CodeExample = "[Hide]"
			},
			new()
			{
				Name = "InfoBox", Category = AttributeCategory.Display, Icon = "info_outline",
				Description = "Shows an information box above the property.",
				Parameters = new()
				{
					new() { Name = "text", ParamType = "string", Required = true },
					new() { Name = "type", ParamType = "string", Required = false, DefaultValue = "Info", Description = "Info, Warning, or Error" }
				},
				CodeExample = "[InfoBox( \"This value is synced over the network\", \"Warning\" )]"
			},

			// Conditional
			new()
			{
				Name = "ShowIf", Category = AttributeCategory.Conditional, Icon = "visibility",
				Description = "Only shows this property when the target property has the specified value.",
				Parameters = new()
				{
					new() { Name = "property", ParamType = "string", Required = true, Description = "Name of the controlling property" },
					new() { Name = "value", ParamType = "object", Required = true, Description = "Value that makes this visible" }
				},
				CodeExample = "[ShowIf( \"HasShields\", true )]"
			},
			new()
			{
				Name = "HideIf", Category = AttributeCategory.Conditional, Icon = "visibility_off",
				Description = "Hides this property when the target property has the specified value.",
				Parameters = new()
				{
					new() { Name = "property", ParamType = "string", Required = true },
					new() { Name = "value", ParamType = "object", Required = true }
				},
				CodeExample = "[HideIf( \"IsDisabled\", true )]"
			},

			// Numeric
			new()
			{
				Name = "Range", Category = AttributeCategory.Numeric, Icon = "linear_scale",
				Description = "Adds a slider constrained to min/max values.",
				ApplicableTypes = numericTypes,
				Parameters = new()
				{
					new() { Name = "min", ParamType = "float", Required = true },
					new() { Name = "max", ParamType = "float", Required = true },
					new() { Name = "step", ParamType = "float", Required = false, DefaultValue = "1" }
				},
				CodeExample = "[Range( 0f, 100f, 1f )]"
			},
			new()
			{
				Name = "MinMax", Category = AttributeCategory.Numeric, Icon = "swap_horiz",
				Description = "Dual-handle slider for min/max range on Vector2.",
				ApplicableTypes = new() { PropertyType.Vector2 },
				Parameters = new()
				{
					new() { Name = "min", ParamType = "float", Required = true },
					new() { Name = "max", ParamType = "float", Required = true }
				},
				CodeExample = "[MinMax( 0f, 100f )]"
			},

			// String
			new()
			{
				Name = "TextArea", Category = AttributeCategory.String, Icon = "notes",
				Description = "Renders as a multiline text editor.",
				ApplicableTypes = stringTypes,
				Parameters = new(),
				CodeExample = "[TextArea]"
			},
			new()
			{
				Name = "Placeholder", Category = AttributeCategory.String, Icon = "text_fields",
				Description = "Shows grayed-out hint text when empty.",
				ApplicableTypes = stringTypes,
				Parameters = new() { new() { Name = "text", ParamType = "string", Required = true } },
				CodeExample = "[Placeholder( \"Enter a name...\" )]"
			},
			new()
			{
				Name = "FontName", Category = AttributeCategory.String, Icon = "font_download",
				Description = "Renders as a font picker dropdown.",
				ApplicableTypes = stringTypes,
				Parameters = new(),
				CodeExample = "[FontName]"
			},
			new()
			{
				Name = "InputAction", Category = AttributeCategory.String, Icon = "gamepad",
				Description = "Renders as an input action selector.",
				ApplicableTypes = stringTypes,
				Parameters = new(),
				CodeExample = "[InputAction]"
			},
			new()
			{
				Name = "ImageAssetPath", Category = AttributeCategory.String, Icon = "photo",
				Description = "Renders as an image asset picker with thumbnail.",
				ApplicableTypes = stringTypes,
				Parameters = new(),
				CodeExample = "[ImageAssetPath]"
			},
			new()
			{
				Name = "IconName", Category = AttributeCategory.String, Icon = "emoji_symbols",
				Description = "Renders as a Material Icon selector grid.",
				ApplicableTypes = stringTypes,
				Parameters = new(),
				CodeExample = "[IconName]"
			},
			new()
			{
				Name = "AssetPath", Category = AttributeCategory.String, Icon = "attach_file",
				Description = "Renders as a generic asset picker.",
				ApplicableTypes = stringTypes,
				Parameters = new() { new() { Name = "extension", ParamType = "string", Required = false, Description = "Filter by file extension" } },
				CodeExample = "[AssetPath]"
			},
			new()
			{
				Name = "FilePath", Category = AttributeCategory.String, Icon = "folder_open",
				Description = "Renders as a file browser.",
				ApplicableTypes = stringTypes,
				Parameters = new(),
				CodeExample = "[FilePath]"
			},
			new()
			{
				Name = "ResourceType", Category = AttributeCategory.String, Icon = "inventory_2",
				Description = "Specifies a resource type for asset picker filtering.",
				ApplicableTypes = stringTypes,
				Parameters = new() { new() { Name = "type", ParamType = "string", Required = true } },
				CodeExample = "[ResourceType( \"vmdl\" )]"
			},

			// Object
			new()
			{
				Name = "KeyProperty", Category = AttributeCategory.Object, Icon = "key",
				Description = "Marks a struct field as the key shown in the collapsed inline view.",
				Parameters = new(),
				CodeExample = "[KeyProperty]"
			},
			new()
			{
				Name = "InlineEditor", Category = AttributeCategory.Object, Icon = "open_in_full",
				Description = "Shows the object's properties expanded inline in the inspector.",
				Parameters = new(),
				CodeExample = "[InlineEditor]"
			},
			new()
			{
				Name = "RequireComponent", Category = AttributeCategory.Object, Icon = "link",
				Description = "Specifies that a component dependency is required.",
				Parameters = new() { new() { Name = "type", ParamType = "string", Required = true, Description = "Component type name" } },
				CodeExample = "[RequireComponent( typeof( Rigidbody ) )]"
			},

			// Enum
			new()
			{
				Name = "EnumButtonGroup", Category = AttributeCategory.Enum, Icon = "view_column",
				Description = "Shows enum values as a horizontal button bar instead of a dropdown.",
				ApplicableTypes = enumTypes,
				Parameters = new(),
				CodeExample = "[EnumButtonGroup]"
			},
			new()
			{
				Name = "BitFlags", Category = AttributeCategory.Enum, Icon = "checklist",
				Description = "Shows [Flags] enum as multi-select checkboxes.",
				ApplicableTypes = enumTypes,
				Parameters = new(),
				CodeExample = "[BitFlags]"
			},

			// Interaction
			new()
			{
				Name = "Button", Category = AttributeCategory.Interaction, Icon = "smart_button",
				Description = "Adds a clickable button that calls a method.",
				Parameters = new()
				{
					new() { Name = "text", ParamType = "string", Required = false, Description = "Button label" },
					new() { Name = "icon", ParamType = "string", Required = false, Description = "Material icon" }
				},
				CodeExample = "[Button( \"Reset\" )]"
			},
			new()
			{
				Name = "Change", Category = AttributeCategory.Interaction, Icon = "sync",
				Description = "Calls a method when the property value changes.",
				Parameters = new() { new() { Name = "callback", ParamType = "string", Required = false, Description = "Method name to call" } },
				CodeExample = "[Change( \"OnHealthChanged\" )]"
			},

			// Color
			new()
			{
				Name = "ColorUsage", Category = AttributeCategory.Color, Icon = "palette",
				Description = "Configures the color picker (alpha, HDR).",
				ApplicableTypes = colorTypes,
				Parameters = new()
				{
					new() { Name = "showAlpha", ParamType = "bool", Required = false, DefaultValue = "true" },
					new() { Name = "hdr", ParamType = "bool", Required = false, DefaultValue = "false" }
				},
				CodeExample = "[ColorUsage( true, true )]"
			},
			new()
			{
				Name = "Tint", Category = AttributeCategory.Color, Icon = "format_paint",
				Description = "Marks a color as a tint (simplified picker).",
				ApplicableTypes = colorTypes,
				Parameters = new(),
				CodeExample = "[Tint]"
			},

			// Network
			new()
			{
				Name = "Sync", Category = AttributeCategory.Network, Icon = "cloud_sync",
				Description = "Synchronizes this property across the network.",
				Parameters = new() { new() { Name = "flags", ParamType = "string", Required = false, Description = "SyncFlags" } },
				CodeExample = "[Sync]"
			},
		};
	}
}
