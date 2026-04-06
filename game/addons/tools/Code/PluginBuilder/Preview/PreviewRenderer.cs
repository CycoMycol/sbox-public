using Sandbox.UI;

namespace Editor.PluginBuilder;

/// <summary>
/// Translates BlueprintElements into live Widget trees for the preview panel.
/// Renders elements to look and behave like the actual S&Box inspector.
/// Uses Theme.* colors and Paint.* API for authentic rendering.
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

		// [Space] attribute on a property — add space above
		if ( element.Attributes.TryGetValue( "Space", out var spaceAttrParams ) )
		{
			float spaceH = GetFloat( spaceAttrParams, "height", 8f );
			float spaceW = GetFloat( spaceAttrParams, "width", 0f );

			if ( spaceH > 0 )
			{
				var vSpacer = new Widget( outer );
				vSpacer.MinimumHeight = spaceH;
				vSpacer.MaximumHeight = spaceH;
				outer.Layout.Add( vSpacer );
			}

			if ( spaceW > 0 )
			{
				var hSpacer = new Widget( outer );
				hSpacer.MinimumWidth = spaceW;
				hSpacer.MaximumWidth = spaceW;
				hSpacer.MinimumHeight = 1;
				outer.Layout.Add( hSpacer );
			}
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

		bool readOnly = element.Attributes.ContainsKey( "ReadOnly" );

		// Inspector-style property row with hover highlight
		var row = new InspectorPropertyRow( outer, readOnly );
		row.Cursor = CursorShape.Finger;
		row.MouseClick = () => _dock.SelectElement( element );

		if ( wideMode )
		{
			row.Layout = Layout.Column();
			row.Layout.Spacing = 2;
			row.Layout.Margin = new Margin( 6, 2, 0, 2 );
			row.MinimumHeight = 24;

			var labelRow = new Widget( row );
			labelRow.Layout = Layout.Row();
			labelRow.Layout.Spacing = 4;

			AddIconIfPresent( labelRow, element );

			var label = new Label( labelText, labelRow );
			label.SetStyles( $"color: {Theme.TextControl.Hex}; font-size: 11px;" );
			labelRow.Layout.Add( label );
			row.Layout.Add( labelRow );

			var valueWidget = CreateValueWidget( row, element );
			if ( valueWidget != null )
			{
				var valueRow = new Widget( row );
				valueRow.Layout = Layout.Row();
				valueRow.Layout.Margin = new Margin( 0, 0, 0, 2 );
				valueRow.Layout.Add( valueWidget, 1 );
				row.Layout.Add( valueRow );
			}
		}
		else
		{
			row.Layout = Layout.Row();
			row.Layout.Spacing = 8;
			row.Layout.Margin = new Margin( 6, 2, 0, 2 );
			row.MinimumHeight = Theme.RowHeight;

			AddIconIfPresent( row, element );

			var label = new Label( labelText, row );
			label.MinimumWidth = 120;
			label.MaximumWidth = 120;
			label.SetStyles( $"color: {Theme.TextControl.Hex}; font-size: 11px;" );
			row.Layout.Add( label );

			var valueWidget = CreateValueWidget( row, element );
			if ( valueWidget != null )
				row.Layout.Add( valueWidget, 1 );
		}

		outer.Layout.Add( row );

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
			return CreateVectorWidget( parent, new[] { "X", "Y", "Z" }, 40 );

		if ( element.PropertyType == PropertyType.Vector2 )
		{
			if ( element.Attributes.TryGetValue( "MinMax", out var mmParams ) )
				return CreateMinMaxWidget( parent, element, mmParams );
			return CreateVectorWidget( parent, new[] { "X", "Y" }, 40 );
		}

		if ( element.PropertyType == PropertyType.Angles )
			return CreateVectorWidget( parent, new[] { "P", "Y", "R" }, 40 );

		if ( element.PropertyType == PropertyType.GameObject )
			return CreateResourceButton( parent, "videogame_asset", "(none)" );

		if ( element.PropertyType == PropertyType.Model )
			return CreateResourceButton( parent, "view_in_ar", "(none)" );

		if ( element.PropertyType == PropertyType.Material )
			return CreateResourceButton( parent, "texture", "(none)" );

		if ( element.PropertyType == PropertyType.PhysicsBody )
			return CreateResourceButton( parent, "settings", "(none)" );

		if ( element.PropertyType == PropertyType.CustomStruct )
		{
			var structLabel = new Label( $"({element.CustomTypeName})", parent );
			structLabel.SetStyles( $"color: {Theme.TextControl.WithAlpha( 0.5f ).Hex}; font-style: italic; font-size: 11px;" );
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
		row.Layout.Spacing = 0;
		row.SetStyles( $"background-color: {Theme.ControlBackground.Hex}; border: 1px solid {Theme.BorderLight.Hex}; border-radius: 3px;" );
		row.MinimumHeight = Theme.RowHeight;

		// Icon
		var iconLabel = new Button( "", icon, row );
		iconLabel.SetStyles( $"padding: 2px 4px; background: transparent; color: {Theme.TextControl.WithAlpha( 0.6f ).Hex};" );
		row.Layout.Add( iconLabel );

		var input = new LineEdit( row );
		input.Text = element.DefaultValue?.StringValue ?? "";
		input.PlaceholderText = buttonText;
		input.SetStyles( "border: none; background: transparent;" );
		if ( element.Attributes.ContainsKey( "ReadOnly" ) )
			input.ReadOnly = true;
		row.Layout.Add( input, 1 );

		// Browse button
		var browseBtn = new Button( "", "more_horiz", row );
		browseBtn.SetStyles( $"padding: 2px 4px; background: transparent; color: {Theme.TextControl.WithAlpha( 0.6f ).Hex};" );
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
		var row = new Widget( parent );
		row.Layout = Layout.Row();
		row.Layout.Spacing = 4;
		row.MinimumHeight = Theme.RowHeight;

		Color defaultColor = Color.White;
		if ( element.DefaultValue?.ColorValue != null )
		{
			var c = element.DefaultValue.ColorValue;
			defaultColor = new Color( c[0], c[1], c[2], c[3] );
		}

		// Color swatch — opens a color picker popup when clicked
		var swatch = new ColorSwatch( row, defaultColor );
		swatch.MinimumWidth = Theme.RowHeight;
		swatch.MaximumWidth = Theme.RowHeight;
		swatch.MinimumHeight = Theme.RowHeight;
		swatch.MaximumHeight = Theme.RowHeight;
		swatch.Cursor = CursorShape.Finger;
		swatch.MouseClick = () =>
		{
			ColorPicker.OpenColorPopup( swatch, swatch.CurrentColor, false, ( c ) =>
			{
				swatch.CurrentColor = c;
				swatch.Update();
			} );
		};
		row.Layout.Add( swatch );

		// Hex value input
		var hexLabel = new LineEdit( row );
		hexLabel.Text = defaultColor.Hex;
		hexLabel.PlaceholderText = "#ffffff";
		row.Layout.Add( hexLabel, 1 );

		return row;
	}

	private Widget CreateEnumWidget( Widget parent, BlueprintElement element )
	{
		var values = GetEnumValues( element );

		// [EnumButtonGroup] — horizontal button bar
		if ( element.Attributes.ContainsKey( "EnumButtonGroup" ) )
		{
			var row = new Widget( parent );
			row.Layout = Layout.Row();
			row.Layout.Spacing = 0;
			row.SetStyles( $"border: 1px solid {Theme.BorderLight.Hex}; border-radius: 3px;" );

			bool first = true;
			foreach ( var val in values )
			{
				var btn = new Button( val, parent: row );
				btn.SetStyles( first
					? $"background-color: {Theme.Primary.Hex}; color: white; padding: 4px 10px; border-radius: 3px 0 0 3px;"
					: $"background-color: {Theme.ControlBackground.Hex}; padding: 4px 10px; border-radius: 0;" );
				row.Layout.Add( btn );
				first = false;
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
				var lbl = new Label( val, flagRow );
				lbl.SetStyles( $"color: {Theme.TextControl.Hex}; font-size: 11px;" );
				flagRow.Layout.Add( lbl );
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

	private static readonly Color[] VectorAxisColors = new[] { Theme.Red, Theme.Green, Theme.Blue };

	private Widget CreateVectorWidget( Widget parent, string[] axes, int inputWidth )
	{
		var row = new Widget( parent );
		row.Layout = Layout.Row();
		row.Layout.Spacing = 2;

		for ( int i = 0; i < axes.Length; i++ )
		{
			var axisColor = i < VectorAxisColors.Length ? VectorAxisColors[i] : Theme.TextControl;

			var axisLabel = new Label( axes[i], row );
			axisLabel.SetStyles( $"color: {axisColor.Hex}; font-weight: bold; font-size: 11px; padding: 0 4px; min-width: 16px;" );
			row.Layout.Add( axisLabel );

			var entry = new LineEdit( row );
			entry.Text = "0";
			entry.MinimumWidth = 40;
			row.Layout.Add( entry, 1 );
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
		bool readOnly = element.Attributes.ContainsKey( "ReadOnly" );

		float min = GetFloat( rangeParams, "min", 0f );
		float max = GetFloat( rangeParams, "max", 100f );
		float step = GetFloat( rangeParams, "step", 0f );

		float currentValue = element.DefaultValue?.ValueType switch
		{
			DefaultValueType.Float => element.DefaultValue.FloatValue,
			DefaultValueType.Int => element.DefaultValue.IntValue,
			_ => min
		};

		var container = new Widget( parent );
		container.Layout = Layout.Row();
		container.Layout.Spacing = 4;
		container.MinimumHeight = Theme.RowHeight;

		// Painted slider with fill bar and drag support
		var slider = new InspectorSlider( container, min, max, currentValue, step );
		container.Layout.Add( slider, 1 );

		// Numeric input beside slider
		var numInput = new LineEdit( container );
		numInput.Text = element.PropertyType == PropertyType.Int
			? ((int)currentValue).ToString()
			: currentValue.ToString( "F1" );
		numInput.MinimumWidth = 50;
		numInput.MaximumWidth = 60;
		if ( readOnly ) numInput.ReadOnly = true;

		// Sync slider drag → text field
		slider.OnValueChanged = ( val ) =>
		{
			numInput.Text = element.PropertyType == PropertyType.Int
				? ((int)val).ToString()
				: val.ToString( "F1" );
		};

		container.Layout.Add( numInput );
		return container;
	}

	// ──────────────────────── Groups / Features ────────────────────────

	private Widget RenderGroup( Widget parent, BlueprintElement element )
	{
		var group = new Widget( parent );
		group.Layout = Layout.Column();
		group.Layout.Margin = new Margin( 0, 2, 0, 2 );
		group.Cursor = CursorShape.Finger;
		group.MouseRightClick = () => ShowElementContextMenu( group, element );

		// Group header — styled like ControlSheetGroup GroupHeader with +/- icon
		var headerWidget = new InspectorGroupHeader( group, element.Name, true );
		headerWidget.MouseClick = () =>
		{
			_dock.SelectElement( element );
			headerWidget.Toggle();
		};
		group.Layout.Add( headerWidget );

		// Body container with left gutter line
		var body = new InspectorGroupBody( group );
		body.Layout = Layout.Column();
		body.Layout.Margin = new Margin( 12, 4, 0, 4 );
		body.Layout.Spacing = 0;

		foreach ( var child in element.Children )
		{
			var childWidget = RenderElement( body, child );
			if ( childWidget != null )
				body.Layout.Add( childWidget );
		}

		group.Layout.Add( body );
		return group;
	}

	private Widget RenderToggleGroup( Widget parent, BlueprintElement element )
	{
		var group = new Widget( parent );
		group.Layout = Layout.Column();
		group.Layout.Margin = new Margin( 0, 2, 0, 2 );
		group.Cursor = CursorShape.Finger;
		group.MouseRightClick = () => ShowElementContextMenu( group, element );

		// Header row with toggle checkbox + group header
		var headerRow = new Widget( group );
		headerRow.Layout = Layout.Row();
		headerRow.Layout.Spacing = 6;
		headerRow.Layout.Margin = new Margin( 4, 2, 0, 2 );
		headerRow.MinimumHeight = Theme.RowHeight;

		var toggle = new Checkbox( headerRow );
		toggle.Value = true;
		toggle.FixedWidth = 17;
		toggle.FixedHeight = 17;
		headerRow.Layout.Add( toggle );

		var headerWidget = new InspectorGroupHeader( headerRow, element.Name, true );
		headerWidget.MouseClick = () =>
		{
			_dock.SelectElement( element );
			headerWidget.Toggle();
		};
		headerRow.Layout.Add( headerWidget, 1 );

		group.Layout.Add( headerRow );

		// Body with gutter
		var body = new InspectorGroupBody( group );
		body.Layout = Layout.Column();
		body.Layout.Margin = new Margin( 12, 4, 0, 4 );
		body.Layout.Spacing = 0;

		foreach ( var child in element.Children )
		{
			var childWidget = RenderElement( body, child );
			if ( childWidget != null )
				body.Layout.Add( childWidget );
		}

		group.Layout.Add( body );
		return group;
	}

	private Widget RenderFeature( Widget parent, BlueprintElement element )
	{
		var feature = new Widget( parent );
		feature.Layout = Layout.Column();
		feature.Layout.Margin = new Margin( 0, 2, 0, 2 );
		feature.Cursor = CursorShape.Finger;
		feature.MouseClick = () => _dock.SelectElement( element );
		feature.MouseRightClick = () => ShowElementContextMenu( feature, element );

		// Tab-like header with Theme colors
		var header = new Widget( feature );
		header.Layout = Layout.Row();
		header.MinimumHeight = Theme.RowHeight;
		header.SetStyles( $"background-color: {Theme.Primary.WithAlpha( 0.15f ).Hex}; padding: 4px 8px; border-radius: 4px 4px 0 0;" );

		var tabLabel = new Label( element.Name, header );
		tabLabel.SetStyles( $"font-weight: bold; font-size: 11px; color: {Theme.Primary.Hex};" );
		header.Layout.Add( tabLabel );

		header.Layout.AddStretchCell( 1 );
		feature.Layout.Add( header );

		var childContainer = new Widget( feature );
		childContainer.Layout = Layout.Column();
		childContainer.Layout.Margin = new Margin( 12, 4, 0, 4 );
		childContainer.SetStyles( $"border-left: 2px solid {Theme.Primary.WithAlpha( 0.3f ).Hex};" );

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
		header.SetStyles( $"font-weight: bold; font-size: 11px; padding: 8px 6px 4px 6px; color: {Theme.Text.Hex}; border-bottom: 1px solid {Theme.BorderLight.Hex};" );
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

		float height = element.SpacerSize;
		float width = element.SpacerWidth;

		// Fallback to attribute-based values for backward compat
		if ( height <= 0 && element.Attributes.TryGetValue( "Space", out var spaceParams ) )
			height = GetFloat( spaceParams, "height", 8f );
		if ( width <= 0 && element.Attributes.TryGetValue( "Space", out var spaceParamsW ) )
			width = GetFloat( spaceParamsW, "width", 0f );

		// Apply height (vertical space between elements)
		if ( height > 0 )
		{
			spacer.MinimumHeight = height;
			spacer.MaximumHeight = height;
		}
		else
		{
			// Default minimum height
			spacer.MinimumHeight = 8;
			spacer.MaximumHeight = 8;
		}

		// Apply width (horizontal space — only constrains if set)
		if ( width > 0 )
		{
			spacer.MinimumWidth = width;
			spacer.MaximumWidth = width;
		}

		spacer.SetStyles( "background-color: transparent;" );
		return spacer;
	}

	private Widget RenderInfoBox( Widget parent, BlueprintElement element )
	{
		var bgColor = "rgba(60,130,255,0.12)";
		var borderColor = "rgba(60,130,255,0.3)";

		var box = new Widget( parent );
		box.Layout = Layout.Row();
		box.Layout.Spacing = 8;
		box.Layout.Margin = new Margin( 6, 6, 6, 6 );
		box.SetStyles( $"background-color: {bgColor}; border: 1px solid {borderColor}; border-radius: 4px; padding: 8px;" );
		box.MinimumHeight = 36;
		box.Cursor = CursorShape.Finger;
		box.MouseClick = () => _dock.SelectElement( element );
		box.MouseRightClick = () => ShowElementContextMenu( box, element );

		var iconWidget = new Button( "", "info", box );
		iconWidget.SetStyles( "background: transparent; padding: 0;" );
		box.Layout.Add( iconWidget );

		var text = new Label( element.Name, box );
		text.SetStyles( $"color: {Theme.TextControl.Hex}; font-size: 11px;" );
		box.Layout.Add( text, 1 );

		return box;
	}

	// ──────────────────────── Resource Buttons ────────────────────────

	private Widget CreateResourceButton( Widget parent, string icon, string placeholder )
	{
		var row = new Widget( parent );
		row.Layout = Layout.Row();
		row.Layout.Spacing = 0;
		row.SetStyles( $"background-color: {Theme.ControlBackground.Hex}; border: 1px solid {Theme.BorderLight.Hex}; border-radius: 3px;" );
		row.MinimumHeight = Theme.RowHeight;

		var iconWidget = new Button( "", icon, row );
		iconWidget.SetStyles( $"padding: 2px 6px; background: transparent; color: {Theme.TextControl.WithAlpha( 0.6f ).Hex};" );
		iconWidget.ToolTip = "Browse...";
		row.Layout.Add( iconWidget );

		var textLabel = new Label( placeholder, row );
		textLabel.SetStyles( $"color: {Theme.TextControl.WithAlpha( 0.4f ).Hex}; font-size: 11px; padding: 0 4px;" );
		row.Layout.Add( textLabel, 1 );

		return row;
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

// ──────────────────────── Custom Widget Classes ────────────────────────

/// <summary>
/// Inspector-style property row with subtle hover highlighting.
/// </summary>
class InspectorPropertyRow : Widget
{
	private readonly bool _readOnly;

	public InspectorPropertyRow( Widget parent, bool readOnly = false ) : base( parent )
	{
		_readOnly = readOnly;
		MouseTracking = true;
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		if ( Paint.HasMouseOver )
		{
			Paint.ClearPen();
			Paint.SetBrush( Theme.Blue.WithAlpha( 0.04f ) );
			Paint.DrawRect( LocalRect );
		}

		if ( _readOnly )
		{
			Paint.ClearPen();
			Paint.SetBrush( Color.White.WithAlpha( 0.02f ) );
			Paint.DrawRect( LocalRect );
		}
	}
}

/// <summary>
/// Inspector-style group header with +/- toggle icon, matching ControlSheetGroup GroupHeader.
/// </summary>
class InspectorGroupHeader : Widget
{
	public string Title { get; set; }
	private bool _expanded = true;
	public Action OnToggled;

	public InspectorGroupHeader( Widget parent, string title, bool startExpanded = true ) : base( parent )
	{
		Title = title;
		_expanded = startExpanded;
		FixedHeight = Theme.RowHeight;
		Cursor = CursorShape.Finger;
		MouseTracking = true;
	}

	public void Toggle()
	{
		_expanded = !_expanded;
		OnToggled?.Invoke();
		Update();
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		// Background pill (like GroupHeader from ControlSheetGroup)
		var bgRect = LocalRect.Shrink( 3, 4, 4, 4 );
		bgRect.Height = Theme.RowHeight - 8;
		bgRect.Width = bgRect.Height;

		var bgColor = Theme.WindowBackground.Lighten( 0.2f ).WithAlphaMultiplied( _expanded ? 1f : 0.5f );
		Paint.SetBrushAndPen( bgColor );
		Paint.DrawRect( bgRect, 6 );
		Paint.ClearBrush();

		// +/- icon
		float iconIntensity = Paint.HasMouseOver ? 1.5f : 1f;
		if ( _expanded )
		{
			Paint.Pen = Theme.TextControl.WithAlpha( 0.2f * iconIntensity );
			Paint.DrawIcon( bgRect, "remove", 12, TextFlag.Center );
		}
		else
		{
			Paint.Pen = Theme.TextControl.WithAlpha( 0.4f * iconIntensity );
			Paint.DrawIcon( bgRect, "add", 12, TextFlag.Center );
		}

		// Group title text
		Paint.Pen = Theme.TextControl.WithAlpha( _expanded ? 1f : 0.8f );
		Paint.SetDefaultFont( 11, weight: 400, sizeInPixels: true );
		Paint.DrawText( LocalRect.Shrink( 26, 0, 0, 0 ), Title, TextFlag.LeftCenter );
	}
}

/// <summary>
/// Group body with left gutter line, matching ControlSheetGroup.
/// </summary>
class InspectorGroupBody : Widget
{
	public InspectorGroupBody( Widget parent ) : base( parent )
	{
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		// Left gutter line
		var r = LocalRect.Shrink( 4, 4, 0, 0 );
		r.Width = 3;
		Paint.SetBrushAndPen( Theme.WindowBackground );
		Paint.DrawRect( r, 5 );

		// Bottom cap
		var capRect = LocalRect.Shrink( 6, 0, 0, 0 );
		capRect.Top = capRect.Bottom - 3;
		capRect.Width = 8;
		Paint.SetBrushAndPen( Theme.WindowBackground );
		Paint.DrawRect( capRect, 5 );
	}
}

/// <summary>
/// Interactive range slider with fill bar and mouse drag support.
/// Matches the inspector's built-in slider appearance using Paint API.
/// </summary>
class InspectorSlider : Widget
{
	public float Min { get; set; }
	public float Max { get; set; }
	public float Value { get; set; }
	public float Step { get; set; }
	public Action<float> OnValueChanged { get; set; }

	private bool _dragging;

	public InspectorSlider( Widget parent, float min, float max, float value, float step = 0f ) : base( parent )
	{
		Min = min;
		Max = max;
		Value = Math.Clamp( value, min, max );
		Step = step;
		MinimumHeight = Theme.RowHeight;
		Cursor = CursorShape.Finger;
		MouseTracking = true;
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		var rect = LocalRect.Shrink( 1 );

		// Track background
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground );
		Paint.DrawRect( rect, 3 );

		// Fill bar
		float fraction = (Max > Min) ? (Value - Min) / (Max - Min) : 0f;
		fraction = Math.Clamp( fraction, 0f, 1f );
		var fillRect = rect;
		fillRect.Width = rect.Width * fraction;

		Paint.SetBrush( Theme.Primary.WithAlpha( 0.6f ) );
		Paint.DrawRect( fillRect, 3 );

		// Value text centered on track
		Paint.ClearBrush();
		Paint.Pen = Theme.TextControl;
		Paint.SetDefaultFont( 10, weight: 400, sizeInPixels: true );
		Paint.DrawText( rect, Value.ToString( Step >= 1f ? "F0" : "F1" ), TextFlag.Center );
	}

	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );
		if ( e.Button == MouseButtons.Left )
		{
			_dragging = true;
			UpdateFromMouse( e.LocalPosition.x );
		}
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		base.OnMouseMove( e );
		if ( _dragging )
			UpdateFromMouse( e.LocalPosition.x );
	}

	protected override void OnMouseReleased( MouseEvent e )
	{
		base.OnMouseReleased( e );
		_dragging = false;
	}

	private void UpdateFromMouse( float localX )
	{
		float fraction = Math.Clamp( localX / Width, 0f, 1f );
		float newValue = Min + fraction * (Max - Min);

		if ( Step > 0f )
			newValue = MathF.Round( newValue / Step ) * Step;

		newValue = Math.Clamp( newValue, Min, Max );

		if ( MathF.Abs( newValue - Value ) > 0.001f )
		{
			Value = newValue;
			OnValueChanged?.Invoke( Value );
			Update();
		}
	}
}

/// <summary>
/// Color swatch widget that displays a filled color rectangle with transparency checkerboard.
/// </summary>
class ColorSwatch : Widget
{
	public Color CurrentColor { get; set; }

	public ColorSwatch( Widget parent, Color color ) : base( parent )
	{
		CurrentColor = color;
		MouseTracking = true;
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		var rect = LocalRect.Shrink( 2 );

		// Checker background for alpha visibility
		Paint.ClearPen();
		Paint.SetBrush( Color.White );
		Paint.DrawRect( rect, 3 );
		Paint.SetBrush( Color.Gray.WithAlpha( 0.3f ) );
		var halfW = rect.Width / 2;
		var halfH = rect.Height / 2;
		Paint.DrawRect( new Rect( rect.Left, rect.Top, halfW, halfH ) );
		Paint.DrawRect( new Rect( rect.Left + halfW, rect.Top + halfH, halfW, halfH ) );

		// Color fill
		Paint.SetBrush( CurrentColor );
		Paint.DrawRect( rect, 3 );

		// Border
		Paint.ClearBrush();
		Paint.SetPen( Theme.BorderLight );
		Paint.DrawRect( rect, 3 );
	}
}
