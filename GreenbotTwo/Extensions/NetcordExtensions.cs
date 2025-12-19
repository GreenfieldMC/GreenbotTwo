using NetCord;

namespace GreenbotTwo.Extensions;

public static class NetcordExtensions
{

    public static T? FromLabel<T>(this IEnumerable<IModalComponent> components, string? withId = null) where T : ILabelComponent, IInteractiveComponent
    {
        var res = components.OfType<Label>().Select(l => l.Component).OfType<T>();
        return withId is null ? res.FirstOrDefault() : res.FirstOrDefault(c => c.CustomId == withId);
    }
    
}