namespace Connect4;

public static class TextBoxExtensions
{
	public static void AddLine(this TextBox textBox, string message, int maxLines = 1000, int maxChars = 100_000)
	{
		if (textBox.IsDisposed)
		{
			return;
		}

		if (textBox.InvokeRequired)
		{
			_ = textBox.BeginInvoke(() => AppendAndTrim(textBox, message, maxLines, maxChars));
		}
		else
		{
			AppendAndTrim(textBox, message, maxLines, maxChars);
		}
	}

	private static void AppendAndTrim(TextBox textBox, string message, int maxLines, int maxChars)
	{
		textBox.AppendText(message);
		textBox.AppendText(Environment.NewLine);

		// Trim by line count (remove from the top when exceeding maxLines)
		string[] lines = textBox.Lines;
		if (lines.Length > maxLines)
		{
			int removeCount = lines.Length - maxLines;
			int cutIndex = textBox.GetFirstCharIndexFromLine(removeCount);
			if (cutIndex > 0)
			{
				textBox.Select(0, cutIndex);
				textBox.SelectedText = string.Empty;
			}
		}

		// Trim by total character count (keep tail, drop head)
		int excess = textBox.TextLength - maxChars;
		if (excess > 0)
		{
			textBox.Select(0, excess);
			textBox.SelectedText = string.Empty;
		}

		// Keep caret at end and ensure visibility
		textBox.SelectionStart = textBox.TextLength;
		textBox.ScrollToCaret();
	}
}   
