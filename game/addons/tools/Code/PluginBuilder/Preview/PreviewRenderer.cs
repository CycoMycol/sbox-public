using Sandbox.UI;

namespace Editor.PluginBuilder;

/// <summary>
/// Translates BlueprintElements into live Widget trees for the preview panel.
/// Uses manual widget construction — NOT ControlWidgets (which require SerializedProperty).
/// Fully implements attribute effects in the preview.
/// </summary>
public class PreviewRenderer
{
	private readonly PluginBuilderDock _dock;

	/// <summary>
	/// Tracks all rendered element wrappers by element Id.
	/// Used by the preview panel for highlight/selection.
	/// </summary>
	public Dictionary<string, Widget> ElementWidgets { get; } = new();

	public PreviewRenderer( PluginBuilderDock dock )
	{
		_dock = dock;
	}

	public void ClearTracking()
	{
		ElementWidgets.Clear();
	}

	public Widget RenderElement( Widget parent, BlueprintElement element )
	{
		Widget widget = element.ElementType switch
		{
			ElementType.Property => RenderProperty( parent, element ),
			ElementType.Group => RenderGroup( parent, element ),
			ElementType.ToggleGroup => RenderToggleGroup( parent, element ),
			ElementType.Feature => RenderFeature( parent, element ),
			ElementType.Header => RenderHeader( parent, element ),
			ElementType.Space => RenderSpace( parent, element ),
			ElementType.InfoBox => RenderInfoBox( parent, element ),
			_ => RenderProperty( parent, element )
		};

		if ( widget == null ) return null;

		// Wrap in a container for consistent highlight sizing.
		// Highlight styles go on the wrapper so they never conflict with the element's own styles.
		var wrapper = new Widget( parent );
		wrapper.Layout = Layout.Column();
		wrapper.Layout.Add( widget );

		// Tooltip from HelpText or [Description] attribute
		SetTooltip( wrapper, element );

		ElementWidgets[element.Id] = wrapper;
		return wrapper;
	}

	// ──────────────────────── Property ────────────────────────

	private Widget RenderProperty( Widget parent, BlueprintElement element )
	{
		// [Hide] — don't render
		if ( element.Attributes.ContainsKey( "Hide" ) )
			return null;

		var outer = new Widget( parent );
		outer.Layout = Layout.Column();
		outer.Cursor = CursorShape.Finger;
		outer.MouseClick = () => _dock.SelectElement( element );
		outer.MouseRightClick = () => ShowElementContextMenu( outer, element );

		// [InfoBox] attribute above the property
		if ( element.Attributes.TryGetValue( "InfoBox", out var infoParams ) )
		{
			var infoText = GetString( infoParams, "text", "" );
			if ( !string.IsNullOrEmpty( infoText ) )
			{
				var infoType = GetString( infoParams, "type", "Info" );
				var bgColor = infoType switch
				{
					"Warning" => "rgba(255,200,50,0.15)",
					"Error" => "rgba(255,80,80,0.15)",
					_ => "rgba(60,130,255,0.15)"
				};
				var infoIcon = infoType switch
				{
					"Warning" => "warning",
					"Error" => "error",
					_ => "info"
				};

				var infoRow = new Widget( outer );
				infoRow.Layout = Layout.Row();
				infoRow.Layout.Spacing = 6;
				infoRow.Layout.Margin = new Margin( 4, 2, 4, 4 );
				infoRow.SetStyles( $"background-color: {bgColor}; border-radius: 4px; padding: 4px 8px;" );

				var iconBtn = new Button( "", infoIcon, infoRow );
				infoRow.Layout.Add( iconBtn );
				infoRow.Layout.Add( new Label( infoText, infoRow ), 1 );
				outer.Layout.Add( infoRow );
			}
		}

		// [Header] attribute — bold header above the property
		if ( element.Attributes.TryGetValue( "Header", out var headerParams ) )
		{
			var headerText = GetString( headerParams, "text", "" );
			if ( !string.IsNullOrEmpty( headerText ) )
			{
				var hdr = new Label( headerText, outer );
				hdr.SetStyles( "font-weight: bold; font-size: 12px; padding: 6px 0 2px 4px;" );
				outer.Layout.Add( hdr );
			}
		}

		// [Space] attribute on a property — add vertical space above
		if ( element.Attributes.TryGetValue( "Space", out var spaceAttrParams ) )
		{
			float spaceH = GetFloat( spaceAttrParams, "height", 8f );
			var spacer = new Widget( outer );
			spacer.MinimumHeight = spaceH;
			spacer.MaximumHeight = spaceH;
			outer.Layout.Add( spacer );
		}

		// Determine label — [Title] overrides Name
		var labelText = !string.IsNullOrEmpty( element.Name ) ? element.Name : "(unnamed)";
		if ( element.Attributes.TryGetValue( "Title", out var titleParams ) )
		{
			var titleText = GetString( titleParams, "text", "" );
			if ( !string.IsNullOrEmpty( titleText ) )
				labelText = titleText;
		}

		bool wideMode = element.Attributes.ContainsKey( "WideMode" );

		// Build core property content (label + value)
		var coreContent = new Widget( outer );

		if ( wideMode )
		{
			coreContent.Layout = Layout.Column();

			var labelRow = new Widget( coreContent );
			labelRow.Layout = Layout.Row();
			labelRow.Layout.Spacing = 4;
			labelRow.Layout.Margin = new Margin( 4, 2, 4, 0 );

			AddIconIfPresent( labelRow, element );

			var label = new Label( labelText, labelRow );
			labelRow.Layout.Add( label );
			coreContent.Layout.Add( labelRow );

			var valueWidget = CreateValueWidget( coreContent, element );
			if ( valueWidget != null )
			{
				var valueRow = new Widget( coreContent );
				valueRow.Layout = Layout.Row();
				valueRow.Layout.Margin = new Margin( 4, 0, 4, 2 );
				valueRow.Layout.Add( valueWidget, 1 );
				coreContent.Layout.Add( valueRow );
			}
		}
		else
		{
			coreContent.Layout = Layout.Row();
			coreContent.Layout.Spacing = 8;
			coreContent.Layout.Margin = new Margin( 4, 2, 4, 2 );
			coreContent.MinimumHeight = 24;

			AddIconIfPresent( coreContent, element );

			var label = new Label( labelText, coreContent );
			label.MinimumWidth = 120;
			coreContent.Layout.Add( label );

			var valueWidget = CreateValueWidget( coreContent, element );
			if ( valueWidget != null )
				coreContent.Layout.Add( valueWidget, 1 );
		}

		// Add core content to outer
		outer.Layout.Add( coreContent );

		return outer;
	}

