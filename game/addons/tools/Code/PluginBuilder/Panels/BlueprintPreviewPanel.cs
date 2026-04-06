using Sandbox.UI;

namespace Editor.PluginBuilder;

/// <summary>
/// Interactive live preview of the designed inspector.
/// The floating toolbar is inserted inline directly above the selected element.
/// Attributes render as visible, clickable tags positioned relative to the element.
/// Sort arrows allow reordering elements. Hidden elements are dimmed.
/// </summary>
public class BlueprintPreviewPanel : Widget
{
	private readonly PluginBuilderDock _dock;
	private Widget _previewContainer;
	private PreviewRenderer _renderer;
	private readonly Dictionary<string, Widget> _elementWidgets = new();
	private string _selectedId;
	private string _selectedAttrName;
	private FloatingToolbar _toolbar;
	private Button _toolbarToggleBtn;
	private bool _toolbarVisible = true;

	private Label _blueprintNameLabel;

	public BlueprintPreviewPanel( Widget parent, PluginBuilderDock dock ) : base( parent )
	{
		_dock = dock;
		_renderer = new PreviewRenderer( dock );

		Layout = Layout.Column();
		Layout.Spacing = 4;
		Layout.Margin = 8;

		// Header row: "Preview" + blueprint name + toggle button
		var header = Layout.Add( new Widget( this ) );
		header.Layout = Layout.Row();
		header.Layout.Spacing = 8;

		var title = new Label( "Preview", header );
		title.SetStyles( "font-weight: bold; font-size: 14px;" );
		header.Layout.Add( title );

		_blueprintNameLabel = new Label( "", header );
		_blueprintNameLabel.SetStyles( "font-size: 13px; color: #aaa;" );
		header.Layout.Add( _blueprintNameLabel );

		header.Layout.AddStretchCell( 1 );

		// Toolbar toggle button — floating window icon
		_toolbarToggleBtn = new Button( "", "picture_in_picture", header );
		_toolbarToggleBtn.ToolTip = "Toggle floating toolbar";
		_toolbarToggleBtn.SetStyles( "padding: 2px 4px;" );
		_toolbarToggleBtn.Clicked = () =>
		{
			if ( _toolbar != null )
			{
				_toolbarVisible = !_toolbarVisible;
				_toolbar.ToolbarEnabled = _toolbarVisible;
				UpdateToggleButtonStyle();
				RebuildPreview();
			}
		};
		header.Layout.Add( _toolbarToggleBtn );

		// Preview scroll area
		var scroll = new ScrollArea( this );
		scroll.Canvas = new Widget( scroll );
		scroll.Canvas.Layout = Layout.Column();
		_previewContainer = scroll.Canvas;
		Layout.Add( scroll, 1 );

		// Create the floating toolbar (will be inserted inline during rebuild)
		_toolbar = new FloatingToolbar( _previewContainer, dock );

		_dock.OnBlueprintChanged += RebuildPreview;
		_dock.OnElementSelected += OnElementSelected;

		RebuildPreview();
	}

	public override void OnDestroyed()
	{
		_dock.OnBlueprintChanged -= RebuildPreview;
		_dock.OnElementSelected -= OnElementSelected;
		base.OnDestroyed();
	}

	private void UpdateToggleButtonStyle()
	{
		if ( _toolbarVisible )
			_toolbarToggleBtn.SetStyles( "padding: 2px 4px; background-color: rgba(100,180,255,0.2); border-radius: 3px;" );
		else
			_toolbarToggleBtn.SetStyles( "padding: 2px 4px;" );
	}

	private void OnElementSelected( BlueprintElement element )
	{
		var newId = element?.Id;
		if ( newId == _selectedId && _selectedAttrName == null ) return;
		_selectedId = newId;
		_selectedAttrName = null;

		// Rebuild to reposition toolbar inline above the newly selected element
		RebuildPreview();
	}

