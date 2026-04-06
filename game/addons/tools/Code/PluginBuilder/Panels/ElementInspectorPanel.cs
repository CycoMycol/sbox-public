using Sandbox.UI;

namespace Editor.PluginBuilder;

/// <summary>
/// Right-side property editor for the selected blueprint element.
/// Shows: name, display name, type, attributes, AI hint, help text.
/// </summary>
public class ElementInspectorPanel : Widget
{
	private readonly PluginBuilderDock _dock;
	private BlueprintElement _selected;
	private Widget _content;
	private bool _selfEditing;

	public ElementInspectorPanel( Widget parent, PluginBuilderDock dock ) : base( parent )
	{
		_dock = dock;

		Layout = Layout.Column();
		Layout.Spacing = 4;
		Layout.Margin = 8;

		var header = Layout.Add( new Label( "Inspector", this ) );
		header.SetStyles( "font-weight: bold; font-size: 14px;" );

		_content = new Widget( this );
		_content.Layout = Layout.Column();
		Layout.Add( _content, 1 );

		_dock.OnElementSelected += OnElementSelected;
		_dock.OnBlueprintChanged += OnBlueprintChanged;

		ShowEmpty();
	}

	public override void OnDestroyed()
	{
		_dock.OnElementSelected -= OnElementSelected;
		_dock.OnBlueprintChanged -= OnBlueprintChanged;
		base.OnDestroyed();
	}

	private void OnBlueprintChanged()
	{
		// Don't rebuild when we're the source of the change (prevents focus loss)
		if ( _selfEditing ) return;
		if ( _selected != null )
			Rebuild();
	}

	private void OnElementSelected( BlueprintElement element )
	{
		_selected = element;
		Rebuild();
	}

	private void ShowEmpty()
	{
		_content.Layout.Clear( true );
		var label = new Label( "Select an element to edit its properties.", _content );
		label.SetStyles( "color: #888;" );
		_content.Layout.Add( label );
	}

	private void Rebuild()
	{
		_content.Layout.Clear( true );

		if ( _selected == null )
		{
			ShowEmpty();
			return;
		}

		var scroll = new ScrollArea( _content );
		scroll.Canvas = new Widget( scroll );
		scroll.Canvas.Layout = Layout.Column();
		scroll.Canvas.Layout.Spacing = 6;
		scroll.Canvas.Layout.Margin = 4;
		_content.Layout.Add( scroll, 1 );

		var canvas = scroll.Canvas;

		// Element type indicator
		var typeLabel = new Label( $"{_selected.ElementType}", canvas );
		typeLabel.SetStyles( "color: #aaa; font-size: 11px;" );
		canvas.Layout.Add( typeLabel );

		// Name
		canvas.Layout.Add( new Label( "Name", canvas ) );
		var nameEntry = new LineEdit( canvas );
		nameEntry.Text = _selected.Name;
		nameEntry.TextChanged += ( val ) =>
		{
			_selected.Name = val;
			_selfEditing = true;
			_dock.MarkDirty();
			_selfEditing = false;
		};
		canvas.Layout.Add( nameEntry );

		// Property Type (only for Property elements)
		if ( _selected.ElementType == ElementType.Property )
		{
			canvas.Layout.Add( new Label( "Property Type", canvas ) );
			var typeDropdown = new ComboBox( canvas );
			foreach ( var pt in Enum.GetValues<PropertyType>() )
				typeDropdown.AddItem( pt.ToString(), null );
			typeDropdown.CurrentIndex = (int)_selected.PropertyType;
			typeDropdown.ItemChanged += () =>
			{
				var newType = (PropertyType)typeDropdown.CurrentIndex;
				if ( newType == _selected.PropertyType ) return;
				_selected.PropertyType = newType;
				_selfEditing = true;
				_dock.MarkDirty();
				_selfEditing = false;
			};
			canvas.Layout.Add( typeDropdown );
		}

		// Space height editor (only for Space elements)
		if ( _selected.ElementType == ElementType.Space )
		{
			canvas.Layout.Add( new Label( "Height", canvas ) );

			// Read current height from the Space attribute
			float currentHeight = 8f;
			if ( _selected.Attributes.TryGetValue( "Space", out var spaceParams ) )
			{
				if ( spaceParams.TryGetValue( "height", out var hVal ) )
				{
					if ( hVal is float f ) currentHeight = f;
					else if ( hVal is double d ) currentHeight = (float)d;
					else if ( hVal is int i ) currentHeight = i;
					else if ( hVal is long l ) currentHeight = l;
					else if ( hVal is string s && float.TryParse( s, out var parsed ) ) currentHeight = parsed;
					else if ( hVal is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Number ) currentHeight = je.GetSingle();
				}
			}

			var heightEntry = new LineEdit( canvas );
			heightEntry.Text = currentHeight.ToString( "F0" );
			heightEntry.PlaceholderText = "8";
			heightEntry.TextChanged += ( val ) =>
			{
				if ( !float.TryParse( val, out var h ) ) return;
				if ( h < 1 ) h = 1;
				if ( h > 200 ) h = 200;

				if ( !_selected.Attributes.ContainsKey( "Space" ) )
					_selected.Attributes["Space"] = new Dictionary<string, object>();

				_selected.Attributes["Space"]["height"] = h;
				_selfEditing = true;
				_dock.MarkDirty();
				_selfEditing = false;
			};
			canvas.Layout.Add( heightEntry );
		}

		// Attributes section
		canvas.Layout.AddSpacingCell( 8 );
		var attrHeader = new Label( "Attributes", canvas );
		attrHeader.SetStyles( "font-weight: bold;" );
		canvas.Layout.Add( attrHeader );

		if ( _selected.Attributes.Count == 0 )
		{
			var noAttrs = new Label( "No attributes applied.", canvas );
			noAttrs.SetStyles( "color: #888; font-size: 11px;" );
			canvas.Layout.Add( noAttrs );
		}
		else
		{
			foreach ( var attr in _selected.Attributes )
			{
				AddAttributeEditor( canvas, attr.Key, attr.Value );
			}
		}

		// Add Attribute button
		var addAttrBtn = new Button( "Add Attribute", "add", canvas );
		addAttrBtn.Clicked = () => ShowAddAttributeMenu();
		canvas.Layout.Add( addAttrBtn );

		// Notes section (AI Hint + Help Text)
		canvas.Layout.AddSpacingCell( 12 );
		var notesHeader = new Label( "Notes", canvas );
		notesHeader.SetStyles( "font-weight: bold;" );
		canvas.Layout.Add( notesHeader );

		canvas.Layout.Add( new Label( "AI Hint", canvas ) );
		var aiHint = new TextEdit( canvas );
		aiHint.MinimumHeight = 60;
		aiHint.PlainText = _selected.AiHint;
		aiHint.TextChanged += ( text ) =>
		{
			_selected.AiHint = text;
			_selfEditing = true;
			_dock.MarkDirty();
			_selfEditing = false;
		};
		canvas.Layout.Add( aiHint );

		canvas.Layout.Add( new Label( "Help Text (Description tooltip)", canvas ) );
		var helpText = new TextEdit( canvas );
		helpText.MinimumHeight = 60;
		helpText.PlainText = _selected.HelpText;
		helpText.TextChanged += ( text ) =>
		{
			_selected.HelpText = text;
			_selfEditing = true;
			_dock.MarkDirty();
			_selfEditing = false;
		};
		canvas.Layout.Add( helpText );

		canvas.Layout.AddStretchCell( 1 );
	}