	private void AddIconIfPresent( Widget row, BlueprintElement element )
	{
		if ( !element.Attributes.TryGetValue( "Icon", out var iconParams ) ) return;
		var iconName = GetString( iconParams, "name", "" );
		if ( string.IsNullOrEmpty( iconName ) ) return;

		var iconBtn = new Button( "", iconName, row );
		row.Layout.Add( iconBtn );
	}

	// ──────────────────────── Value Widgets ────────────────────────

	private Widget CreateValueWidget( Widget parent, BlueprintElement element )
	{
		// [Button] attribute overrides the value widget entirely
		if ( element.Attributes.TryGetValue( "Button", out var btnParams ) )
		{
			var btnText = GetString( btnParams, "text", element.Name );
			var btnIcon = GetString( btnParams, "icon", "" );
			return string.IsNullOrEmpty( btnIcon )
				? new Button( btnText, parent: parent )
				: new Button( btnText, btnIcon, parent );
		}

		if ( element.PropertyType == PropertyType.String )
			return CreateStringWidget( parent, element );

		if ( element.PropertyType == PropertyType.Float || element.PropertyType == PropertyType.Int )
			return CreateNumericWidget( parent, element );

		if ( element.PropertyType == PropertyType.Bool )
		{
			var checkbox = new Checkbox( parent );
			checkbox.Value = element.DefaultValue?.BoolValue ?? false;
			return checkbox;
		}

		if ( element.PropertyType == PropertyType.Color )
			return CreateColorWidget( parent, element );

		if ( element.PropertyType == PropertyType.Enum || element.PropertyType == PropertyType.CustomEnum )
			return CreateEnumWidget( parent, element );

		if ( element.PropertyType == PropertyType.Vector3 )
			return CreateVectorWidget( parent, new[] { "X", "Y", "Z" }, 50 );

		if ( element.PropertyType == PropertyType.Vector2 )
		{
			if ( element.Attributes.TryGetValue( "MinMax", out var mmParams ) )
				return CreateMinMaxWidget( parent, element, mmParams );
			return CreateVectorWidget( parent, new[] { "X", "Y" }, 60 );
		}

		if ( element.PropertyType == PropertyType.Angles )
			return CreateVectorWidget( parent, new[] { "P", "Y", "R" }, 50 );

		if ( element.PropertyType == PropertyType.GameObject )
		{
			var goBtn = new Button( "(none)", "videogame_asset", parent );
			goBtn.SetStyles( "color: #aaa;" );
			return goBtn;
		}

		if ( element.PropertyType == PropertyType.Model )
		{
			var mdlBtn = new Button( "(none)", "view_in_ar", parent );
			mdlBtn.SetStyles( "color: #aaa;" );
			return mdlBtn;
		}

		if ( element.PropertyType == PropertyType.Material )
		{
			var matBtn = new Button( "(none)", "texture", parent );
			matBtn.SetStyles( "color: #aaa;" );
			return matBtn;
		}

		if ( element.PropertyType == PropertyType.PhysicsBody )
		{
			var pbBtn = new Button( "(none)", "settings", parent );
			pbBtn.SetStyles( "color: #aaa;" );
			return pbBtn;
		}

		if ( element.PropertyType == PropertyType.CustomStruct )
		{
			var structLabel = new Label( $"({element.CustomTypeName})", parent );
			structLabel.SetStyles( "color: #aaa; font-style: italic;" );
			return structLabel;
		}

		var fallback = new LineEdit( parent );
		fallback.PlaceholderText = element.PropertyType.ToString();
		return fallback;
	}

