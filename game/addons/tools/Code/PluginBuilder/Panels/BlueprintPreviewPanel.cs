using Sandbox.UI;

namespace Editor.PluginBuilder;

/// <summary>
/// Interactive live preview of the designed inspector.
/// The floating toolbar is inserted inline directly above the selected element.
/// Attributes render as visible, clickable tags in the preview.
/// </summary>
public class BlueprintPreviewPanel : Widget
{
	private readonly PluginBuilderDock _dock;
	private Widget _previewContainer;
	private PreviewRenderer _renderer;
	private readonly Dictionary<string, Widget> _elementWidgets = new();
	private string _selectedId;
	private FloatingToolbar _toolbar;
	private Button _toolbarToggleBtn;
	private bool _toolbarVisible = true;

	public BlueprintPreviewPanel( Widget parent, PluginBuilderDock dock ) : base( parent )
	{
		_dock = dock;
		_renderer = new PreviewRenderer( dock );

		Layout = Layout.Column();
		Layout.Spacing = 4;
		Layout.Margin = 8;

		// Header row
		var header = Layout.Add( new Widget( this ) );
		header.Layout = Layout.Row();
		header.Layout.Spacing = 4;

		var title = new Label( "Preview", header );
		title.SetStyles( "font-weight: bold; font-size: 14px;" );
		header.Layout.Add( title );
		header.Layout.AddStretchCell( 1 );

		// Toolbar toggle button
		_toolbarToggleBtn = new Button( "", "build", header );
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
		if ( newId == _selectedId ) return;
		_selectedId = newId;

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
			var empty = new Label( "No blueprint loaded.", _previewContainer );
			empty.SetStyles( "color: #888;" );
			_previewContainer.Layout.Add( empty );
			return;
		}

		// Component header
		var componentHeader = new Widget( _previewContainer );
		componentHeader.Layout = Layout.Row();
		componentHeader.Layout.Spacing = 8;
		componentHeader.Layout.Margin = new Margin( 0, 0, 0, 8 );

		if ( !string.IsNullOrEmpty( blueprint.Icon ) )
		{
			var iconLabel = new Label( blueprint.Icon, componentHeader );
			iconLabel.SetStyles( "font-size: 16px;" );
			componentHeader.Layout.Add( iconLabel );
		}

		var nameLabel = new Label( blueprint.Name, componentHeader );
		nameLabel.SetStyles( "font-weight: bold; font-size: 14px;" );
		componentHeader.Layout.Add( nameLabel );

		_previewContainer.Layout.Add( componentHeader );

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
		if ( selectedElement != null )
		{
			if ( _toolbar.Element == null )
				_toolbar.ShowForElement( selectedElement );
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
			// Insert toolbar above the selected element
			if ( element == selectedElement )
			{
				_toolbar.ShowForElement( element );
				container.Layout.Add( _toolbar );
			}

			var widget = _renderer.RenderElement( container, element );
			if ( widget != null )
			{
				container.Layout.Add( widget );
				_elementWidgets[element.Id] = widget;

				// Render attributes as visible clickable tags below the element
				RenderAttributeWidgets( container, element );

				// Recursively track child widgets
				TrackChildWidgets( element, selectedElement );
			}
		}
	}

	private void RenderAttributeWidgets( Widget container, BlueprintElement element )
	{
		if ( element.Attributes.Count == 0 ) return;

		var attrRow = new Widget( container );
		attrRow.Layout = Layout.Row();
		attrRow.Layout.Spacing = 4;
		attrRow.Layout.Margin = new Margin( 24, 0, 4, 2 );

		foreach ( var attr in element.Attributes )
		{
			var attrName = attr.Key;
			var def = AttributeCatalog.Get( attrName );
			var icon = def?.Icon ?? "label";

			var tag = new Button( $"[{attrName}]", icon, attrRow );
			tag.SetStyles( "color: #6ab4ff; font-size: 10px; background-color: rgba(100,180,255,0.12); border-radius: 3px; padding: 2px 6px; border: 1px solid rgba(100,180,255,0.2);" );
			tag.ToolTip = def?.Description ?? attrName;
			tag.Cursor = CursorShape.Finger;

			// Clicking an attribute shows the attribute toolbar
			tag.Clicked = () =>
			{
				_selectedId = element.Id;
				_toolbar.ShowForAttribute( element, attrName );

				// Highlight the parent element
				foreach ( var kv in _elementWidgets )
					kv.Value.SetStyles( "" );
				if ( _elementWidgets.TryGetValue( element.Id, out var elWidget ) )
					elWidget.SetStyles( "border-left: 3px solid rgba(100,180,255,0.8); border-right: 3px solid rgba(100,180,255,0.8);" );
			};

			attrRow.Layout.Add( tag );
		}

		container.Layout.Add( attrRow );
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