	private void AddAttributeEditor( Widget canvas, string attrName, Dictionary<string, object> parameters )
	{
		var row = new Widget( canvas );
		row.Layout = Layout.Row();
		row.Layout.Spacing = 4;

		var def = AttributeCatalog.Get( attrName );
		var icon = def?.Icon ?? "extension";

		var label = new Label( $"[{attrName}]", row );
		label.SetStyles( "font-weight: bold;" );
		row.Layout.Add( label );

		row.Layout.AddStretchCell( 1 );

		var removeBtn = new Button( "", "close", row );
		removeBtn.Clicked = () =>
		{
			_selected.Attributes.Remove( attrName );
			_selfEditing = true;
			_dock.MarkDirty();
			_selfEditing = false;
			Rebuild();
		};
		removeBtn.ToolTip = $"Remove [{attrName}]";
		row.Layout.Add( removeBtn );

		canvas.Layout.Add( row );

		// Parameter editors
		if ( def != null )
		{
			foreach ( var param in def.Parameters )
			{
				var paramRow = new Widget( canvas );
				paramRow.Layout = Layout.Row();
				paramRow.Layout.Spacing = 4;
				paramRow.Layout.Margin = new Margin( 16, 0, 0, 0 );

				paramRow.Layout.Add( new Label( param.Name, paramRow ) );

				var entry = new LineEdit( paramRow );
				if ( parameters.TryGetValue( param.Name, out var val ) )
					entry.Text = val?.ToString() ?? "";
				entry.TextChanged += ( v ) =>
				{
					parameters[param.Name] = v;
					_selfEditing = true;
					_dock.MarkDirty();
					_selfEditing = false;
				};
				paramRow.Layout.Add( entry, 1 );

				canvas.Layout.Add( paramRow );
			}
		}
	}

	private void ShowAddAttributeMenu()
	{
		if ( _selected == null ) return;

		var menu = new Menu( this );
		var applicable = AttributeCatalog.GetApplicable( _selected.PropertyType, _selected.ElementType );

		AttributeCategory? lastCategory = null;
		foreach ( var attr in applicable.OrderBy( a => a.Category ).ThenBy( a => a.Name ) )
		{
			if ( _selected.Attributes.ContainsKey( attr.Name ) )
				continue;

			if ( lastCategory != null && lastCategory != attr.Category )
				menu.AddSeparator();
			lastCategory = attr.Category;

			menu.AddOption( $"[{attr.Name}]", attr.Icon, () =>
			{
				_selected.Attributes[attr.Name] = new Dictionary<string, object>();
				_selfEditing = true;
				_dock.MarkDirty();
				_selfEditing = false;
				Rebuild();
			} );
		}

		menu.OpenAtCursor();
	}
}