	private Widget CreateStringWidget( Widget parent, BlueprintElement element )
	{
		// [TextArea]
		if ( element.Attributes.ContainsKey( "TextArea" ) )
		{
			var textEdit = new TextEdit( parent );
			textEdit.MinimumHeight = 60;
			textEdit.PlainText = element.DefaultValue?.StringValue ?? "";
			if ( element.Attributes.ContainsKey( "ReadOnly" ) )
				textEdit.ReadOnly = true;
			return textEdit;
		}

		// [FontName] — font picker
		if ( element.Attributes.ContainsKey( "FontName" ) )
		{
			var dropdown = new ComboBox( parent );
			dropdown.AddItem( "Roboto" );
			dropdown.AddItem( "Poppins" );
			dropdown.AddItem( "Consolas" );
			dropdown.AddItem( "Arial" );
			return dropdown;
		}

		// [InputAction] — action selector
		if ( element.Attributes.ContainsKey( "InputAction" ) )
		{
			var dropdown = new ComboBox( parent );
			dropdown.AddItem( "attack1" );
			dropdown.AddItem( "attack2" );
			dropdown.AddItem( "jump" );
			dropdown.AddItem( "duck" );
			dropdown.AddItem( "use" );
			dropdown.AddItem( "reload" );
			return dropdown;
		}

		// [ImageAssetPath] — image picker
		if ( element.Attributes.ContainsKey( "ImageAssetPath" ) )
			return CreateAssetPickerWidget( parent, element, "photo", "Select image..." );

		// [IconName] — icon selector
		if ( element.Attributes.ContainsKey( "IconName" ) )
			return CreateAssetPickerWidget( parent, element, "emoji_symbols", "Select icon..." );

		// [AssetPath] — generic asset picker
		if ( element.Attributes.ContainsKey( "AssetPath" ) )
			return CreateAssetPickerWidget( parent, element, "attach_file", "Browse..." );

		// [FilePath] — file browser
		if ( element.Attributes.ContainsKey( "FilePath" ) )
			return CreateAssetPickerWidget( parent, element, "folder_open", "Browse..." );

		// [ResourceType] — resource picker
		if ( element.Attributes.TryGetValue( "ResourceType", out var resParams ) )
		{
			var resType = GetString( resParams, "type", "asset" );
			return CreateAssetPickerWidget( parent, element, "inventory_2", $"Select {resType}..." );
		}

		// Default — LineEdit
		var textEntry = new LineEdit( parent );
		textEntry.Text = element.DefaultValue?.StringValue ?? "";

		if ( element.Attributes.TryGetValue( "Placeholder", out var phParams ) &&
			phParams.TryGetValue( "text", out var phText ) )
		{
			textEntry.PlaceholderText = phText?.ToString() ?? "";
		}

		if ( element.Attributes.ContainsKey( "ReadOnly" ) )
			textEntry.ReadOnly = true;

		return textEntry;
	}

