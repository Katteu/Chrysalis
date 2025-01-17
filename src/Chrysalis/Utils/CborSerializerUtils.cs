using System.Formats.Cbor;
using System.Reflection;
using Chrysalis.Cbor;

namespace Chrysalis.Utils;

public static class CborSerializerUtils
{
    private const int BaseTagValue = 121;

    public static CborTag GetCborTag(int? index = null)
    {
        int actualIndex = index ?? 0;
        return (CborTag)(BaseTagValue + actualIndex);
    }

    public static object GetValue(this ICbor cbor, Type objType)
    {
        if (cbor == null)
            throw new ArgumentNullException(nameof(cbor), "The CBOR object cannot be null.");

        if (objType == null)
            throw new ArgumentNullException(nameof(objType), "The target type cannot be null.");

        PropertyInfo? valueProperty = objType.GetProperty("Value") ?? 
            throw new InvalidOperationException($"Type {objType.Name} does not have a 'Value' property.");

        object? value = valueProperty.GetValue(cbor) ?? 
            throw new InvalidOperationException($"The 'Value' property of type {objType.Name} is null.");

        return value;
    }

    public static CborType? GetCborType(this Type objType)
    {
        if (typeof(ICbor).IsAssignableFrom(objType))
        {
            CborSerializableAttribute? attr = objType.GetCustomAttribute<CborSerializableAttribute>();
            if (attr != null)
                return attr.Type;
        }
        return null;
    }

    public static IEnumerable<Type> GetBaseTypes(Type type)
    {
        var baseType = type.BaseType;
        while (baseType != null)
        {
            yield return baseType;
            baseType = baseType.BaseType;
        }
    }
}