	public void RebuildPreview()
	{
		_previewContainer.Layout.Clear( true );
		_elementWidgets.Clear();
		_renderer.ClearTracking();

		// Recreate toolbar fresh each rebuild so it can be placed inline
		_toolbar = new FloatingToolbar( _previewContainer, _dock );
		_toolbar.ToolbarEnabled = _toolbarVisible;
		UpdateToggleButtonStyle();

		var blueprint = _dock.ActiveBlueprint;
		if ( blueprint == null )
		{
			_blueprintNameLabel.Text = "";
			var empty = new Label( "No blueprint loaded.", _previewContainer );
			empty.SetStyles( "color: #888;" );
			_previewContainer.Layout.Add( empty );
			return;
		}

		// Update header with blueprint name
		_blueprintNameLabel.Text = $"— {blueprint.Name}";

		// Find selected element to determine toolbar placement
		BlueprintElement selectedElement = null;
		if ( _selectedId != null )
			selectedElement = FindElementById( blueprint.Elements, _selectedId );

		// Render elements with inline toolbar above the selected one
		RenderElements( _previewContainer, blueprint.Elements, selectedElement );

		_previewContainer.Layout.AddStretchCell( 1 );

		// Highlight the selected element
		if ( _selectedId != null && _elementWidgets.TryGetValue( _selectedId, out var selWidget ) )
		{
			selWidget.SetStyles( "border-left: 3px solid rgba(100,180,255,0.8); border-right: 3px solid rgba(100,180,255,0.8);" );
		}

		// If toolbar wasn't placed (element not visible at top level), show it for the element anyway
		if ( selectedElement != null && _toolbarVisible )
		{
			if ( _toolbar.Element == null )
			{
				if ( _selectedAttrName != null && selectedElement.Attributes.ContainsKey( _selectedAttrName ) )
					_toolbar.ShowForAttribute( selectedElement, _selectedAttrName );
				else
					_toolbar.ShowForElement( selectedElement );
			}
		}
		else
		{
			_toolbar.Hide();
		}
	}

	private BlueprintElement FindElementById( List<BlueprintElement> elements, string id )
	{
		if ( elements == null ) return null;
		foreach ( var el in elements )
		{
			if ( el.Id == id ) return el;
			var found = FindElementById( el.Children, id );
			if ( found != null ) return found;
		}
		return null;
	}