	private Widget CreateAssetPickerWidget( Widget parent, BlueprintElement element, string icon, string buttonText )
	{
		var row = new Widget( parent );
		row.Layout = Layout.Row();
		row.Layout.Spacing = 4;

		var input = new LineEdit( row );
		input.Text = element.DefaultValue?.StringValue ?? "";
		input.PlaceholderText = buttonText;
		if ( element.Attributes.ContainsKey( "ReadOnly" ) )
			input.ReadOnly = true;
		row.Layout.Add( input, 1 );

		var browseBtn = new Button( "", icon, row );
		browseBtn.ToolTip = buttonText;
		row.Layout.Add( browseBtn );

		return row;
	}

	private Widget CreateNumericWidget( Widget parent, BlueprintElement element )
	{
		if ( element.Attributes.TryGetValue( "Range", out var rangeParams ) )
			return CreateSlider( parent, element, rangeParams );

		var numEntry = new LineEdit( parent );
		numEntry.Text = element.DefaultValue?.ValueType switch
		{
			DefaultValueType.Float => element.DefaultValue.FloatValue.ToString( "F1" ),
			DefaultValueType.Int => element.DefaultValue.IntValue.ToString(),
			_ => "0"
		};

		if ( element.Attributes.ContainsKey( "ReadOnly" ) )
			numEntry.ReadOnly = true;

		return numEntry;
	}

	private Widget CreateColorWidget( Widget parent, BlueprintElement element )
	{
		var colorBtn = new Button( "Color", "palette", parent );
		if ( element.DefaultValue?.ColorValue != null )
		{
			var c = element.DefaultValue.ColorValue;
			colorBtn.SetStyles( $"background-color: rgba({(int)(c[0] * 255)},{(int)(c[1] * 255)},{(int)(c[2] * 255)},{c[3]});" );
		}

		if ( element.Attributes.ContainsKey( "Tint" ) )
			colorBtn.Text = "Tint";

		return colorBtn;
	}

	private Widget CreateEnumWidget( Widget parent, BlueprintElement element )
	{
		var values = GetEnumValues( element );

		// [EnumButtonGroup] — horizontal button bar
		if ( element.Attributes.ContainsKey( "EnumButtonGroup" ) )
		{
			var row = new Widget( parent );
			row.Layout = Layout.Row();
			row.Layout.Spacing = 2;

			foreach ( var val in values )
			{
				var btn = new Button( val, parent: row );
				row.Layout.Add( btn );
			}
			return row;
		}

		// [BitFlags] — multi-select checkboxes
		if ( element.Attributes.ContainsKey( "BitFlags" ) )
		{
			var col = new Widget( parent );
			col.Layout = Layout.Column();
			col.Layout.Spacing = 2;

			foreach ( var val in values )
			{
				var flagRow = new Widget( col );
				flagRow.Layout = Layout.Row();
				flagRow.Layout.Spacing = 4;

				var cb = new Checkbox( flagRow );
				flagRow.Layout.Add( cb );
				flagRow.Layout.Add( new Label( val, flagRow ) );
				col.Layout.Add( flagRow );
			}
			return col;
		}

		// Default — dropdown
		var dropdown = new ComboBox( parent );
		foreach ( var val in values )
			dropdown.AddItem( val );
		return dropdown;
	}

	private List<string> GetEnumValues( BlueprintElement element )
	{
		if ( element.PropertyType == PropertyType.CustomEnum && !string.IsNullOrEmpty( element.CustomTypeName ) )
		{
			var enumDef = _dock.ActiveBlueprint?.CustomEnums.FirstOrDefault( e => e.Name == element.CustomTypeName );
			if ( enumDef != null )
				return enumDef.Values.Select( v => v.Name ).ToList();
		}
		return new List<string> { "Value1", "Value2", "Value3" };
	}

	private Widget CreateVectorWidget( Widget parent, string[] axes, int inputWidth )
	{
		var row = new Widget( parent );
		row.Layout = Layout.Row();
		row.Layout.Spacing = 4;

		foreach ( var axis in axes )
		{
			row.Layout.Add( new Label( axis, row ) );
			var entry = new LineEdit( row );
			entry.Text = "0";
			entry.MinimumWidth = inputWidth;
			row.Layout.Add( entry );
		}
		return row;
	}

