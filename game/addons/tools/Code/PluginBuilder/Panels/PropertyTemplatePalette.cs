using Sandbox.UI;

namespace Editor.PluginBuilder;

/// <summary>
/// Pre-built property templates — drag-and-drop common property patterns.
/// Health Bar, Color Picker, Weapon Stats, etc.
/// </summary>
public class PropertyTemplatePalette : Widget
{
	private readonly PluginBuilderDock _dock;

	public PropertyTemplatePalette( Widget parent, PluginBuilderDock dock ) : base( parent )
	{
		_dock = dock;

		Layout = Layout.Column();
		Layout.Spacing = 4;
		Layout.Margin = 8;

		var header = new Label( "Property Templates", this );
		header.SetStyles( "font-weight: bold; font-size: 14px;" );
		Layout.Add( header );

		var scroll = new ScrollArea( this );
		scroll.Canvas = new Widget( scroll );
		scroll.Canvas.Layout = Layout.Column();
		scroll.Canvas.Layout.Spacing = 4;

		AddTemplate( scroll.Canvas, "Health Bar", "favorite", "Float property with [Range(0,100)] and [Title]", CreateHealthBar );
		AddTemplate( scroll.Canvas, "Speed Value", "speed", "Float with [Range(0,500)]", CreateSpeedValue );
		AddTemplate( scroll.Canvas, "Color Picker", "palette", "Color property with [ColorUsage]", CreateColorPicker );
		AddTemplate( scroll.Canvas, "Display Name", "label", "String with [Title] and [Placeholder]", CreateDisplayName );
		AddTemplate( scroll.Canvas, "Damage Range", "gps_fixed", "MinMax float pair", CreateDamageRange );
		AddTemplate( scroll.Canvas, "Toggle Feature", "toggle_on", "Bool ToggleGroup with a child property", CreateToggleFeature );
		AddTemplate( scroll.Canvas, "Description Text", "description", "String with [TextArea]", CreateDescriptionText );
		AddTemplate( scroll.Canvas, "Asset Reference", "inventory_2", "String with [ImageAssetPath]", CreateAssetReference );
		AddTemplate( scroll.Canvas, "Stat Group", "bar_chart", "Group with Header + 3 float properties", CreateStatGroup );

		scroll.Canvas.Layout.AddStretchCell( 1 );
		Layout.Add( scroll, 1 );
	}

	private void AddTemplate( Widget container, string name, string icon, string desc, Func<BlueprintElement> factory )
	{
		var card = new Widget( container );
		card.Layout = Layout.Row();
		card.Layout.Spacing = 8;
		card.Layout.Margin = new Margin( 8, 4, 8, 4 );
		card.SetStyles( "background-color: rgba(255,255,255,0.05); border-radius: 4px; padding: 6px;" );
		card.Cursor = CursorShape.Finger;
		card.ToolTip = desc;

		card.MouseClick = () =>
		{
			if ( _dock.ActiveBlueprint == null ) return;
			_dock.AddElementAtSelection( factory );
		};

		var iconLabel = new Label( name, card );
		iconLabel.SetStyles( "font-weight: bold;" );
		card.Layout.Add( iconLabel );

		card.Layout.AddStretchCell( 1 );

		var descLabel = new Label( desc, card );
		descLabel.SetStyles( "color: #aaa; font-size: 11px;" );
		card.Layout.Add( descLabel );

		container.Layout.Add( card );
	}

	// --- Template Factories ---

	private static BlueprintElement CreateHealthBar()
	{
		var el = new BlueprintElement
		{
			Name = "Health",
			ElementType = ElementType.Property,
			PropertyType = PropertyType.Float,
			DefaultValue = DefaultValueContainer.FromFloat( 100f ),
			AiHint = "Maximum health for the entity"
		};
		el.Attributes["Range"] = new Dictionary<string, object> { ["min"] = 0f, ["max"] = 100f, ["step"] = 1f };
		el.Attributes["Title"] = new Dictionary<string, object> { ["text"] = "Health" };
		return el;
	}

