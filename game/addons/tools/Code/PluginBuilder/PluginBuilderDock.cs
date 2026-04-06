using Sandbox.UI;

namespace Editor.PluginBuilder;

/// <summary>
/// Main dock window for the Plugin Builder tool.
/// Provides a NavigationView (sidebar + content) for designing component blueprints.
/// </summary>
[Dock( "Editor", "Plugin Builder", "construction" )]
public class PluginBuilderDock : Widget
{
	private PluginBlueprint _activeBlueprint;
	private NavigationView _nav;
	private ElementTreePanel _treePanel;
	private BlueprintPreviewPanel _previewPanel;
	private ElementInspectorPanel _inspectorPanel;
	private ProjectSettingsPanel _settingsPanel;
	private AttributePalettePanel _attributePalette;
	private PropertyTemplatePalette _templatePalette;
	private bool _isDirty;
	private string _currentFilePath;
	private bool _isUpdating;

	public UndoSystem Undo { get; } = new();

	public PluginBlueprint ActiveBlueprint => _activeBlueprint;

	/// <summary>
	/// Fired when an element is selected in the tree or preview.
	/// </summary>
	public Action<BlueprintElement> OnElementSelected { get; set; }

	/// <summary>
	/// Fired when the blueprint is modified.
	/// </summary>
	public Action OnBlueprintChanged { get; set; }

	public PluginBuilderDock( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();

		BuildToolbar();
		BuildMainContent();

		// Start with the landing screen
		ShowStartScreen();
	}

	private void BuildToolbar()
	{
		var toolbar = Layout.Add( new Widget( this ) );
		toolbar.Layout = Layout.Row();
		toolbar.Layout.Margin = 4;
		toolbar.Layout.Spacing = 4;
		toolbar.MinimumHeight = 32;
		toolbar.MaximumHeight = 32;

		var newBtn = new Button( "New", "add", toolbar );
		newBtn.Clicked = () => CreateNewBlueprint();
		toolbar.Layout.Add( newBtn );

		var openBtn = new Button( "Open", "folder_open", toolbar );
		openBtn.Clicked = () => OpenBlueprint();
		toolbar.Layout.Add( openBtn );

		var saveBtn = new Button( "Save", "save", toolbar );
		saveBtn.Clicked = () => SaveBlueprint();
		toolbar.Layout.Add( saveBtn );

		toolbar.Layout.AddSeparator();

		var templateBtn = new Button( "Templates", "dashboard_customize", toolbar );
		templateBtn.Clicked = () => ShowTemplatePalette();
		toolbar.Layout.Add( templateBtn );

		var undoBtn = new Button( "Undo", "undo", toolbar );
		undoBtn.Clicked = () => Undo.Undo();
		toolbar.Layout.Add( undoBtn );

		var redoBtn = new Button( "Redo", "redo", toolbar );
		redoBtn.Clicked = () => Undo.Redo();
		toolbar.Layout.Add( redoBtn );

		toolbar.Layout.AddStretchCell( 1 );

		var exportBtn = new Button( "Export", "upload", toolbar );
		exportBtn.Clicked = () => ExportBlueprint();
		toolbar.Layout.Add( exportBtn );
	}

	private void BuildMainContent()
	{
		// Main content area will be rebuilt depending on state
	}

	private void ShowStartScreen()
	{
		// For now, show a simple start panel
		var startPanel = new BlueprintStartPanel( this, this );
		Layout.Add( startPanel, 1 );
	}

	public void CreateNewBlueprint()
	{
		_activeBlueprint = new PluginBlueprint();
		ShowSettingsPanel();
	}

	private void ShowSettingsPanel()
	{
		Layout.Clear( true );
		BuildToolbar();

		_settingsPanel = new ProjectSettingsPanel( this, _activeBlueprint );
		_settingsPanel.OnCreateClicked = () => ShowEditorLayout();
		Layout.Add( _settingsPanel, 1 );
	}

