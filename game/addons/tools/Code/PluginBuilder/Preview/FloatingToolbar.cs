using Sandbox.UI;

namespace Editor.PluginBuilder;

/// <summary>
/// Single floating toolbar that appears inline in the preview, directly above
/// the selected element. Shows actions for the selected element or attribute.
/// Inserted into the preview layout flow — not absolutely positioned.
/// </summary>
public class FloatingToolbar : Widget
{
	private readonly PluginBuilderDock _dock;
	private BlueprintElement _element;
	private string _selectedAttrName;
	private bool _toolbarEnabled = true;

	public bool ToolbarEnabled
	{
		get => _toolbarEnabled;
		set
		{
			_toolbarEnabled = value;
			UpdateVisibility();
		}
	}

	public BlueprintElement Element => _element;
	public string SelectedAttributeName => _selectedAttrName;

	public FloatingToolbar( Widget parent, PluginBuilderDock dock ) : base( parent )
	{
		_dock = dock;
		Visible = false;

		Layout = Layout.Row();
		Layout.Spacing = 2;

		SetStyles( "background-color: rgba(30,30,30,0.95); border: 1px solid rgba(100,180,255,0.4); border-radius: 4px; padding: 2px 4px;" );
	}

	public void ShowForElement( BlueprintElement element )
	{
		_element = element;
		_selectedAttrName = null;
		Rebuild();
	}

	public void ShowForAttribute( BlueprintElement element, string attrName )
	{
		_element = element;
		_selectedAttrName = attrName;
		Rebuild();
	}

	public void Hide()
	{
		_element = null;
		_selectedAttrName = null;
		Visible = false;
	}

	private void UpdateVisibility()
	{
		Visible = _toolbarEnabled && _element != null;
	}

	private void Rebuild()
	{
		Layout.Clear( true );
		UpdateVisibility();
		if ( !Visible ) return;

		if ( _selectedAttrName != null )
			BuildAttributeToolbar();
		else
			BuildElementToolbar();
	}

	private void BuildElementToolbar()
	{
		// Add Element
		var addElemBtn = new Button( "", "add_circle", this );
		addElemBtn.ToolTip = "Add Element";
		addElemBtn.SetStyles( "padding: 2px 4px;" );
		addElemBtn.Clicked = () => ShowAddElementMenu( addElemBtn );
		Layout.Add( addElemBtn );

		// Add Attribute
		var addAttrBtn = new Button( "", "label", this );
		addAttrBtn.ToolTip = "Add Attribute";
		addAttrBtn.SetStyles( "padding: 2px 4px;" );
		addAttrBtn.Clicked = () => ShowAddAttributeMenu( addAttrBtn );
		Layout.Add( addAttrBtn );

		AddSep();

		// Duplicate
		var dupeBtn = new Button( "", "content_copy", this );
		dupeBtn.ToolTip = "Duplicate";
		dupeBtn.SetStyles( "padding: 2px 4px;" );
		dupeBtn.Clicked = () => DuplicateElement();
		Layout.Add( dupeBtn );

		// Delete
		var delBtn = new Button( "", "delete", this );
		delBtn.ToolTip = "Remove Element";
		delBtn.SetStyles( "padding: 2px 4px; color: #ff6b6b;" );
		delBtn.Clicked = () => RemoveElement();
		Layout.Add( delBtn );
	}

	private void BuildAttributeToolbar()
	{
		// Back to element
		var backBtn = new Button( "", "arrow_back", this );
		backBtn.ToolTip = "Back to Element";
		backBtn.SetStyles( "padding: 2px 4px;" );
		backBtn.Clicked = () =>
		{
			_selectedAttrName = null;
			_dock.SelectElement( _element );
			Rebuild();
		};
		Layout.Add( backBtn );

		var attrLabel = new Label( $"[{_selectedAttrName}]", this );
		attrLabel.SetStyles( "color: #6ab4ff; font-weight: bold; font-size: 11px; padding: 0 4px;" );
		Layout.Add( attrLabel );

		AddSep();

		// Position buttons
		var currentPos = AttributePosition.Above;
		if ( _element.AttributePositions.TryGetValue( _selectedAttrName, out var pos ) )
			currentPos = pos;

		var posButtons = new (string icon, string tip, AttributePosition pos)[]
		{
			("arrow_back", "Position: Left", AttributePosition.Left),
			("arrow_upward", "Position: Above", AttributePosition.Above),
			("arrow_downward", "Position: Below", AttributePosition.Below),
			("arrow_forward", "Position: Right", AttributePosition.Right)
		};

		var attrName = _selectedAttrName;
		foreach ( var (icon, tip, p) in posButtons )
		{
			var btn = new Button( "", icon, this );
			btn.ToolTip = tip;
			btn.SetStyles( currentPos == p
				? "padding: 2px 3px; background-color: rgba(100,180,255,0.3); border-radius: 3px;"
				: "padding: 2px 3px;" );
			btn.Clicked = () =>
			{
				_element.AttributePositions[attrName] = p;
				_dock.MarkDirty();
				Rebuild();
			};
			Layout.Add( btn );
		}

		AddSep();

		// Remove attribute
		var removeBtn = new Button( "", "delete", this );
		removeBtn.ToolTip = $"Remove [{_selectedAttrName}]";
		removeBtn.SetStyles( "padding: 2px 4px; color: #ff6b6b;" );
		var capturedName = _selectedAttrName;
		removeBtn.Clicked = () =>
		{
			_element.Attributes.Remove( capturedName );
			_element.AttributePositions.Remove( capturedName );
			_selectedAttrName = null;
			_dock.MarkDirty();
			Rebuild();
		};
		Layout.Add( removeBtn );
	}

