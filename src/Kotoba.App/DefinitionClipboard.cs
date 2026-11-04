using System.Globalization;
using System.Net;
using System.Text;
using System.Windows;
using Kotoba.Core;

namespace Kotoba.App;

internal static class DefinitionClipboard
{
    internal static DataObject Create(Entry entry)
    {
        var lines = entry.Senses.Select((sense, i) =>
            (entry.Senses.Length > 1 ? $"{i + 1}. " : "") + string.Join("; ", sense.Glosses)).ToArray();
        var text = string.Join("\r\n", lines);
        // Windows HTML clipboard offsets are UTF-8 byte positions, including the header.
        const string header = "Version:1.0\r\nStartHTML:{0:0000000000}\r\nEndHTML:{1:0000000000}\r\nStartFragment:{2:0000000000}\r\nEndFragment:{3:0000000000}\r\n";
        const string before = "<html><body><!--StartFragment-->";
        const string after = "<!--EndFragment--></body></html>";
        var fragment = WebUtility.HtmlEncode(text).Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "<br>");
        var startHtml = Encoding.UTF8.GetByteCount(string.Format(CultureInfo.InvariantCulture, header, 0, 0, 0, 0));
        var startFragment = startHtml + Encoding.UTF8.GetByteCount(before);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        var endHtml = endFragment + Encoding.UTF8.GetByteCount(after);
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, text);
        data.SetData(DataFormats.Html, string.Format(CultureInfo.InvariantCulture, header, startHtml, endHtml, startFragment, endFragment) + before + fragment + after);
        return data;
    }
}
