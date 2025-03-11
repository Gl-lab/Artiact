using System.ComponentModel;

namespace Artiact.Models;

public record ResourceSkill(string Value)
{
    public override string ToString() => Value;

    public static readonly ResourceSkill Woodcutting = new("woodcutting");
    public static readonly ResourceSkill Fishing = new("fishing");
    public static readonly ResourceSkill Mining = new("mining");
    public static readonly ResourceSkill Alchemy = new("alchemy");
}