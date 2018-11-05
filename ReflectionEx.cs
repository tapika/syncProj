using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

/// <summary>
/// Reflection helper class.
/// </summary>
public static class ReflectionEx
{
    /// <summary>
    /// Gets type of member
    /// </summary>
    public static Type GetUnderlyingType(this MemberInfo member)
    {
        Type type;
        switch (member.MemberType)
        {
            case MemberTypes.Field:
                type = ((FieldInfo)member).FieldType;
                break;
            case MemberTypes.Property:
                type = ((PropertyInfo)member).PropertyType;
                break;
            case MemberTypes.Event:
                type = ((EventInfo)member).EventHandlerType;
                break;
            default:
                throw new ArgumentException("member must be if type FieldInfo, PropertyInfo or EventInfo", "member");
        }
        return Nullable.GetUnderlyingType(type) ?? type;
    }

    /// <summary>
    /// Gets fields and properties into one array.
    /// Order of properties / fields will be preserved in order of appearance in class / struct. (MetadataToken is used for sorting such cases)
    /// </summary>
    /// <param name="type">Type from which to get</param>
    /// <returns>array of fields and properties</returns>
    public static MemberInfo[] GetFieldsAndProperties(this Type type)
    {
        List<MemberInfo> fps = new List<MemberInfo>();
        fps.AddRange(type.GetFields());
        fps.AddRange(type.GetProperties());
        fps = fps.OrderBy(x => x.MetadataToken).ToList();
        return fps.ToArray();
    }

    /// <summary>
    /// Queries value of field or property.
    /// </summary>
    /// <param name="member">member to query from</param>
    /// <param name="target">target object</param>
    /// <returns>member value</returns>
    public static object GetValue(this MemberInfo member, object target)
    {
        if (member is PropertyInfo)
        {
            return (member as PropertyInfo).GetValue(target, null);
        }
        else if (member is FieldInfo)
        {
            return (member as FieldInfo).GetValue(target);
        }
        else
        {
            throw new Exception("member must be either PropertyInfo or FieldInfo");
        }
    }

    /// <summary>
    /// Sets value to target class.
    /// </summary>
    /// <param name="member">member</param>
    /// <param name="target">object</param>
    /// <param name="value">value to set</param>
    public static void SetValue(this MemberInfo member, object target, object value)
    {
        if (member is PropertyInfo)
        {
            (member as PropertyInfo).SetValue(target, value, null);
        }
        else if (member is FieldInfo)
        {
            (member as FieldInfo).SetValue(target, value);
        }
        else
        {
            throw new Exception("destinationMember must be either PropertyInfo or FieldInfo");
        }
    }

    /// <summary>
    /// Deep clones specific object.
    /// Analogue can be found here: https://stackoverflow.com/questions/129389/how-do-you-do-a-deep-copy-an-object-in-net-c-specifically
    /// This is now improved version (list support added)
    /// </summary>
    /// <param name="obj">object to be cloned</param>
    /// <returns>full copy of object.</returns>
    public static object DeepClone(this object obj)
    {
        if (obj == null)
            return null;

        Type type = obj.GetType();

        if (obj is IList)
        {
            IList list = ((IList)obj);
            IList newlist = (IList)Activator.CreateInstance(obj.GetType(), list.Count);

            foreach (object elem in list)
                newlist.Add(DeepClone(elem));

            return newlist;
        } //if

        if (type.IsValueType || type == typeof(string))
        {
            return obj;
        }
        else if (type.IsArray)
        {
            Type elementType = Type.GetType(type.FullName.Replace("[]", string.Empty));
            var array = obj as Array;
            Array copied = Array.CreateInstance(elementType, array.Length);

            for (int i = 0; i < array.Length; i++)
                copied.SetValue(DeepClone(array.GetValue(i)), i);

            return Convert.ChangeType(copied, obj.GetType());
        }
        else if (type.IsClass)
        {
            object toret = Activator.CreateInstance(obj.GetType());

            MemberInfo[] fields = type.GetFieldsAndProperties();
            foreach (MemberInfo field in fields)
            {
                // Don't clone
                if (
                    // Solution / parent reference
                    field.Name == "parent" || 
                    // projects list, no need to clone, as it's copy of solution projects
                    field.Name == "nodes" || 
                    // backreference to solution.
                    field.Name == "solution" )
                {
                    continue;
                }

                // Constant fields, don't care.
                FieldInfo fi = field as FieldInfo;
                if (fi != null && fi.IsLiteral && !fi.IsInitOnly)
                    continue;

                // Properties with only get or only set, not copyable.
                PropertyInfo pi = field as PropertyInfo;
                if( pi != null && (!pi.CanRead || !pi.CanWrite) )
                    continue;

                object fieldValue = field.GetValue(obj);

                if (fieldValue == null)
                    continue;

                field.SetValue(toret, DeepClone(fieldValue));
            }

            return toret;
        }
        else
        {
            // Don't know that type, don't know how to clone it.
            if (Debugger.IsAttached)
                Debugger.Break();

            return null;
        }
    } //DeepClone
}

