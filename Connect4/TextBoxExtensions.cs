namespace Connect4;

public static class TextBoxExtensions
{
    public static void AddLine(this TextBox textBox, string message)
    {
        _ = textBox.BeginInvoke(() =>
        {
            textBox.AppendText($"{message}{Environment.NewLine}");
        });
    }
}   
