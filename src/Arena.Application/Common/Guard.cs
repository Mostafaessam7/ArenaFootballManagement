namespace Arena.Application.Common;

public static class Guard
{
    /// <summary>Throws 409 with <paramref name="message"/> when <paramref name="condition"/> is true.</summary>
    public static void Against(bool condition, string message, string code = "business_rule")
    {
        if (condition) throw new BusinessException(409, code, message);
    }

    /// <summary>Returns <paramref name="value"/> or throws 404.</summary>
    public static T Found<T>(T? value) where T : class => value ??
        throw new BusinessException(404, "not_found", "The requested resource was not found.");

    /// <summary>Rejects an update that was based on an older read of the entity.</summary>
    public static void Version(Entity entity, Guid expected) => Against(entity.Version != expected,
        "This resource changed. Reload it and retry with its current version.", "stale_version");
}
