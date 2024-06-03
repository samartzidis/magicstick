using System.IO;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System.Windows;
using System.Xml;
using System;
using System.Linq;
using ICSharpCode.AvalonEdit.CodeCompletion;
using System.Collections.Generic;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System.Xml.Linq;

namespace MagicStickUI
{
    /// <summary>
    /// Interaction logic for EditorWindow.xaml
    /// </summary>
    public partial class EditorWindow : Window
    {
        private readonly Rpc _rpc;
        private CompletionWindow _completionWindow;
        private readonly List<CompletionData> _allCompletionData;

        public EditorWindow(Rpc rpc)
        {
            _rpc = rpc;

            InitializeComponent();
            ApplyCustomHighlighting();

            Title = Constants.AppName;
            avEditor.TextArea.TextEntered += TextEditor_TextArea_TextEntered;
            avEditor.TextArea.TextEntering += TextEditor_TextArea_TextEntering;
            avEditor.TextArea.KeyDown += TextEditor_TextArea_KeyDown;
            Loaded += EditorWindow_Loaded;

            _allCompletionData = new List<CompletionData>();
            var syntaxDef = Util.GetStringResource("syntax_definition.xml");
            _allCompletionData = ExtractKeywords(syntaxDef).ToList();
        }

        public IEnumerable<CompletionData> ExtractKeywords(string xmlContent)
        {
            var doc = XDocument.Parse(xmlContent);
            XNamespace ns = "http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008";
            foreach (var word in doc.Descendants(ns + "Word"))
            {
                var descriptionAttribute = word.Attribute("_description");
                var descriptionValue = descriptionAttribute?.Value;

                yield return new CompletionData(this, word.Value, descriptionValue);
            }
        }
      
        private void TextEditor_TextArea_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ShowFullCompletionWindow();
                e.Handled = true;
            }
        }

        private void TextEditor_TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length <= 0 || _completionWindow == null) 
                return;

            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
                _completionWindow.CompletionList.RequestInsertion(e);
            else
                ShowCompletionWindow();
        }

        private void TextEditor_TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            // Trigger autocomplete on specific characters
            if (char.IsLetter(e.Text[0]))
                ShowCompletionWindow();
        }

        private void ShowCompletionWindow()
        {
            if (_completionWindow == null)
            {
                _completionWindow = new CompletionWindow(avEditor.TextArea);
                _completionWindow.Closed += delegate { _completionWindow = null; };
            }

            var data = _completionWindow.CompletionList.CompletionData;
            data.Clear();

            var wordBeforeCaret = GetWordBeforeCaret();
            var filteredData = _allCompletionData.Where(c => c.Text.StartsWith(wordBeforeCaret, StringComparison.InvariantCultureIgnoreCase));

            foreach (var item in filteredData)
            {
                data.Add(item);
            }

            if (!data.Any())
            {
                _completionWindow.Close();
            }
            else
            {
                if (!_completionWindow.IsVisible)
                {
                    _completionWindow.Show();
                }
            }
        }

        private void ShowFullCompletionWindow()
        {
            if (_completionWindow == null)
            {
                _completionWindow = new CompletionWindow(avEditor.TextArea);
                _completionWindow.Closed += delegate { _completionWindow = null; };
            }

            var data = _completionWindow.CompletionList.CompletionData;
            data.Clear();

            foreach (var item in _allCompletionData)
                data.Add(item);

            if (data.Any())
            {
                if (!_completionWindow.IsVisible)
                    _completionWindow.Show();
            }
            else
                _completionWindow.Close();
        }

        public string GetWordBeforeCaret()
        {
            var caretOffset = avEditor.CaretOffset;
            var currentLine = avEditor.Document.GetLineByOffset(caretOffset);
            var start = caretOffset;

            while (start > currentLine.Offset)
            {
                var c = avEditor.Document.GetCharAt(start - 1);
                if (!char.IsLetterOrDigit(c) && c != '_') // Allow underscore as part of the word
                    break;
                start--;
            }

            return avEditor.Document.GetText(start, caretOffset - start);
        }

        private void EditorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                using (new Hourglass())
                {
                    var getReply = _rpc.GetKeymap().GetAwaiter().GetResult();
                    var text = string.Join(Environment.NewLine, getReply.items);
                    avEditor.Document = new TextDocument(text);
                }
            }
            catch (Exception m)
            {
                MessageBox.Show(m.Message, Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var text = avEditor.Document.Text;

            if (text.Length > 4000)
            {
                MessageBox.Show("Total size of 4000 characters exceeded.", Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);

                e.Handled = true;
                return;
            }

            var items = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            if (items.Count > 100)
            {
                MessageBox.Show("Total line limit of 100 lines exceeded.", Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);

                e.Handled = true;
                return;
            }

            try
            {
                using (new Hourglass())
                {
                    var req = new SetKeymapRequest { items = items };
                    var setReply = _rpc.SetKeymap(req).GetAwaiter().GetResult();
                    if (!setReply.success)
                    {
                        MessageBox.Show(setReply.error, Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);

                        e.Handled = true;
                        return;
                    }
                }
            }
            catch (Exception m)
            {
                MessageBox.Show(m.Message, Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);

                e.Handled = true;
                return;
            }

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void DefaultButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (new Hourglass())
                {
                    var getReply = _rpc.GetKeymap(true).GetAwaiter().GetResult();
                    var text = string.Join(Environment.NewLine, getReply.items);
                    avEditor.Document = new ICSharpCode.AvalonEdit.Document.TextDocument(text);
                }
            }
            catch (Exception m)
            {
                MessageBox.Show(m.Message, Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string RemoveDescriptionAttributes(string xmlContent)
        {
            // Load the XML document from the string
            var doc = XDocument.Parse(xmlContent);

            // Find all Word elements and remove the _description attribute if it exists
            foreach (var wordElement in doc.Descendants().Where(e => e.Name.LocalName == "Word"))
            {
                var descriptionAttribute = wordElement.Attribute("_description");
                if (descriptionAttribute != null)
                {
                    descriptionAttribute.Remove();
                }
            }

            // Return the processed XML content as a string
            return doc.ToString();
        }

        private void ApplyCustomHighlighting()
        {
            var str = Util.GetStringResource("syntax_definition.xml");
            str = RemoveDescriptionAttributes(str);

            using var reader = new XmlTextReader(new StringReader(str));
            var xshd = HighlightingLoader.LoadXshd(reader);
            var highlighting = HighlightingLoader.Load(xshd, HighlightingManager.Instance);
            avEditor.SyntaxHighlighting = highlighting;
        }
        
    }

    public class CompletionData : ICompletionData
    {
        private readonly EditorWindow _editorWindow;

        public CompletionData(EditorWindow editorWindow, string text, string description)
        {
            _editorWindow = editorWindow;

            Text = text;
            Description = description ?? text;            
        }

        public System.Windows.Media.ImageSource Image => null;
        public string Text { get; }
        public object Content => Text;
        public object Description { get; }
        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var wordBeforeCaret = _editorWindow.GetWordBeforeCaret();
            var caretOffset = textArea.Caret.Offset;
            var startOffset = caretOffset - wordBeforeCaret.Length;           
            if (startOffset < 0)  // Ensure startOffset is not negative
                startOffset = 0;

            var length = wordBeforeCaret.Length;
            var replaceSegment = new TextSegment { StartOffset = startOffset, Length = length };

            textArea.Document.Replace(replaceSegment, Text);
        }
    }
}
