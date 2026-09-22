using System.ComponentModel.DataAnnotations;

namespace smartHRMS.Application.Common.Validation;

/// <summary>
/// Rejects the default value of a value type (e.g. <see cref="Guid.Empty"/> or <c>0001-01-01</c>).
/// <c>[Required]</c> cannot do this: a non-nullable value type is never null, so an omitted
/// JSON property silently binds to its default and would pass validation.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotDefaultAttribute : ValidationAttribute
{
    public NotDefaultAttribute()
        : base("The {0} field is required.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return false;
        }

        var type = value.GetType();
        return !type.IsValueType || !value.Equals(Activator.CreateInstance(type));
    }
}