	private Widget CreateMinMaxWidget( Widget parent, BlueprintElement element, Dictionary<string, object> mmParams )
	{
		var row = new Widget( parent );
		row.Layout = Layout.Row();
		row.Layout.Spacing = 4;

		float min = GetFloat( mmParams, "min", 0f );
		float max = GetFloat( mmParams, "max", 100f );

		row.Layout.Add( new Label( "Min", row ) );
		var minEntry = new LineEdit( row );
		minEntry.Text = min.ToString( "F0" );
		minEntry.MinimumWidth = 50;
		row.Layout.Add( minEntry );

		row.Layout.Add( new Label( "Max", row ) );
		var maxEntry = new LineEdit( row );
		maxEntry.Text = max.ToString( "F0" );
		maxEntry.MinimumWidth = 50;
		row.Layout.Add( maxEntry );

		return row;
	}

	private Widget CreateSlider( Widget parent, BlueprintElement element, Dictionary<string, object> rangeParams )
	{
		var container = new Widget( parent );
		container.Layout = Layout.Row();
		container.Layout.Spacing = 4;

		float min = GetFloat( rangeParams, "min", 0f );
		float max = GetFloat( rangeParams, "max", 100f );

		float currentValue = element.DefaultValue?.ValueType switch
		{
			DefaultValueType.Float => element.DefaultValue.FloatValue,
			DefaultValueType.Int => element.DefaultValue.IntValue,
			_ => min
		};

		var rangeLabel = new Label( $"[{min:F0}\u2013{max:F0}]", container );
		rangeLabel.SetStyles( "color: #888; font-size: 11px;" );
		container.Layout.Add( rangeLabel );

		var numInput = new LineEdit( container );
		numInput.Text = currentValue.ToString( "F1" );
		numInput.MinimumWidth = 60;
		if ( element.Attributes.ContainsKey( "ReadOnly" ) )
			numInput.ReadOnly = true;
		container.Layout.Add( numInput, 1 );

		return container;
	}

	// ──────────────────────── Groups / Features ────────────────────────

	private Widget RenderGroup( Widget parent, BlueprintElement element )
	{
		var group = new Widget( parent );
		group.Layout = Layout.Column();
		group.Layout.Margin = new Margin( 0, 4, 0, 4 );
		group.Cursor = CursorShape.Finger;
		group.MouseClick = () => _dock.SelectElement( element );
		group.MouseRightClick = () => ShowElementContextMenu( group, element );

		var header = new Widget( group );
		header.Layout = Layout.Row();
		header.Layout.Spacing = 4;
		header.SetStyles( "background-color: rgba(255,255,255,0.05); padding: 4px 8px; border-radius: 4px;" );

		var arrow = new Label( "\u25bc", header );
		header.Layout.Add( arrow );

		var label = new Label( element.Name, header );
		label.SetStyles( "font-weight: bold;" );
		header.Layout.Add( label );

		group.Layout.Add( header );

		var childContainer = new Widget( group );
		childContainer.Layout = Layout.Column();
		childContainer.Layout.Margin = new Margin( 16, 0, 0, 0 );

		foreach ( var child in element.Children )
		{
			var childWidget = RenderElement( childContainer, child );
			if ( childWidget != null )
				childContainer.Layout.Add( childWidget );
		}

		group.Layout.Add( childContainer );
		return group;
	}

	private Widget RenderToggleGroup( Widget parent, BlueprintElement element )
	{
		var group = new Widget( parent );
		group.Layout = Layout.Column();
		group.Layout.Margin = new Margin( 0, 4, 0, 4 );
		group.Cursor = CursorShape.Finger;
		group.MouseClick = () => _dock.SelectElement( element );
		group.MouseRightClick = () => ShowElementContextMenu( group, element );

		var toggleHeader = new Widget( group );
		toggleHeader.Layout = Layout.Row();
		toggleHeader.Layout.Spacing = 4;
		toggleHeader.SetStyles( "background-color: rgba(255,255,255,0.05); padding: 4px 8px; border-radius: 4px;" );

		var toggle = new Checkbox( toggleHeader );
		toggle.Value = true;
		toggleHeader.Layout.Add( toggle );

		var label = new Label( element.Name, toggleHeader );
		label.SetStyles( "font-weight: bold;" );
		toggleHeader.Layout.Add( label );

		group.Layout.Add( toggleHeader );

		var childContainer = new Widget( group );
		childContainer.Layout = Layout.Column();
		childContainer.Layout.Margin = new Margin( 16, 0, 0, 0 );

		foreach ( var child in element.Children )
		{
			var childWidget = RenderElement( childContainer, child );
			if ( childWidget != null )
				childContainer.Layout.Add( childWidget );
		}

		group.Layout.Add( childContainer );
		return group;
	}

