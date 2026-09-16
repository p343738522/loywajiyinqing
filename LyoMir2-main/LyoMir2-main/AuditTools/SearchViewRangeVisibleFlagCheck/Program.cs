using System.Text.RegularExpressions;

var root = FindRepositoryRoot();
var viewRange = File.ReadAllText(Path.Combine(root, "GameSvr", "Actors",
    "TBaseObject.ViewRange.cs"));
var playObject = File.ReadAllText(Path.Combine(root, "GameSvr", "Players",
    "TPlayObject.Base.cs"));

var baseSearch = MethodBody(viewRange, "public virtual void SearchViewRange()");
var deathSearch = MethodBody(viewRange, "public virtual void SearchViewRange_Death()");
var playSearch = MethodBody(playObject, "public override void SearchViewRange()");
var baseUpdate = MethodBody(viewRange, "protected virtual void UpdateVisibleGay(");
var playUpdate = MethodBody(playObject, "protected override void UpdateVisibleGay(");

AssertMarkThenSweep(baseSearch, "TBaseObject.SearchViewRange");
AssertMarkThenSweep(deathSearch, "TBaseObject.SearchViewRange_Death");
AssertMarkThenSweep(playSearch, "TPlayObject.SearchViewRange");

Require(baseSearch, "m_nCurrX - m_nViewRange",
    "TBaseObject.SearchViewRange radius left m_nViewRange");
Require(playSearch, "m_nCurrX - m_nViewRange",
    "TPlayObject.SearchViewRange radius left m_nViewRange");
Require(baseSearch, "GlobalSeeZone",
    "TBaseObject.SearchViewRange lost the GlobalSeeZone vs m_nViewRange note");
Assert(!Regex.IsMatch(baseSearch,
        @"nStartX\s*=\s*.*GlobalSeeZone",
        RegexOptions.CultureInvariant),
    "TBaseObject.SearchViewRange scan radius switched to GlobalSeeZone");
Assert(!Regex.IsMatch(playSearch,
        @"nStartX\s*=\s*.*GlobalSeeZone",
        RegexOptions.CultureInvariant),
    "TPlayObject.SearchViewRange scan radius switched to GlobalSeeZone");

Assert(!HasCellArm(baseSearch, "OS_ITEMOBJECT"),
    "TBaseObject.SearchViewRange grew an item arm");
Assert(!HasCellArm(baseSearch, "OS_EVENTOBJECT"),
    "TBaseObject.SearchViewRange grew an event arm");
Assert(HasCellArm(baseSearch, "OS_MOVINGOBJECT"),
    "TBaseObject.SearchViewRange lost the moving-object arm");
Assert(HasCellArm(playSearch, "OS_MOVINGOBJECT"),
    "TPlayObject.SearchViewRange lost the moving-object arm");
Assert(HasCellArm(playSearch, "OS_ITEMOBJECT"),
    "TPlayObject.SearchViewRange lost the item arm");
Assert(HasCellArm(playSearch, "OS_EVENTOBJECT"),
    "TPlayObject.SearchViewRange lost the event arm");

Require(baseUpdate, "nVisibleFlag = 1",
    "base UpdateVisibleGay no longer marks already-visible as 1");
Require(playUpdate, "nVisibleFlag = 1",
    "player UpdateVisibleGay no longer marks already-visible as 1");
Require(viewRange, "item.nVisibleFlag = 2",
    "RentVisibleBaseObject no longer stamps newly-visible as 2");

Require(playSearch, "m_VisibleItems[i].nVisibleFlag = 0",
    "TPlayObject.SearchViewRange no longer zeros item flags first");
Require(playSearch, "m_VisibleEvents[i].nVisibleFlag = 0",
    "TPlayObject.SearchViewRange no longer zeros event flags first");
AssertRemoveFlagZero(playSearch, "m_VisibleItems",
    "TPlayObject.SearchViewRange item list");
AssertRemoveFlagZero(playSearch, "m_VisibleEvents",
    "TPlayObject.SearchViewRange event list");

Console.WriteLine(
    "SearchViewRangeVisibleFlagCheck PASS mark0-then-remove0 " +
    "radius=m_nViewRange player-arms=1/2/3");
return;

static void AssertMarkThenSweep(string method, string label)
{
    var mark = method.IndexOf("m_VisibleActors[i].nVisibleFlag = 0",
        StringComparison.Ordinal);
    Assert(mark >= 0, label + " no longer zeros m_VisibleActors nVisibleFlag");

    var scan = method.IndexOf("GetMapCellInfo", StringComparison.Ordinal);
    Assert(scan > mark, label + " zeros visible flags after the cell scan");

    AssertRemoveFlagZero(method, "m_VisibleActors", label);
}

static void AssertRemoveFlagZero(string method, string list, string label)
{
    var flagZero = list + "[";
    var remove = Regex.Match(method,
        list + @"\.RemoveAt\((?<idx>[^\)]+)\)",
        RegexOptions.CultureInvariant);
    Assert(remove.Success, label + " no longer RemoveAt from " + list);

    var idx = remove.Groups["idx"].Value.Trim();
    var flag = Regex.Match(method,
        @"(?:var\s+\w+\s*=\s*)?" + Regex.Escape(list) + @"\[" + Regex.Escape(idx) +
        @"\](?:\.\w+)?\.nVisibleFlag\s*==\s*0",
        RegexOptions.CultureInvariant);
    if (!flag.Success)
    {
        flag = Regex.Match(method,
            @"nVisibleFlag\s*==\s*0",
            RegexOptions.CultureInvariant);
    }
    Assert(flag.Success, label + " no longer removes nVisibleFlag==0 from " + list);

    var removeAt = remove.Index;
    var after = method.Substring(removeAt, Math.Min(180, method.Length - removeAt));
    Assert(after.Contains("continue", StringComparison.Ordinal),
        label + " increments past a nVisibleFlag==0 " + list + " RemoveAt");
}

static bool HasCellArm(string method, string cellType)
{
    return method.Contains("CellType." + cellType, StringComparison.Ordinal);
}

static string MethodBody(string source, string signature)
{
    var start = source.IndexOf(signature, StringComparison.Ordinal);
    Assert(start >= 0, "missing method: " + signature);
    var brace = source.IndexOf('{', start);
    Assert(brace > start, "missing body: " + signature);
    var depth = 0;
    for (var i = brace; i < source.Length; i++)
    {
        var ch = source[i];
        if (ch == '{') depth++;
        else if (ch == '}')
        {
            depth--;
            if (depth == 0)
                return source[start..(i + 1)];
        }
    }
    throw new InvalidOperationException("unclosed method: " + signature);
}

static void Require(string source, string value, string message)
{
    Assert(source.Contains(value, StringComparison.Ordinal), message);
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static string FindRepositoryRoot()
{
    foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
    {
        var current = new DirectoryInfo(start);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "GameSvr", "GameSvr.csproj")))
                return current.FullName;
            current = current.Parent;
        }
    }

    throw new DirectoryNotFoundException("repository root not found");
}
