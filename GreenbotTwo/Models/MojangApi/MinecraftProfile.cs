namespace GreenbotTwo.Models;

public class MinecraftProfile(string id, string name)
{

    public string Id
    {
        set => Uuid = Guid.ParseExact(value, "N");
    }

    public Guid Uuid { get; set; }
    public string Name { get; set; } = name;

    public MinecraftProfile() : this(Guid.Empty.ToString(), string.Empty)
    {
    }
}