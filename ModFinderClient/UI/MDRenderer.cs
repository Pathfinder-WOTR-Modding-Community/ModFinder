using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
//using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using MdBlock = Markdig.Syntax.Block;
using WpfInline = System.Windows.Documents.Inline;
using WpfTable = System.Windows.Documents.Table;
using WpfTableCell = System.Windows.Documents.TableCell;
using WpfTableColumn = System.Windows.Documents.TableColumn;
using WpfTableRow = System.Windows.Documents.TableRow;
using WpfTableRowGroup = System.Windows.Documents.TableRowGroup;

using FlowDocument = System.Windows.Documents.FlowDocument;
using InlineUIContainer = System.Windows.Documents.InlineUIContainer;
using Run = System.Windows.Documents.Run;
using Bold = System.Windows.Documents.Bold;
using Italic = System.Windows.Documents.Italic;
using Span = System.Windows.Documents.Span;
using LineBreak = System.Windows.Documents.LineBreak;
using Paragraph = System.Windows.Documents.Paragraph;
using Hyperlink = System.Windows.Documents.Hyperlink;
using Markdig.Extensions.Tables;
using Markdig;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Threading;

namespace ModFinder.UI
{
  static class MarkdownRenderer
  {
    public static RoutedCommand Image { get; } = new RoutedCommand(nameof(Image), typeof(MarkdownRenderer));
    public static RoutedCommand Hyperlink { get; } = new RoutedCommand(nameof(Hyperlink), typeof(MarkdownRenderer));

    public class State
    {
      public Dictionary<string, int> tags = new()
      {
        { "b", 0 },
        { "i", 0 },
        { "center", 0 },
        { "size", 0 },
      };
      public string url = null;
      public Stack<int> size = new();
      public Stack<string> font = new();
    }

    private static readonly Regex sizePattern = new(@"size=(\d+)");
    private static readonly Regex fontPattern = new(@"font=(.*)");


    public static void Render(FlowDocument doc, string raw)
    {
      var pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();
      Markdig.Syntax.MarkdownDocument md = Markdown.Parse(raw, pipeline);

      doc.Foreground = new SolidColorBrush(Color.FromRgb(20, 20, 20));

      var renderer = new WpfRenderer(doc);
      renderer.Render(md);


    }

    public static string Raw(this StringSlice slice)
    {
      return slice.Text[slice.Start..(slice.End + 1)];
    }
  }

  /// <summary>
  /// WPF renderer for a Markdown <see cref="MarkdownDocument"/> object.
  /// </summary>
  /// <seealso cref="RendererBase" />
  public class WpfRenderer : RendererBase
  {
    private readonly Stack<IAddChild> stack = new Stack<IAddChild>();
    private char[] buffer;

    public WpfRenderer()
    {
      buffer = new char[1024];
    }

    public WpfRenderer(FlowDocument document)
    {
      buffer = new char[1024];
      LoadDocument(document);
    }

    public virtual void LoadDocument(FlowDocument document)
    {
      Document = document ?? throw new ArgumentNullException(nameof(document));
      //document.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.DocumentStyleKey);
      stack.Push(document);
      LoadRenderers();
    }

    public FlowDocument Document { get; protected set; }

    /// <inheritdoc/>
    public override object Render(MarkdownObject markdownObject)
    {
      Write(markdownObject);
      return Document;
    }

    /// <summary>
    /// Writes the inlines of a leaf inline.
    /// </summary>
    /// <param name="leafBlock">The leaf block.</param>
    /// <returns>This instance</returns>
    public void WriteLeafInline(LeafBlock leafBlock)
    {
      if (leafBlock == null) throw new ArgumentNullException(nameof(leafBlock));
      var inline = leafBlock.Inline as Markdig.Syntax.Inlines.Inline;
      while (inline != null)
      {
        Write(inline);
        inline = inline.NextSibling;
      }
    }

