readonly record struct Rgb(byte R, byte G, byte B);

/// <summary>The colorls "dark" theme.</summary>
static class Theme
{
    public static readonly Rgb
        Dir = new(30, 144, 255), Recognised = new(255, 255, 0), Unrecognised = new(255, 215, 0),
        Exec = new(50, 205, 50), Link = new(0, 255, 255), Dead = new(255, 60, 60),
        User = new(221, 160, 221), Group = new(189, 183, 107),
        Read = new(50, 205, 50), Write = new(189, 183, 107), Execute = new(255, 60, 60), NoAccess = new(205, 92, 92),
        HourOld = new(0, 255, 0), DayOld = new(0, 250, 154), Older = new(46, 139, 87),
        Large = new(255, 69, 0), Medium = new(255, 140, 0), Small = new(255, 215, 0),
        Tree = new(0, 255, 255), Dim = new(120, 120, 120), Report = new(255, 255, 255),
        Added = new(127, 255, 0), Modified = new(189, 183, 107), Deleted = new(205, 50, 50),
        Untracked = new(255, 140, 0), Unchanged = new(34, 139, 34), Renamed = new(100, 149, 237);
}
