using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
// using InGameTextEditor.Format;  // Missing file
// using InGameTextEditor.History;  // Missing file
// using InGameTextEditor.Operations;  // Missing file
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InGameTextEditor;

// Stub classes for missing dependencies
public class Line { }
public class TextPosition { public int lineIndex; public int colIndex; }
public class Selection { }
public class State { }
public interface IOperation { }
public class MoveCaretOperation : IOperation { }
public class SetTextOperation : IOperation { }
public class InsertTextOperation : IOperation { }
public class InsertCharacterOperation : IOperation { }
public class DeleteTextOperation : IOperation { }
public class DeleteOperation : IOperation { }
public class ModifyIndentOperation : IOperation { }
public class RebuildLinesOperation : IOperation { }
public class PlaceCaretOperation : IOperation { }
public class SetSelectionOperation : IOperation { }
public class SelectAllOperation : IOperation { }
public class CopyOperation : IOperation { }
public class CutOperation : IOperation { }
public class PasteOperation : IOperation { }
public class UndoOperation : IOperation { }
public class RedoOperation : IOperation { }
public class FindOperation : IOperation { }

namespace History
{
    public class CommandHistory { }
}

[ExecuteInEditMode]
public class TextEditor : MonoBehaviour
{
	[Header("Text")]
	public Font font;

	public int fontSize = 12;

	public float lineSpacing = 1f;

	public FontStyle mainFontStyle;

	public Color mainFontColor = new Color(0f, 0f, 0f);

	public Color mainBackgroundColor = new Color(1f, 1f, 1f, 0.7f);

	public float mainMarginLeft = 5f;

	public float mainMarginRight = 5f;

	public float mainMarginTop = 5f;

	public float mainMarginBottom = 5f;

	[TextArea]
	public string defaultText = "";

	public bool wrapLines;

	public bool indentNewLines = true;

	[Header("Line Numbers")]
	public bool showLineNumbers = true;

	public int startingLineNumber = 1;

	public FontStyle lineNumberFontStyle;

	public Color lineNumberFontColor = new Color(0.3f, 0.3f, 0.3f);

	public Color lineNumberBackgroundColor = new Color(0f, 0f, 0f, 0.2f);

	public float lineNumberMinWidth = 40f;

	public float lineNumberMarginLeft = 5f;

	public float lineNumberMarginRight = 5f;

	[Header("Line Label Icons")]
	public bool showLineLabelIcons;

