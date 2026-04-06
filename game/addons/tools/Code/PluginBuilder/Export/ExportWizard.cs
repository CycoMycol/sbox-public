using Sandbox.UI;

namespace Editor.PluginBuilder;

/// <summary>
/// Export wizard dialog — previews generated code before exporting.
/// Two-pane: left = options, right = live code preview.
/// </summary>
public class ExportWizard : Dialog
{
	private readonly PluginBlueprint _blueprint;
	private TextEdit _codePreview;
	private ComboBox _formatSelector;
	private LineEdit _outputPath;
	private Label _validationLabel;

	public ExportWizard( Widget parent, PluginBlueprint blueprint ) : base( parent )
	{
		_blueprint = blueprint;

		WindowTitle = $"Export — {blueprint.Name}";
		MinimumWidth = 800;
		MinimumHeight = 600;

		Layout = Layout.Column();
		Layout.Spacing = 8;
		Layout.Margin = 16;

		// Validation summary
		var issues = BlueprintValidator.Validate( blueprint );
		var errors = issues.Count( i => i.Severity == ValidationSeverity.Error );
		var warnings = issues.Count( i => i.Severity == ValidationSeverity.Warning );

		_validationLabel = new Label( this );
		if ( errors > 0 )
		{
			_validationLabel.Text = $"{errors} error(s), {warnings} warning(s) — fix errors before exporting.";
			_validationLabel.SetStyles( "color: #f44; font-weight: bold;" );
		}
		else if ( warnings > 0 )
		{
			_validationLabel.Text = $"{warnings} warning(s) — review before exporting.";
			_validationLabel.SetStyles( "color: #fa0;" );
		}
		else
		{
			_validationLabel.Text = "Blueprint is valid.";
			_validationLabel.SetStyles( "color: #4f4;" );
		}
		Layout.Add( _validationLabel );

		// Format selector
		var formatRow = new Widget( this );
		formatRow.Layout = Layout.Row();
		formatRow.Layout.Spacing = 8;

		formatRow.Layout.Add( new Label( "Format:", formatRow ) );
		_formatSelector = new ComboBox( formatRow );
		_formatSelector.AddItem( "C# Source" );
		_formatSelector.AddItem( "JSON Spec" );
		_formatSelector.AddItem( "Markdown" );
		_formatSelector.CurrentIndex = 0;
		_formatSelector.ItemChanged += UpdatePreview;
		formatRow.Layout.Add( _formatSelector );
		Layout.Add( formatRow );

		// Output path
		var pathRow = new Widget( this );
		pathRow.Layout = Layout.Row();
		pathRow.Layout.Spacing = 8;

		pathRow.Layout.Add( new Label( "Output:", pathRow ) );
		_outputPath = new LineEdit( pathRow );
		_outputPath.Text = $"{CodeGenerator.SanitizeName( blueprint.Name )}.cs";
		pathRow.Layout.Add( _outputPath, 1 );
		Layout.Add( pathRow );

		// Code preview
		_codePreview = new TextEdit( this );
		_codePreview.ReadOnly = true;
		_codePreview.SetStyles( "font-family: monospace; background-color: #1e1e1e; color: #dcdcdc;" );
		Layout.Add( _codePreview, 1 );

		// Buttons
		var buttonRow = new Widget( this );
		buttonRow.Layout = Layout.Row();
		buttonRow.Layout.Spacing = 8;
		buttonRow.Layout.AddStretchCell( 1 );

		var copyBtn = new Button( "Copy to Clipboard", "content_copy", buttonRow );
		copyBtn.Clicked = CopyToClipboard;
		buttonRow.Layout.Add( copyBtn );

		var exportBtn = new Button.Primary( "Export File", "save", buttonRow );
		exportBtn.Clicked = DoExport;
		if ( errors > 0 ) exportBtn.Enabled = false;
		buttonRow.Layout.Add( exportBtn );

		var cancelBtn = new Button( "Cancel", buttonRow );
		cancelBtn.Clicked = Close;
		buttonRow.Layout.Add( cancelBtn );

		Layout.Add( buttonRow );

		UpdatePreview();
	}

	private void UpdatePreview()
	{
		var format = _formatSelector.CurrentIndex;

		switch ( format )
		{
			case 0:
				_codePreview.PlainText = CodeGenerator.Generate( _blueprint );
				_outputPath.Text = $"{CodeGenerator.SanitizeName( _blueprint.Name )}.cs";
				break;
			case 1:
				_codePreview.PlainText = BlueprintExporter.ExportJson( _blueprint );
				_outputPath.Text = $"{CodeGenerator.SanitizeName( _blueprint.Name )}.pluginbuilder.json";
				break;
			case 2:
				_codePreview.PlainText = BlueprintExporter.ExportMarkdown( _blueprint );
				_outputPath.Text = $"{CodeGenerator.SanitizeName( _blueprint.Name )}.md";
				break;
		}
	}

	private void CopyToClipboard()
	{
		EditorUtility.Clipboard.Copy( _codePreview.PlainText ?? "" );
		Log.Info( "Plugin Builder: Code copied to clipboard." );
	}

	private void DoExport()
	{
		var path = _outputPath.Text?.Trim();
		if ( string.IsNullOrEmpty( path ) ) return;

		try
		{
			System.IO.File.WriteAllText( path, _codePreview.PlainText ?? "" );

			_blueprint.ExportHistory.Add( new ExportHistoryEntry
			{
				Timestamp = DateTime.UtcNow,
				SchemaVersion = _blueprint.SchemaVersion,
				TargetPath = path
			} );

			Log.Info( $"Plugin Builder: Exported to {path}" );
			Close();
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Plugin Builder: Export failed — {ex.Message}" );
		}
	}
}