    /// <summary>
    /// Writes the lines of a <see cref="LeafBlock"/>
    /// </summary>
    /// <param name="leafBlock">The leaf block.</param>
    public void WriteLeafRawLines(LeafBlock leafBlock)
    {
      if (leafBlock == null) throw new ArgumentNullException(nameof(leafBlock));
      if (leafBlock.Lines.Lines != null)
      {
        var lines = leafBlock.Lines;
        var slices = lines.Lines;
        for (var i = 0; i < lines.Count; i++)
        {
          if (i != 0)
            WriteInline(new LineBreak());

          WriteText(ref slices[i].Slice);
        }
      }
    }

    public void Push(IAddChild o)
    {
      stack.Push(o);
    }

    public void Pop()
    {
      var popped = stack.Pop();
      stack.Peek().AddChild(popped);
    }

    public void WriteBlock(MdBlock block)
    {
      stack.Peek().AddChild(block);
    }

    public void WriteInline(WpfInline inline)
    {
      AddInline(stack.Peek(), inline);
    }

    public void WriteText(ref StringSlice slice)
    {
      if (slice.Start > slice.End)
        return;

      WriteText(slice.Text, slice.Start, slice.Length);
    }

    public void WriteText(string text)
    {
      WriteInline(new Run(text));
    }

    public void WriteText(string text, int offset, int length)
    {
      if (text == null)
        return;

      if (offset == 0 && text.Length == length)
      {
        WriteText(text);
      }
      else
      {
        if (length > buffer.Length)
        {
          buffer = text.ToCharArray();
          WriteText(new string(buffer, offset, length));
        }
        else
        {
          text.CopyTo(offset, buffer, 0, length);
          WriteText(new string(buffer, 0, length));
        }
      }
    }

    /// <summary>
    /// Loads the renderer used for render WPF
    /// </summary>
    protected virtual void LoadRenderers()
    {
      // Default block renderers
      ObjectRenderers.Add(new CodeBlockRenderer());
      ObjectRenderers.Add(new ListRenderer());
      ObjectRenderers.Add(new HeadingRenderer());
      ObjectRenderers.Add(new ParagraphRenderer());
      //ObjectRenderers.Add(new QuoteBlockRenderer());
      //ObjectRenderers.Add(new ThematicBreakRenderer());

      //// Default inline renderers
      //ObjectRenderers.Add(new AutolinkInlineRenderer());
      ObjectRenderers.Add(new CodeInlineRenderer());
      //ObjectRenderers.Add(new DelimiterInlineRenderer());
      ObjectRenderers.Add(new EmphasisInlineRenderer());
      //ObjectRenderers.Add(new HtmlEntityInlineRenderer());
      //ObjectRenderers.Add(new LinkInlineRenderer());
      ObjectRenderers.Add(new LiteralInlineRenderer());

      Add<LinkInline>(obj =>
      {
        var url = obj.GetDynamicUrl != null ? obj.GetDynamicUrl() ?? obj.Url : obj.Url;

        if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
        {
          url = "#";
        }

        if (obj.IsImage)
        {
          //var template = new ControlTemplate();
          //var image = new FrameworkElementFactory(typeof(Image));
          //var img = new BitmapImage(new Uri(url, UriKind.RelativeOrAbsolute));
          //image.SetValue(Image.SourceProperty, ));
          //image.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.ImageStyleKey);
          //template.VisualTree = image;

          //while (img.IsDownloading)
          //{
          //  Thread.Sleep(20);
          //}

          var btn = new Button()
          {
            //Background = new ImageBrush(img),
            //Command = MarkdownRenderer.Image,
            //CommandParameter = url,
            //Width = 100,
            //Height = 100,
          };

          WriteInline(new InlineUIContainer(btn));

        }
        else
        {

          var hlink = new Hyperlink()
          {
            Command = MarkdownRenderer.Hyperlink,
            CommandParameter = url,
            NavigateUri = new Uri(url, UriKind.RelativeOrAbsolute),
            ToolTip = !string.IsNullOrEmpty(obj.Title) ? obj.Title : null,
          };

          WriteWithChildren(hlink, obj);
        }

      });

      Add<LineBreakInline>(obj =>
      {
        if (obj.IsHard)
        {
          WriteInline(new LineBreak());
        }
        else
        {
          // Soft line break.
          WriteText(" ");
        }
      });

      //// Extension renderers
      ObjectRenderers.Add(new TableRenderer());
      //ObjectRenderers.Add(new TaskListRenderer());
    }

