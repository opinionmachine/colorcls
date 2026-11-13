static class Icons
{
    const string File = "", Folder = "";

    static readonly Dictionary<string, string> folders = new(StringComparer.OrdinalIgnoreCase)
    {
        [".git"] = "", [".github"] = "", ["node_modules"] = "",
        [".config"] = "", ["config"] = "", [".vscode"] = "", [".idea"] = "",
    };

    static readonly Dictionary<string, string> names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dockerfile"] = "", ["docker-compose.yml"] = "", ["docker-compose.yaml"] = "", [".dockerignore"] = "",
        ["makefile"] = "", ["justfile"] = "",
        ["license"] = "", ["license.txt"] = "", ["license.md"] = "", ["notice.txt"] = "",
        ["readme"] = "", ["readme.md"] = "", ["readme.txt"] = "",
        [".gitignore"] = "", [".gitattributes"] = "", [".gitmodules"] = "", [".gitconfig"] = "",
        [".editorconfig"] = "", [".env"] = "", [".ds_store"] = "",
        ["package.json"] = "", ["package-lock.json"] = "",
        ["gemfile"] = "", ["rakefile"] = "", ["cargo.toml"] = "", ["cargo.lock"] = "",
        ["go.mod"] = "", ["go.sum"] = "",
    };

    static readonly Dictionary<string, string> exts = Build(
        ("\U000f031b", "cs csx"), ("", "csproj sln slnx fsproj vbproj"), ("", "fs fsx fsi"),
        ("", "js mjs cjs"), ("", "ts mts cts"), ("", "jsx tsx"), ("", "json jsonc json5"),
        ("", "html htm xhtml"), ("", "css"), ("", "scss sass less"),
        ("", "py pyc pyi ipynb"), ("", "rb erb gemspec"), ("", "rs"), ("", "go"),
        ("", "java jar class gradle"), ("", "kt kts"), ("", "c h"), ("", "cpp cc cxx hpp hh"),
        ("", "swift"), ("", "php"), ("", "lua"), ("", "vim"),
        ("", "sh bash zsh fish ps1 psm1 bat cmd"),
        ("", "md markdown mdx rst"), ("", "txt log text"),
        ("", "pdf"), ("", "doc docx odt rtf"), ("", "xls xlsx ods csv tsv"), ("", "ppt pptx odp"),
        ("", "png jpg jpeg gif bmp webp ico tiff heic svg avif"),
        ("", "zip tar gz tgz bz2 xz zst 7z rar nupkg"),
        ("", "mp3 wav flac ogg m4a aac opus"), ("", "mp4 mkv mov avi webm m4v"),
        ("", "yml yaml toml ini conf cfg env tf tfvars hcl plist"),
        ("", "xml xaml props targets resx xsd xslt"),
        ("", "sql db sqlite sqlite3 mdb"), ("", "lock"), ("", "pem crt cer key pub pfx p12"),
        ("", "ttf otf woff woff2 eot"), ("", "exe dll so dylib a o bin pdb"), ("", "iso dmg img"));

    static Dictionary<string, string> Build(params (string icon, string exts)[] groups)
    {
        var d = new Dictionary<string, string>();
        foreach (var (icon, list) in groups)
            foreach (var x in list.Split(' ')) d[x] = icon;
        return d;
    }

    public static (string icon, bool recognised) For(Entry e)
    {
        if (e.IsDir) return (folders.GetValueOrDefault(e.Name, Folder), true);
        if (names.TryGetValue(e.Name, out var n)) return (n, true);
        if (exts.TryGetValue(e.Extension, out var x)) return (x, true);
        return (File, false);
    }
}