	private static BlueprintElement CreateSpeedValue()
	{
		var el = new BlueprintElement
		{
			Name = "MoveSpeed",
			ElementType = ElementType.Property,
			PropertyType = PropertyType.Float,
			DefaultValue = DefaultValueContainer.FromFloat( 200f ),
			AiHint = "Movement speed in units per second"
		};
		el.Attributes["Range"] = new Dictionary<string, object> { ["min"] = 0f, ["max"] = 500f, ["step"] = 10f };
		return el;
	}

	private static BlueprintElement CreateColorPicker()
	{
		var el = new BlueprintElement
		{
			Name = "TintColor",
			ElementType = ElementType.Property,
			PropertyType = PropertyType.Color,
			DefaultValue = DefaultValueContainer.FromColor( 1f, 1f, 1f, 1f )
		};
		el.Attributes["ColorUsage"] = new Dictionary<string, object>();
		return el;
	}

	private static BlueprintElement CreateDisplayName()
	{
		var el = new BlueprintElement
		{
			Name = "DisplayName",
			ElementType = ElementType.Property,
			PropertyType = PropertyType.String,
			DefaultValue = DefaultValueContainer.FromString( "" )
		};
		el.Attributes["Title"] = new Dictionary<string, object> { ["text"] = "Display Name" };
		el.Attributes["Placeholder"] = new Dictionary<string, object> { ["text"] = "Enter name..." };
		return el;
	}

	private static BlueprintElement CreateDamageRange()
	{
		var el = new BlueprintElement
		{
			Name = "Damage",
			ElementType = ElementType.Property,
			PropertyType = PropertyType.Float,
			DefaultValue = DefaultValueContainer.FromFloat( 10f ),
			AiHint = "Damage dealt per hit"
		};
		el.Attributes["MinMax"] = new Dictionary<string, object> { ["min"] = 0f, ["max"] = 500f };
		return el;
	}

	private static BlueprintElement CreateToggleFeature()
	{
		var toggle = new BlueprintElement
		{
			Name = "EnableFeature",
			ElementType = ElementType.ToggleGroup,
		};
		toggle.Children.Add( new BlueprintElement
		{
			Name = "FeatureValue",
			ElementType = ElementType.Property,
			PropertyType = PropertyType.Float,
			DefaultValue = DefaultValueContainer.FromFloat( 1f )
		} );
		return toggle;
	}

	private static BlueprintElement CreateDescriptionText()
	{
		var el = new BlueprintElement
		{
			Name = "Description",
			ElementType = ElementType.Property,
			PropertyType = PropertyType.String,
			DefaultValue = DefaultValueContainer.FromString( "" )
		};
		el.Attributes["TextArea"] = new Dictionary<string, object>();
		return el;
	}

	private static BlueprintElement CreateAssetReference()
	{
		var el = new BlueprintElement
		{
			Name = "AssetPath",
			ElementType = ElementType.Property,
			PropertyType = PropertyType.String,
			DefaultValue = DefaultValueContainer.FromString( "" )
		};
		el.Attributes["ImageAssetPath"] = new Dictionary<string, object>();
		return el;
	}

	private static BlueprintElement CreateStatGroup()
	{
		var group = new BlueprintElement
		{
			Name = "Stats",
			ElementType = ElementType.Group,
		};

		foreach ( var (name, defaultVal) in new[]
		{
			("Attack", 10f),
			("Defense", 5f),
			("Speed", 100f)
		} )
		{
			var child = new BlueprintElement
			{
				Name = name,
				ElementType = ElementType.Property,
				PropertyType = PropertyType.Float,
				DefaultValue = DefaultValueContainer.FromFloat( defaultVal )
			};
			child.Attributes["Range"] = new Dictionary<string, object> { ["min"] = 0f, ["max"] = 999f };
			group.Children.Add( child );
		}

		return group;
	}
}
