using Sandbox.UI;

namespace Editor.PluginBuilder;

/// <summary>
/// Floating toolbar that appears above the selected element in the preview panel.
/// Provides quick-access buttons: Add Element, Add Attribute, Delete, Duplicate,
/// and attribute position controls (Left, Right, Above, Below).
/// </summary>
public class FloatingToolbar : Widget
{
	private readonly PluginBuilderDock _dock;
	private BlueprintElement _element;

	private Widget _positionRow;
	private Button _btnAbove;
	private Button _btnBelow;
	private Button _btnLeft;
	private Button _btnRight;

	public FloatingToolbar( Widget parent, PluginBuilderDock dock ) : base( parent )
	{
		_dock = dock;
		Visible = false;

		Layout = Layout.Column();
		Layout.Spacing = 2;

		SetStyles( "background-color: rgba(30,30,30,0.95); border: 1px solid rgba(100,180,255,0.4); border-radius: 6px; padding: 4px 6px;" );

		BuildToolbar();
	}

	private void BuildToolbar()
	{
		// Main action row
		var actionRow = new Widget( this );
		actionRow.Layout = Layout.Row();
		actionRow.Layout.Spacing = 2;

		// Add Element dropdown
		var addElemBtn = new Button( "", "add_circle", actionRow );
		addElemBtn.ToolTip = "Add Element";
		addElemBtn.SetStyles( "padding: 2px 4px;" );
		addElemBtn.Clicked = () => ShowAddElementMenu( addElemBtn );
		actionRow.Layout.Add( addElemBtn );

		// Add Attribute dropdown
		var addAttrBtn = new Button( "", "label", actionRow );
		addAttrBtn.ToolTip = "Add Attribute";
		addAttrBtn.SetStyles( "padding: 2px 4px;" );
		addAttrBtn.Clicked = () => ShowAddAttributeMenu( addAttrBtn );
		actionRow.Layout.Add( addAttrBtn );

		// Separator
		var sep1 = new Widget( actionRow );
		sep1.MinimumWidth = 1;
		sep1.MaximumWidth = 1;
		sep1.SetStyles( "background-color: rgba(255,255,255,0.15);" );
		actionRow.Layout.Add( sep1 );

		// Duplicate
		var dupeBtn = new Button( "", "content_copy", actionRow );
		dupeBtn.ToolTip = "Duplicate";
		dupeBtn.SetStyles( "padding: 2px 4px;" );
		dupeBtn.Clicked = () => DuplicateElement();
		actionRow.Layout.Add( dupeBtn );

		// Delete
		var delBtn = new Button( "", "delete", actionRow );
		delBtn.ToolTip = "Remove Element";
		delBtn.SetStyles( "padding: 2px 4px; color: #ff6b6b;" );
		delBtn.Clicked = () => RemoveElement();
		actionRow.Layout.Add( delBtn );

		// Separator
		var sep2 = new Widget( actionRow );
		sep2.MinimumWidth = 1;
		sep2.MaximumWidth = 1;
		sep2.SetStyles( "background-color: rgba(255,255,255,0.15);" );
		actionRow.Layout.Add( sep2 );

		// Attribute position buttons
		_btnLeft = new Button( "", "arrow_back", actionRow );
		_btnLeft.ToolTip = "Attributes: Left";
		_btnLeft.SetStyles( "padding: 2px 3px;" );
		_btnLeft.Clicked = () => SetAttributePosition( AttributePosition.Left );
		actionRow.Layout.Add( _btnLeft );

		_btnAbove = new Button( "", "arrow_upward", actionRow );
		_btnAbove.ToolTip = "Attributes: Above";
		_btnAbove.SetStyles( "padding: 2px 3px;" );
		_btnAbove.Clicked = () => SetAttributePosition( AttributePosition.Above );
		actionRow.Layout.Add( _btnAbove );

		_btnBelow = new Button( "", "arrow_downward", actionRow );
		_btnBelow.ToolTip = "Attributes: Below";
		_btnBelow.SetStyles( "padding: 2px 3px;" );
		_btnBelow.Clicked = () => SetAttributePosition( AttributePosition.Below );
		actionRow.Layout.Add( _btnBelow );

		_btnRight = new Button( "", "arrow_forward", actionRow );
		_btnRight.ToolTip = "Attributes: Right";
		_btnRight.SetStyles( "padding: 2px 3px;" );
		_btnRight.Clicked = () => SetAttributePosition( AttributePosition.Right );
		actionRow.Layout.Add( _btnRight );

		Layout.Add( actionRow );

		// Attribute list row — shows current attributes with individual position controls
		_positionRow = new Widget( this );
		_positionRow.Layout = Layout.Column();
		_positionRow.Layout.Spacing = 1;
		Layout.Add( _positionRow );
	}

