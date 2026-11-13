# colorcls

A colourised `ls` with file-type icons and git status, written as a C# file-based app for .NET 11.

It is a port of the idea behind [colorls](https://github.com/athityakumar/colorls), the Ruby gem by Athitya Kumar. The look, the colour theme and most of the flags come from there. The different name is deliberate: this is not that project, and the credit belongs to it.

```
$ colorcls -l
drwxr-xr-x  rikard  staff           -  14 Nov 2026 12:04    M    src/
-rwxr-xr-x  rikard  staff     4.3 KiB  14 Nov 2026 12:05    ✓  󰌛  cake.cs
-rwxr-xr-x  rikard  staff     822 B    14 Nov 2026 12:04    ✓  󰌛  colorcls.cs
-rw-r--r--  rikard  staff      25 B    14 Nov 2026 12:04    ?    notes.txt
```

## What it does

- Grid, long (`-l`), one-per-line (`-1`) and tree (`--tree`) listings
- Colours by file type, permission bit, size and age
- Nerd Font icons for common file types and folders
- A git status column whenever the listed folder is inside a work tree
- Starts in a few milliseconds once compiled to a native binary

## Requirements

- .NET 11 SDK
- `git` on your PATH, for the status column
- A [Nerd Font](https://www.nerdfonts.com/) in your terminal, for the icons (or pass `--no-icons`)
- To build the native binary: Xcode command line tools on macOS, or `clang` and `zlib1g-dev` on Linux

## Install

```bash
git clone git@github.com:opinionmachine/colorcls.git
```

```bash
cd colorcls
```

```bash
./cake.cs --target=Install
```

This compiles a native AOT binary into `artifacts/colorcls/` and symlinks it as `colorcls` into the first writable folder on your PATH out of `/usr/local/bin`, `~/.local/bin` and Homebrew's `bin`. The script prints where it put the link.

To choose the folder or the command name yourself:

```bash
./cake.cs --target=Install --bindir=$HOME/bin --name=cls
```

The link points into this checkout, so keep the repo where it is. After pulling changes, run `Install` again to rebuild.

To remove it:

```bash
./cake.cs --target=Uninstall
```

If you would like it to replace `ls`, add an alias to your shell profile:

```bash
alias ls=colorcls
```

### Run without installing

`colorcls.cs` is executable and runs straight from source. The first run compiles; later runs reuse the cached build.

```bash
./colorcls.cs -la
```

### Homebrew .NET on macOS

Homebrew's .NET links native binaries against Homebrew's OpenSSL, brotli and zstd. `cake.cs` adds Homebrew's `lib` folder to the linker path for you. The binary loads those libraries at startup, so if a `brew upgrade` replaces OpenSSL with a new major version and `colorcls` stops starting, run `Install` again.

## Usage

```
colorcls [options] [path ...]
```

| Option | Effect |
| --- | --- |
| `-l`, `--long` | Long listing: mode, owner, group, size, modified time, git, name |
| `-1` | One entry per line |
| `--tree[=DEPTH]` | Recurse and draw a tree |
| `-a`, `--all` | Include dotfiles, plus `.` and `..` |
| `-A`, `--almost-all` | Include dotfiles, without `.` and `..` |
| `-d`, `--dirs` | Directories only |
| `-f`, `--files` | Files only |
| `-t` | Newest first |
| `-S` | Largest first |
| `-X` | By extension |
| `-U` | Directory order, unsorted |
| `--sort=KEY` | `name`, `time`, `size`, `extension` or `none` |
| `-r`, `--reverse` | Reverse the order |
| `--sd`, `--sort-dirs` | Directories first |
| `--sf`, `--sort-files` | Files first |
| `--gs`, `--git-status` | Show git status (already on inside a work tree) |
| `--no-git` | Never run git |
| `--no-icons` | Leave out the icons |
| `--report` | Print a count of folders and files at the end |
| `--hyperlink` | Make names clickable in terminals that support OSC 8 |
| `--color[=WHEN]` | `always`, `auto` or `never` |
| `-h`, `--help` | Show help |

The short flags follow colorls, not `ls`: `-d` lists directories only and `-h` is help.

When the output is piped, colorcls prints one plain entry per line, as `ls` does. `--color=always` keeps the colours and the grid. `NO_COLOR` and `CLICOLOR_FORCE` are honoured.

### Git status column

| Mark | Meaning |
| --- | --- |
| `✓` | Tracked and unchanged |
| `M` | Modified |
| `A` | Added |
| `D` | Deleted |
| `R` | Renamed |
| `U` | Merge conflict |
| `?` | Untracked |
| `!` | Ignored |

A directory shows the combined marks of everything changed inside it. Ignored files are the exception: a directory is only marked `!` when it is itself ignored. The column widens when an entry carries more than three marks.

## Development

The app is a file-based program: there is no `.csproj`. `colorcls.cs` is the entry point and pulls in the rest with `#:include`.

| File | Contents |
| --- | --- |
| `colorcls.cs` | Entry point and build directives |
| `src/App.cs` | One invocation: resolves paths, drives the renderer, tracks errors and the report |
| `src/Options.cs` | The flag table; the parser and the help text are generated from it |
| `src/Terminal.cs` | Colour support, width and escape codes of the output device |
| `src/Listing.cs` | Reads directories into entries, filters and sorts them |
| `src/Entry.cs` | One file system entry |
| `src/Posix.cs` | File type, permissions, owner and group through libc |
| `src/GitRepo.cs` | Loads `git status` once per work tree and answers lookups by path |
| `src/Icons.cs` | Icon tables |
| `src/Theme.cs` | Colours |
| `src/Cell.cs` | Painted text that knows its on-screen width |
| `src/Columns.cs` | Formats each field of an entry as a cell |
| `src/Renderer.cs` | The grid, long, one-per-line and tree layouts |
| `tests/` | Unit tests, one file per source file under test |
| `cake.cs` | Build script ([Cake](https://cakebuild.net/) as a file-based app) |

Build targets:

| Command | Effect |
| --- | --- |
| `./cake.cs` | Run the tests, then publish the native binary |
| `./cake.cs --target=Test` | Run the tests only |
| `./cake.cs --target=Install` | Test, publish and symlink onto your PATH |
| `./cake.cs --target=Uninstall` | Remove the symlink |
| `./cake.cs --target=Clean` | Delete `artifacts/` |

### Tests

The tests are a second file-based app, `tests/tests.cs`, which includes the same source files and runs them under xunit. No project file is involved.

```bash
./tests/tests.cs
```

They cover option parsing, git status parsing, sorting and filtering, column formatting and the grid, long and tree layouts. A few create a scratch directory or a throwaway git repository under the system temp folder.

GitHub Actions runs the tests and a native build on Linux x64, Linux arm64, macOS and Windows for every push and pull request (`.github/workflows/ci.yml`).

A devcontainer with the .NET 11 SDK and the native build tools is included, for working without a local SDK.

### Platform support

Developed on macOS (Apple Silicon) and Linux arm64. Linux x64 and Windows are covered by CI only. On Windows the long listing leaves out mode, owner and group.

## Licence

GPL-3.0. See [LICENSE](LICENSE).