    private void WriteWithChildren(IAddChild miniParent, ContainerInline obj)
    {
      Push(miniParent);
      WriteChildren(obj);
      Pop();
    }
    private void WriteWithChildren(IAddChild miniParent, ContainerBlock obj)
    {
      Push(miniParent);
      WriteChildren(obj);
      Pop();
    }

    private void Add<T>(Action<T> render) where T : MarkdownObject
    {
      ObjectRenderers.Add(new GenericRenderer<T>(render));
    }

    public FontFamily Mono = new("Lucida Sans Typewrite");

    private static void AddInline(IAddChild parent, WpfInline inline)
    {
      parent.AddChild(inline);
    }

    public abstract class WpfObjectRenderer<TObject> : MarkdownObjectRenderer<WpfRenderer, TObject>
      where TObject : MarkdownObject
    {
    }

    private class LiteralInlineRenderer : WpfObjectRenderer<LiteralInline>
    {
      protected override void Write(WpfRenderer renderer, LiteralInline obj)
      {
        renderer.WriteInline(new Run(obj.Content.Raw()));
      }
    }

    private class ParagraphRenderer : WpfObjectRenderer<ParagraphBlock>
    {
      protected override void Write(WpfRenderer renderer, ParagraphBlock obj)
      {
        Paragraph p = new();
        p.Margin = new(6);
        if (obj.Parent is ListItemBlock)
          p.Margin = new(2);
        renderer.Push(p);
        renderer.WriteLeafInline(obj);
        renderer.Pop();
      }
    }

    private class HeadingRenderer : WpfObjectRenderer<HeadingBlock>
    {
      protected override void Write(WpfRenderer renderer, HeadingBlock obj)
      {
        Paragraph p = new();
        p.Margin = new(12);
        p.FontSize = 32 - (3.5f * obj.Level);
        p.TextAlignment = TextAlignment.Left;

        renderer.Push(p);
        renderer.Push(new Bold());
        renderer.WriteLeafInline(obj);
        var line = new Line()
        {
          X1 = 0,
          X2 = 1000,
          Y1 = 0,
          Y2 = 0,
          StrokeThickness = 2,
          Stroke = Brushes.Gray,
        };
        renderer.Pop();
        renderer.WriteInline(new InlineUIContainer(line));
        renderer.Pop();
      }
    }

    private class EmphasisInlineRenderer : WpfObjectRenderer<EmphasisInline>
    {
      protected override void Write(WpfRenderer renderer, EmphasisInline obj)
      {
        if (obj.DelimiterChar is '*' or '_')
        {
          var span = obj.DelimiterCount == 2 ? (Span)new Bold() : new Italic();
          renderer.WriteWithChildren(span, obj);
        }
        else
        {
          renderer.WriteChildren(obj);
        }
      }
    }

    private class CodeBlockRenderer : WpfObjectRenderer<CodeBlock>
    {
      protected override void Write(WpfRenderer renderer, CodeBlock obj)
      {
        Paragraph p = new();
        p.FontFamily = renderer.Mono;
        p.Background = new SolidColorBrush(Color.FromArgb(130, 200, 200, 200));
        p.Foreground = Brushes.Black;

        p.FontSize = 16;
        renderer.Push(p);
        renderer.WriteLeafRawLines(obj);
        renderer.Pop();

      }
    }

