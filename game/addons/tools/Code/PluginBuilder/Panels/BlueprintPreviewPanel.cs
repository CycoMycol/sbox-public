using Sandbox.UI;

namespace Editor.PluginBuilder;

/// <summary>
/// Interactive live preview of the designed inspector.
/// Built from manual widget construction (NOT ControlSheet).
/// </summary>
public class BlueprintPreviewPanel : Widget
{
	private readonly PluginBuilderDock _dock;
	private Widget _previewContainer;
	private PreviewRenderer _renderer;
	private readonly Dictionary<string, Widget> _elementWidgets = new();
	private string _selectedId;

	public BlueprintPreviewPanel( Widget parent, PluginBuilderDock dock ) : base( parent )
	{
		_dock = dock;
		_renderer = new PreviewRenderer( dock );

		Layout = Layout.Column();
		Layout.Spacing = 4;
		Layout.Margin = 8;

		// Header
		var header = Layout.Add( new Widget( this ) );
		header.Layout = Layout.Row();
		header.Layout.Spacing = 4;

		var title = new Label( "Preview", header );
		title.SetStyles( "font-weight: bold; font-size: 14px;" );
		header.Layout.Add( title );
		header.Layout.AddStretchCell( 1 );

		// Preview scroll area
		var scroll = new ScrollArea( this );
		scroll.Canvas = new Widget( scroll );
		scroll.Canvas.Layout = Layout.Column();
		_previewContainer = scroll.Canvas;
		Layout.Add( scroll, 1 );

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

	private void OnElementSelected( BlueprintElement element )
	{
		var newId = element?.Id;
		if ( newId == _selectedId ) return;

		// Remove old highlight
		if ( _selectedId != null && _elementWidgets.TryGetValue( _selectedId, out var oldWidget ) )
		{
			oldWidget.SetStyles( "" );
		}

		_selectedId = newId;

		// Apply new highlight — left and right edge bars only
		if ( _selectedId != null && _elementWidgets.TryGetValue( _selectedId, out var newWidget ) )
		{
			newWidget.SetStyles( "border-left: 3px solid rgba(100,180,255,0.8); border-right: 3px solid rgba(100,180,255,0.8);" );
		}
	}

	public void RebuildPreview()
	{
		_previewContainer.Layout.Clear( true );
		_elementWidgets.Clear();
		_renderer.ClearTracking();

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

		// Render elements
		RenderElements( _previewContainer, blueprint.Elements );

		_previewContainer.Layout.AddStretchCell( 1 );

		// Re-apply highlight if an element is selected
		if ( _selectedId != null && _elementWidgets.TryGetValue( _selectedId, out var selWidget ) )
		{
			selWidget.SetStyles( "border-left: 3px solid rgba(100,180,255,0.8); border-right: 3px solid rgba(100,180,255,0.8);" );
		}
	}

	private void RenderElements( Widget container, List<BlueprintElement> elements )
	{
		foreach ( var element in elements )
		{
			var widget = _renderer.RenderElement( container, element );
			if ( widget != null )
			{
				container.Layout.Add( widget );
				_elementWidgets[element.Id] = widget;

				// Recursively track child widgets if group/toggle/feature rendered children
				TrackChildWidgets( element );
			}
		}
	}

	private void TrackChildWidgets( BlueprintElement parent )
	{
		foreach ( var child in parent.Children )
		{
			// The child widget was already rendered by the renderer's group/toggle/feature methods.
			// We need to find and track them — the renderer stores them via the MouseClick/ContextMenu
			// event wiring, but we can't easily reach them. Instead, rely on the renderer to
			// register child widgets through the callback we provide.
			// For now, child elements rendered inside groups are tracked via _renderer.ElementWidgets.
			if ( _renderer.ElementWidgets.TryGetValue( child.Id, out var childWidget ) )
			{
				_elementWidgets[child.Id] = childWidget;
			}
			TrackChildWidgets( child );
		}
	}
}
