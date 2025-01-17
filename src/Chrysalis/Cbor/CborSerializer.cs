﻿using System.Formats.Cbor;
using System.Reflection;
using Chrysalis.Cardano.Models.Cbor;
using Chrysalis.Cardano.Models.Core;
using Chrysalis.Utils;

namespace Chrysalis.Cbor;
public static class CborSerializer
{
    public static byte[] Serialize(ICbor cbor)
    {
        CborWriter writer = new(CborConformanceMode.Strict);
        SerializeCbor(writer, cbor, cbor.GetType());
        return writer.Encode();
    }

    public static T? Deserialize<T>(byte[] cborData)
    {
        CborReader reader = new(cborData, CborConformanceMode.Strict);
        return (T?)DeserializeCbor(reader, typeof(T));
    }

    private static void SerializeCbor(CborWriter writer, ICbor cbor, Type objType)
    {
        CborType? cborType = CborSerializerUtils.GetCborType(objType);

        if (cborType is not null)
        {
            switch (cborType)
            {
                case CborType.Bytes:
                    SerializeCborBytes(writer, cbor, objType);
                    break;
                case CborType.Int:
                    SerializeCborInt(writer, cbor, objType);
                    break;
                case CborType.Ulong:
                    SerializeCborUlong(writer, cbor, objType);
                    break;
                case CborType.List:
                    SerializeList(writer, cbor, objType);
                    break;
                case CborType.Map:
                    SerializeMap(writer, cbor, objType);
                    break;
                case CborType.Constr:
                    SerializeConstructor(writer, cbor, objType);
                    break;
                case CborType.EncodedValue:
                    SerializeEncodedValue(writer, cbor, objType);
                    break;
                default:
                    throw new NotSupportedException($"CBOR type {cborType} is not supported in this context.");
            }
        }
        else
        {
            throw new NotImplementedException($"Type not supported {objType}");
        }
    }

