namespace Editor.PluginBuilder;

/// <summary>
/// Full undo/redo stack for all builder operations.
/// Capped at 200 entries. Text edits are coalesced into single undo entries.
/// </summary>
public class UndoSystem
{
	private const int MaxUndoDepth = 200;

	private readonly List<UndoEntry> _undoStack = new();
	private readonly List<UndoEntry> _redoStack = new();

	public int UndoCount => _undoStack.Count;
	public int RedoCount => _redoStack.Count;
	public bool CanUndo => _undoStack.Count > 0;
	public bool CanRedo => _redoStack.Count > 0;

	public Action OnChanged { get; set; }

	public void Push( string description, Action undo, Action redo )
	{
		_undoStack.Add( new UndoEntry
		{
			Description = description,
			Undo = undo,
			Redo = redo,
			Timestamp = DateTime.UtcNow
		} );

		// Cap the stack
		while ( _undoStack.Count > MaxUndoDepth )
			_undoStack.RemoveAt( 0 );

		// Clear redo stack on new action
		_redoStack.Clear();

		OnChanged?.Invoke();
	}

	public void Undo()
	{
		if ( _undoStack.Count == 0 ) return;

		var entry = _undoStack[^1];
		_undoStack.RemoveAt( _undoStack.Count - 1 );

		entry.Undo?.Invoke();
		_redoStack.Add( entry );

		OnChanged?.Invoke();
	}

	public void Redo()
	{
		if ( _redoStack.Count == 0 ) return;

		var entry = _redoStack[^1];
		_redoStack.RemoveAt( _redoStack.Count - 1 );

		entry.Redo?.Invoke();
		_undoStack.Add( entry );

		OnChanged?.Invoke();
	}

	public void Clear()
	{
		_undoStack.Clear();
		_redoStack.Clear();
		OnChanged?.Invoke();
	}

	public IReadOnlyList<UndoEntry> GetUndoHistory() => _undoStack.AsReadOnly();
	public IReadOnlyList<UndoEntry> GetRedoHistory() => _redoStack.AsReadOnly();
}

public class UndoEntry
{
	public string Description { get; set; }
	public Action Undo { get; set; }
	public Action Redo { get; set; }
	public DateTime Timestamp { get; set; }
}
