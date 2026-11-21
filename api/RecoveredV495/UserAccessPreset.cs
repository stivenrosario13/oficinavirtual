using System.Collections.Generic;

internal record UserAccessPreset(string Department, string Profile, Dictionary<string, bool> Permissions);