    private static void SerializeList(CborWriter writer, ICbor cbor, Type objType)
    {
        if (objType.IsGenericType)
        {
            CborSerializableAttribute? cborSerializableAttr = objType.GetCustomAttribute<CborSerializableAttribute>();
            MethodInfo? method = typeof(CborSerializer).GetMethod(nameof(SerializeGenericList), BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo genericMethod = method!.MakeGenericMethod(objType.GetGenericArguments());
            genericMethod.Invoke(null, [writer, cbor, cborSerializableAttr!.IsDefinite]);
            return;
        }
        else if (objType.GetCustomAttribute<CborSerializableAttribute>()?.Type == CborType.List)
        {
            SerializeListStructure(writer, cbor, objType);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported list type for deserialization: {cbor.GetType().Name}");
        }
    }

    private static void SerializeGenericList<T>(CborWriter writer, ICbor cborList, bool definite = false) where T : ICbor
    {
        T[] elements = (T[])cborList.GetValue(cborList.GetType());
        writer.WriteStartArray(definite ? elements.Length : null);

        foreach (T element in elements)
        {
            SerializeCbor(writer, element, element.GetType());
        }

        writer.WriteEndArray();
    }

    private static void SerializeListStructure(CborWriter writer, ICbor cbor, Type objType)
    {
        Type? baseType = objType.BaseType;
        CborSerializableAttribute? cborSerializableAttr = objType.GetCustomAttribute<CborSerializableAttribute>();
        bool IsDefinite = cborSerializableAttr?.IsDefinite ?? true;

        if (cbor is null)
            return;

        if (baseType != null && baseType.IsGenericType)
        {
            MethodInfo? method = typeof(CborSerializer).GetMethod(nameof(SerializeGenericList), BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo genericMethod = method!.MakeGenericMethod(baseType.GetGenericArguments());
            genericMethod.Invoke(null, [writer, cbor, IsDefinite]);
        }
        else
        {
            PropertyInfo[] properties = cbor.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            writer.WriteStartArray(IsDefinite ? properties.Length : null);

            foreach (PropertyInfo property in properties)
            {
                object? value = property.GetValue(cbor);
                if (value is ICbor cborValue)
                {
                    SerializeCbor(writer, cborValue, property.PropertyType);
                }
                else
                {
                    throw new InvalidOperationException($"Property {property.Name} in {baseType?.Name} does not implement ICbor.");
                }
            }

            writer.WriteEndArray();
        }
    }

    private static void SerializeCborUlong(CborWriter writer, ICbor cbor, Type objType)
    {
        writer.WriteUInt64((ulong)cbor.GetValue(objType));
    }

    private static void SerializeCborInt(CborWriter writer, ICbor cbor, Type objType)
    {
        writer.WriteInt32((int)cbor.GetValue(objType));
    }

    private static void SerializeCborBytes(CborWriter writer, ICbor cbor, Type objType)
    {
        CborSerializableAttribute? cborSerializableAttr = objType.GetCustomAttribute<CborSerializableAttribute>();
        if (!cborSerializableAttr!.IsDefinite)
        {
            writer.WriteStartIndefiniteLengthByteString();
            byte[] bytesValue = (byte[])cbor.GetValue(objType);
            const int chunkSize = 1024;
            for (int i = 0; i < bytesValue.Length; i += chunkSize)
            {
                int length = Math.Min(chunkSize, bytesValue.Length - i);
                writer.WriteByteString(((byte[])cbor.GetValue(objType)).AsSpan(i, length));
            }
            writer.WriteEndIndefiniteLengthByteString();
        }
        else
        {
            writer.WriteByteString((byte[])cbor.GetValue(objType));
        }
    }

    private static void SerializeMap(CborWriter writer, ICbor obj, Type objType)
    {
            CborSerializableAttribute? cborSerializableAttr = objType.GetCustomAttribute<CborSerializableAttribute>();
            if (obj is CborMap cborMap)
            {
                writer.WriteStartMap(cborSerializableAttr!.IsDefinite ? cborMap.Value.Count : null);
                foreach (KeyValuePair<ICbor, ICbor> kvp in cborMap.Value)
                {
                    SerializeCbor(writer, kvp.Key, kvp.Key.GetType());
                    SerializeCbor(writer, kvp.Value, kvp.Value.GetType());
                }
                writer.WriteEndMap();
            }
            else if (obj is not null && objType.IsGenericType && objType.GetGenericTypeDefinition() == typeof(CborMap<,>))
            {
                MethodInfo? method = typeof(CborSerializer).GetMethod(nameof(SerializeGenericMap), BindingFlags.NonPublic | BindingFlags.Static);
                MethodInfo genericMethod = method!.MakeGenericMethod(objType.GetGenericArguments());
                genericMethod.Invoke(null, [writer, obj, cborSerializableAttr!.IsDefinite]);
            }
            else if (obj is not null && objType.GetCustomAttribute<CborSerializableAttribute>()?.Type == CborType.Map)
            {
                SerializeCustomMap(writer, obj, objType, cborSerializableAttr!.IsDefinite);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported map type for serialization: {objType.Name}");
            }
    }

    private static void SerializeCustomMap(CborWriter writer, ICbor obj, Type objType, bool definite = false)
    {
        Type? baseType = objType.BaseType;

        if (baseType != null && baseType.IsGenericType)
        {
            Type[] genericArgs = baseType.GetGenericArguments();
            MethodInfo? method = typeof(CborSerializer).GetMethod(nameof(SerializeGenericMap), BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo genericMethod = method!.MakeGenericMethod(genericArgs);
            genericMethod.Invoke(null, [writer, obj, definite]);
        }
        else
        {
            Type[] types = objType.GetProperties()
                       .Select(p => p.PropertyType)
                       .ToArray();
            ConstructorInfo? constructor = objType.GetConstructor(types);
            ParameterInfo[]? parameters = (constructor?.GetParameters()) ??
                throw new InvalidOperationException($"No suitable constructor found for type: {objType.Name}");

            int mapSize = parameters.Count(p =>
            {
                CborPropertyAttribute? cborPropertyAttr = p.GetCustomAttribute<CborPropertyAttribute>();
                if (cborPropertyAttr == null)
                    return false;

                PropertyInfo? property = objType.GetProperty(p.Name!);
                object? value = property?.GetValue(obj);

                return value != null;
            });

            writer.WriteStartMap(definite ? mapSize : null);

            foreach (ParameterInfo parameter in parameters)
            {
                CborPropertyAttribute? cborPropertyAttr = parameter.GetCustomAttribute<CborPropertyAttribute>();
                if (cborPropertyAttr != null)
                {
                    PropertyInfo? property = objType.GetProperty(parameter.Name!);
                    object? value = property?.GetValue(obj);

                    if (value is null)
                    {
                        continue;
                    }

                    if (cborPropertyAttr.Key != null)
                    {
                        writer.WriteTextString(cborPropertyAttr.Key);
                    }
                    else if (cborPropertyAttr.Index != null)
                    {
                        writer.WriteInt32((int)cborPropertyAttr.Index);
                    }


                    if (value is ICbor cborValue)
                    {
                        SerializeCbor(writer, cborValue, value.GetType());
                    }
                    else
                    {
                        throw new InvalidOperationException($"Parameter {parameter.Name} in {objType.Name} does not implement ICbor.");
                    }
                }
            }

            writer.WriteEndMap();
        }
    }

    private static void SerializeGenericMap<TKey, TValue>(CborWriter writer, CborMap<TKey, TValue> map, bool definite = false)
        where TKey : ICbor
        where TValue : ICbor
    {
        writer.WriteStartMap(definite ? map.Value.Count : null);
        foreach (KeyValuePair<TKey, TValue> kvp in map.Value)
        {
            SerializeCbor(writer, kvp.Key, typeof(TKey));
            SerializeCbor(writer, kvp.Value, typeof(TValue));
        }
        writer.WriteEndMap();
    }

    private static void SerializeConstructor(CborWriter writer, ICbor cbor, Type objType)
    {
        CborSerializableAttribute? cborSerializableAttr = objType.GetCustomAttribute<CborSerializableAttribute>();
        if (cborSerializableAttr == null || cborSerializableAttr.Type != CborType.Constr)
            throw new InvalidOperationException($"Type {objType.Name} is not marked as CborSerializable with CborType.Constr");

        ConstructorInfo? constructor = objType.GetConstructors().FirstOrDefault() ??
            throw new InvalidOperationException($"No constructor found for type {objType.Name}");

        ParameterInfo[] parameters = constructor.GetParameters();

        writer.WriteTag(CborSerializerUtils.GetCborTag(cborSerializableAttr.Index));
        if (parameters.Length == 0)
        {
            writer.WriteStartArray(0);
            writer.WriteEndArray();
            return;
        }
        writer.WriteStartArray(null);

        foreach (ParameterInfo parameter in parameters)
        {
            PropertyInfo? property = objType.GetProperty(parameter.Name!, BindingFlags.Public | BindingFlags.Instance) ??
                throw new InvalidOperationException($"Property {parameter.Name} not found in {objType.Name}");

            object? value = property.GetValue(cbor) ??
                throw new InvalidOperationException($"Value for property {property.Name} in {objType.Name} is null");

            SerializeCbor(writer, (ICbor)value, value.GetType());
        }

        writer.WriteEndArray();
    }

    private static void SerializeEncodedValue(CborWriter writer, ICbor cbor, Type objType)
    {
        CborTag tag = CborTag.EncodedCborDataItem;
        writer.WriteTag(tag);

        byte[] value = (byte[])cbor.GetValue(objType);
        writer.WriteByteString(value);
    }

    private static ICbor? DeserializeCbor(CborReader reader, Type targetType)
    {
        if (reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            return null;
        }

        CborType? cborType = CborSerializerUtils.GetCborType(targetType);

        if (cborType != null)
        {
            return cborType switch
            {
                CborType.Map => DeserializeMap(reader, targetType),
                CborType.Int => DeserializeCborInt(reader, targetType),
                CborType.Ulong => DeserializeCborUlong(reader, targetType),
                CborType.Union => DeserializeUnion(reader, targetType),
                CborType.Bytes => DeserializeCborBytes(reader, targetType),
                CborType.Constr => DeserializeConstructor(reader, targetType),
                CborType.List => DeserializeList(reader, targetType),
                CborType.EncodedValue => DeserializeEncodedValue(reader, targetType),
                _ => throw new NotImplementedException($"Unknown CborType: {cborType}"),
            };
        }

        throw new NotImplementedException($"Deserialization not implemented for target type {targetType.Name}");
    }

    private static ICbor DeserializeCborBytes(CborReader reader, Type targetType)
    {
        byte[] value = reader.ReadByteString();
        return (CborBytes)Activator.CreateInstance(targetType, value)!;
    }

    private static ICbor DeserializeCborInt(CborReader reader, Type targetType)
    {
        int value = reader.ReadInt32();
        return (CborInt)Activator.CreateInstance(targetType, value)!;
    }

    private static ICbor DeserializeCborUlong(CborReader reader, Type targetType)
    {
        ulong value = reader.ReadUInt64();
        return (ICbor)Activator.CreateInstance(targetType, value)!;
    }

    private static ICbor? DeserializeList(CborReader reader, Type targetType)
    {
        if (targetType.IsGenericType)
        {
            Type itemType = targetType.GetGenericArguments()[0];
            MethodInfo? method = typeof(CborSerializer).GetMethod(nameof(DeserializeGenericList), BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo genericMethod = method!.MakeGenericMethod(itemType, targetType);
            return (ICbor?)genericMethod.Invoke(null, [reader, targetType]);
        }
        else if (targetType.GetCustomAttribute<CborSerializableAttribute>()?.Type == CborType.List)
        {
            return DeserializeListStructure(reader, targetType);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported list type for deserialization: {targetType.Name}");
        }
    }

    private static TList? DeserializeGenericList<T, TList>(CborReader reader, Type targetType)
        where T : ICbor
        where TList : ICbor
    {
        if (reader.PeekState() != CborReaderState.StartArray)
            throw new InvalidOperationException("Expected start of array in CBOR data");

        Type? itemType = targetType.GetGenericArguments()[0];
        List<T> list = [];

        reader.ReadStartArray();
        while (reader.PeekState() != CborReaderState.EndArray)
        {
            T? item = (T)DeserializeCbor(reader, itemType!)!;
            list.Add(item);
        }
        reader.ReadEndArray();

        return (TList?)Activator.CreateInstance(typeof(TList), [list.ToArray()]);
    }

    private static ICbor? DeserializeListStructure(CborReader reader, Type targetType)
    {
        Type? baseType = targetType.BaseType;

        if (baseType != null && baseType.IsGenericType)
        {
            Type genericArg = baseType.GetGenericArguments()[0];

            MethodInfo? method = typeof(CborSerializer).GetMethod(nameof(DeserializeGenericList), BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo genericMethod = method!.MakeGenericMethod(genericArg, baseType);

            ICbor list = (ICbor)genericMethod.Invoke(null, [reader, baseType])!;

            ConstructorInfo? ctor = targetType.GetConstructors()[0];
            if (ctor != null)
            {
                return (ICbor?)ctor.Invoke([list.GetValue(list.GetType())]);
            }
        }
        else
        {
            reader.ReadStartArray();

            List<PropertyInfo> properties = [.. targetType.GetProperties()];

            object[] values = new object[properties.Count];

            for (int i = 0; i < properties.Count; i++)
            {
                Type propType = properties[i].PropertyType;
                values[i] = DeserializeCbor(reader, propType)!;
            }

            reader.ReadEndArray();

            return (ICbor?)Activator.CreateInstance(targetType, values);
        }

        throw new InvalidOperationException($"No suitable constructor found for type: {targetType.Name}");
    }

    private static ICbor? DeserializeConstructor(CborReader reader, Type targetType)
    {
        reader.ReadTag();
        reader.ReadStartArray();

        if (targetType.IsGenericType)
        {
            Type genericTypeDef = targetType.GetGenericTypeDefinition();
            Type[] genericArgs = targetType.GetGenericArguments();

            CborSerializableAttribute? cborSerializableAttr = genericTypeDef.GetCustomAttribute<CborSerializableAttribute>();
            if (cborSerializableAttr != null)
            {
                ParameterInfo[] constructorParams = targetType.GetConstructors().First().GetParameters();
                object?[] args = new object?[constructorParams.Length];

                for (int i = 0; i < constructorParams.Length; i++)
                {
                    Type paramType = constructorParams[i].ParameterType;
                    args[i] = DeserializeCbor(reader, paramType);
                }

                reader.ReadEndArray();
                return (ICbor?)Activator.CreateInstance(targetType, args);
            }
            else
            {
                if (genericArgs.Length == 1)
                {
                    Type genericArg = genericArgs[0];
                    ICbor? value = DeserializeCbor(reader, genericArg);
                    reader.ReadEndArray();
                    return (ICbor?)Activator.CreateInstance(targetType, value);
                }

                throw new InvalidOperationException($"Unhandled generic type: {genericTypeDef.Name}");
            }
        }
        else if (targetType.GetCustomAttribute<CborSerializableAttribute>()?.Type == CborType.Constr)
        {
            ParameterInfo[] constructorParams = targetType.GetConstructors().First().GetParameters();
            object?[] args = new object?[constructorParams.Length];
            if (constructorParams is not null && constructorParams.Length != 0)
            {
                for (int i = 0; i < constructorParams.Length; i++)
                {
                    Type paramType = constructorParams[i].ParameterType;
                    args[i] = DeserializeCbor(reader, paramType);
                }
                reader.ReadEndArray();
                return (ICbor?)Activator.CreateInstance(targetType, args);
            }
        }

        throw new InvalidOperationException($"Unhandled generic type: {targetType.Name}");
    }

    private static ICbor? DeserializeMap(CborReader reader, Type targetType)
    {
        if (targetType == typeof(CborMap))
        {
            return DeserializeCborMap(reader);
        }
        else if (targetType.IsGenericType)
        {
            MethodInfo? method = typeof(CborSerializer).GetMethod(nameof(DeserializeGenericMap), BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo? genericMethod = method?.MakeGenericMethod(targetType.GetGenericArguments());
            return (ICbor?)genericMethod?.Invoke(null, [reader]);
        }
        else if (targetType.GetCustomAttribute<CborSerializableAttribute>()?.Type == CborType.Map)
        {
            return DeserializeCustomMap(reader, targetType);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported map type for deserialization: {targetType.Name}");
        }
    }

    private static CborMap? DeserializeCborMap(CborReader reader)
    {
        reader.ReadStartMap();
        Dictionary<ICbor, ICbor> dictionary = [];

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            ICbor key = DeserializeCbor(reader, typeof(ICbor))!;
            ICbor value = DeserializeCbor(reader, typeof(ICbor))!;
            dictionary.Add(key, value);
        }

        reader.ReadEndMap();
        return new CborMap(dictionary);
    }

    private static CborMap<TKey, TValue> DeserializeGenericMap<TKey, TValue>(CborReader reader)
        where TKey : ICbor
        where TValue : ICbor
    {
        reader.ReadStartMap();
        Dictionary<TKey, TValue> dictionary = [];

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            TKey key = (TKey)DeserializeCbor(reader, typeof(TKey))!;
            TValue value = (TValue)DeserializeCbor(reader, typeof(TValue))!;
            dictionary.Add(key, value);
        }

        reader.ReadEndMap();
        return new CborMap<TKey, TValue>(dictionary);
    }

    private static ICbor? DeserializeCustomMap(CborReader reader, Type targetType)
    {
        Type? baseType = targetType.BaseType;
        Type[] genericArgs;

        if (baseType != null && baseType.IsGenericType)
        {
            genericArgs = baseType.GetGenericArguments();

            MethodInfo? method = typeof(CborSerializer).GetMethod(nameof(DeserializeGenericMap), BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo genericMethod = method!.MakeGenericMethod(genericArgs);

            ICbor map = (ICbor)genericMethod.Invoke(null, [reader])!;

            ConstructorInfo? ctor = targetType.GetConstructor([typeof(Dictionary<,>).MakeGenericType(genericArgs)]);
            if (ctor != null)
            {
                return (ICbor)ctor.Invoke([map.GetValue(map.GetType())]);
            }
        }
        else
        {
            reader.ReadStartMap();

            Type[] types = targetType.GetProperties()
                       .Select(p => p.PropertyType)
                       .ToArray();
            ConstructorInfo? constructor = targetType.GetConstructor(types);
            ParameterInfo[]? parameters = constructor?.GetParameters();

            if (parameters is null || parameters.Length == 0)
                return null;

            object?[] values = new object?[parameters.Length];

            while (reader.PeekState() != CborReaderState.EndMap)
            {
                object? key = reader.PeekState() == CborReaderState.TextString
                    ? reader.ReadTextString()
                    : reader.ReadInt32();

                for (int i = 0; i < parameters.Length; i++)
                {
                    CborPropertyAttribute? cborPropertyAttribute = parameters[i].GetCustomAttribute<CborPropertyAttribute>();
                    if (cborPropertyAttribute != null)
                    {
                        string? keyString = cborPropertyAttribute.Key;
                        int? keyIndex = cborPropertyAttribute.Index;

                        if ((keyString != null && keyString.Equals(key?.ToString())) ||
                            (keyIndex != null && keyIndex == key as int?))
                        {
                            values[i] = DeserializeCbor(reader, parameters[i].ParameterType);
                            break;
                        }
                    }
                }
            }

            reader.ReadEndMap();
            return (ICbor?)Activator.CreateInstance(targetType, values);
        }

        throw new InvalidOperationException($"No suitable constructor found for type: {targetType.Name}");
    }

    private static ICbor? DeserializeUnion(CborReader reader, Type targetType)
    {
        CborUnionTypesAttribute unionAttr = targetType.GetCustomAttribute<CborUnionTypesAttribute>() ??
            throw new InvalidOperationException($"Type {targetType.Name} is not marked with CborUnionTypesAttribute");

        ReadOnlyMemory<byte> unionByte = reader.ReadEncodedValue();
        CborReader cborReader = new(unionByte);

        foreach (Type type in unionAttr.UnionTypes)
        {
            try
            {
                Type? unionType = type.IsGenericTypeDefinition ? type.MakeGenericType(targetType.GetGenericArguments()) : type;
                return DeserializeCbor(cborReader, unionType);
            }
            catch (Exception ex)
            {
                cborReader.Reset(unionByte);
                continue;
            }
        }

        throw new InvalidOperationException("No matching type found in the union");
    }

    private static ICbor? DeserializeEncodedValue(CborReader reader, Type targetType)
    {
        CborTag tag = reader.ReadTag();

        if (tag == CborTag.EncodedCborDataItem)
        {
            ReadOnlyMemory<byte> encodedReadonlyValue = reader.ReadByteString();
            byte[] value = encodedReadonlyValue.ToArray();
            return (ICbor)Activator.CreateInstance(targetType, value)!;
        }

        throw new InvalidOperationException("Invalid Encoded Value");
    }
}