	private Widget RenderFeature( Widget parent, BlueprintElement element )
	{
		var feature = new Widget( parent );
		feature.Layout = Layout.Column();
		feature.Layout.Margin = new Margin( 0, 4, 0, 4 );
		feature.Cursor = CursorShape.Finger;
		feature.MouseClick = () => _dock.SelectElement( element );
		feature.MouseRightClick = () => ShowElementContextMenu( feature, element );

		var header = new Widget( feature );
		header.Layout = Layout.Row();
		header.SetStyles( "background-color: rgba(100,180,255,0.15); padding: 4px 8px; border-radius: 4px 4px 0 0;" );

		var tabLabel = new Label( $"\u2b21 {element.Name}", header );
		tabLabel.SetStyles( "font-weight: bold; color: #6ab4ff;" );
		header.Layout.Add( tabLabel );

		feature.Layout.Add( header );

		var childContainer = new Widget( feature );
		childContainer.Layout = Layout.Column();
		childContainer.Layout.Margin = new Margin( 16, 4, 0, 4 );
		childContainer.SetStyles( "border-left: 2px solid rgba(100,180,255,0.3);" );

		foreach ( var child in element.Children )
		{
			var childWidget = RenderElement( childContainer, child );
			if ( childWidget != null )
				childContainer.Layout.Add( childWidget );
		}

		feature.Layout.Add( childContainer );
		return feature;
	}

	// ──────────────────────── Simple Elements ────────────────────────

	private Widget RenderHeader( Widget parent, BlueprintElement element )
	{
		var header = new Label( element.Name, parent );
		header.SetStyles( "font-weight: bold; font-size: 12px; padding: 8px 0 4px 0; border-bottom: 1px solid rgba(255,255,255,0.1);" );
		header.Cursor = CursorShape.Finger;
		header.MouseClick = () => _dock.SelectElement( element );
		header.MouseRightClick = () => ShowElementContextMenu( header, element );
		return header;
	}

	private Widget RenderSpace( Widget parent, BlueprintElement element )
	{
		var spacer = new Widget( parent );
		spacer.Cursor = CursorShape.Finger;
		spacer.MouseClick = () => _dock.SelectElement( element );
		spacer.MouseRightClick = () => ShowElementContextMenu( spacer, element );

		float height = 8f;
		if ( element.Attributes.TryGetValue( "Space", out var spaceParams ) )
			height = GetFloat( spaceParams, "height", 8f );

		spacer.MinimumHeight = height;
		spacer.MaximumHeight = height;
		spacer.SetStyles( "background-color: rgba(255,255,255,0.02);" );
		return spacer;
	}

	private Widget RenderInfoBox( Widget parent, BlueprintElement element )
	{
		var box = new Widget( parent );
		box.Layout = Layout.Row();
		box.Layout.Spacing = 8;
		box.Layout.Margin = new Margin( 4, 4, 4, 4 );
		box.SetStyles( "background-color: rgba(60,130,255,0.15); border-radius: 4px; padding: 8px;" );
		box.Cursor = CursorShape.Finger;
		box.MouseClick = () => _dock.SelectElement( element );
		box.MouseRightClick = () => ShowElementContextMenu( box, element );

		var iconLabel = new Label( "\u2139", box );
		box.Layout.Add( iconLabel );

		var text = new Label( element.Name, box );
		box.Layout.Add( text, 1 );

		return box;
	}

	// ──────────────────────── Context Menu ────────────────────────