    private class ListRenderer : WpfObjectRenderer<ListBlock>
    {
      protected override void Write(WpfRenderer renderer, ListBlock obj)
      {
        var list = new System.Windows.Documents.List();
        list.Margin = new(4);

        if (obj.IsOrdered)
        {
          list.MarkerStyle = TextMarkerStyle.Decimal;

          if (obj.OrderedStart != null && (obj.DefaultOrderedStart != obj.OrderedStart))
          {
            list.StartIndex = int.Parse(obj.OrderedStart, NumberFormatInfo.InvariantInfo);
          }
        }
        else
        {
          list.MarkerStyle = TextMarkerStyle.Disc;
        }

        renderer.Push(list);

        foreach (var item in obj)
        {
          var listItemBlock = (ListItemBlock)item;
          var listItem = new System.Windows.Documents.ListItem();
          renderer.WriteWithChildren(listItem, listItemBlock);
        }

        renderer.Pop();

      }
    }

    private class CodeInlineRenderer : WpfObjectRenderer<CodeInline>
    {
      protected override void Write(WpfRenderer renderer, CodeInline obj)
      {
        Run run = new(obj.Content);
        run.FontFamily = renderer.Mono;
        run.Background = new SolidColorBrush(Color.FromArgb(130, 200, 200, 200));
        run.Foreground = Brushes.Black;
        renderer.WriteInline(run);
      }
    }

    private class GenericRenderer<T> : WpfObjectRenderer<T> where T : MarkdownObject
    {
      private Action<T> render;

      public GenericRenderer(Action<T> render)
      {
        this.render = render;
      }

      protected override void Write(WpfRenderer renderer, T obj)
      {
        render(obj);
      }
    }

    public class TableRenderer : WpfObjectRenderer<Table>
    {
      protected override void Write(WpfRenderer renderer, Table table)
      {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        if (table == null) throw new ArgumentNullException(nameof(table));

        var wpfTable = new WpfTable();
        wpfTable.FontSize = 13;
        wpfTable.TextAlignment = TextAlignment.Left;

        //wpfTable.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.TableStyleKey);

        foreach (var tableColumnDefinition in table.ColumnDefinitions)
        {
          wpfTable.Columns.Add(new WpfTableColumn
          {
            Width = (tableColumnDefinition?.Width ?? 0) != 0 ?
                  new GridLength(tableColumnDefinition!.Width, GridUnitType.Star) :
                  GridLength.Auto,
          });
        }

        var wpfRowGroup = new WpfTableRowGroup();

        renderer.Push(wpfTable);
        renderer.Push(wpfRowGroup);

        foreach (var rowObj in table)
        {
          var row = (TableRow)rowObj;
          var wpfRow = new WpfTableRow();

          renderer.Push(wpfRow);

          if (row.IsHeader)
          {
            //wpfRow.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.TableHeaderStyleKey);
          }

          for (var i = 0; i < row.Count; i++)
          {
            var cellObj = row[i];
            var cell = (TableCell)cellObj;
            var wpfCell = new WpfTableCell
            {
              ColumnSpan = cell.ColumnSpan,
              RowSpan = cell.RowSpan,
              BorderThickness = new(1),
              BorderBrush = Brushes.DarkSlateGray,
            };

            //wpfCell.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.TableCellStyleKey);

            renderer.Push(wpfCell);
            renderer.Write(cell);
            renderer.Pop();

            if (table.ColumnDefinitions.Count > 0)
            {
              var columnIndex = cell.ColumnIndex < 0 || cell.ColumnIndex >= table.ColumnDefinitions.Count
                  ? i
                  : cell.ColumnIndex;
              columnIndex = columnIndex >= table.ColumnDefinitions.Count ? table.ColumnDefinitions.Count - 1 : columnIndex;
              var alignment = table.ColumnDefinitions[columnIndex].Alignment;
              if (alignment.HasValue)
              {
                switch (alignment)
                {
                  case TableColumnAlign.Center:
                    wpfCell.TextAlignment = TextAlignment.Center;
                    break;
                  case TableColumnAlign.Right:
                    wpfCell.TextAlignment = TextAlignment.Right;
                    break;
                  case TableColumnAlign.Left:
                    wpfCell.TextAlignment = TextAlignment.Left;
                    break;
                }
              }
            }
          }

          renderer.Pop();
        }

        renderer.Pop();
        renderer.Pop();
      }
    }
  }
}