	private void RenderElements( Widget container, List<BlueprintElement> elements, BlueprintElement selectedElement )
	{
		foreach ( var element in elements )
		{
			// Skip hidden elements in preview
			if ( element.Hidden ) continue;

			// Insert toolbar + sort controls above the selected element (only when toolbar visible)
			if ( element == selectedElement && _toolbarVisible )
			{
				if ( _selectedAttrName != null && element.Attributes.ContainsKey( _selectedAttrName ) )
				{
					// Attribute position control row
					var attrSortRow = new Widget( container );
					attrSortRow.Layout = Layout.Row();
					attrSortRow.Layout.Spacing = 2;
					attrSortRow.Layout.Margin = new Margin( 0, 2, 0, 2 );

					var capturedAttr = _selectedAttrName;
					var capturedElement = element;

					var currentPos = element.AttributePositions.TryGetValue( capturedAttr, out var cp ) ? cp : AttributePosition.Below;

					AddPositionButton( attrSortRow, "arrow_upward", "Above", capturedElement, capturedAttr, AttributePosition.Above, currentPos );
					AddPositionButton( attrSortRow, "arrow_downward", "Below", capturedElement, capturedAttr, AttributePosition.Below, currentPos );
					AddPositionButton( attrSortRow, "arrow_back", "Left", capturedElement, capturedAttr, AttributePosition.Left, currentPos );
					AddPositionButton( attrSortRow, "arrow_forward", "Right", capturedElement, capturedAttr, AttributePosition.Right, currentPos );

					// Show current position label
					var posLabel = new Label( $"[{capturedAttr}] → {currentPos}", attrSortRow );
					posLabel.SetStyles( "color: #6ab4ff; font-size: 10px; margin-left: 6px;" );
					attrSortRow.Layout.Add( posLabel );

					attrSortRow.Layout.AddStretchCell( 1 );
					container.Layout.Add( attrSortRow );

					_toolbar.ShowForAttribute( element, capturedAttr );
					container.Layout.Add( _toolbar );
				}
				else
				{
					// Element sort control row
					_selectedAttrName = null;

					var sortRow = new Widget( container );
					sortRow.Layout = Layout.Row();
					sortRow.Layout.Spacing = 2;
					sortRow.Layout.Margin = new Margin( 0, 2, 0, 2 );

					var upBtn = new Button( "", "arrow_upward", sortRow );
					upBtn.ToolTip = "Move element up";
					upBtn.SetStyles( "padding: 1px 3px; font-size: 10px;" );
					upBtn.Clicked = () => MoveElementInPreview( element, -1 );
					sortRow.Layout.Add( upBtn );

					var downBtn = new Button( "", "arrow_downward", sortRow );
					downBtn.ToolTip = "Move element down";
					downBtn.SetStyles( "padding: 1px 3px; font-size: 10px;" );
					downBtn.Clicked = () => MoveElementInPreview( element, 1 );
					sortRow.Layout.Add( downBtn );

					var leftBtn = new Button( "", "arrow_back", sortRow );
					leftBtn.ToolTip = "Unnest (move to parent level)";
					leftBtn.SetStyles( "padding: 1px 3px; font-size: 10px;" );
					leftBtn.Clicked = () => IndentElement( element, -1 );
					sortRow.Layout.Add( leftBtn );

					var rightBtn = new Button( "", "arrow_forward", sortRow );
					rightBtn.ToolTip = "Nest inside previous sibling";
					rightBtn.SetStyles( "padding: 1px 3px; font-size: 10px;" );
					rightBtn.Clicked = () => IndentElement( element, 1 );
					sortRow.Layout.Add( rightBtn );

					sortRow.Layout.AddStretchCell( 1 );
					container.Layout.Add( sortRow );

					_toolbar.ShowForElement( element );
					container.Layout.Add( _toolbar );
				}
			}

			// Render attribute tags ABOVE the element (for Above-positioned attributes)
			RenderPositionedAttributes( container, element, AttributePosition.Above );

			// Render the element itself (hidden elements get dimmed wrapper)
			var widget = _renderer.RenderElement( container, element );
			if ( widget != null )
			{
				// For Left/Right positioned attributes, wrap in a row
				var hasLeft = HasAttributeAtPosition( element, AttributePosition.Left );
				var hasRight = HasAttributeAtPosition( element, AttributePosition.Right );

				if ( hasLeft || hasRight )
				{
					var row = new Widget( container );
					row.Layout = Layout.Row();
					row.Layout.Spacing = 4;

					if ( hasLeft )
						RenderPositionedAttributesInline( row, element, AttributePosition.Left );

					row.Layout.Add( widget, 1 );

					if ( hasRight )
						RenderPositionedAttributesInline( row, element, AttributePosition.Right );

					container.Layout.Add( row );
					_elementWidgets[element.Id] = row;
				}
				else
				{
					container.Layout.Add( widget );
					_elementWidgets[element.Id] = widget;
				}

				// Render attribute tags BELOW the element
				RenderPositionedAttributes( container, element, AttributePosition.Below );

				// Recursively track child widgets
				TrackChildWidgets( element, selectedElement );
			}
		}
	}

	private bool HasAttributeAtPosition( BlueprintElement element, AttributePosition targetPos )
	{
		foreach ( var attr in element.Attributes )
		{
			var pos = element.AttributePositions.TryGetValue( attr.Key, out var p ) ? p : AttributePosition.Below;
			if ( pos == targetPos ) return true;
		}
		return false;
	}

	/// <summary>
	/// Renders attribute tags for a specific position (Above or Below) as a row of buttons.
	/// </summary>
	private void RenderPositionedAttributes( Widget container, BlueprintElement element, AttributePosition targetPos )
	{
		var matching = new List<KeyValuePair<string, Dictionary<string, object>>>();
		foreach ( var attr in element.Attributes )
		{
			var pos = element.AttributePositions.TryGetValue( attr.Key, out var p ) ? p : AttributePosition.Below;
			if ( pos == targetPos )
				matching.Add( attr );
		}

		if ( matching.Count == 0 ) return;

		var attrRow = new Widget( container );
		attrRow.Layout = Layout.Row();
		attrRow.Layout.Spacing = 4;
		attrRow.Layout.Margin = new Margin( 24, 0, 4, 2 );

		foreach ( var attr in matching )
		{
			AddAttributeTag( attrRow, element, attr.Key );
		}

		container.Layout.Add( attrRow );
	}

	/// <summary>
	/// Renders attribute tags for Left/Right positions inline in a row widget.
	/// </summary>
	private void RenderPositionedAttributesInline( Widget row, BlueprintElement element, AttributePosition targetPos )
	{
		foreach ( var attr in element.Attributes )
		{
			var pos = element.AttributePositions.TryGetValue( attr.Key, out var p ) ? p : AttributePosition.Below;
			if ( pos == targetPos )
			{
				var col = new Widget( row );
				col.Layout = Layout.Column();
				AddAttributeTag( col, element, attr.Key );
				row.Layout.Add( col );
			}
		}
	}