	private void AddSep()
	{
		var sep = new Widget( this );
		sep.MinimumWidth = 1;
		sep.MaximumWidth = 1;
		sep.MinimumHeight = 16;
		sep.SetStyles( "background-color: rgba(255,255,255,0.15);" );
		Layout.Add( sep );
	}

	private void ShowAddElementMenu( Widget anchor )
	{
		var menu = new Menu( anchor );
		menu.AddOption( "Property", "circle", () => _dock.AddElementAtSelection( () => new BlueprintElement { ElementType = ElementType.Property, Name = "NewProperty", PropertyType = PropertyType.String } ) );
		menu.AddOption( "Group", "folder", () => _dock.AddElementAtSelection( () => new BlueprintElement { ElementType = ElementType.Group, Name = "NewGroup", PropertyType = PropertyType.String } ) );
		menu.AddOption( "Toggle Group", "toggle_on", () => _dock.AddElementAtSelection( () => new BlueprintElement { ElementType = ElementType.ToggleGroup, Name = "NewToggleGroup" } ) );
		menu.AddOption( "Header", "title", () => _dock.AddElementAtSelection( () => new BlueprintElement { ElementType = ElementType.Header, Name = "Header" } ) );
		menu.AddOption( "Space", "space_bar", () => _dock.AddElementAtSelection( () => new BlueprintElement { ElementType = ElementType.Space, Name = "Space" } ) );
		menu.AddOption( "Info Box", "info", () => _dock.AddElementAtSelection( () => new BlueprintElement { ElementType = ElementType.InfoBox, Name = "Info" } ) );
		menu.OpenAtCursor();
	}

	private void ShowAddAttributeMenu( Widget anchor )
	{
		if ( _element == null ) return;

		var menu = new Menu( anchor );
		var applicable = AttributeCatalog.GetApplicable( _element.PropertyType, _element.ElementType );

		AttributeCategory? lastCategory = null;
		foreach ( var attr in applicable.OrderBy( a => a.Category ).ThenBy( a => a.Name ) )
		{
			if ( _element.Attributes.ContainsKey( attr.Name ) ) continue;

			if ( lastCategory != null && lastCategory != attr.Category )
				menu.AddSeparator();
			lastCategory = attr.Category;

			menu.AddOption( $"[{attr.Name}]", attr.Icon, () =>
			{
				_element.Attributes[attr.Name] = new Dictionary<string, object>();
				_element.AttributePositions[attr.Name] = AttributePosition.Above;
				_dock.MarkDirty();
			} );
		}

		if ( !applicable.Any( a => !_element.Attributes.ContainsKey( a.Name ) ) )
		{
			menu.AddOption( "(no more attributes available)", "", () => { } );
		}

		menu.OpenAtCursor();
	}

	private void DuplicateElement()
	{
		if ( _element == null ) return;

		var blueprint = _dock.ActiveBlueprint;
		if ( blueprint == null ) return;

		var clone = _element.Clone();
		clone.Name = _element.Name + "_copy";
		_dock.AddElementAtSelection( () => clone );
	}

	private void RemoveElement()
	{
		if ( _element == null ) return;

		var blueprint = _dock.ActiveBlueprint;
		if ( blueprint == null ) return;

		var list = FindParentList( blueprint, _element );
		if ( list == null ) return;

		var idx = list.IndexOf( _element );
		var element = _element;
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

		Hide();
		_dock.MarkDirty();
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
}