	public void ShowEditorLayout()
	{
		Layout.Clear( true );
		BuildToolbar();

		// Three-column layout: Tree | Inspector | Preview
		var splitter = Layout.Add( new Widget( this ), 1 );
		splitter.Layout = Layout.Row();

		// Left: Element Tree
		_treePanel = new ElementTreePanel( splitter, this );
		_treePanel.MinimumWidth = 220;
		_treePanel.MaximumWidth = 350;
		splitter.Layout.Add( _treePanel );

		// Center: Element Inspector
		_inspectorPanel = new ElementInspectorPanel( splitter, this );
		_inspectorPanel.MinimumWidth = 250;
		_inspectorPanel.MaximumWidth = 400;
		splitter.Layout.Add( _inspectorPanel );

		// Right: Live Preview
		_previewPanel = new BlueprintPreviewPanel( splitter, this );
		splitter.Layout.Add( _previewPanel, 1 );
	}

	public void SelectElement( BlueprintElement element )
	{
		if ( _isUpdating ) return;
		_isUpdating = true;
		try
		{
			OnElementSelected?.Invoke( element );
		}
		finally
		{
			_isUpdating = false;
		}
	}

	/// <summary>
	/// Adds an element via the tree panel's insertion logic (below selected, or at bottom).
	/// </summary>
	public void AddElementAtSelection( Func<BlueprintElement> factory )
	{
		_treePanel?.AddElementFromFactory( factory );
	}

	public void MarkDirty()
	{
		_isDirty = true;
		if ( _isUpdating ) return;
		_isUpdating = true;
		try
		{
			OnBlueprintChanged?.Invoke();
		}
		finally
		{
			_isUpdating = false;
		}
	}

	public void OpenBlueprint()
	{
		var fd = new FileDialog( null );
		fd.WindowTitle = "Open Blueprint";
		fd.SetNameFilter( "Blueprint Files (*.pluginbuilder.json)" );
		fd.SetModeOpen();
		if ( !fd.Execute() ) return;

		try
		{
			var blueprint = BlueprintStorage.Load( fd.SelectedFile );
			_currentFilePath = fd.SelectedFile;
			LoadBlueprint( blueprint );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Plugin Builder: Failed to open — {ex.Message}" );
		}
	}

	public void SaveBlueprint()
	{
		if ( _activeBlueprint == null ) return;

		if ( string.IsNullOrEmpty( _currentFilePath ) )
		{
			var fd = new FileDialog( null );
			fd.WindowTitle = "Save Blueprint";
			fd.SetNameFilter( "Blueprint Files (*.pluginbuilder.json)" );
			fd.SetModeSave();
			if ( !fd.Execute() ) return;
			_currentFilePath = fd.SelectedFile;
		}

		try
		{
			BlueprintStorage.Save( _activeBlueprint, _currentFilePath );
			_isDirty = false;
			Log.Info( $"Plugin Builder: Saved to {_currentFilePath}" );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Plugin Builder: Failed to save — {ex.Message}" );
		}
	}

	public void ExportBlueprint()
	{
		if ( _activeBlueprint == null ) return;
		var wizard = new ExportWizard( this, _activeBlueprint );
		wizard.Show();
	}

	public void LoadBlueprint( PluginBlueprint blueprint )
	{
		_activeBlueprint = blueprint;
		_isDirty = false;
		ShowEditorLayout();
	}

	private void ShowTemplatePalette()
	{
		if ( _activeBlueprint == null ) return;

		var dialog = new Dialog( this );
		dialog.WindowTitle = "Property Templates";
		dialog.MinimumWidth = 400;
		dialog.MinimumHeight = 500;
		dialog.Layout = Layout.Column();

		var palette = new PropertyTemplatePalette( dialog, this );
		dialog.Layout.Add( palette, 1 );

		dialog.Show();
	}
}
