using System.Text;

/// <summary>One invocation: resolves the paths, feeds listings to the renderer, tracks errors and the report.</summary>
sealed class App(Options options, Terminal term, TextWriter output)
{
    readonly Renderer renderer = new(output, new Columns(term, options, DateTime.UtcNow, Posix.LocalTime), term);
    // Piped output is one name per line, as with ls, unless colour was forced for a pager.
    readonly Layout layout =
        options.Layout == Layout.Grid && term.Piped && options.Color != ColorWhen.Always ? Layout.OnePerLine : options.Layout;

    int exitCode, folders, recognised, unrecognised;

    public static int Run(string[] args)
    {
        Options options;
        try { options = Options.Parse(args); }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"colorcls: {ex.Message}\nTry 'colorcls --help'.");
            return 2;
        }

        var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false), 1 << 16);
        App? app = null;
        try
        {
            if (options.Help) { output.Write(Options.HelpText()); return 0; }
            if (options.Version) { output.WriteLine("colorcls 1.0"); return 0; }
            app = new App(options, Terminal.Detect(options.Color), output);
            return app.List();
        }
        catch (IOException) { return app?.exitCode ?? 0; }   // the reader of a pipe went away, e.g. `| head`
        finally { try { output.Flush(); } catch (IOException) { } }
    }

    int List()
    {
        var files = new List<Entry>();
        var dirs = new List<string>();
        foreach (var path in options.Paths)
        {
            if (Directory.Exists(path)) dirs.Add(path);
            else if (Listing.ReadArgument(path, options) is { } file) files.Add(file);
            else Fail(path, "No such file or directory", 2);
        }

        bool first = true;
        if (files.Count > 0)
        {
            Show(Tally(Listing.Sort(files, options)));
            first = false;
        }
        foreach (var dir in dirs)
        {
            if (!first) output.WriteLine();
            if (options.Paths.Count > 1) output.WriteLine(term.Paint(Theme.Dir, dir).Text + ":");
            first = false;
            if (Load(dir) is { } entries) Show(entries);
        }

        if (options.Report)
            output.Write(term.Paint(Theme.Report, $"""

                    Found {folders + recognised + unrecognised} items in total.

                    Folders            : {folders}
                    Recognized files   : {recognised}
                    Unrecognized files : {unrecognised}

                """).Text);
        return exitCode;
    }

    void Show(List<Entry> entries) => renderer.Render(layout, entries, options.TreeDepth, Load);

    /// <summary>The visible, ordered contents of a directory, or null (after reporting) if it cannot be read.</summary>
    List<Entry>? Load(string dir)
    {
        try
        {
            bool withDots = options.Hidden == Hidden.ShowWithDots && layout != Layout.Tree;
            return Tally(Listing.Sort(Listing.Filter(Listing.ReadDirectory(dir, options, withDots), options), options));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Fail(dir, ex is UnauthorizedAccessException ? "Permission denied" : ex.Message, 1);
            return null;
        }
    }

    List<Entry> Tally(List<Entry> shown)
    {
        foreach (var e in shown)
        {
            if (e.IsDir) folders++;
            else if (e.Icon.Recognised) recognised++;
            else unrecognised++;
        }
        return shown;
    }

    void Fail(string path, string message, int code)
    {
        output.Flush();   // keep stdout and stderr in order
        Console.Error.WriteLine($"colorcls: {path}: {message}");
        exitCode = Math.Max(exitCode, code);
    }
}
