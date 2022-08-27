using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ModFinder
{
    class BBCodeRenderer
    {
        public class State
        {
            public Dictionary<string, int> tags = new()
            {
                { "b", 0 },
                { "i", 0 },
                { "center", 0 },
                { "size", 0 },
            };
            public Stack<int> size = new();
            public Stack<string> font = new();
            public Stack<List> lists = new();
        }

        private static Regex sizePattern = new(@"size=(\d+)");
        private static Regex fontPattern = new(@"font=(.*)");

        public static void Render(FlowDocument doc, string raw)
        {
            Paragraph p = new();

            State state = new();
            state.size.Push(16);
            state.font.Push(null);

            StringBuilder tagBuilder = new();

#if BUBBLEDEBUG
            File.WriteAllText(@"D:\raw.txt", raw);
#endif

            StringBuilder stripped = new();

            Stack<List> lists = new();
            Stack<BlockCollection> elems = new();
            elems.Push(doc.Blocks);

            int i = 0;
            while (i < raw.Length)
            {
                if (raw[i] == '\n')
                {
                    i++;
                    FlushBlock();
                }
                else if (raw[i] == '[')
                {
                    tagBuilder.Clear();
                    int mod = 1;
                    i++;
                    if (i == raw.Length) break;
                    if (raw[i] == '/')
                    {
                        mod = -1;
                        i++;
                    }

                    if (i == raw.Length) break;

                    while (i < raw.Length && raw[i] != ']')
                        tagBuilder.Append(raw[i++]);

                    if (i == raw.Length) break;
                    i++;

                    var tag = tagBuilder.ToString();

                    if (tag.StartsWith("font"))
                    {
                        FlushInline();

                        if (mod == 1)
                            state.font.Push(fontPattern.Match(tag).Groups[1].Value);
                        else
                            state.font.Pop();

                    }
                    if (tag.StartsWith("size"))
                    {
                        FlushInline();

                        if (mod == 1)
                        {
                            int size = 16 + 2 * int.Parse(sizePattern.Match(tag).Groups[1].Value);
                            state.size.Push(size);
                        }
                        else
                            state.size.Pop();
                    }
                    else if (tag.StartsWith("list"))
                    {
                        FlushBlock();
                        if (mod == 1)
                        {
                            lists.Push(new());
                            if (tag.EndsWith("=1"))
                                lists.Peek().MarkerStyle = TextMarkerStyle.Decimal;
                        }
                        else
                        {
                            var list = lists.Pop();
                            if (list.ListItems.Count > 0)
                                elems.Pop();
                            elems.Peek().Add(list);
                        }
                    }
                    else if (tag == "*")
                    {
                        FlushBlock();
                        if (lists.Peek().ListItems.Count > 0)
                            elems.Pop();
                        var item = new ListItem();
                        elems.Push(item.Blocks);
                        lists.Peek().ListItems.Add(item);
                    }
                    else if (state.tags.TryGetValue(tag, out var current))
                    {
                        if (mod == -1 && current == 1 || mod == 1 && current == 0)
                            FlushInline();

                        if (tag == "center")
                            FlushBlock();

                        state.tags[tag] += mod;
                    }
                    else
                    {

                        if (mod == 1)
                            Debug.WriteLine("Unhandled tag: " + tag);
                        continue;
                    }
                }
                else if (raw[i] == '&')
                {
                    int maybeEnd = raw.IndexOf(';', i);
                    if (maybeEnd == -1)
                    {
                        stripped.Append(raw[i++]);
                        continue;
                    }

                    int len = maybeEnd - i + 1;

                    var span = raw.AsSpan(i, len);

                    if (span.SequenceEqual("&gt;"))
                    {
                        stripped.Append(">");
                        i += len;
                    }
                    else if (span.SequenceEqual("&amp;"))
                    {
                        stripped.Append("&");
                        i += len;
                    }
                    else if (span.SequenceEqual("&#92;"))
                    {
                        stripped.Append("/");
                        i += len;
                    }
                    else
                        stripped.Append(raw[i++]);
                }
                else
                {
                    stripped.Append(raw[i++]);
                }
            }

            FlushInline();

            void FlushInline()
            {
                if (stripped.Length == 0)
                    return;

                Inline run = new Run(stripped.ToString());
                stripped.Clear();

                if (state.tags["b"] > 0)
                    run = new Bold(run);
                if (state.tags["i"] > 0)
                    run = new Italic(run);

                run.FontSize = state.size.Peek();

                string fontFamily = state.font.Peek();
                if (fontFamily != null)
                {
                    run.FontFamily = new FontFamily(fontFamily);
                }

                p.Inlines.Add(run);

            }

            void FlushBlock()
            {
                FlushInline();

                if (p.Inlines.Count == 0)
                    return;

                if (state.tags["center"] > 0)
                    p.TextAlignment = TextAlignment.Center;
                elems.Peek().Add(p);
                p = new Paragraph();
            }

            elems.Peek().Add(p);
        }
    }
}
