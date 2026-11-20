using System.Text.RegularExpressions;

record AgencyImportGroup(string Name,int Agencies);

// Only an initial numeric label followed by a separator is treated as a group code.
// Do not remove words, internal punctuation or different numeric codes.
sealed class AgencyImportGroups
{
    static readonly Regex Spaces=new(@"\s+",RegexOptions.CultureInvariant);
    static readonly Regex Prefix=new(@"^(\d+)\s*[-|–—]\s*(\S.*)$",RegexOptions.CultureInvariant);
    readonly Dictionary<string,List<(string Name,string? Code,int Agencies)>> candidates=new(StringComparer.OrdinalIgnoreCase);

    public AgencyImportGroups(IEnumerable<AgencyImportGroup> existing,IEnumerable<string> incoming)
    {
        foreach(var group in existing)Add(group.Name,group.Agencies);
        foreach(var name in incoming)Add(name,0);
    }

    static (string Name,string Key,string? Code) Identity(string value)
    {
        var name=Spaces.Replace(value.Trim()," ");
        var match=Prefix.Match(name);
        if(!match.Success)return(name,name,null);
        var code=match.Groups[1].Value.TrimStart('0');
        return(name,match.Groups[2].Value.Trim(),code.Length==0?"0":code);
    }

    void Add(string value,int agencies)
    {
        var identity=Identity(value);
        if(!candidates.TryGetValue(identity.Key,out var list))candidates[identity.Key]=list=[];
        // Keep the actual stored spelling for permission checks against admin_user_groups.
        list.Add((agencies>0?value:identity.Name,identity.Code,agencies));
    }

    public (string Name,string? Error) Resolve(string value)
    {
        var identity=Identity(value);
        var options=candidates[identity.Key];
        if(options.Where(x=>x.Code is not null).Select(x=>x.Code).Distinct().Count()>1)
            return(identity.Name,"El nombre del grupo coincide con códigos distintos; revisa el grupo antes de importar");
        var canonical=options.OrderByDescending(x=>x.Agencies)
            .ThenBy(x=>x.Name,StringComparer.Ordinal).First().Name;
        return(canonical,null);
    }
}
