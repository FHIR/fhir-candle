
using System.Runtime.CompilerServices;

namespace FhirCandle.Extensions;

internal static class DictionaryExtensions
{
    /// <summary>
    /// A Dictionary&lt;KT,VT&gt; extension method that deep copies the dictionary.
    /// </summary>
    /// <typeparam name="KT">Key Type.</typeparam>
    /// <typeparam name="VT">Value Type.</typeparam>
    /// <param name="source">The source dictionary to copy.</param>
    /// <returns>A Dictionary&lt;KT,VT&gt;</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<KT, VT> DeepCopy<KT, VT>(this Dictionary<KT, VT> source)
        where KT : notnull
        where VT : ICloneable
    {
        Dictionary<KT, VT> dest = [];

        foreach (KeyValuePair<KT, VT> kvp in source)
        {
            dest.Add(kvp.Key, (VT)kvp.Value.Clone());
        }

        return dest;
    }

    /// <summary>
    /// A Dictionary&lt;KT,VT&gt; extension method that deep copies the dictionary.
    /// </summary>
    /// <typeparam name="KT">Key Type.</typeparam>
    /// <typeparam name="VT">Value Type.</typeparam>
    /// <param name="source">The source dictionary to copy.</param>
    /// <returns>A Dictionary&lt;KT,VT&gt;</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<KT, List<VT>> DeepCopy<KT, VT>(this Dictionary<KT, List<VT>> source)
        where KT : notnull
        where VT : ICloneable
    {
        Dictionary<KT, List<VT>> dest = [];

        foreach (KeyValuePair<KT, List<VT>> kvp in source)
        {
            List<VT> list = [];

            foreach (VT value in kvp.Value)
            {
                list.Add((VT)value.Clone());
            }

            dest.Add(kvp.Key, list);
        }

        return dest;
    }


    /// <summary>
    /// A Dictionary&lt;KT,VT&gt; extension method that deep copies the dictionary.
    /// </summary>
    /// <param name="source">The source dictionary to copy.</param>
    /// <returns>A Dictionary&lt;KT,VT&gt;</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<string, List<object>> DeepCopy(this Dictionary<string, List<object>> source)
    {
        Dictionary<string, List<object>> dest = [];

        foreach (KeyValuePair<string, List<object>> kvp in source)
        {
            List<object> list = [];

            foreach (object value in kvp.Value)
            {
                switch (value)
                {
                    case ICloneable cloneable: list.Add(cloneable.Clone()); break;
                    default: list.Add(value); break;
                }
            }

            dest.Add(kvp.Key, list);
        }

        return dest;
    }


    /// <summary>
    /// A Dictionary&lt;KT,VT&gt; extension method that shallow copies the given source.
    /// </summary>
    /// <typeparam name="KT">Key Type.</typeparam>
    /// <typeparam name="VT">Value Type.</typeparam>
    /// <param name="source">The source dictionary to copy.</param>
    /// <returns>A Dictionary&lt;KT,VT&gt;</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<KT, VT> ShallowCopy<KT, VT>(this Dictionary<KT, VT> source)
        where KT : notnull
    {
        Dictionary<KT, VT> dest = [];

        foreach (KeyValuePair<KT, VT> kvp in source)
        {
            dest.Add(kvp.Key, kvp.Value);
        }

        return dest;
    }

    /// <summary>
    /// A Dictionary&lt;KT,VT&gt; extension method that shallow copies the given source.
    /// </summary>
    /// <typeparam name="KT">Type of the kt.</typeparam>
    /// <typeparam name="VT">Type of the vt.</typeparam>
    /// <param name="source">The source dictionary to copy.</param>
    /// <returns>A Dictionary&lt;KT,VT&gt;</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<KT, List<VT>> ShallowCopy<KT, VT>(this Dictionary<KT, List<VT>> source)
        where KT : notnull
    {
        Dictionary<KT, List<VT>> dest = [];

        foreach (KeyValuePair<KT, List<VT>> kvp in source)
        {
            dest.Add(kvp.Key, kvp.Value);
        }

        return dest;
    }
}