	public Color lineLabelIconsBackgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);

	public float lineLabelIconsWidth = 20f;

	[Header("Tooltip")]
	public Font tooltipFont;

	public int tooltipFontSize = 12;

	public float tooltipLineSpacing = 1f;

	public FontStyle tooltipFontStyle;

	public Color tooltipFontColor = new Color(0.8f, 0.8f, 0.8f, 1f);

	public Color tooltipBackgroundColor = new Color(0f, 0f, 0f, 0.8f);

	public float tooltipMarginLeft = 8f;

	public float tooltipMarginRight = 8f;

	public float tooltipMarginTop = 8f;

	public float tooltipMarginBottom = 8f;

	public float tooltipAboveCursor = 3f;

	public float tooltipBelowCursor = 15f;

	public float tooltipDelay = 0.2f;

	public float tooltipFadeDuration = 0.05f;

	[Header("Behavior")]
	public bool disableInput;

	public bool deactivateOnLostApplicationFocus = true;

	public bool deactivateOnClickOutsideOfEditor;

	public float resizeTimeout = 0.1f;

	public bool showLockMask = true;

	[Header("Scrolling")]
	public bool blockVerticalScrollingInteraction;

	public bool showVerticalScrollbar = true;

	public bool showHorizontalScrollbar = true;

	public float scrollbarWidth = 20f;

	public float verticalWheelScrollSpeed = 20f;

	public float horizontalWheelScrollSpeed = 20f;

	public float verticalDragScrollSpeed = 5f;

	public float horizontalDragScrollSpeed = 5f;

	public bool invertVerticalScrollDirection;

	public bool invertHorizontalScrollDirection;

	[Header("Caret")]
	public Color caretColor = new Color(0f, 0f, 0f, 1f);

	public float caretWidth = 1f;

	public float caretBlinkRate = 2f;

	[Header("Selection")]
	public Color selectionActiveColor = new Color(0f, 0.7f, 1f, 0.5f);

	public Color selectionInactiveColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

	public float doubleClickInterval = 0.5f;

	public List<char> wordDelimiters = new List<char>(new char[26]
	{
		' ', '\t', '.', ',', ':', ';', '!', '?', '_', '+',
		'-', '*', '/', '%', '=', '@', '|', '(', ')', '[',
		']', '{', '}', '<', '>', '"'
	});

	[Header("Tab Stops")]
	public int tabStopWidth = 4;

	public bool replaceTabsBySpaces;

	[Header("History")]
	public bool enableHistory = true;

	public int maxHistoryLength = 500;

	[Header("Keyboard")]
	public bool useDefaultKeyboardShortcuts = true;

	public bool useArrowKeys = true;

	public float keyRepeatThreshold = 0.5f;

	public float keyRepeatRate = 30f;

	[Header("Internal")]
	public Transform mainPanel;

	public Transform mainContent;

	public Transform textContainer;

	public Transform selectionContainer;

	public Transform labelContainer;

	public Text mainText;

	public Transform lineNumberPanel;

	public Transform lineNumberContent;

	public Text lineNumberText;

	public Transform lineLabelIconsPanel;

	public Transform lineLabelIconsContent;

	public GameObject caret;

	public Scrollbar verticalScrollbar;

	public Scrollbar horizontalScrollbar;

	public GameObject lockMask;

	public GameObject tooltip;

	public Text tooltipText;

	private List<Line> lines = new List<Line>();

	private float longestLineWidth;

	private float verticalSpaceRequired;

	private float verticalSpaceAvailable;

	private float horizontalSpaceRequired;

	private float horizontalSpaceAvailable;

	private float verticalOffset;

	private float horizontalOffset;

	[SerializeField]
	[HideInInspector]
	private float characterWidth;

	[SerializeField]
	[HideInInspector]
	private float characterHeight;

	private bool resize;

	private float resizeTime;

	private TextPosition caretTextPosition;

	private TextPosition dragStartTextPosition;

	private float caretBlinkTime;

	private bool caretVisible;

	private float preferredCaretX;

	private float clickTime = -1000f;

	private int clicks;

	private Vector2 clickPoint = Vector2.zero;

	private bool doubleClick;

	private bool tripleClick;

	private Selection selection;

	private List<GameObject> selectionRects = new List<GameObject>();

	private float canvasScale = 1f;

	private bool ctrlOrCmdPressed;

	private KeyCode keyHold;

	private bool keyHoldDown;

	private float keyHoldTime;

	private float keyRepeatTime;

	private bool editorActive;

	private int editorActiveFrames;

	private bool mouseHoverEditor;

	private bool mouseHoverMainPanel;

	private bool mouseHoverLineNumberPanel;

	private bool draggingEditor;

	private float tooltipVisibility;

	private float tooltipActivationTime;

	private Queue<IOperation> operations = new Queue<IOperation>();

	private float maxOperationTimePerFrame = 0.01f;

	private float maxFormatTimePerFrame = 0.01f;

	private Stopwatch operationsStopWatch = new Stopwatch();

	private InGameTextEditor.History.Event currentEvent;

	private int textFormatLinePointer;

	// Autocomplete support
	private bool _autocompleteActive;
	private string _autocompletePartial = "";
	private bool _pendingIdentifierCheck;

	private readonly HashSet<char> wordDelimitersHashSet = new HashSet<char>();

	public string Text
	{
		get
		{
			return GetSelectedText(new Selection(new TextPosition(0, 0), new TextPosition(lines.Count - 1, lines[lines.Count - 1].Text.Length)));
		}
		[Obsolete("Avoid using direct Text change, use PrepareForChanges(), then SetText( text, immediatel: true)", false)]
		set
		{
			if (Application.isPlaying)
			{
				SetText(value);
			}
		}
	}

	public Selection Selection
	{
		get
		{
			return selection;
		}
		set
		{
			SetSelection(value);
		}
	}

	public TextPosition CaretPosition
	{
		get
		{
			return caretTextPosition;
		}
		set
		{
			PlaceCaret(value);
		}
	}

	public ReadOnlyCollection<Line> Lines => new ReadOnlyCollection<Line>(lines);

	public float CharacterWidth => characterWidth;

	public float CharacterHeight => characterHeight;

	public float HorizontalSpaceAvailable => horizontalSpaceAvailable;

	public float VerticalSpaceAvailable => verticalSpaceAvailable;

	public float LongestLineWidth => longestLineWidth;

	public HashSet<char> WordDelimiters => wordDelimitersHashSet;

	public bool EditorActive
	{
		get
		{
			return editorActive;
		}
		set
		{
			editorActive = value;
			if (editorActive)
			{
				EventSystem.current.GetComponent<EventSystem>().SetSelectedGameObject(base.gameObject);
			}
			else
			{
				editorActiveFrames = 0;
				doubleClick = false;
				tripleClick = false;
				draggingEditor = false;
			}
			UpdateSelectionAndCaret();
		}
	}

	public void PrepareForChanges()
	{
		Selection = null;
		CaretPosition = new TextPosition(0, 0);
		ForceExecutePendingOperations();
	}

	public void SetText(string text, bool immediately = false)
	{
		text = text.Replace("\r\n", "\n").Replace("\v", "\n").Replace("\f", "\n")
			.Replace("\r", "\n")
			.Replace("\u0085", "\n")
			.Replace("\u2028", "\n")
			.Replace("\u2029", "\n");
		if (string.IsNullOrEmpty(text))
		{
			text = "";
		}
		longestLineWidth = 0f;
		SetTextOperation setTextOperation = new SetTextOperation(text);
		if (immediately)
		{
			while (!ExecuteOperation(setTextOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(setTextOperation);
		}
	}

	public void InsertText(TextPosition textPosition, string text, bool addToHistory = false, bool immediately = false)
	{
		InsertTextOperation insertTextOperation = new InsertTextOperation(textPosition, text, addToHistory);
		if (immediately)
		{
			while (!ExecuteOperation(insertTextOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(insertTextOperation);
		}
	}

	public void DeleteText(Selection deleteSelection, bool addToHistory = false, bool immediately = false)
	{
		DeleteTextOperation deleteTextOperation = new DeleteTextOperation(deleteSelection, addToHistory);
		if (immediately)
		{
			while (!ExecuteOperation(deleteTextOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(deleteTextOperation);
		}
	}

	public void Copy(bool immediately = false)
	{
		CopyOperation copyOperation = new CopyOperation();
		if (immediately)
		{
			while (!ExecuteOperation(copyOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(copyOperation);
		}
		EditorActive = true;
	}

	public void Cut(bool immediately = false)
	{
		if (disableInput)
		{
			return;
		}
		CutOperation cutOperation = new CutOperation();
		if (immediately)
		{
			while (!ExecuteOperation(cutOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(cutOperation);
		}
		EditorActive = true;
	}

	public void Paste(bool immediately = false)
	{
		if (disableInput)
		{
			return;
		}
		PasteOperation pasteOperation = new PasteOperation();
		if (immediately)
		{
			while (!ExecuteOperation(pasteOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(pasteOperation);
		}
		EditorActive = true;
	}

	public void Undo(bool immediately = false)
	{
		if (disableInput)
		{
			return;
		}
		UndoOperation undoOperation = new UndoOperation();
		if (immediately)
		{
			while (!ExecuteOperation(undoOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(undoOperation);
		}
		EditorActive = true;
	}

	public void Redo(bool immediately = false)
	{
		if (disableInput)
		{
			return;
		}
		RedoOperation redoOperation = new RedoOperation();
		if (immediately)
		{
			while (!ExecuteOperation(redoOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(redoOperation);
		}
		EditorActive = true;
	}

	public void MoveCaret(MoveCaretOperation.Direction direction, bool select = false, bool entireWord = false, bool immediately = false)
	{
		if (disableInput)
		{
			return;
		}
		MoveCaretOperation moveCaretOperation = new MoveCaretOperation(direction, select, entireWord);
		if (immediately)
		{
			while (!ExecuteOperation(moveCaretOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(moveCaretOperation);
		}
	}

	public void SelectAll(bool immediately = false)
	{
		SelectAllOperation selectAllOperation = new SelectAllOperation();
		if (immediately)
		{
			while (!ExecuteOperation(selectAllOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(selectAllOperation);
		}
		EditorActive = true;
	}

	public string GetSelectedText(Selection textSelection)
	{
		if (textSelection == null || !textSelection.IsValid)
		{
			return "";
		}
		if (textSelection.IsReversed)
		{
			textSelection = new Selection(textSelection.end, textSelection.start);
		}
		if (textSelection.start.lineIndex == textSelection.end.lineIndex)
		{
			return lines[textSelection.start.lineIndex].Text.Substring(textSelection.start.colIndex, textSelection.end.colIndex - textSelection.start.colIndex);
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(lines[textSelection.start.lineIndex].Text.Substring(textSelection.start.colIndex));
		stringBuilder.Append("\n");
		for (int i = textSelection.start.lineIndex + 1; i < textSelection.end.lineIndex; i++)
		{
			stringBuilder.Append(lines[i].Text);
			stringBuilder.Append("\n");
		}
		stringBuilder.Append(lines[textSelection.end.lineIndex].Text.Substring(0, textSelection.end.colIndex));
		return stringBuilder.ToString();
	}

	public void Find(string searchString, bool forward, bool immediately = false)
	{
		FindOperation findOperation = new FindOperation(searchString, forward);
		if (immediately)
		{
			while (!ExecuteOperation(findOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(findOperation);
		}
	}

	public void ShowTooltip(string message, Vector2 position)
	{
		if (!tooltipText.text.Equals(message))
		{
			tooltip.SetActive(value: false);
		}
		tooltipText.font = tooltipFont;
		tooltipText.fontSize = tooltipFontSize;
		tooltipText.lineSpacing = tooltipLineSpacing;
		tooltipText.fontStyle = tooltipFontStyle;
		tooltipText.text = message;
		float preferredWidth = tooltipText.cachedTextGenerator.GetPreferredWidth(message, tooltipText.GetGenerationSettings(tooltipText.cachedTextGenerator.rectExtents.size * 0.5f));
		float preferredHeight = tooltipText.cachedTextGenerator.GetPreferredHeight(message, tooltipText.GetGenerationSettings(tooltipText.cachedTextGenerator.rectExtents.size * 0.5f));
		tooltipText.GetComponent<RectTransform>().offsetMin = new Vector2(tooltipMarginLeft, tooltipMarginBottom);
		tooltipText.GetComponent<RectTransform>().offsetMax = new Vector2(0f - tooltipMarginRight, 0f - tooltipMarginTop);
		tooltip.GetComponent<RectTransform>().sizeDelta = new Vector2(preferredWidth + tooltipMarginLeft + tooltipMarginRight, preferredHeight + tooltipMarginTop + tooltipMarginBottom);
		bool flag = position.x - preferredWidth - tooltipMarginLeft - tooltipMarginRight > 0f;
		bool flag2 = position.x + preferredWidth + tooltipMarginLeft + tooltipMarginRight < GetComponent<RectTransform>().rect.width;
		bool flag3 = position.y + preferredHeight + tooltipMarginTop + tooltipMarginBottom + tooltipAboveCursor < 0f;
		bool num = position.y - preferredHeight - tooltipMarginTop - tooltipMarginBottom - tooltipBelowCursor > 0f - GetComponent<RectTransform>().rect.height;
		float x = ((flag2 || !flag) ? position.x : (position.x - preferredWidth - tooltipMarginLeft - tooltipMarginRight));
		float y = ((num || !flag3) ? (position.y - tooltipBelowCursor) : (position.y + preferredHeight + tooltipMarginTop + tooltipMarginBottom + tooltipAboveCursor));
		tooltip.GetComponent<RectTransform>().localPosition = new Vector2(x, y);
		if (!tooltip.activeSelf)
		{
			tooltipActivationTime = Time.time;
		}
		tooltip.SetActive(value: true);
	}

	public void HideTooltip()
	{
		tooltip.SetActive(value: false);
	}

	public void RemoveLabels()
	{
		foreach (Line line in lines)
		{
			line.RemoveLabels();
		}
	}

	public void UpdateLayout()
	{
		GetComponent<Image>().color = mainBackgroundColor;
		lineNumberPanel.GetComponent<Image>().color = lineNumberBackgroundColor;
		lineLabelIconsPanel.GetComponent<Image>().color = lineLabelIconsBackgroundColor;
		verticalScrollbar.gameObject.SetActive(showVerticalScrollbar);
		verticalScrollbar.GetComponent<RectTransform>().offsetMin = new Vector2(0f - scrollbarWidth, showHorizontalScrollbar ? scrollbarWidth : 0f);
		horizontalScrollbar.gameObject.SetActive(showHorizontalScrollbar);
		horizontalScrollbar.GetComponent<RectTransform>().offsetMax = new Vector2(showVerticalScrollbar ? (0f - scrollbarWidth) : 0f, scrollbarWidth);
		lineNumberPanel.gameObject.SetActive(showLineNumbers);
		float num = Mathf.Max(lineNumberMinWidth, (float)Mathf.FloorToInt(1f + ((lines.Count > 0) ? Mathf.Log10(lines.Count) : 0f)) * characterWidth + lineNumberMarginLeft + lineNumberMarginRight);
		lineNumberPanel.GetComponent<RectTransform>().offsetMin = new Vector2(0f, showHorizontalScrollbar ? scrollbarWidth : 0f);
		lineNumberPanel.GetComponent<RectTransform>().offsetMax = new Vector2(num, 0f);
		lineNumberText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f - lineNumberMarginRight, 0f - mainMarginTop);
		lineLabelIconsPanel.gameObject.SetActive(showLineLabelIcons);
		lineLabelIconsPanel.GetComponent<RectTransform>().offsetMin = new Vector2(showLineNumbers ? num : 0f, showHorizontalScrollbar ? scrollbarWidth : 0f);
		lineLabelIconsPanel.GetComponent<RectTransform>().offsetMax = new Vector2(showLineNumbers ? (num + lineLabelIconsWidth) : lineLabelIconsWidth, 0f);
		mainPanel.GetComponent<RectTransform>().offsetMax = new Vector2(showVerticalScrollbar ? (0f - scrollbarWidth) : 0f, 0f);
		mainPanel.GetComponent<RectTransform>().offsetMin = new Vector2((!showLineNumbers) ? (showLineLabelIcons ? lineLabelIconsWidth : 0f) : (showLineLabelIcons ? (num + lineLabelIconsWidth) : num), showHorizontalScrollbar ? scrollbarWidth : 0f);
		if (Application.isPlaying)
		{
			verticalSpaceRequired = ((lines.Count == 0) ? characterHeight : (0f - lines[lines.Count - 1].VerticalOffset + lines[lines.Count - 1].Height));
			horizontalSpaceRequired = longestLineWidth;
		}
		else
		{
			string[] array = mainText.text.Split('\n');
			verticalSpaceRequired = (float)array.Length * characterHeight;
			horizontalSpaceRequired = 0f;
			for (int i = 0; i < array.Length; i++)
			{
				horizontalSpaceRequired = Mathf.Max(horizontalSpaceRequired, (float)array[i].Length * characterWidth);
			}
		}
		mainText.GetComponent<RectTransform>().anchoredPosition = new Vector2(mainMarginLeft, 0f - mainMarginTop);
		lineNumberText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f - lineNumberMarginRight, 0f - mainMarginTop);
		UpdateScrollableContent();
		UpdateScrollbars();
		UpdateSelectionAndCaret();
	}

	public void UpdateFont()
	{
		mainText.font = font;
		mainText.fontSize = fontSize;
		mainText.lineSpacing = lineSpacing;
		mainText.fontStyle = mainFontStyle;
		mainText.color = mainFontColor;
		mainText.alignment = TextAnchor.UpperLeft;
		mainText.horizontalOverflow = HorizontalWrapMode.Overflow;
		mainText.verticalOverflow = VerticalWrapMode.Overflow;
		lineNumberText.font = font;
		lineNumberText.fontSize = fontSize;
		lineNumberText.lineSpacing = lineSpacing;
		lineNumberText.fontStyle = lineNumberFontStyle;
		lineNumberText.color = lineNumberFontColor;
		lineNumberText.alignment = TextAnchor.UpperRight;
		lineNumberText.horizontalOverflow = HorizontalWrapMode.Overflow;
		lineNumberText.verticalOverflow = VerticalWrapMode.Overflow;
		int num = 1000;
		string text = "";
		for (int i = 0; i < num; i++)
		{
			text += " ";
		}
		for (int j = 1; j < num; j++)
		{
			text += "\n ";
		}
		characterWidth = mainText.cachedTextGeneratorForLayout.GetPreferredWidth(text, mainText.GetGenerationSettings(mainText.cachedTextGenerator.rectExtents.size * 0.5f)) / (float)num;
		characterHeight = mainText.cachedTextGeneratorForLayout.GetPreferredHeight(text, mainText.GetGenerationSettings(mainText.cachedTextGenerator.rectExtents.size * 0.5f)) / ((float)(num - 1) * lineSpacing + 1f) * lineSpacing;
		characterWidth /= canvasScale;
		characterHeight /= canvasScale;
		RebuildLines(!Application.isPlaying);
	}

	public void OnLineChanged(Line line)
	{
		textFormatLinePointer = Mathf.Min(textFormatLinePointer, line.LineNumber);
	}

	public void OnVerticalScrollbarUpdated()
	{
		if (!blockVerticalScrollingInteraction)
		{
			if (verticalSpaceRequired > verticalSpaceAvailable)
			{
				verticalOffset = verticalScrollbar.value * (verticalSpaceRequired - verticalSpaceAvailable);
			}
			UpdateScrollableContent();
		}
	}

	public void OnHorizontalScrollbarUpdated()
	{
		if (horizontalSpaceRequired > horizontalSpaceAvailable)
		{
			horizontalOffset = horizontalScrollbar.value * (horizontalSpaceAvailable - horizontalSpaceRequired);
		}
		UpdateScrollableContent();
	}

	private void Start()
	{
		if (!Application.isPlaying)
		{
			return;
		}
		foreach (char wordDelimiter in wordDelimiters)
		{
			wordDelimitersHashSet.Add(wordDelimiter);
		}
		Input.imeCompositionMode = IMECompositionMode.On;
		invertHorizontalScrollDirection = Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;
		mainText.gameObject.SetActive(value: false);
		lineNumberText.gameObject.SetActive(value: false);
		lines.Add(new Line(0, "", 0f, this));
		caretTextPosition = new TextPosition(0, 0);
		verticalSpaceAvailable = mainPanel.GetComponent<RectTransform>().rect.height - mainMarginTop - mainMarginBottom;
		horizontalSpaceAvailable = mainPanel.GetComponent<RectTransform>().rect.width - mainMarginLeft - mainMarginRight;
		UpdateFont();
		UpdateLayout();
		SetText(defaultText, immediately: true);
		lockMask.SetActive(value: false);
	}

	public void ForceExecutePendingOperations()
	{
		int num = 1000;
		while (operations.Count != 0)
		{
			IOperation op = operations.Peek();
			if (ExecuteOperation(op))
			{
				operations.Dequeue();
			}
			if (--num <= 0)
			{
				break;
			}
		}
	}

	private void Update()
	{
		// Handle autocomplete keyboard navigation
		if (_autocompleteActive && DeviceAutocomplete.Instance.IsVisible)
		{
			if (Input.GetKeyDown(KeyCode.UpArrow))
			{
				DeviceAutocomplete.Instance.SelectPrevious();
				return;
			}
			if (Input.GetKeyDown(KeyCode.DownArrow))
			{
				DeviceAutocomplete.Instance.SelectNext();
				return;
			}
			if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Return))
			{
				// Insert selected completion
				string completion = DeviceAutocomplete.Instance.GetSelectedCompletion();
				if (!string.IsNullOrEmpty(completion))
				{
					foreach (char ch in completion)
					{
						InsertCharacterOperation op = new InsertCharacterOperation(ch);
						operations.Enqueue(op);
					}
				}
				_autocompleteActive = false;
				DeviceAutocomplete.Instance.Hide();
				return;
			}
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				_autocompleteActive = false;
				DeviceAutocomplete.Instance.Hide();
				return;
			}
		}

		if (!Application.isPlaying)
		{
			verticalSpaceAvailable = mainPanel.GetComponent<RectTransform>().rect.height - mainMarginTop - mainMarginBottom;
			horizontalSpaceAvailable = mainPanel.GetComponent<RectTransform>().rect.width - mainMarginLeft - mainMarginRight;
			UpdateFont();
			return;
		}
		if (!Mathf.Approximately(GetComponentInParent<Canvas>().scaleFactor, canvasScale))
		{
			canvasScale = GetComponentInParent<Canvas>().scaleFactor;
			UpdateFont();
		}
		operationsStopWatch.Reset();
		operationsStopWatch.Start();
		while (operations.Count != 0)
		{
			IOperation op = operations.Peek();
			if (ExecuteOperation(op))
			{
				operations.Dequeue();
			}
			if (!((float)operationsStopWatch.ElapsedMilliseconds < maxOperationTimePerFrame * 1000f))
			{
				break;
			}
		}

		// Check for identifier autocomplete after processing operations
		if (operations.Count == 0 && _pendingIdentifierCheck)
		{
			_pendingIdentifierCheck = false;
			TryTriggerIdentifierAutocomplete();
		}

		if (!Mathf.Approximately(verticalSpaceAvailable, mainPanel.GetComponent<RectTransform>().rect.height - mainMarginTop - mainMarginBottom) || !Mathf.Approximately(horizontalSpaceAvailable, mainPanel.GetComponent<RectTransform>().rect.width - mainMarginLeft - mainMarginRight))
		{
			verticalSpaceAvailable = mainPanel.GetComponent<RectTransform>().rect.height - mainMarginTop - mainMarginBottom;
			horizontalSpaceAvailable = mainPanel.GetComponent<RectTransform>().rect.width - mainMarginLeft - mainMarginRight;
			resize = true;
			resizeTime = Time.time;
		}
		lockMask.SetActive(showLockMask && operations.Count > 0);
		if (operations.Count == 0)
		{
			operationsStopWatch.Reset();
			operationsStopWatch.Start();
			while (textFormatLinePointer < lines.Count)
			{
				TextFormatter[] components = GetComponents<TextFormatter>();
				foreach (TextFormatter textFormatter in components)
				{
					if (!textFormatter.Initialized)
					{
						textFormatter.Init();
					}
					textFormatter.OnLineChanged(lines[textFormatLinePointer]);
				}
				textFormatLinePointer++;
				if (!((float)operationsStopWatch.ElapsedMilliseconds < maxFormatTimePerFrame * 1000f))
				{
					break;
				}
			}
			if (resize && (Mathf.Approximately(resizeTimeout, 0f) || Time.time > resizeTime + resizeTimeout))
			{
				if (wrapLines)
				{
					RebuildLines();
				}
				UpdateLayout();
				resize = false;
			}
			if (editorActive && !disableInput && selection == null)
			{
				if (Time.time > caretBlinkTime + 1f / caretBlinkRate)
				{
					caretVisible = !caretVisible;
					caretBlinkTime = Time.time;
				}
			}
			else
			{
				caretVisible = false;
				caretBlinkTime = 0f;
			}
			caret.GetComponent<Image>().enabled = caretVisible;
			Camera camera = null;
			camera = ((GetComponentInParent<Canvas>().renderMode != RenderMode.WorldSpace) ? GetComponentInParent<Canvas>().worldCamera : Camera.main);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(mainContent.GetComponent<RectTransform>(), Input.mousePosition, camera, out var localPoint);
			localPoint += new Vector2(0f - mainMarginLeft, mainMarginTop);
			mouseHoverEditor = RectTransformUtility.RectangleContainsScreenPoint(GetComponent<RectTransform>(), Input.mousePosition, camera);
			mouseHoverMainPanel = RectTransformUtility.RectangleContainsScreenPoint(mainPanel.GetComponent<RectTransform>(), Input.mousePosition, camera);
			mouseHoverLineNumberPanel = RectTransformUtility.RectangleContainsScreenPoint(lineNumberPanel.GetComponent<RectTransform>(), Input.mousePosition, camera);
			if (Input.GetMouseButtonUp(0) && mouseHoverMainPanel && !doubleClick && !tripleClick)
			{
				FollowCaret();
			}
			if (!Input.GetMouseButton(0))
			{
				doubleClick = false;
				tripleClick = false;
				draggingEditor = false;
			}
			if (Input.GetMouseButtonDown(0))
			{
				if (mouseHoverEditor)
				{
					EditorActive = true;
				}
				else if (deactivateOnClickOutsideOfEditor)
				{
					EditorActive = false;
				}
				if (mouseHoverMainPanel || mouseHoverLineNumberPanel)
				{
					draggingEditor = true;
				}
				if (Time.time - clickTime < doubleClickInterval && localPoint == clickPoint)
				{
					clicks++;
				}
				else
				{
					clicks = 1;
				}
				clickTime = Time.time;
				clickPoint = localPoint;
				doubleClick = clicks == 2;
				tripleClick = clicks == 3;
			}
			if (mouseHoverEditor && Input.mouseScrollDelta != Vector2.zero)
			{
				verticalOffset += Input.mouseScrollDelta.y * verticalWheelScrollSpeed * (invertVerticalScrollDirection ? 1f : (-1f));
				verticalOffset = Mathf.Clamp(verticalOffset, 0f, verticalSpaceRequired - verticalSpaceAvailable);
				horizontalOffset -= Input.mouseScrollDelta.x * horizontalWheelScrollSpeed * (invertHorizontalScrollDirection ? (-1f) : 1f);
				horizontalOffset = Mathf.Clamp(horizontalOffset, horizontalSpaceAvailable - horizontalSpaceRequired, 0f);
				UpdateScrollableContent();
				UpdateScrollbars();
			}
			if ((mouseHoverMainPanel || mouseHoverLineNumberPanel) && Input.GetMouseButtonDown(0))
			{
				Click(localPoint);
			}
			if (editorActive && draggingEditor && Input.GetMouseButton(0) && !Input.GetMouseButtonDown(0))
			{
				Drag(localPoint);
			}
			int num = int.MaxValue;
			int num2 = -1;
			foreach (Line line in lines)
			{
				if (verticalOffset + line.VerticalOffset - line.Height <= 0f && verticalOffset + line.VerticalOffset >= 0f - verticalSpaceAvailable)
				{
					line.Visible = true;
					num = Mathf.Min(num, line.LineNumber);
					num2 = Mathf.Max(num2, line.LineNumber);
				}
				else
				{
					line.Visible = false;
				}
			}
			RectTransformUtility.ScreenPointToLocalPointInRectangle(GetComponent<RectTransform>(), Input.mousePosition, camera, out var localPoint2);
			bool flag = false;
			string message = null;
			if (!draggingEditor)
			{
				// First, check for label tooltips
				for (int j = num; j <= num2; j++)
				{
					if (flag)
					{
						break;
					}
					foreach (Line.Label label in lines[j].Labels)
					{
						if (flag)
						{
							break;
						}
						if (string.IsNullOrEmpty(label.tooltipMessage))
						{
							continue;
						}
						if (showLineLabelIcons && label.icon != null && RectTransformUtility.RectangleContainsScreenPoint(label.icon.GetComponent<RectTransform>(), Input.mousePosition, camera))
						{
							flag = true;
							message = label.tooltipMessage;
							break;
						}
						foreach (GameObject labelRect in label.labelRects)
						{
							if (RectTransformUtility.RectangleContainsScreenPoint(labelRect.GetComponent<RectTransform>(), Input.mousePosition, camera))
							{
								flag = true;
								message = label.tooltipMessage;
								break;
							}
						}
					}
				}
				
				// If no label tooltip, check for hash under cursor
				if (!flag && mouseHoverMainPanel)
				{
					string hashTooltip = GetHashTooltipAtPosition(localPoint);
					if (hashTooltip != null)
					{
						flag = true;
						message = hashTooltip;
					}
				}
			}
			if (flag)
			{
				ShowTooltip(message, localPoint2);
			}
			else
			{
				HideTooltip();
			}
		}
		else
		{
			doubleClick = false;
			tripleClick = false;
			draggingEditor = false;
			clicks = 1;
			HideTooltip();
		}
		if (editorActive && editorActiveFrames > 1)
		{
			ctrlOrCmdPressed = false;
			if (Util.IsMacOS())
			{
				if ((Input.GetKey(KeyCode.LeftMeta) || Input.GetKey(KeyCode.RightMeta)) && !Input.GetKey(KeyCode.AltGr) && !Input.GetKey(KeyCode.RightAlt))
				{
					ctrlOrCmdPressed = true;
				}
			}
			else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && !Input.GetKey(KeyCode.AltGr) && !Input.GetKey(KeyCode.RightAlt))
			{
				ctrlOrCmdPressed = true;
			}
			if (Input.GetKeyDown(KeyCode.A))
			{
				keyHold = KeyCode.A;
				keyHoldDown = true;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if (Input.GetKeyDown(KeyCode.C))
			{
				keyHold = KeyCode.C;
				keyHoldDown = true;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if (Input.GetKeyDown(KeyCode.X))
			{
				keyHold = KeyCode.X;
				keyHoldDown = true;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if (Input.GetKeyDown(KeyCode.V))
			{
				keyHold = KeyCode.V;
				keyHoldDown = true;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.Insert))
			{
				ctrlOrCmdPressed = true;
				keyHold = KeyCode.V;
				keyHoldDown = true;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if (Input.GetKeyDown(KeyCode.Z))
			{
				keyHold = KeyCode.Z;
				keyHoldDown = true;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if (Input.GetKeyDown(KeyCode.Y))
			{
				keyHold = KeyCode.Y;
				keyHoldDown = true;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if (Input.GetKeyDown(KeyCode.Tab))
			{
				keyHold = KeyCode.Tab;
				keyHoldDown = true;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if (Input.GetKeyDown(KeyCode.Delete))
			{
				keyHold = KeyCode.Delete;
				keyHoldDown = true;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if (Input.GetKeyDown(KeyCode.Backspace) && ctrlOrCmdPressed)
			{
				keyHold = KeyCode.Backspace;
				keyHoldDown = true;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if (useArrowKeys && Input.GetKeyDown(KeyCode.UpArrow))
			{
				keyHold = KeyCode.UpArrow;
				keyHoldDown = true;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if (useArrowKeys && Input.GetKeyDown(KeyCode.DownArrow))
			{
				keyHold = KeyCode.DownArrow;
				keyHoldDown = true;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if (useArrowKeys && Input.GetKeyDown(KeyCode.LeftArrow))
			{
				keyHold = KeyCode.LeftArrow;
				keyHoldDown = true;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if (useArrowKeys && Input.GetKeyDown(KeyCode.RightArrow))
			{
				keyHold = KeyCode.RightArrow;
				keyHoldDown = true;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if (useArrowKeys && Input.GetKeyDown(KeyCode.Home))
			{
				keyHold = KeyCode.Home;
				keyHoldDown = false;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if (useArrowKeys && Input.GetKeyDown(KeyCode.End))
			{
				keyHold = KeyCode.End;
				keyHoldDown = false;
				keyHoldTime = Time.time;
				keyRepeatTime = 0f;
				HandleKeyStroke(keyHold);
			}
			if (!Input.GetKey(keyHold))
			{
				keyHoldDown = false;
			}
			if (!ctrlOrCmdPressed && !disableInput)
			{
				string inputString = Input.inputString;
				foreach (char c in inputString)
				{
					switch (c)
					{
					case '\n':
					case '\v':
					case '\f':
					case '\r':
					case '\u0085':
					case '\u2028':
					case '\u2029':
						InsertCharacter('\n');
						continue;
					case '\b':
						Delete(forward: false);
						continue;
					}
					if (Util.IsPrintableCharacter(c))
					{
						InsertCharacter(c);
					}
				}
			}
		}
		if (editorActive && editorActiveFrames < int.MaxValue)
		{
			editorActiveFrames++;
		}
		if (tooltip.activeSelf)
		{
			if (tooltipFadeDuration > 0f)
			{
				tooltipVisibility = Mathf.Lerp(0f, 1f, (Time.time - tooltipDelay - tooltipActivationTime) / tooltipFadeDuration);
			}
			else
			{
				tooltipVisibility = ((Time.time - tooltipDelay > tooltipActivationTime) ? 1f : 0f);
			}
		}
		else
		{
			tooltipVisibility = 0f;
		}
		tooltip.GetComponent<Image>().color = new Color(tooltipBackgroundColor.r, tooltipBackgroundColor.g, tooltipBackgroundColor.b, tooltipBackgroundColor.a * tooltipVisibility);
		tooltipText.color = new Color(tooltipFontColor.r, tooltipFontColor.g, tooltipFontColor.b, tooltipFontColor.a * tooltipVisibility);
	}

	private void FixedUpdate()
	{
		if (Application.isPlaying && keyHoldDown && Time.time >= keyHoldTime + keyRepeatThreshold && Time.time >= keyRepeatTime + 1f / keyRepeatRate)
		{
			HandleKeyStroke(keyHold, repeatedStroke: true);
			keyRepeatTime = Time.time;
		}
	}

	private void OnValidate()
	{
		fontSize = Mathf.Max(fontSize, 1);
		lineSpacing = Mathf.Max(lineSpacing, 1f);
		mainMarginLeft = Mathf.Max(mainMarginLeft, 0f);
		mainMarginRight = Mathf.Max(mainMarginRight, 0f);
		mainMarginTop = Mathf.Max(mainMarginTop, 0f);
		mainMarginBottom = Mathf.Max(mainMarginBottom, 0f);
		lineNumberMinWidth = Mathf.Max(lineNumberMinWidth, 0f);
		lineNumberMarginLeft = Mathf.Max(lineNumberMarginLeft, 0f);
		lineNumberMarginRight = Mathf.Max(lineNumberMarginRight, 0f);
		scrollbarWidth = Mathf.Max(scrollbarWidth, 0f);
		verticalWheelScrollSpeed = Mathf.Max(verticalWheelScrollSpeed, 0f);
		horizontalWheelScrollSpeed = Mathf.Max(horizontalWheelScrollSpeed, 0f);
		verticalDragScrollSpeed = Mathf.Max(verticalDragScrollSpeed, 0f);
		horizontalDragScrollSpeed = Mathf.Max(horizontalDragScrollSpeed, 0f);
		caretWidth = Mathf.Max(caretWidth, 0f);
		caretBlinkRate = Mathf.Max(caretBlinkRate, 0.1f);
		doubleClickInterval = Mathf.Max(doubleClickInterval, 0.1f);
		tabStopWidth = Mathf.Max(tabStopWidth, 1);
		keyRepeatThreshold = Mathf.Max(keyRepeatThreshold, 0.1f);
		keyRepeatRate = Mathf.Max(keyRepeatRate, 0.1f);
		maxHistoryLength = Mathf.Max(maxHistoryLength, 0);
		lineLabelIconsWidth = Mathf.Max(lineLabelIconsWidth, 0f);
		tooltipFontSize = Mathf.Max(tooltipFontSize, 1);
		tooltipLineSpacing = Mathf.Max(tooltipLineSpacing, 0f);
		tooltipMarginLeft = Mathf.Max(tooltipMarginLeft, 0f);
		tooltipMarginRight = Mathf.Max(tooltipMarginRight, 0f);
		tooltipMarginTop = Mathf.Max(tooltipMarginTop, 0f);
		tooltipMarginBottom = Mathf.Max(tooltipMarginBottom, 0f);
		tooltipAboveCursor = Mathf.Max(tooltipAboveCursor, 0f);
		tooltipBelowCursor = Mathf.Max(tooltipBelowCursor, 0f);
		tooltipDelay = Mathf.Max(tooltipDelay, 0f);
		tooltipFadeDuration = Mathf.Max(tooltipFadeDuration, 0f);
		if (Application.isPlaying)
		{
			UpdateFont();
		}
	}

	private void OnApplicationFocus(bool focus)
	{
		if (!focus && deactivateOnLostApplicationFocus)
		{
			EditorActive = false;
		}
	}

	private void HandleKeyStroke(KeyCode keyCode, bool repeatedStroke = false)
	{
		bool flag = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		bool entireWord = (Util.IsMacOS() && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))) || (!Util.IsMacOS() && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)));
		switch (keyCode)
		{
		case KeyCode.UpArrow:
			MoveCaret(MoveCaretOperation.Direction.UP, flag);
			break;
		case KeyCode.DownArrow:
			MoveCaret(MoveCaretOperation.Direction.DOWN, flag);
			break;
		case KeyCode.LeftArrow:
			MoveCaret(MoveCaretOperation.Direction.LEFT, flag, entireWord);
			break;
		case KeyCode.RightArrow:
			MoveCaret(MoveCaretOperation.Direction.RIGHT, flag, entireWord);
			break;
		case KeyCode.End:
			MoveCaret(MoveCaretOperation.Direction.END_OF_LINE, flag);
			break;
		case KeyCode.Home:
			MoveCaret(MoveCaretOperation.Direction.START_OF_LINE, flag);
			break;
		}
		switch (keyCode)
		{
		case KeyCode.Tab:
			if (selection != null && selection.IsValid)
			{
				if (!repeatedStroke)
				{
					ModifyIndent(!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift));
				}
			}
			else if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
			{
				InsertCharacter('\t');
			}
			break;
		case KeyCode.Delete:
			Delete(forward: true);
			break;
		case KeyCode.Backspace:
			Delete(forward: false);
			break;
		}
		if (!ctrlOrCmdPressed || !useDefaultKeyboardShortcuts)
		{
			return;
		}
		switch (keyCode)
		{
		case KeyCode.C:
			Copy();
			break;
		case KeyCode.X:
			if (!disableInput)
			{
				Cut();
			}
			break;
		case KeyCode.V:
			if (!disableInput)
			{
				Paste();
			}
			break;
		case KeyCode.A:
			SelectAll();
			break;
		case KeyCode.Z:
			if (!disableInput)
			{
				if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
				{
					Undo();
				}
				else if (Util.IsMacOS())
				{
					Redo();
				}
			}
			break;
		case KeyCode.Y:
			if (!disableInput && !Util.IsMacOS())
			{
				Redo();
			}
			break;
		}
	}

	private void Click(Vector2 mousePosition)
	{
		TextPosition textPositionForCoordinates = GetTextPositionForCoordinates(mousePosition);
		if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
		{
			if (selection != null)
			{
				selection.end = textPositionForCoordinates;
			}
			else
			{
				selection = new Selection(caretTextPosition, textPositionForCoordinates);
				caretTextPosition = textPositionForCoordinates;
			}
		}
		else
		{
			caretTextPosition = textPositionForCoordinates;
			preferredCaretX = lines[textPositionForCoordinates.lineIndex].GetCaretPosition(textPositionForCoordinates).x;
			if (doubleClick)
			{
				TextPosition start = lines[caretTextPosition.lineIndex].FindWordStartOrEnd(caretTextPosition, forward: false);
				TextPosition end = lines[caretTextPosition.lineIndex].FindWordStartOrEnd(caretTextPosition, forward: true);
				selection = new Selection(start, end);
				dragStartTextPosition = start;
			}
			else if (tripleClick)
			{
				TextPosition start2 = new TextPosition(caretTextPosition.lineIndex, 0);
				TextPosition end2 = new TextPosition(caretTextPosition.lineIndex, lines[caretTextPosition.lineIndex].Text.Length);
				selection = new Selection(start2, end2);
				dragStartTextPosition = start2;
			}
			else
			{
				selection = null;
				dragStartTextPosition = textPositionForCoordinates;
			}
		}
		caretBlinkTime = 0f;
		caretVisible = false;
		UpdateSelectionAndCaret();
		AddMilestoneToHistory();
	}

	private void Drag(Vector2 mousePosition)
	{
		Vector2 coordinates = new Vector2(Mathf.Clamp(mousePosition.x, 0f - horizontalOffset - mainMarginLeft - characterWidth, 0f - horizontalOffset + horizontalSpaceAvailable + mainMarginRight + characterWidth), Mathf.Clamp(mousePosition.y, 0f - verticalOffset - verticalSpaceAvailable - mainMarginBottom - characterHeight, 0f - verticalOffset + mainMarginTop + characterHeight));
		TextPosition textPositionForCoordinates = GetTextPositionForCoordinates(coordinates);
		if (dragStartTextPosition.lineIndex == textPositionForCoordinates.lineIndex && dragStartTextPosition.colIndex == textPositionForCoordinates.colIndex && !doubleClick && !tripleClick)
		{
			caretTextPosition = dragStartTextPosition;
			selection = null;
		}
		preferredCaretX = lines[textPositionForCoordinates.lineIndex].GetCaretPosition(textPositionForCoordinates).x;
		if (doubleClick)
		{
			TextPosition start;
			TextPosition end;
			if (dragStartTextPosition.lineIndex < textPositionForCoordinates.lineIndex || (dragStartTextPosition.lineIndex == textPositionForCoordinates.lineIndex && dragStartTextPosition.colIndex <= textPositionForCoordinates.colIndex))
			{
				start = lines[dragStartTextPosition.lineIndex].FindWordStartOrEnd(dragStartTextPosition, forward: false);
				end = lines[textPositionForCoordinates.lineIndex].FindWordStartOrEnd(textPositionForCoordinates, forward: true);
			}
			else
			{
				start = lines[dragStartTextPosition.lineIndex].FindWordStartOrEnd(dragStartTextPosition, forward: true);
				end = lines[textPositionForCoordinates.lineIndex].FindWordStartOrEnd(textPositionForCoordinates, forward: false);
			}
			selection = new Selection(start, end);
		}
		else if (tripleClick)
		{
			TextPosition start2;
			TextPosition end2;
			if (dragStartTextPosition.lineIndex < textPositionForCoordinates.lineIndex || (dragStartTextPosition.lineIndex == textPositionForCoordinates.lineIndex && dragStartTextPosition.colIndex <= textPositionForCoordinates.colIndex))
			{
				start2 = new TextPosition(dragStartTextPosition.lineIndex, 0);
				end2 = new TextPosition(textPositionForCoordinates.lineIndex, lines[textPositionForCoordinates.lineIndex].Text.Length);
			}
			else
			{
				start2 = new TextPosition(dragStartTextPosition.lineIndex, lines[dragStartTextPosition.lineIndex].Text.Length);
				end2 = new TextPosition(textPositionForCoordinates.lineIndex, 0);
			}
			selection = new Selection(start2, end2);
		}
		else
		{
			selection = new Selection(dragStartTextPosition, textPositionForCoordinates);
		}
		float num = 0f;
		if (mousePosition.y > 0f - verticalOffset + mainMarginTop)
		{
			num = mousePosition.y + verticalOffset - mainMarginTop;
		}
		else if (mousePosition.y < 0f - verticalOffset - verticalSpaceAvailable - mainMarginBottom)
		{
			num = mousePosition.y + verticalOffset + verticalSpaceAvailable + mainMarginBottom;
		}
		float num2 = 0f;
		if (mousePosition.x < 0f - horizontalOffset - mainMarginLeft)
		{
			num2 = 0f - horizontalOffset - mousePosition.x - mainMarginLeft;
		}
		else if (mousePosition.x > 0f - horizontalOffset + horizontalSpaceAvailable + mainMarginRight && lines[selection.end.lineIndex].Width > 0f - horizontalOffset + horizontalSpaceAvailable)
		{
			num2 = 0f - horizontalOffset + horizontalSpaceAvailable - mousePosition.x + mainMarginRight;
		}
		verticalOffset -= num * verticalDragScrollSpeed * Time.deltaTime;
		verticalOffset = Mathf.Clamp(verticalOffset, 0f, verticalSpaceRequired - verticalSpaceAvailable);
		horizontalOffset += num2 * horizontalDragScrollSpeed * Time.deltaTime;
		horizontalOffset = Mathf.Clamp(horizontalOffset, horizontalSpaceAvailable - horizontalSpaceRequired, 0f);
		UpdateLayout();
	}

	private void InsertCharacter(char c, bool immediately = false)
	{
		if ((!Util.IsPrintableCharacter(c) && c != '\t') || disableInput)
		{
			return;
		}

		// Handle autocomplete
		if (c == '.')
		{
			// Dismiss identifier autocomplete first
			if (_autocompleteActive && DeviceAutocomplete.Instance.CurrentMode == AutocompleteMode.Identifier)
			{
				_autocompleteActive = false;
				DeviceAutocomplete.Instance.Hide();
			}

			// Try to trigger property autocomplete on dot
			string tokenBefore = GetTokenBeforeCaret();
			if (DeviceAutocomplete.Instance.TryTrigger(this, tokenBefore))
			{
				_autocompleteActive = true;
				_autocompletePartial = "";
			}
		}
		else if (_autocompleteActive)
		{
			// Update filter if typing
			if (char.IsLetterOrDigit(c) || c == '_')
			{
				_autocompletePartial += c;
				DeviceAutocomplete.Instance.UpdateFilter(_autocompletePartial);
			}
			else
			{
				// Non-identifier char cancels autocomplete
				_autocompleteActive = false;
				DeviceAutocomplete.Instance.Hide();
			}
		}
		else if (char.IsLetter(c) || c == '_')
		{
			// Potentially start identifier autocomplete
			// We'll check after inserting the character
			_pendingIdentifierCheck = true;
		}

		InsertCharacterOperation insertCharacterOperation = new InsertCharacterOperation(c);
		if (immediately)
		{
			while (!ExecuteOperation(insertCharacterOperation))
			{
			}
			// Check for identifier autocomplete after immediate insert
			if (_pendingIdentifierCheck)
			{
				_pendingIdentifierCheck = false;
				TryTriggerIdentifierAutocomplete();
			}
		}
		else
		{
			operations.Enqueue(insertCharacterOperation);
		}
	}

	/// <summary>
	/// Called after a character is inserted to potentially trigger identifier autocomplete.
	/// </summary>
	private void TryTriggerIdentifierAutocomplete()
	{
		if (_autocompleteActive) return;  // Already showing autocomplete

		string currentWord = GetCurrentWordAtCaret();
		if (!string.IsNullOrEmpty(currentWord) && currentWord.Length >= 1)
		{
			if (DeviceAutocomplete.Instance.TryTriggerIdentifier(this, currentWord))
			{
				_autocompleteActive = true;
				_autocompletePartial = currentWord;
			}
		}
	}

	/// <summary>
	/// Get the current word being typed (including the character at/before caret).
	/// </summary>
	private string GetCurrentWordAtCaret()
	{
		if (lines.Count == 0 || caretTextPosition == null)
			return "";

		int lineIdx = caretTextPosition.lineIndex;
		int colIdx = caretTextPosition.colIndex;

		if (lineIdx >= lines.Count)
			return "";

		string lineText = lines[lineIdx].Text;
		if (string.IsNullOrEmpty(lineText) || colIdx <= 0)
			return "";

		int end = Mathf.Min(colIdx, lineText.Length);
		int start = end - 1;

		// Walk backwards to find word start
		while (start >= 0 && IsIdentifierChar(lineText[start]))
			start--;

		start++;  // Move past the non-identifier char

		if (start >= end)
			return "";

		return lineText.Substring(start, end - start);
	}

	/// <summary>
	/// Get the token (word) immediately before the caret position.
	/// </summary>
	private string GetTokenBeforeCaret()
	{
		if (lines.Count == 0 || caretTextPosition == null)
			return "";

		int lineIdx = caretTextPosition.lineIndex;
		int colIdx = caretTextPosition.colIndex;

		if (lineIdx >= lines.Count)
			return "";

		string lineText = lines[lineIdx].Text;
		if (string.IsNullOrEmpty(lineText) || colIdx <= 0)
			return "";

		int pos = Mathf.Min(colIdx, lineText.Length) - 1;

		// Skip whitespace
		while (pos >= 0 && char.IsWhiteSpace(lineText[pos]))
			pos--;

		if (pos < 0)
			return "";

		int end = pos;
		int start = pos;

		// Walk backwards to find token start
		while (start > 0 && IsIdentifierChar(lineText[start - 1]))
			start--;

		return lineText.Substring(start, end - start + 1);
	}

	private static bool IsIdentifierChar(char c)
	{
		return char.IsLetterOrDigit(c) || c == '_' || c == '$';
	}

	private void Delete(bool forward, bool immediately = false)
	{
		if (disableInput)
		{
			return;
		}
		
		// Dismiss autocomplete on delete
		if (_autocompleteActive)
		{
			_autocompleteActive = false;
			DeviceAutocomplete.Instance.Hide();
		}

		DeleteOperation deleteOperation = new DeleteOperation(forward);
		if (immediately)
		{
			while (!ExecuteOperation(deleteOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(deleteOperation);
		}
	}

	private void ModifyIndent(bool increase, bool immediately = false)
	{
		if (disableInput)
		{
			return;
		}
		ModifyIndentOperation modifyIndentOperation = new ModifyIndentOperation(increase);
		if (immediately)
		{
			while (!ExecuteOperation(modifyIndentOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(modifyIndentOperation);
		}
	}

	private void PlaceCaret(TextPosition textPosition, bool immediately = false)
	{
		PlaceCaretOperation placeCaretOperation = new PlaceCaretOperation(textPosition);
		if (immediately)
		{
			while (!ExecuteOperation(placeCaretOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(placeCaretOperation);
		}
	}

	private void FollowCaret()
	{
		if (selection != null)
		{
			Vector2 caretPosition = lines[selection.end.lineIndex].GetCaretPosition(selection.end);
			if (caretPosition.x < 0f - horizontalOffset)
			{
				horizontalOffset = 0f - caretPosition.x;
			}
			else if (caretPosition.x > 0f - horizontalOffset + horizontalSpaceAvailable)
			{
				horizontalOffset = 0f - caretPosition.x + horizontalSpaceAvailable;
			}
			if (caretPosition.y < 0f - verticalOffset - verticalSpaceAvailable + characterHeight)
			{
				verticalOffset = 0f - caretPosition.y - verticalSpaceAvailable + characterHeight;
			}
			else if (caretPosition.y > 0f - verticalOffset)
			{
				verticalOffset = 0f - caretPosition.y;
			}
		}
		else
		{
			Vector2 caretPosition2 = lines[caretTextPosition.lineIndex].GetCaretPosition(caretTextPosition);
			dragStartTextPosition = caretTextPosition;
			if (caretPosition2.x < 0f - horizontalOffset)
			{
				horizontalOffset = 0f - caretPosition2.x;
			}
			else if (caretPosition2.x > 0f - horizontalOffset + horizontalSpaceAvailable)
			{
				horizontalOffset = 0f - caretPosition2.x + horizontalSpaceAvailable;
			}
			if (caretPosition2.y < 0f - verticalOffset - verticalSpaceAvailable + characterHeight)
			{
				verticalOffset = 0f - caretPosition2.y - verticalSpaceAvailable + characterHeight;
			}
			else if (caretPosition2.y > 0f - verticalOffset)
			{
				verticalOffset = 0f - caretPosition2.y;
			}
		}
		UpdateScrollableContent();
		UpdateScrollbars();
	}

	private void SetSelection(Selection textSelection, bool immediately = false)
	{
		if (textSelection != null && !textSelection.IsValid)
		{
			throw new UnityException("Invalid selection");
		}
		SetSelectionOperation setSelectionOperation = new SetSelectionOperation(textSelection);
		if (immediately)
		{
			while (!ExecuteOperation(setSelectionOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(setSelectionOperation);
		}
	}

	private void AddSelectionRect(Vector2 topLeft, Vector2 bottomRight)
	{
		GameObject gameObject = new GameObject("Selection");
		gameObject.transform.SetParent(selectionContainer);
		gameObject.AddComponent<Image>();
		gameObject.GetComponent<Image>().color = ((editorActive && !disableInput) ? selectionActiveColor : selectionInactiveColor);
		gameObject.GetComponent<RectTransform>().localPosition = Vector3.zero;
		gameObject.GetComponent<RectTransform>().localRotation = Quaternion.identity;
		gameObject.GetComponent<RectTransform>().localScale = Vector3.one;
		gameObject.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 1f);
		gameObject.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
		gameObject.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
		gameObject.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
		gameObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(mainMarginLeft + topLeft.x, 0f - mainMarginTop + topLeft.y);
		gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(bottomRight.x - topLeft.x, topLeft.y - bottomRight.y);
		gameObject.transform.SetAsLastSibling();
		selectionRects.Add(gameObject);
	}

	private void RebuildLines(bool immediately = false)
	{
		RebuildLinesOperation rebuildLinesOperation = new RebuildLinesOperation();
		if (immediately)
		{
			while (!ExecuteOperation(rebuildLinesOperation))
			{
			}
		}
		else
		{
			operations.Enqueue(rebuildLinesOperation);
		}
	}

	private TextPosition GetTextPositionForCoordinates(Vector2 coordinates)
	{
		if (coordinates.y > 0f)
		{
			return new TextPosition(0, 0);
		}
		if (coordinates.y < lines[lines.Count - 1].VerticalOffset - lines[lines.Count - 1].Height)
		{
			return new TextPosition(lines.Count - 1, lines[lines.Count - 1].Text.Length);
		}
		int num = -1;
		for (int i = 0; i < lines.Count; i++)
		{
			if (coordinates.y >= lines[i].VerticalOffset - lines[i].Height)
			{
				num = i;
				break;
			}
		}
		if (num == -1)
		{
			num = lines.Count - 1;
		}
		return lines[num].GetTextPosition(coordinates);
	}

	/// <summary>
	/// Gets the word (potential hash number) at the given coordinates and looks it up
	/// Returns tooltip text if a valid prefab hash is found, null otherwise
	/// </summary>
	private string GetHashTooltipAtPosition(Vector2 coordinates)
	{
		// Get the text position under the cursor
		TextPosition pos = GetTextPositionForCoordinates(coordinates);
		if (pos.lineIndex < 0 || pos.lineIndex >= lines.Count)
			return null;
		
		string lineText = lines[pos.lineIndex].Text;
	if (string.IsNullOrEmpty(lineText) || pos.colIndex < 0 || pos.colIndex > lineText.Length)
		return null;
	
	// Find the word boundaries around the cursor position
	int start = pos.colIndex;
	int end = pos.colIndex;
		// Expand forwards to find end of word/number
		while (end < lineText.Length && IsHashCharacter(lineText[end]))
		{
			end++;
		}
		
		// Extract the word
		if (start >= end)
			return null;
		
		string word = lineText.Substring(start, end - start);
		
		// Try to parse as a hash and look it up
		if (DeviceAutocomplete.TryParseHashFromWord(word, out int hash))
		{
			string tooltipText = DeviceAutocomplete.LookupPrefabHash(hash);
			return tooltipText;
		}
		
		return null;
	}

	/// <summary>
	/// Checks if a character can be part of a hash number (digit or minus sign)
	/// </summary>
	private bool IsHashCharacter(char c)
	{
		return char.IsDigit(c) || c == '-';
	}

	private void UpdateSelectionAndCaret()
	{
		foreach (GameObject selectionRect in selectionRects)
		{
			UnityEngine.Object.Destroy(selectionRect);
		}
		if (lines.Count == 0)
		{
			selection = null;
		}
		if (selection != null && selection.IsValid)
		{
			Vector2 vector = lines[selection.start.lineIndex].GetCaretPosition(selection.start);
			Vector2 vector2 = lines[selection.end.lineIndex].GetCaretPosition(selection.end);
			if ((Mathf.Approximately(vector.y, vector2.y) && vector.x > vector2.x) || (!Mathf.Approximately(vector.y, vector2.y) && vector.y < vector2.y))
			{
				Vector2 vector3 = vector2;
				vector2 = vector;
				vector = vector3;
			}
			if (Mathf.Approximately(vector.y, vector2.y))
			{
				AddSelectionRect(vector, new Vector2(vector2.x, vector2.y - characterHeight));
				return;
			}
			float x = Mathf.Max((float)Mathf.FloorToInt(horizontalSpaceAvailable / characterWidth) * characterWidth, longestLineWidth);
			AddSelectionRect(vector, new Vector2(x, vector.y - characterHeight));
			if (!Mathf.Approximately(vector.y - characterHeight, vector2.y))
			{
				AddSelectionRect(new Vector2(0f, vector.y - characterHeight), new Vector2(x, vector2.y));
			}
			AddSelectionRect(new Vector2(0f, vector2.y), new Vector2(vector2.x, vector2.y - characterHeight));
		}
		else
		{
			selection = null;
			Vector2 vector4 = ((caretTextPosition != null && caretTextPosition.lineIndex < lines.Count) ? lines[caretTextPosition.lineIndex].GetCaretPosition(caretTextPosition) : Vector2.zero);
			caret.GetComponent<Image>().color = caretColor;
			caret.GetComponent<RectTransform>().anchoredPosition = vector4 + new Vector2(mainMarginLeft, 0f - mainMarginTop);
			if (Application.isPlaying)
			{
				caret.GetComponent<RectTransform>().sizeDelta = new Vector2(Mathf.Max(caretWidth, 1f / canvasScale), characterHeight / lineSpacing);
			}
		}
	}

	private void UpdateScrollableContent()
	{
		verticalOffset = Mathf.Max(0f, Mathf.Min(verticalOffset, verticalSpaceRequired - verticalSpaceAvailable));
		horizontalOffset = Mathf.Min(0f, Mathf.Max(horizontalOffset, horizontalSpaceAvailable - horizontalSpaceRequired));
		mainContent.GetComponent<RectTransform>().anchoredPosition = new Vector2(horizontalOffset, verticalOffset);
		lineNumberContent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, verticalOffset);
		lineLabelIconsContent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, verticalOffset);
	}

	private void UpdateScrollbars()
	{
		if (verticalSpaceRequired <= verticalSpaceAvailable)
		{
			verticalScrollbar.size = 1f;
			verticalScrollbar.value = 0f;
			verticalScrollbar.interactable = false;
		}
		else
		{
			verticalScrollbar.size = verticalSpaceAvailable / verticalSpaceRequired;
			verticalScrollbar.value = verticalOffset / (verticalSpaceRequired - verticalSpaceAvailable);
			verticalScrollbar.interactable = !blockVerticalScrollingInteraction;
		}
		if (horizontalSpaceRequired <= horizontalSpaceAvailable)
		{
			horizontalScrollbar.size = 1f;
			horizontalScrollbar.value = 0f;
			horizontalScrollbar.interactable = false;
		}
		else
		{
			horizontalScrollbar.size = horizontalSpaceAvailable / horizontalSpaceRequired;
			horizontalScrollbar.value = horizontalOffset / (horizontalSpaceAvailable - horizontalSpaceRequired);
			horizontalScrollbar.interactable = true;
		}
	}

	private State GetEditorState()
	{
		return new State(caretTextPosition, selection);
	}

	private void ApplyEditorState(State state)
	{
		caretTextPosition = state.caretTextPosition.Clone();
		selection = ((state.selection != null) ? state.selection.Clone() : null);
		UpdateSelectionAndCaret();
	}

	private void AddEventToHistory(InGameTextEditor.History.Event newEvent)
	{
		if (enableHistory)
		{
			if (currentEvent != null)
			{
				currentEvent.next = newEvent;
				newEvent.previous = currentEvent;
			}
			currentEvent = newEvent;
		}
	}

	private void AddMilestoneToHistory()
	{
		if (!enableHistory || (currentEvent != null && currentEvent is Milestone))
		{
			return;
		}
		AddEventToHistory(new Milestone());
		if (maxHistoryLength <= 0)
		{
			return;
		}
		int num = 0;
		for (InGameTextEditor.History.Event previous = currentEvent; previous != null; previous = previous.previous)
		{
			if (previous is Milestone)
			{
				num++;
			}
			if (num > maxHistoryLength)
			{
				if (previous.previous != null)
				{
					previous.previous.next = null;
				}
				previous.previous = null;
				break;
			}
		}
	}

	private bool ExecuteOperation(IOperation op)
	{
		if (op is CopyOperation)
		{
			return ExecuteCopyOperation((CopyOperation)op);
		}
		if (op is CutOperation)
		{
			return ExecuteCutOperation((CutOperation)op);
		}
		if (op is DeleteOperation)
		{
			return ExecuteDeleteOperation((DeleteOperation)op);
		}
		if (op is DeleteTextOperation)
		{
			return ExecuteDeleteTextOperation((DeleteTextOperation)op);
		}
		if (op is FindOperation)
		{
			return ExecuteFindOperation((FindOperation)op);
		}
		if (op is InsertCharacterOperation)
		{
			return ExecuteInsertCharacterOperation((InsertCharacterOperation)op);
		}
		if (op is InsertTextOperation)
		{
			return ExecuteInsertTextOperation((InsertTextOperation)op);
		}
		if (op is ModifyIndentOperation)
		{
			return ExecuteModifyIndentOperation((ModifyIndentOperation)op);
		}
		if (op is MoveCaretOperation)
		{
			return ExecuteMoveCaretOperation((MoveCaretOperation)op);
		}
		if (op is PasteOperation)
		{
			return ExecutePasteOperation((PasteOperation)op);
		}
		if (op is PlaceCaretOperation)
		{
			return ExecutePlaceCaretOperation((PlaceCaretOperation)op);
		}
		if (op is RebuildLinesOperation)
		{
			return ExecuteRebuildLinesOperation((RebuildLinesOperation)op);
		}
		if (op is RedoOperation)
		{
			return ExecuteRedoOperation((RedoOperation)op);
		}
		if (op is SelectAllOperation)
		{
			return ExecuteSelectAllOperation((SelectAllOperation)op);
		}
		if (op is SetSelectionOperation)
		{
			return ExecuteSetSelectionOperation((SetSelectionOperation)op);
		}
		if (op is SetTextOperation)
		{
			return ExecuteSetTextOperation((SetTextOperation)op);
		}
		if (op is UndoOperation)
		{
			return ExecuteUndoOperation((UndoOperation)op);
		}
		throw new UnityException("Invalid operation");
	}

	private bool ExecuteSetTextOperation(SetTextOperation op)
	{
		if (op.state == SetTextOperation.State.DELETING)
		{
			longestLineWidth = 0f;
			if (lines.Count > 0)
			{
				lines[0].Destroy();
				lines.RemoveAt(0);
			}
			else
			{
				op.state = SetTextOperation.State.INSERTING;
			}
		}
		if (op.state == SetTextOperation.State.INSERTING)
		{
			if (op.remainingText.Length > 0)
			{
				string text = "";
				int num = op.remainingText.IndexOf('\n');
				if (num >= 0)
				{
					text = op.remainingText.Substring(0, num);
					op.remainingText = op.remainingText.Substring(num + 1);
				}
				else
				{
					text = op.remainingText;
					op.remainingText = "";
				}
				for (int i = 0; i < text.Length; i++)
				{
					if (!Util.IsPrintableCharacter(text[i]) && text[i] != '\n' && text[i] != '\t')
					{
						text = text.Remove(i, 1);
						i--;
					}
				}
				if (replaceTabsBySpaces)
				{
					text = Util.ReplaceTabsWithSpaces(text, tabStopWidth);
				}
				Line line = new Line(lines.Count, text, op.tmpOffset, this);
				lines.Add(line);
				longestLineWidth = Mathf.Max(longestLineWidth, line.Width);
				op.tmpOffset -= line.Height;
			}
			else
			{
				if (lines.Count == 0)
				{
					lines.Add(new Line(0, "", 0f, this));
				}
				op.state = SetTextOperation.State.CLEANUP;
			}
		}
		if (op.state == SetTextOperation.State.CLEANUP)
		{
			UpdateLayout();
			currentEvent = null;
			AddMilestoneToHistory();
			textFormatLinePointer = 0;
			return true;
		}
		return false;
	}

	private bool ExecuteInsertTextOperation(InsertTextOperation op)
	{
		if (op.state == InsertTextOperation.State.START)
		{
			if (string.IsNullOrEmpty(op.text))
			{
				op.state = InsertTextOperation.State.CLEANUP;
				return false;
			}
			op.editorStateBefore = ((enableHistory && op.addToHistory) ? GetEditorState() : null);
			op.startTextPosition = op.textPosition.Clone();
			if (op.text.Contains("\n"))
			{
				op.oldLineHeight = lines[op.textPosition.lineIndex].Height;
				op.oldLineWidth = lines[op.textPosition.lineIndex].Width;
				op.before = lines[op.textPosition.lineIndex].Text.Substring(0, op.textPosition.colIndex);
				op.after = lines[op.textPosition.lineIndex].Text.Substring(op.textPosition.colIndex);
				op.textLines = op.text.Split('\n');
				op.state = InsertTextOperation.State.INSERT_FIRST_LINE;
			}
			else
			{
				op.state = InsertTextOperation.State.INSERT_SINGLE_LINE;
			}
		}
		if (op.state == InsertTextOperation.State.INSERT_SINGLE_LINE)
		{
			float height = lines[op.textPosition.lineIndex].Height;
			float width = lines[op.textPosition.lineIndex].Width;
			int length = lines[op.textPosition.lineIndex].Text.Length;
			lines[op.textPosition.lineIndex].Text = (replaceTabsBySpaces ? Util.ReplaceTabsWithSpaces(lines[op.textPosition.lineIndex].Text.Insert(op.textPosition.colIndex, op.text), tabStopWidth) : lines[op.textPosition.lineIndex].Text.Insert(op.textPosition.colIndex, op.text));
			longestLineWidth = Mathf.Max(longestLineWidth, lines[op.textPosition.lineIndex].Width);
			if (lines[op.textPosition.lineIndex].Width >= width)
			{
				op.recalculateLongestLineWidth = false;
			}
			if (!Mathf.Approximately(lines[op.textPosition.lineIndex].Height, height))
			{
				float num = lines[op.textPosition.lineIndex].VerticalOffset - lines[op.textPosition.lineIndex].Height;
				for (int i = op.textPosition.lineIndex + 1; i < lines.Count; i++)
				{
					lines[i].VerticalOffset = num;
					num -= lines[i].Height;
				}
			}
			caretTextPosition = new TextPosition(op.textPosition.lineIndex, op.textPosition.colIndex + lines[op.textPosition.lineIndex].Text.Length - length);
			preferredCaretX = lines[caretTextPosition.lineIndex].GetCaretPosition(caretTextPosition).x;
			for (int j = 0; j < op.textPosition.lineIndex; j++)
			{
				lines[j].OnNextLineChanged();
			}
			for (int k = op.textPosition.lineIndex + 1; k < lines.Count; k++)
			{
				lines[k].OnPreviousLineChanged();
			}
			if (selection != null && selection.start.lineIndex == op.startTextPosition.lineIndex && selection.start.colIndex >= op.startTextPosition.colIndex)
			{
				selection.start.colIndex += lines[op.textPosition.lineIndex].Text.Length - length;
			}
			if (selection != null && selection.end.lineIndex == op.startTextPosition.lineIndex && selection.end.colIndex >= op.startTextPosition.colIndex)
			{
				selection.end.colIndex += lines[op.textPosition.lineIndex].Text.Length - length;
			}
			op.state = InsertTextOperation.State.CLEANUP;
		}
		if (op.state == InsertTextOperation.State.INSERT_FIRST_LINE)
		{
			lines[op.textPosition.lineIndex].Text = (replaceTabsBySpaces ? Util.ReplaceTabsWithSpaces(op.before + op.textLines[0], tabStopWidth) : (op.before + op.textLines[0]));
			longestLineWidth = Mathf.Max(longestLineWidth, lines[op.textPosition.lineIndex].Width);
			if (lines[op.textPosition.lineIndex].Width >= op.oldLineWidth)
			{
				op.recalculateLongestLineWidth = false;
			}
			op.tmpOffset = lines[op.textPosition.lineIndex].VerticalOffset - lines[op.textPosition.lineIndex].Height;
			op.state = InsertTextOperation.State.INSERT_INTERMEDIATE_LINE;
			for (int l = 0; l < op.textPosition.lineIndex; l++)
			{
				lines[l].OnNextLineChanged();
			}
		}
		if (op.state == InsertTextOperation.State.INSERT_INTERMEDIATE_LINE)
		{
			if (op.lineIndex < op.textLines.Length - 1)
			{
				Line line = new Line(op.textPosition.lineIndex + op.lineIndex, replaceTabsBySpaces ? Util.ReplaceTabsWithSpaces(op.textLines[op.lineIndex], tabStopWidth) : op.textLines[op.lineIndex], op.tmpOffset, this);
				lines.Insert(op.textPosition.lineIndex + op.lineIndex, line);
				longestLineWidth = Mathf.Max(longestLineWidth, line.Width);
				if (line.Width >= op.oldLineWidth)
				{
					op.recalculateLongestLineWidth = false;
				}
				op.tmpOffset -= line.Height;
				op.lineIndex++;
			}
			else
			{
				op.state = InsertTextOperation.State.INSERT_LAST_LINE;
			}
		}
		if (op.state == InsertTextOperation.State.INSERT_LAST_LINE)
		{
			Line line2 = new Line(op.textPosition.lineIndex + op.textLines.Length - 1, replaceTabsBySpaces ? Util.ReplaceTabsWithSpaces(op.textLines[op.textLines.Length - 1] + op.after, tabStopWidth) : (op.textLines[op.textLines.Length - 1] + op.after), op.tmpOffset, this);
			lines.Insert(op.textPosition.lineIndex + op.textLines.Length - 1, line2);
			longestLineWidth = Mathf.Max(longestLineWidth, line2.Width);
			if (line2.Width >= op.oldLineWidth)
			{
				op.recalculateLongestLineWidth = false;
			}
			float num2 = line2.VerticalOffset - line2.Height;
			for (int m = op.textPosition.lineIndex + op.textLines.Length; m < lines.Count; m++)
			{
				lines[m].VerticalOffset = num2;
				num2 -= lines[m].Height;
				lines[m].LineNumber = m;
				lines[m].OnPreviousLineChanged();
			}
			caretTextPosition = new TextPosition(op.textPosition.lineIndex + op.textLines.Length - 1, (replaceTabsBySpaces ? Util.ReplaceTabsWithSpaces(op.textLines[op.textLines.Length - 1], tabStopWidth) : op.textLines[op.textLines.Length - 1]).Length);
			preferredCaretX = lines[caretTextPosition.lineIndex].GetCaretPosition(caretTextPosition).x;
			op.state = InsertTextOperation.State.CLEANUP;
		}
		if (op.state == InsertTextOperation.State.CLEANUP)
		{
			if (op.recalculateLongestLineWidth)
			{
				longestLineWidth = 0f;
				for (int n = 0; n < lines.Count; n++)
				{
					longestLineWidth = Mathf.Max(longestLineWidth, lines[n].Width);
				}
			}
			UpdateLayout();
			if (enableHistory && op.addToHistory)
			{
				AddEventToHistory(new Insert(op.startTextPosition, caretTextPosition, op.text, op.editorStateBefore, GetEditorState()));
			}
			textFormatLinePointer = Mathf.Min(textFormatLinePointer, op.textPosition.lineIndex);
			return true;
		}
		return false;
	}

	private bool ExecuteInsertCharacterOperation(InsertCharacterOperation op)
	{
		if (op.state == InsertCharacterOperation.State.START)
		{
			if (selection != null)
			{
				AddMilestoneToHistory();
				DeleteTextOperation deleteTextOp = new DeleteTextOperation(selection, addToHistory: true);
				op.deleteTextOp = deleteTextOp;
				op.state = InsertCharacterOperation.State.DELETE;
			}
			else
			{
				op.state = InsertCharacterOperation.State.INSERT;
			}
		}
		if (op.state == InsertCharacterOperation.State.DELETE && ExecuteDeleteTextOperation(op.deleteTextOp))
		{
			op.state = InsertCharacterOperation.State.INSERT;
		}
		if (op.state == InsertCharacterOperation.State.INSERT)
		{
			if (op.character == ' ' || op.character == '\t' || op.character == '\n')
			{
				AddMilestoneToHistory();
			}
			if (op.character == '\n')
			{
				string text = "";
				if (indentNewLines)
				{
					for (int i = 0; i < lines[caretTextPosition.lineIndex].LineIndent / tabStopWidth; i++)
					{
						text += "\t";
					}
				}
				InsertText(caretTextPosition, op.character + text, addToHistory: true, immediately: true);
			}
			else
			{
				InsertText(caretTextPosition, op.character.ToString(), addToHistory: true, immediately: true);
			}
			if (op.character == ' ' || op.character == '\t' || op.character == '\n')
			{
				AddMilestoneToHistory();
			}
			caretBlinkTime = 0f;
			caretVisible = false;
			FollowCaret();
			return true;
		}
		return false;
	}

	private bool ExecuteDeleteTextOperation(DeleteTextOperation op)
	{
		if (op.state == DeleteTextOperation.State.START)
		{
			if (!op.deleteSelection.IsValid)
			{
				throw new UnityException("Invalid selection");
			}
			op.editorStateBefore = ((enableHistory && op.addToHistory) ? GetEditorState() : null);
			op.selectedText = ((enableHistory && op.addToHistory) ? GetSelectedText(op.deleteSelection) : null);
			if (op.deleteSelection.start.lineIndex != op.deleteSelection.end.lineIndex)
			{
				op.before = lines[op.deleteSelection.start.lineIndex].Text.Substring(0, op.deleteSelection.start.colIndex);
				op.after = lines[op.deleteSelection.end.lineIndex].Text.Substring(op.deleteSelection.end.colIndex);
				op.state = DeleteTextOperation.State.DELETE_FIRST_LINE;
			}
			else
			{
				op.state = DeleteTextOperation.State.DELETE_SINGLE_LINE;
			}
		}
		if (op.state == DeleteTextOperation.State.DELETE_SINGLE_LINE)
		{
			float height = lines[op.deleteSelection.start.lineIndex].Height;
			float width = lines[op.deleteSelection.start.lineIndex].Width;
			string text = lines[op.deleteSelection.start.lineIndex].Text.Substring(0, op.deleteSelection.start.colIndex);
			string text2 = lines[op.deleteSelection.end.lineIndex].Text.Substring(op.deleteSelection.end.colIndex);
			lines[op.deleteSelection.start.lineIndex].Text = text + text2;
			if (lines[op.deleteSelection.start.lineIndex].Width < width && Mathf.Approximately(longestLineWidth, width))
			{
				op.recalculateLongestLineWidth = true;
			}
			if (!Mathf.Approximately(lines[op.deleteSelection.start.lineIndex].Height, height))
			{
				float num = lines[op.deleteSelection.start.lineIndex].VerticalOffset - lines[op.deleteSelection.start.lineIndex].Height;
				for (int i = op.deleteSelection.start.lineIndex + 1; i < lines.Count; i++)
				{
					lines[i].VerticalOffset = num;
					num -= lines[i].Height;
				}
			}
			caretTextPosition = op.deleteSelection.start;
			if (op.deleteSelection.Equals(selection))
			{
				selection = null;
			}
			else if (selection != null && selection.IsValid)
			{
				int num2 = op.deleteSelection.end.colIndex - op.deleteSelection.start.colIndex;
				if (selection.start.lineIndex == op.deleteSelection.start.lineIndex && selection.start.colIndex >= op.deleteSelection.start.colIndex)
				{
					selection.start.colIndex = Mathf.Max(selection.start.colIndex - num2, op.deleteSelection.start.colIndex);
				}
				if (selection.end.lineIndex == op.deleteSelection.start.lineIndex && selection.end.colIndex >= op.deleteSelection.start.colIndex)
				{
					selection.end.colIndex = Mathf.Max(selection.end.colIndex - num2, op.deleteSelection.start.colIndex);
				}
				if (!selection.IsValid)
				{
					selection = null;
				}
			}
			op.state = DeleteTextOperation.State.CLEANUP;
		}
		if (op.state == DeleteTextOperation.State.DELETE_FIRST_LINE)
		{
			float width2 = lines[op.deleteSelection.start.lineIndex].Width;
			lines[op.deleteSelection.start.lineIndex].Text = op.before + op.after;
			longestLineWidth = Mathf.Max(longestLineWidth, lines[op.deleteSelection.start.lineIndex].Width);
			if (lines[op.deleteSelection.start.lineIndex].Width < width2 && Mathf.Approximately(longestLineWidth, width2))
			{
				op.recalculateLongestLineWidth = true;
			}
			op.state = DeleteTextOperation.State.DELETE_INTERMEDIATE_OR_LAST_LINE;
		}
		if (op.state == DeleteTextOperation.State.DELETE_INTERMEDIATE_OR_LAST_LINE)
		{
			if (op.lineIndex < op.deleteSelection.end.lineIndex - op.deleteSelection.start.lineIndex)
			{
				if (Mathf.Approximately(longestLineWidth, lines[op.deleteSelection.start.lineIndex + 1].Width))
				{
					op.recalculateLongestLineWidth = true;
				}
				lines[op.deleteSelection.start.lineIndex + 1].Destroy();
				lines.RemoveAt(op.deleteSelection.start.lineIndex + 1);
				op.lineIndex++;
			}
			else
			{
				float num3 = lines[op.deleteSelection.start.lineIndex].VerticalOffset - lines[op.deleteSelection.start.lineIndex].Height;
				for (int j = op.deleteSelection.start.lineIndex + 1; j < lines.Count; j++)
				{
					lines[j].VerticalOffset = num3;
					num3 -= lines[j].Height;
					lines[j].LineNumber = j;
				}
				caretTextPosition = op.deleteSelection.start;
				selection = null;
				op.state = DeleteTextOperation.State.CLEANUP;
			}
		}
		if (op.state == DeleteTextOperation.State.CLEANUP)
		{
			if (op.recalculateLongestLineWidth)
			{
				longestLineWidth = 0f;
				for (int k = 0; k < lines.Count; k++)
				{
					longestLineWidth = Mathf.Max(longestLineWidth, lines[k].Width);
				}
			}
			for (int l = 0; l < op.deleteSelection.start.lineIndex; l++)
			{
				lines[l].OnNextLineChanged();
			}
			for (int m = op.deleteSelection.start.lineIndex + 1; m < lines.Count; m++)
			{
				lines[m].OnPreviousLineChanged();
			}
			UpdateLayout();
			if (enableHistory && op.addToHistory)
			{
				AddEventToHistory(new Delete(op.deleteSelection, op.selectedText, op.editorStateBefore, GetEditorState()));
			}
			textFormatLinePointer = Mathf.Min(textFormatLinePointer, op.deleteSelection.start.lineIndex);
			return true;
		}
		return false;
	}

	private bool ExecuteDeleteOperation(DeleteOperation op)
	{
		AddMilestoneToHistory();
		if (op.state == DeleteOperation.State.START)
		{
			if (this.selection != null)
			{
				DeleteTextOperation deleteTextOp = new DeleteTextOperation(this.selection, addToHistory: true);
				op.deleteTextOp = deleteTextOp;
				op.state = DeleteOperation.State.DELETE;
			}
			else
			{
				Selection selection = null;
				if (op.forward)
				{
					if (caretTextPosition.colIndex < lines[caretTextPosition.lineIndex].Text.Length)
					{
						selection = ((!ctrlOrCmdPressed) ? new Selection(caretTextPosition, new TextPosition(caretTextPosition.lineIndex, caretTextPosition.colIndex + 1)) : new Selection(caretTextPosition, lines[caretTextPosition.lineIndex].FindWordStart(new TextPosition(caretTextPosition.lineIndex, caretTextPosition.colIndex + 1), forward: true)));
					}
					else if (caretTextPosition.lineIndex < lines.Count - 1)
					{
						selection = new Selection(caretTextPosition, new TextPosition(caretTextPosition.lineIndex + 1, 0));
					}
				}
				else if (caretTextPosition.colIndex > 0)
				{
					selection = ((!ctrlOrCmdPressed) ? new Selection(new TextPosition(caretTextPosition.lineIndex, caretTextPosition.colIndex - 1, preferNextLine: true), caretTextPosition) : new Selection(lines[caretTextPosition.lineIndex].FindWordStart(new TextPosition(caretTextPosition.lineIndex, caretTextPosition.colIndex - 1, preferNextLine: true), forward: false), caretTextPosition));
				}
				else if (caretTextPosition.lineIndex > 0)
				{
					selection = new Selection(new TextPosition(caretTextPosition.lineIndex - 1, lines[caretTextPosition.lineIndex - 1].Text.Length), caretTextPosition);
				}
				if (selection != null)
				{
					DeleteTextOperation deleteTextOp2 = new DeleteTextOperation(selection, addToHistory: true);
					op.deleteTextOp = deleteTextOp2;
					op.state = DeleteOperation.State.DELETE;
				}
				else
				{
					op.state = DeleteOperation.State.CLEANUP;
				}
			}
		}
		if (op.state == DeleteOperation.State.DELETE && ExecuteDeleteTextOperation(op.deleteTextOp))
		{
			op.state = DeleteOperation.State.CLEANUP;
		}
		if (op.state == DeleteOperation.State.CLEANUP)
		{
			AddMilestoneToHistory();
			caretBlinkTime = 0f;
			caretVisible = false;
			FollowCaret();
			return true;
		}
		return false;
	}

	private bool ExecuteModifyIndentOperation(ModifyIndentOperation op)
	{
		if (op.state == ModifyIndentOperation.State.START)
		{
			if (selection == null || !selection.IsValid)
			{
				return true;
			}
			AddMilestoneToHistory();
			op.startLineIndex = (selection.IsReversed ? selection.end.lineIndex : selection.start.lineIndex);
			op.endLineIndex = (selection.IsReversed ? selection.start.lineIndex : selection.end.lineIndex);
			op.lineIndex = op.endLineIndex;
			op.state = ModifyIndentOperation.State.MODIFY_INDENT;
		}
		if (op.state == ModifyIndentOperation.State.MODIFY_INDENT)
		{
			if (op.increase)
			{
				InsertText(new TextPosition(op.lineIndex, 0), "\t", addToHistory: true, immediately: true);
			}
			else if (lines[op.lineIndex].Text.StartsWith("\t", StringComparison.Ordinal))
			{
				DeleteText(new Selection(new TextPosition(op.lineIndex, 0), new TextPosition(op.lineIndex, 1)), addToHistory: true, immediately: true);
			}
			else if (lines[op.lineIndex].Text.StartsWith(" ", StringComparison.Ordinal))
			{
				int num = 1;
				for (int i = 1; i < tabStopWidth && lines[op.lineIndex].Text.Length > i && lines[op.lineIndex].Text[i] == ' '; i++)
				{
					num++;
				}
				DeleteText(new Selection(new TextPosition(op.lineIndex, 0), new TextPosition(op.lineIndex, num)), addToHistory: true, immediately: true);
			}
			if (op.lineIndex > op.startLineIndex)
			{
				op.lineIndex--;
				return false;
			}
			op.state = ModifyIndentOperation.State.CLEANUP;
		}
		if (op.state == ModifyIndentOperation.State.CLEANUP)
		{
			AddMilestoneToHistory();
			return true;
		}
		return false;
	}

	private bool ExecuteRebuildLinesOperation(RebuildLinesOperation op)
	{
		if (op.state == RebuildLinesOperation.State.START)
		{
			longestLineWidth = 0f;
			op.state = RebuildLinesOperation.State.REBUILD;
		}
		if (op.state == RebuildLinesOperation.State.REBUILD)
		{
			if (op.lineIndex >= lines.Count)
			{
				op.state = RebuildLinesOperation.State.CLEANUP;
			}
			else
			{
				lines[op.lineIndex].VerticalOffset = op.tmpOffset;
				lines[op.lineIndex].Text = (replaceTabsBySpaces ? Util.ReplaceTabsWithSpaces(lines[op.lineIndex].Text, tabStopWidth) : lines[op.lineIndex].Text);
				longestLineWidth = Mathf.Max(longestLineWidth, lines[op.lineIndex].Width);
				op.tmpOffset -= lines[op.lineIndex].Height;
				op.lineIndex++;
			}
		}
		if (op.state == RebuildLinesOperation.State.CLEANUP)
		{
			UpdateLayout();
			if (Application.isPlaying)
			{
				FollowCaret();
			}
			return true;
		}
		return false;
	}

	private bool ExecuteMoveCaretOperation(MoveCaretOperation op)
	{
		AddMilestoneToHistory();
		if (op.direction == MoveCaretOperation.Direction.UP)
		{
			if (op.select)
			{
				if (selection == null)
				{
					selection = new Selection(caretTextPosition, caretTextPosition);
				}
				Vector2 caretPosition = lines[selection.end.lineIndex].GetCaretPosition(selection.end);
				if (Mathf.Approximately(caretPosition.y, 0f))
				{
					selection.end = new TextPosition(0, 0);
					preferredCaretX = 0f;
				}
				else
				{
					selection.end = GetTextPositionForCoordinates(new Vector2(preferredCaretX, caretPosition.y + characterHeight * 0.5f));
				}
			}
			else
			{
				if (selection != null)
				{
					if (selection.IsReversed)
					{
						caretTextPosition = selection.end;
					}
					else
					{
						caretTextPosition = selection.start;
					}
					preferredCaretX = lines[caretTextPosition.lineIndex].GetCaretPosition(caretTextPosition).x;
					selection = null;
				}
				Vector2 caretPosition2 = lines[caretTextPosition.lineIndex].GetCaretPosition(caretTextPosition);
				if (Mathf.Approximately(caretPosition2.y, 0f))
				{
					caretTextPosition = new TextPosition(0, 0);
					preferredCaretX = 0f;
				}
				else
				{
					caretTextPosition = GetTextPositionForCoordinates(new Vector2(preferredCaretX, caretPosition2.y + characterHeight * 0.5f));
				}
			}
		}
		if (op.direction == MoveCaretOperation.Direction.DOWN)
		{
			if (op.select)
			{
				if (selection == null)
				{
					selection = new Selection(caretTextPosition, caretTextPosition);
				}
				Vector2 caretPosition3 = lines[selection.end.lineIndex].GetCaretPosition(selection.end);
				if (Mathf.Approximately(caretPosition3.y, lines[lines.Count - 1].VerticalOffset - lines[lines.Count - 1].Height + characterHeight))
				{
					selection.end = new TextPosition(lines.Count - 1, lines[lines.Count - 1].Text.Length);
					preferredCaretX = lines[selection.end.lineIndex].GetCaretPosition(selection.end).x;
				}
				else
				{
					selection.end = GetTextPositionForCoordinates(new Vector2(preferredCaretX, caretPosition3.y - characterHeight * 1.5f));
				}
			}
			else
			{
				if (selection != null)
				{
					if (selection.IsReversed)
					{
						caretTextPosition = selection.start;
					}
					else
					{
						caretTextPosition = selection.end;
					}
					preferredCaretX = lines[caretTextPosition.lineIndex].GetCaretPosition(caretTextPosition).x;
					selection = null;
				}
				Vector2 caretPosition4 = lines[caretTextPosition.lineIndex].GetCaretPosition(caretTextPosition);
				if (Mathf.Approximately(caretPosition4.y, lines[lines.Count - 1].VerticalOffset - lines[lines.Count - 1].Height + characterHeight))
				{
					caretTextPosition = new TextPosition(lines.Count - 1, lines[lines.Count - 1].Text.Length);
					preferredCaretX = lines[caretTextPosition.lineIndex].GetCaretPosition(caretTextPosition).x;
				}
				else
				{
					caretTextPosition = GetTextPositionForCoordinates(new Vector2(preferredCaretX, caretPosition4.y - characterHeight * 1.5f));
				}
			}
		}
		if (op.direction == MoveCaretOperation.Direction.LEFT)
		{
			if (op.select)
			{
				if (selection == null)
				{
					selection = new Selection(caretTextPosition, caretTextPosition);
				}
				if (selection.end.colIndex > 0)
				{
					selection.end.colIndex--;
				}
				else if (selection.end.lineIndex > 0)
				{
					selection.end.lineIndex--;
					selection.end.colIndex = lines[selection.end.lineIndex].Text.Length;
				}
				if (op.entireWord)
				{
					selection.end = lines[selection.end.lineIndex].FindWordStart(selection.end, forward: false);
				}
				selection.end.preferNextLine = true;
				preferredCaretX = lines[selection.end.lineIndex].GetCaretPosition(selection.end).x;
			}
			else if (selection != null)
			{
				if (selection.IsReversed)
				{
					caretTextPosition = selection.end;
				}
				else
				{
					caretTextPosition = selection.start;
				}
				preferredCaretX = lines[caretTextPosition.lineIndex].GetCaretPosition(caretTextPosition).x;
				selection = null;
			}
			else
			{
				if (caretTextPosition.colIndex > 0)
				{
					caretTextPosition.colIndex--;
				}
				else if (caretTextPosition.lineIndex > 0)
				{
					caretTextPosition.lineIndex--;
					caretTextPosition.colIndex = lines[caretTextPosition.lineIndex].Text.Length;
				}
				if (op.entireWord)
				{
					caretTextPosition = lines[caretTextPosition.lineIndex].FindWordStart(caretTextPosition, forward: false);
				}
				caretTextPosition.preferNextLine = true;
				preferredCaretX = lines[caretTextPosition.lineIndex].GetCaretPosition(caretTextPosition).x;
			}
		}
		if (op.direction == MoveCaretOperation.Direction.RIGHT)
		{
			if (op.select)
			{
				if (selection == null)
				{
					selection = new Selection(caretTextPosition, caretTextPosition);
				}
				if (selection.end.colIndex < lines[selection.end.lineIndex].Text.Length)
				{
					selection.end.colIndex++;
				}
				else if (selection.end.lineIndex < lines.Count - 1)
				{
					selection.end.lineIndex++;
					selection.end.colIndex = 0;
				}
				if (op.entireWord)
				{
					selection.end = lines[selection.end.lineIndex].FindWordEnd(selection.end, forward: true);
				}
				selection.end.preferNextLine = false;
				preferredCaretX = lines[selection.end.lineIndex].GetCaretPosition(selection.end).x;
			}
			else if (selection != null)
			{
				if (selection.IsReversed)
				{
					caretTextPosition = selection.start;
				}
				else
				{
					caretTextPosition = selection.end;
				}
				preferredCaretX = lines[caretTextPosition.lineIndex].GetCaretPosition(caretTextPosition).x;
				selection = null;
			}
			else
			{
				if (caretTextPosition.colIndex < lines[caretTextPosition.lineIndex].Text.Length)
				{
					caretTextPosition.colIndex++;
				}
				else if (caretTextPosition.lineIndex < lines.Count - 1)
				{
					caretTextPosition.lineIndex++;
					caretTextPosition.colIndex = 0;
				}
				if (op.entireWord)
				{
					caretTextPosition = lines[caretTextPosition.lineIndex].FindWordEnd(caretTextPosition, forward: true);
				}
				caretTextPosition.preferNextLine = false;
				preferredCaretX = lines[caretTextPosition.lineIndex].GetCaretPosition(caretTextPosition).x;
			}
		}
		if (op.direction == MoveCaretOperation.Direction.START_OF_LINE)
		{
			if (op.select)
			{
				if (selection == null)
				{
					selection = new Selection(caretTextPosition, caretTextPosition);
				}
				selection.end.colIndex = 0;
				selection.end.preferNextLine = true;
				preferredCaretX = lines[selection.end.lineIndex].GetCaretPosition(selection.end).x;
			}
			else
			{
				selection = null;
				caretTextPosition.colIndex = 0;
				caretTextPosition.preferNextLine = true;
				preferredCaretX = lines[caretTextPosition.lineIndex].GetCaretPosition(caretTextPosition).x;
			}
		}
		if (op.direction == MoveCaretOperation.Direction.END_OF_LINE)
		{
			if (op.select)
			{
				if (selection == null)
				{
					selection = new Selection(caretTextPosition, caretTextPosition);
				}
				selection.end.colIndex = lines[selection.end.lineIndex].Text.Length;
				selection.end.preferNextLine = false;
				preferredCaretX = lines[selection.end.lineIndex].GetCaretPosition(selection.end).x;
			}
			else
			{
				selection = null;
				caretTextPosition.colIndex = lines[caretTextPosition.lineIndex].Text.Length;
				caretTextPosition.preferNextLine = false;
				preferredCaretX = lines[caretTextPosition.lineIndex].GetCaretPosition(caretTextPosition).x;
			}
		}
		caretBlinkTime = 0f;
		caretVisible = false;
		UpdateSelectionAndCaret();
		FollowCaret();
		return true;
	}

	private bool ExecutePlaceCaretOperation(PlaceCaretOperation op)
	{
		selection = null;
		caretTextPosition = op.textPosition;
		preferredCaretX = lines[op.textPosition.lineIndex].GetCaretPosition(op.textPosition).x;
		UpdateSelectionAndCaret();
		return true;
	}

	private bool ExecuteSetSelectionOperation(SetSelectionOperation op)
	{
		selection = op.selection;
		FollowCaret();
		UpdateSelectionAndCaret();
		return true;
	}

	private bool ExecuteSelectAllOperation(SelectAllOperation op)
	{
		if (lines.Count == 1 && lines[0].Text.Length == 0)
		{
			return true;
		}
		SetSelection(new Selection(new TextPosition(0, 0), new TextPosition(lines.Count - 1, lines[lines.Count - 1].Text.Length)), immediately: true);
		return true;
	}

	private bool ExecuteCopyOperation(CopyOperation op)
	{
		if (selection != null && selection.IsValid)
		{
			GUIUtility.systemCopyBuffer = GetSelectedText(selection);
		}
		return true;
	}

	private bool ExecuteCutOperation(CutOperation op)
	{
		if (op.state == CutOperation.State.START)
		{
			if (selection == null || !selection.IsValid)
			{
				return true;
			}
			AddMilestoneToHistory();
			Copy(immediately: true);
			DeleteTextOperation deleteTextOp = new DeleteTextOperation(selection, addToHistory: true);
			op.deleteTextOp = deleteTextOp;
			op.state = CutOperation.State.DELETE;
		}
		if (op.state == CutOperation.State.DELETE && ExecuteDeleteTextOperation(op.deleteTextOp))
		{
			op.state = CutOperation.State.CLEANUP;
		}
		if (op.state == CutOperation.State.CLEANUP)
		{
			AddMilestoneToHistory();
			FollowCaret();
			textFormatLinePointer = Mathf.Min(textFormatLinePointer, caretTextPosition.lineIndex);
			return true;
		}
		return false;
	}

	private bool ExecutePasteOperation(PasteOperation op)
	{
		if (op.state == PasteOperation.State.START)
		{
			AddMilestoneToHistory();
			op.clipboardText = GUIUtility.systemCopyBuffer;
			if (op.clipboardText.Length == 0)
			{
				return true;
			}
			op.clipboardText = op.clipboardText.Replace("\r\n", "\n").Replace("\v", "\n").Replace("\f", "\n")
				.Replace("\r", "\n")
				.Replace("\u0085", "\n")
				.Replace("\u2028", "\n")
				.Replace("\u2029", "\n");
			for (int i = 0; i < op.clipboardText.Length; i++)
			{
				if (!Util.IsPrintableCharacter(op.clipboardText[i]) && op.clipboardText[i] != '\n' && op.clipboardText[i] != '\t')
				{
					op.clipboardText = op.clipboardText.Remove(i, 1);
					i--;
				}
			}
			if (op.clipboardText.Length == 0)
			{
				return true;
			}
			op.insertTextOp = new InsertTextOperation(caretTextPosition, op.clipboardText, addToHistory: true);
			if (selection != null && selection.IsValid)
			{
				DeleteTextOperation deleteTextOp = new DeleteTextOperation(selection, addToHistory: true);
				op.deleteTextOp = deleteTextOp;
				op.state = PasteOperation.State.DELETE;
				op.insertTextOp = new InsertTextOperation(selection.IsReversed ? selection.end : selection.start, op.clipboardText, addToHistory: true);
			}
			else
			{
				op.insertTextOp = new InsertTextOperation(caretTextPosition, op.clipboardText, addToHistory: true);
				op.state = PasteOperation.State.INSERT;
			}
		}
		if (op.state == PasteOperation.State.DELETE && ExecuteDeleteTextOperation(op.deleteTextOp))
		{
			op.state = PasteOperation.State.INSERT;
		}
		if (op.state == PasteOperation.State.INSERT && ExecuteInsertTextOperation(op.insertTextOp))
		{
			op.state = PasteOperation.State.CLEANUP;
		}
		if (op.state == PasteOperation.State.CLEANUP)
		{
			AddMilestoneToHistory();
			FollowCaret();
			textFormatLinePointer = Mathf.Min(textFormatLinePointer, op.insertTextOp.textPosition.lineIndex);
			return true;
		}
		return false;
	}

	private bool ExecuteUndoOperation(UndoOperation op)
	{
		if (op.state == UndoOperation.State.START)
		{
			if (!enableHistory || currentEvent == null)
			{
				return true;
			}
			AddMilestoneToHistory();
			op.e = currentEvent;
			op.state = UndoOperation.State.TRAVERSE_HISTORY;
		}
		if (op.state == UndoOperation.State.TRAVERSE_HISTORY)
		{
			if (op.e == null)
			{
				op.state = UndoOperation.State.CLEANUP;
			}
			else if (op.e is InGameTextEditor.History.Action)
			{
				if (op.e is Insert)
				{
					Insert insert = (Insert)op.e;
					DeleteTextOperation revertedOperation = new DeleteTextOperation(new Selection(insert.startTextPosition, insert.endTextPosition), addToHistory: false);
					op.revertedOperation = revertedOperation;
				}
				else
				{
					if (!(op.e is Delete))
					{
						throw new UnityException("Invalid action");
					}
					Delete delete = (Delete)op.e;
					InsertTextOperation revertedOperation2 = new InsertTextOperation(delete.selection.start, delete.deletedText, addToHistory: false);
					op.revertedOperation = revertedOperation2;
				}
				op.state = UndoOperation.State.REVERT_ACTION;
			}
			else
			{
				op.e = op.e.previous;
			}
		}
		if (op.state == UndoOperation.State.REVERT_ACTION && ExecuteOperation(op.revertedOperation))
		{
			ApplyEditorState(((InGameTextEditor.History.Action)op.e).stateBefore);
			op.e = op.e.previous;
			if (op.e == null || op.e is Milestone)
			{
				currentEvent = op.e;
				op.state = UndoOperation.State.CLEANUP;
			}
			else
			{
				op.state = UndoOperation.State.TRAVERSE_HISTORY;
			}
		}
		if (op.state == UndoOperation.State.CLEANUP)
		{
			FollowCaret();
			if (op.e is Insert)
			{
				textFormatLinePointer = Mathf.Min(textFormatLinePointer, ((Insert)op.e).startTextPosition.lineIndex);
			}
			else if (op.e is Delete)
			{
				textFormatLinePointer = Mathf.Min(textFormatLinePointer, ((Delete)op.e).selection.start.lineIndex);
			}
			return true;
		}
		return false;
	}

	private bool ExecuteRedoOperation(RedoOperation op)
	{
		if (op.state == RedoOperation.State.START)
		{
			if (!enableHistory || currentEvent == null || currentEvent.next == null)
			{
				return true;
			}
			AddMilestoneToHistory();
			op.e = currentEvent;
			op.state = RedoOperation.State.TRAVERSE_HISTORY;
		}
		if (op.state == RedoOperation.State.TRAVERSE_HISTORY)
		{
			if (op.e == null)
			{
				currentEvent = op.e;
				op.state = RedoOperation.State.CLEANUP;
			}
			else if (op.e is InGameTextEditor.History.Action)
			{
				if (op.e is Insert)
				{
					Insert insert = (Insert)op.e;
					InsertTextOperation appliedOperation = new InsertTextOperation(insert.startTextPosition, insert.text, addToHistory: false);
					op.appliedOperation = appliedOperation;
				}
				else
				{
					if (!(op.e is Delete))
					{
						throw new UnityException("Invalid action");
					}
					DeleteTextOperation appliedOperation2 = new DeleteTextOperation(((Delete)op.e).selection, addToHistory: false);
					op.appliedOperation = appliedOperation2;
				}
				op.state = RedoOperation.State.APPLY_ACTION;
			}
			else
			{
				op.e = op.e.next;
			}
		}
		if (op.state == RedoOperation.State.APPLY_ACTION && ExecuteOperation(op.appliedOperation))
		{
			ApplyEditorState(((InGameTextEditor.History.Action)op.e).stateAfter);
			op.e = op.e.next;
			if (op.e == null || op.e is Milestone)
			{
				currentEvent = op.e;
				op.state = RedoOperation.State.CLEANUP;
			}
			else
			{
				op.state = RedoOperation.State.TRAVERSE_HISTORY;
			}
		}
		if (op.state == RedoOperation.State.CLEANUP)
		{
			FollowCaret();
			if (op.e is Insert)
			{
				textFormatLinePointer = Mathf.Min(textFormatLinePointer, ((Insert)op.e).startTextPosition.lineIndex);
			}
			else if (op.e is Delete)
			{
				textFormatLinePointer = Mathf.Min(textFormatLinePointer, ((Delete)op.e).selection.start.lineIndex);
			}
			return true;
		}
		return false;
	}

	private bool ExecuteFindOperation(FindOperation op)
	{
		if (string.IsNullOrEmpty(op.searchString))
		{
			return true;
		}
		TextPosition textPosition = ((selection == null) ? caretTextPosition : (op.forward ? selection.end : selection.start));
		string text = GetSelectedText(new Selection(textPosition, new TextPosition(lines.Count - 1, lines[lines.Count - 1].Text.Length))) + "\n" + GetSelectedText(new Selection(new TextPosition(0, 0), textPosition));
		int num = (op.forward ? text.IndexOf(op.searchString, StringComparison.Ordinal) : text.LastIndexOf(op.searchString, StringComparison.Ordinal));
		if (num >= 0)
		{
			int i = textPosition.lineIndex;
			int num2;
			for (num2 = textPosition.colIndex + num; num2 >= lines[i % lines.Count].Text.Length; i++)
			{
				num2 -= lines[i % lines.Count].Text.Length + 1;
			}
			int j = i;
			int num3;
			for (num3 = num2 + op.searchString.Length; num3 > lines[j % lines.Count].Text.Length; j++)
			{
				num3 -= lines[j % lines.Count].Text.Length + 1;
			}
			TextPosition start = new TextPosition(i % lines.Count, num2);
			TextPosition end = new TextPosition(j % lines.Count, num3);
			SetSelection(new Selection(start, end), immediately: true);
		}
		return true;
	}

	/// <summary>
	/// Unity OnGUI callback - draws the autocomplete popup overlay
	/// </summary>
	private void OnGUI()
	{
		if (_autocompleteActive)
		{
			DeviceAutocomplete.Instance.DrawPopup();
		}
	}
}