	public void ShowForElement( BlueprintElement element )
	{
		_element = element;
		Visible = element != null;
		RebuildAttributeList();
	}

	public void Hide()
	{
		_element = null;
		Visible = false;
	}

	private void RebuildAttributeList()
	{
		_positionRow.Layout.Clear( true );

		if ( _element == null || _element.Attributes.Count == 0 )
			return;

		foreach ( var attr in _element.Attributes )
		{
			var row = new Widget( _positionRow );
			row.Layout = Layout.Row();
			row.Layout.Spacing = 2;

			var tag = new Label( $"[{attr.Key}]", row );
			tag.SetStyles( "color: rgba(100,180,255,0.8); font-size: 10px; padding: 1px 3px;" );
			row.Layout.Add( tag );

			row.Layout.AddStretchCell( 1 );

			var currentPos = AttributePosition.Above;
			if ( _element.AttributePositions.TryGetValue( attr.Key, out var pos ) )
				currentPos = pos;

			var attrName = attr.Key;

			var lBtn = new Button( "", "arrow_back", row );
			lBtn.SetStyles( GetPositionBtnStyle( currentPos == AttributePosition.Left ) );
			lBtn.ToolTip = "Left";
			lBtn.Clicked = () => SetSingleAttributePosition( attrName, AttributePosition.Left );
			row.Layout.Add( lBtn );

			var aBtn = new Button( "", "arrow_upward", row );
			aBtn.SetStyles( GetPositionBtnStyle( currentPos == AttributePosition.Above ) );
			aBtn.ToolTip = "Above";
			aBtn.Clicked = () => SetSingleAttributePosition( attrName, AttributePosition.Above );
			row.Layout.Add( aBtn );

			var bBtn = new Button( "", "arrow_downward", row );
			bBtn.SetStyles( GetPositionBtnStyle( currentPos == AttributePosition.Below ) );
			bBtn.ToolTip = "Below";
			bBtn.Clicked = () => SetSingleAttributePosition( attrName, AttributePosition.Below );
			row.Layout.Add( bBtn );

			var rBtn = new Button( "", "arrow_forward", row );
			rBtn.SetStyles( GetPositionBtnStyle( currentPos == AttributePosition.Right ) );
			rBtn.ToolTip = "Right";
			rBtn.Clicked = () => SetSingleAttributePosition( attrName, AttributePosition.Right );
			row.Layout.Add( rBtn );

			var removeBtn = new Button( "", "close", row );
			removeBtn.SetStyles( "padding: 1px 2px; color: #ff6b6b; font-size: 10px;" );
			removeBtn.ToolTip = $"Remove [{attrName}]";
			removeBtn.Clicked = () =>
			{
				_element.Attributes.Remove( attrName );
				_element.AttributePositions.Remove( attrName );
				_dock.MarkDirty();
				RebuildAttributeList();
			};
			row.Layout.Add( removeBtn );

			_positionRow.Layout.Add( row );
		}
	}

	private static string GetPositionBtnStyle( bool active )
	{
		return active
			? "padding: 1px 2px; background-color: rgba(100,180,255,0.3); border-radius: 3px;"
			: "padding: 1px 2px;";
	}

	private void SetAttributePosition( AttributePosition position )
	{
		if ( _element == null ) return;

		// Set all current attributes to this position
		foreach ( var attr in _element.Attributes )
		{
			_element.AttributePositions[attr.Key] = position;
		}

		_dock.MarkDirty();
		RebuildAttributeList();
	}

	private void SetSingleAttributePosition( string attrName, AttributePosition position )
	{
		if ( _element == null ) return;

		_element.AttributePositions[attrName] = position;
		_dock.MarkDirty();
		RebuildAttributeList();
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
				RebuildAttributeList();
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