	private void ShowElementContextMenu( Widget source, BlueprintElement element )
	{
		_dock.SelectElement( element );

		var menu = new Menu( source );

		var addMenu = menu.AddMenu( "Add Element" );
		addMenu.AddOption( "Property", "circle", () => _dock.AddElementAtSelection( () => new BlueprintElement { ElementType = ElementType.Property, Name = "NewProperty", PropertyType = PropertyType.String } ) );
		addMenu.AddOption( "Group", "folder", () => _dock.AddElementAtSelection( () => new BlueprintElement { ElementType = ElementType.Group, Name = "NewGroup", PropertyType = PropertyType.String } ) );
		addMenu.AddOption( "Toggle Group", "toggle_on", () => _dock.AddElementAtSelection( () => new BlueprintElement { ElementType = ElementType.ToggleGroup, Name = "NewToggleGroup" } ) );
		addMenu.AddOption( "Header", "title", () => _dock.AddElementAtSelection( () => new BlueprintElement { ElementType = ElementType.Header, Name = "Header" } ) );
		addMenu.AddOption( "Space", "space_bar", () => _dock.AddElementAtSelection( () => new BlueprintElement { ElementType = ElementType.Space, Name = "Space" } ) );
		addMenu.AddOption( "Info Box", "info", () => _dock.AddElementAtSelection( () => new BlueprintElement { ElementType = ElementType.InfoBox, Name = "Info" } ) );

		var attrMenu = menu.AddMenu( "Add Attribute" );
		var applicable = AttributeCatalog.GetApplicable( element.PropertyType, element.ElementType );
		foreach ( var attr in applicable.OrderBy( a => a.Category ).ThenBy( a => a.Name ) )
		{
			if ( element.Attributes.ContainsKey( attr.Name ) ) continue;
			attrMenu.AddOption( $"[{attr.Name}]", attr.Icon, () =>
			{
				element.Attributes[attr.Name] = new Dictionary<string, object>();
				_dock.MarkDirty();
			} );
		}

		menu.AddSeparator();

		menu.AddOption( "Duplicate", "content_copy", () =>
		{
			var blueprint = _dock.ActiveBlueprint;
			if ( blueprint == null ) return;
			var clone = element.Clone();
			clone.Name = element.Name + "_copy";
			_dock.SelectElement( element );
			_dock.AddElementAtSelection( () => clone );
		} );

		menu.AddOption( "Remove", "delete", () =>
		{
			var blueprint = _dock.ActiveBlueprint;
			if ( blueprint == null ) return;
			var list = FindParentList( blueprint, element );
			if ( list == null ) return;
			var idx = list.IndexOf( element );
			list.Remove( element );
			_dock.Undo.Push( "Remove element",
				undo: () =>
				{
					if ( idx >= 0 && idx <= list.Count )
						list.Insert( idx, element );
					else
						list.Add( element );
					_dock.MarkDirty();
				},
				redo: () => { list.Remove( element ); _dock.MarkDirty(); }
			);
			_dock.MarkDirty();
		} );

		menu.OpenAtCursor();
	}

	// ──────────────────────── Helpers ────────────────────────

	private void SetTooltip( Widget widget, BlueprintElement element )
	{
		string tooltip = null;

		if ( !string.IsNullOrEmpty( element.HelpText ) )
			tooltip = element.HelpText;
		else if ( element.Attributes.TryGetValue( "Description", out var descParams ) )
			tooltip = GetString( descParams, "text", "" );

		if ( !string.IsNullOrEmpty( tooltip ) )
			widget.ToolTip = tooltip;
	}

	private static List<BlueprintElement> FindParentList( PluginBlueprint blueprint, BlueprintElement element )
	{
		if ( blueprint.Elements.Contains( element ) )
			return blueprint.Elements;

		return FindInChildLists( blueprint.Elements, element );
	}

	private static List<BlueprintElement> FindInChildLists( List<BlueprintElement> elements, BlueprintElement target )
	{
		foreach ( var el in elements )
		{
			if ( el.Children.Contains( target ) )
				return el.Children;
			var found = FindInChildLists( el.Children, target );
			if ( found != null ) return found;
		}
		return null;
	}

	private static string GetString( Dictionary<string, object> dict, string key, string fallback )
	{
		if ( !dict.TryGetValue( key, out var val ) )
			return fallback;

		if ( val is string s ) return s;
		if ( val is JsonElement je && je.ValueKind == JsonValueKind.String )
			return je.GetString() ?? fallback;

		return val?.ToString() ?? fallback;
	}

	private static float GetFloat( Dictionary<string, object> dict, string key, float fallback )
	{
		if ( !dict.TryGetValue( key, out var val ) )
			return fallback;

		if ( val is float f ) return f;
		if ( val is double d ) return (float)d;
		if ( val is int i ) return i;
		if ( val is long l ) return l;
		if ( val is string s && float.TryParse( s, out var parsed ) ) return parsed;
		if ( val is JsonElement je )
		{
			if ( je.ValueKind == JsonValueKind.Number ) return je.GetSingle();
			if ( je.ValueKind == JsonValueKind.String && float.TryParse( je.GetString(), out var jp ) ) return jp;
		}

		return fallback;
	}
}