	private void AddAttributeTag( Widget parent, BlueprintElement element, string attrName )
	{
		var def = AttributeCatalog.Get( attrName );
		var icon = def?.Icon ?? "label";

		var tag = new Button( $"[{attrName}]", icon, parent );
		tag.SetStyles( "color: #6ab4ff; font-size: 10px; background-color: rgba(100,180,255,0.12); border-radius: 3px; padding: 2px 6px; border: 1px solid rgba(100,180,255,0.2);" );
		tag.ToolTip = def?.Description ?? attrName;
		tag.Cursor = CursorShape.Finger;

		// Clicking an attribute selects it for position editing
		tag.Clicked = () =>
		{
			_selectedId = element.Id;
			_selectedAttrName = attrName;
			RebuildPreview();
		};

		parent.Layout.Add( tag );
	}

	private void AddPositionButton( Widget row, string icon, string label, BlueprintElement element, string attrName, AttributePosition targetPos, AttributePosition currentPos )
	{
		var btn = new Button( "", icon, row );
		btn.ToolTip = $"Position attribute {label.ToLower()}";
		var isActive = currentPos == targetPos;
		btn.SetStyles( isActive
			? "padding: 1px 3px; font-size: 10px; background-color: rgba(100,180,255,0.3); border-radius: 2px;"
			: "padding: 1px 3px; font-size: 10px;" );
		btn.Clicked = () =>
		{
			element.AttributePositions[attrName] = targetPos;
			_selectedAttrName = attrName;
			_selectedId = element.Id;
			_dock.MarkDirty();
		};
		row.Layout.Add( btn );
	}

	private void MoveElementInPreview( BlueprintElement element, int direction )
	{
		var blueprint = _dock.ActiveBlueprint;
		if ( blueprint == null ) return;

		var list = FindParentList( blueprint, element );
		if ( list == null ) return;

		var idx = list.IndexOf( element );
		var newIdx = idx + direction;
		if ( newIdx < 0 || newIdx >= list.Count ) return;

		list.RemoveAt( idx );
		list.Insert( newIdx, element );
		_dock.MarkDirty();
	}

	private void IndentElement( BlueprintElement element, int direction )
	{
		var blueprint = _dock.ActiveBlueprint;
		if ( blueprint == null ) return;

		var list = FindParentList( blueprint, element );
		if ( list == null ) return;

		if ( direction > 0 )
		{
			// Nest inside previous sibling
			var idx = list.IndexOf( element );
			if ( idx <= 0 ) return;

			var prevSibling = list[idx - 1];
			list.Remove( element );
			prevSibling.Children.Add( element );
			_dock.MarkDirty();
		}
		else
		{
			// Unnest — move to parent's parent list, after the parent
			var grandparentList = FindGrandparentList( blueprint, element );
			if ( grandparentList == null ) return;

			var parent = FindParentElement( blueprint.Elements, element );
			if ( parent == null ) return;

			list.Remove( element );
			var parentIdx = grandparentList.IndexOf( parent );
			grandparentList.Insert( parentIdx + 1, element );
			_dock.MarkDirty();
		}
	}

	private BlueprintElement FindParentElement( List<BlueprintElement> elements, BlueprintElement target )
	{
		foreach ( var el in elements )
		{
			if ( el.Children.Contains( target ) )
				return el;
			var found = FindParentElement( el.Children, target );
			if ( found != null ) return found;
		}
		return null;
	}

	private List<BlueprintElement> FindGrandparentList( PluginBlueprint blueprint, BlueprintElement target )
	{
		var parent = FindParentElement( blueprint.Elements, target );
		if ( parent == null ) return null;
		return FindParentList( blueprint, parent );
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

	private void TrackChildWidgets( BlueprintElement parent, BlueprintElement selectedElement )
	{
		foreach ( var child in parent.Children )
		{
			if ( _renderer.ElementWidgets.TryGetValue( child.Id, out var childWidget ) )
			{
				_elementWidgets[child.Id] = childWidget;
			}
			TrackChildWidgets( child, selectedElement );
		}
	}
}
