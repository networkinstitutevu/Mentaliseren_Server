using System;

namespace Unity.Collections.NotBurstCompatible
{
    /// <summary>
    /// <undoc />
    /// </summary>
    public static class HashSetExtensions
    {
        /// <summary>
        /// Returns managed array populated with elements from the container.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="container">The container to perform conversion to array.</param>
        /// <returns>Array of elements of the container.</returns>
        public static T[] ToArray<T>(this NativeHashSet<T> container)
            where T : unmanaged, IEquatable<T>
        {
            var array = container.ToNativeArray(Allocator.TempJob);
            var managed = array.ToArray();
            array.Dispose();
            return managed;
        }
    }
}
