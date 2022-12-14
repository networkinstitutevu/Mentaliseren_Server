using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Collections
{
    /// <summary>
    /// Extension methods for sorting various containers.
    /// </summary>
    [BurstCompatible]
    public static class NativeSortExtension
    {
        /// <summary>
        /// A comparer that uses IComparable.CompareTo(). For primitive types, this is an ascending sort.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        [BurstCompatible(GenericTypeArguments = new [] { typeof(int) })]
        public struct DefaultComparer<T> : IComparer<T> where T : IComparable<T>
        {
            /// <summary>
            /// Compares two values.
            /// </summary>
            /// <param name="x">First value to compare.</param>
            /// <param name="y">Second value to compare.</param>
            /// <returns>A signed integer that indicates the relative values of x and y (=0: x == y, &lt;0: x &lt; y, &gt;0: x &gt; y).</returns>
            public int Compare(T x, T y) => x.CompareTo(y);
        }

        /// <summary>
        /// Sorts an array in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="array">Array to perform sort.</param>
        /// <param name="length">Number of elements to perform sort.</param>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static void Sort<T>(T* array, int length) where T : unmanaged, IComparable<T>
        {
            IntroSort<T, DefaultComparer<T>>(array, length, new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts an array using a custom comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="array">Array to perform sort.</param>
        /// <param name="length">Number of elements to perform sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static void Sort<T, U>(T* array, int length, U comp) where T : unmanaged where U : IComparer<T>
        {
            IntroSort<T, U>(array, length, comp);
        }

        /// <summary>
        /// Sorts an array in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="array">Array to perform sort.</param>
        /// <param name="length">Number of elements to perform sort.</param>
        /// <param name="inputDeps">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that sorts
        /// the container.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        [Obsolete("Instead call SortJob(T*, int).Schedule(JobHandle). (RemovedAfter 2021-06-20)", false)]
        public unsafe static JobHandle Sort<T>(T* array, int length, JobHandle inputDeps)
            where T : unmanaged, IComparable<T>
        {
            return Sort(array, length, new DefaultComparer<T>(), inputDeps);
        }

        /// <summary>
        /// Creates a job that will sort an array in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="array">Array to perform sort.</param>
        /// <param name="length">Number of elements to perform sort.</param>
        /// <returns>The job that will sort the container. Scheduling the job is left to the user.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static SortJob<T, DefaultComparer<T>> SortJob<T>(T* array, int length)
            where T : unmanaged, IComparable<T>
        {
            return new SortJob<T, DefaultComparer<T>> {Data = array, Length = length, Comp = new DefaultComparer<T>()};
        }

        /// <summary>
        /// Sorts an array using a custom comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="array">Array to perform sort.</param>
        /// <param name="length">Number of elements to perform sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <param name="inputDeps">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that sorts
        /// the container.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        [Obsolete("Instead call SortJob(T*, int, U).Schedule(JobHandle). (RemovedAfter 2021-06-20)", false)]
        public unsafe static JobHandle Sort<T, U>(T* array, int length, U comp, JobHandle inputDeps)
            where T : unmanaged
            where U : IComparer<T>
        {
            if (length == 0)
            {
                return inputDeps;
            }

            var segmentCount = (length + 1023) / 1024;

            var workerCount = math.max(1, JobsUtility.MaxJobThreadCount);
            var workerSegmentCount = segmentCount / workerCount;
            var segmentSortJob = new SegmentSort<T, U> { Data = array, Comp = comp, Length = length, SegmentWidth = 1024 };
            var segmentSortJobHandle = segmentSortJob.Schedule(segmentCount, workerSegmentCount, inputDeps);
            var segmentSortMergeJob = new SegmentSortMerge<T, U> { Data = array, Comp = comp, Length = length, SegmentWidth = 1024 };
            var segmentSortMergeJobHandle = segmentSortMergeJob.Schedule(segmentSortJobHandle);
            return segmentSortMergeJobHandle;
        }

        /// <summary>
        /// Creates a job that will sort an array using a comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="array">Array to perform sort.</param>
        /// <param name="length">Number of elements to perform sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <returns>The job that will sort the container. Scheduling the job is left to the user.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static SortJob<T, U> SortJob<T, U>(T* array, int length, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return new SortJob<T, U>() {Data = array, Length = length, Comp = comp};
        }

        /// <summary>
        /// Binary search for the value in the sorted container.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="ptr">Array to perform sort.</param>
        /// <param name="length">Number of elements to perform binary search.</param>
        /// <param name="value">The value to search in sorted array.</param>
        /// <returns>Positive index of the specified value if value is found. Otherwise bitwise complement of index of first greater value.</returns>
        /// <remarks>Array must be sorted, otherwise value searched might not be found even when it is in array. IComparer corresponds to IComparer used by sort.</remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static int BinarySearch<T>(T* ptr, int length, T value)
            where T : unmanaged, IComparable<T>
        {
            return BinarySearch(ptr, length, value, new DefaultComparer<T>());
        }

        /// <summary>
        /// Binary search for the value in the sorted array.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="ptr">Array to perform binary search.</param>
        /// <param name="length">Number of elements to perform binary search.</param>
        /// <param name="value">The value to search in sorted array.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <returns>Positive index of the specified value if value is found. Otherwise bitwise complement of index of first greater value.</returns>
        /// <remarks>Array must be sorted, otherwise value searched might not be found even when it is in array. IComparer corresponds to IComparer used by sort.</remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static int BinarySearch<T, U>(T* ptr, int length, T value, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            var offset = 0;

            for (var l = length; l != 0; l >>= 1)
            {
                var idx = offset + (l >> 1);
                var curr = ptr[idx];
                var r = comp.Compare(value, curr);
                if (r == 0)
                {
                    return idx;
                }

                if (r > 0)
                {
                    offset = idx + 1;
                    --l;
                }
            }

            return ~offset;
        }

        /// <summary>
        /// Sorts an array in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="container">Array to perform sort.</param>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static void Sort<T>(this NativeArray<T> container) where T : struct, IComparable<T>
        {
            IntroSort<T, DefaultComparer<T>>(container.GetUnsafePtr(), container.Length, new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts an array using a custom comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="container">Array to perform sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static void Sort<T, U>(this NativeArray<T> container, U comp) where T : struct where U : IComparer<T>
        {
            IntroSort<T, U>(container.GetUnsafePtr(), container.Length, comp);
        }

        /// <summary>
        /// Sorts an array in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="container">Array to perform sort.</param>
        /// <param name="inputDeps">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that sorts
        /// the container.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        [Obsolete("Instead call SortJob(this NativeArray<T>).Schedule(JobHandle). (RemovedAfter 2021-06-20)", false)]
        public unsafe static JobHandle Sort<T>(this NativeArray<T> container, JobHandle inputDeps)
            where T : unmanaged, IComparable<T>
        {
            return Sort((T*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(container), container.Length, new DefaultComparer<T>(), inputDeps);
        }

        /// <summary>
        /// Creates a job that will sort an array in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="container">Array to sort.</param>
        /// <returns>The job that will sort the container. Scheduling the job is left to the user.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static SortJob<T, DefaultComparer<T>> SortJob<T>(this NativeArray<T> container)
            where T : unmanaged, IComparable<T>
        {
            return SortJob((T*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(container), container.Length, new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts an array using a custom comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="container">Array to perform sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <param name="inputDeps">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that sorts
        /// the container.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        [Obsolete("Instead call SortJob(this NativeArray<T>, U).Schedule(JobHandle). (RemovedAfter 2021-06-20)", false)]
        public unsafe static JobHandle Sort<T, U>(this NativeArray<T> container, U comp, JobHandle inputDeps)
            where T : unmanaged
            where U : IComparer<T>
        {
            return Sort((T*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(container), container.Length, comp, inputDeps);
        }

        /// <summary>
        /// Creates a job that will sort an array using a comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="container">Array to perform sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <returns>The job that will sort the container. Scheduling the job is left to the user.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static SortJob<T, U> SortJob<T, U>(this NativeArray<T> container, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return new SortJob<T, U>
            {
                Data = (T*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(container),
                Length = container.Length,
                Comp = comp
            };
        }

        /// <summary>
        /// Binary search for the value in the sorted container.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="container">The container to perform search.</param>
        /// <param name="value">The value to search for.</param>
        /// <returns>Positive index of the specified value if value is found. Otherwise bitwise complement of index of first greater value.</returns>
        /// <remarks>Array must be sorted, otherwise value searched might not be found even when it is in array. IComparer corresponds to IComparer used by sort.</remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public static int BinarySearch<T>(this NativeArray<T> container, T value)
            where T : unmanaged, IComparable<T>
        {
            return container.BinarySearch(value, new DefaultComparer<T>());
        }

        /// <summary>
        /// Binary search for the value in the sorted container.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="container">The container to perform search.</param>
        /// <param name="value">The value to search for.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <returns>Positive index of the specified value if value is found. Otherwise bitwise complement of index of first greater value.</returns>
        /// <remarks>Array must be sorted, otherwise value searched might not be found even when it is in array. IComparer corresponds to IComparer used by sort.</remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static int BinarySearch<T, U>(this NativeArray<T> container, T value, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return BinarySearch((T*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(container), container.Length, value, comp);
        }

        /// <summary>
        /// Sorts a list in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="list">List to perform sort.</param>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static void Sort<T>(this NativeList<T> list) where T : struct, IComparable<T>
        {
            list.Sort(new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts a list using a custom comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="list">List to perform sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static void Sort<T, U>(this NativeList<T> list, U comp) where T : struct where U : IComparer<T>
        {
            IntroSort<T, U>(list.GetUnsafePtr(), list.Length, comp);
        }

        /// <summary>
        /// Sorts the container in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="container">The container to perform sort.</param>
        /// <param name="inputDeps">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that sorts
        /// the container.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        [Obsolete("Instead call SortJob(this NativeList<T>).Schedule(JobHandle). (RemovedAfter 2021-06-20)", false)]
        public unsafe static JobHandle Sort<T>(this NativeList<T> container, JobHandle inputDeps)
            where T : unmanaged, IComparable<T>
        {
            return container.Sort(new DefaultComparer<T>(), inputDeps);
        }

        /// <summary>
        /// Creates a job that will sort an array in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="list">List to sort.</param>
        /// <returns>The job that will sort the container. Scheduling the job is left to the user.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static SortJob<T, DefaultComparer<T>> SortJob<T>(this NativeList<T> list)
            where T : unmanaged, IComparable<T>
        {
            return SortJob((T*)list.GetUnsafePtr(), list.Length,new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts the container using a custom comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="list">The container to perform sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <param name="inputDeps">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that sorts
        /// the container.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        [Obsolete("Instead call SortJob(this NativeList<T>, U).Schedule(JobHandle). (RemovedAfter 2021-06-20)", false)]
        public unsafe static JobHandle Sort<T, U>(this NativeList<T> list, U comp, JobHandle inputDeps)
            where T : unmanaged
            where U : IComparer<T>
        {
            return Sort((T*)list.GetUnsafePtr(), list.Length, comp, inputDeps);
        }

        /// <summary>
        /// Creates a job that will sort a list using a comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="container">List to sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <returns>The job that will sort the container. Scheduling the job is left to the user.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static SortJob<T, U> SortJob<T, U>(this NativeList<T> container, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return SortJob((T*)container.GetUnsafePtr(), container.Length, comp);
        }

        /// <summary>
        /// Binary search for the value in the sorted container.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="container">The container to perform search.</param>
        /// <param name="value">The value to search for.</param>
        /// <returns>Positive index of the specified value if value is found. Otherwise bitwise complement of index of first greater value.</returns>
        /// <remarks>Array must be sorted, otherwise value searched might not be found even when it is in array. IComparer corresponds to IComparer used by sort.</remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public static int BinarySearch<T>(this NativeList<T> container, T value)
            where T : unmanaged, IComparable<T>
        {
            return container.BinarySearch(value, new DefaultComparer<T>());
        }

        /// <summary>
        /// Binary search for the value in the sorted container.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="container">The container to perform search.</param>
        /// <param name="value">The value to search for.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <returns>Positive index of the specified value if value is found. Otherwise bitwise complement of index of first greater value.</returns>
        /// <remarks>Array must be sorted, otherwise value searched might not be found even when it is in array. IComparer corresponds to IComparer used by sort.</remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static int BinarySearch<T, U>(this NativeList<T> container, T value, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return BinarySearch((T*)container.GetUnsafePtr(), container.Length, value, comp);
        }

        /// <summary>
        /// Sorts a list in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="list">List to perform sort.</param>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static void Sort<T>(this UnsafeList list) where T : struct, IComparable<T>
        {
            list.Sort<T, DefaultComparer<T>>(new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts a list using a custom comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="list">List to perform sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static void Sort<T, U>(this UnsafeList list, U comp) where T : struct where U : IComparer<T>
        {
            IntroSort<T, U>(list.Ptr, list.Length, comp);
        }

        /// <summary>
        /// Sorts the container in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="container">The container to perform sort.</param>
        /// <param name="inputDeps">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that sorts
        /// the container.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        [Obsolete("Instead call SortJob(this UnsafeList).Schedule(JobHandle). (RemovedAfter 2021-06-20)", false)]
        public unsafe static JobHandle Sort<T>(this UnsafeList container, JobHandle inputDeps)
            where T : unmanaged, IComparable<T>
        {
            return container.Sort<T, DefaultComparer<T>>(new DefaultComparer<T>(), inputDeps);
        }

        /// <summary>
        /// Creates a job that will sort a list in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="list">List to sort.</param>
        /// <returns>The job that will sort the list. Scheduling the job is left to the user.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static SortJob<T, DefaultComparer<T>> SortJob<T>(this UnsafeList list)
            where T : unmanaged, IComparable<T>
        {
            return SortJob((T*)list.Ptr, list.Length, new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts the container using a custom comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="container">The container to perform sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <param name="inputDeps">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that sorts
        /// the container.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        [Obsolete("Instead call SortJob(this UnsafeList, U).Schedule(JobHandle). (RemovedAfter 2021-06-20)", false)]
        public unsafe static JobHandle Sort<T, U>(this UnsafeList container, U comp, JobHandle inputDeps)
            where T : unmanaged
            where U : IComparer<T>
        {
            return Sort((T*)container.Ptr, container.Length, comp, inputDeps);
        }

        /// <summary>
        /// Creates a job that will sort a list using a comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="list">List to sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <returns>The job that will sort the list. Scheduling the job is left to the user.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static SortJob<T, U> SortJob<T, U>(this UnsafeList list, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return SortJob((T*)list.Ptr, list.Length, comp);
        }

        /// <summary>
        /// Binary search for the value in the sorted container.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="container">The container to perform search.</param>
        /// <param name="value">The value to search for.</param>
        /// <returns>Positive index of the specified value if value is found. Otherwise bitwise complement of index of first greater value.</returns>
        /// <remarks>Array must be sorted, otherwise value searched might not be found even when it is in array. IComparer corresponds to IComparer used by sort.</remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public static int BinarySearch<T>(this UnsafeList container, T value)
            where T : unmanaged, IComparable<T>
        {
            return container.BinarySearch(value, new DefaultComparer<T>());
        }

        /// <summary>
        /// Binary search for the value in the sorted container.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="container">The container to perform search.</param>
        /// <param name="value">The value to search for.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <returns>Positive index of the specified value if value is found. Otherwise bitwise complement of index of first greater value.</returns>
        /// <remarks>Array must be sorted, otherwise value searched might not be found even when it is in array. IComparer corresponds to IComparer used by sort.</remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static int BinarySearch<T, U>(this UnsafeList container, T value, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return BinarySearch((T*)container.Ptr, container.Length, value, comp);
        }

        /// <summary>
        /// Sorts a list in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="list">List to perform sort.</param>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static void Sort<T>(this UnsafeList<T> list) where T : unmanaged, IComparable<T>
        {
            list.Sort(new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts a list using a custom comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="list">List to perform sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static void Sort<T, U>(this UnsafeList<T> list, U comp) where T : unmanaged where U : IComparer<T>
        {
            IntroSort<T, U>(list.Ptr, list.Length, comp);
        }

        /// <summary>
        /// Sorts the container in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="container">The container to perform sort.</param>
        /// <param name="inputDeps">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that sorts
        /// the container.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        [Obsolete("Instead call SortJob(this UnsafeList<T>).Schedule(JobHandle). (RemovedAfter 2021-06-20)", false)]
        public unsafe static JobHandle Sort<T>(this UnsafeList<T> container, JobHandle inputDeps)
            where T : unmanaged, IComparable<T>
        {
            return container.Sort(new DefaultComparer<T>(), inputDeps);
        }

        /// <summary>
        /// Creates a job that will sort a list using a comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="list">List to sort.</param>
        /// <returns>The job that will sort the list. Scheduling the job is left to the user.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static SortJob<T, DefaultComparer<T>> SortJob<T>(this UnsafeList<T> list)
            where T : unmanaged, IComparable<T>
        {
            return SortJob((T*)list.Ptr, list.Length, new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts the container using a custom comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="container">The container to perform sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <param name="inputDeps">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that sorts
        /// the container.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        [Obsolete("Instead call SortJob(this UnsafeList<T>, U).Schedule(JobHandle). (RemovedAfter 2021-06-20)", false)]
        public unsafe static JobHandle Sort<T, U>(this UnsafeList<T> container, U comp, JobHandle inputDeps)
            where T : unmanaged
            where U : IComparer<T>
        {
            return Sort(container.Ptr, container.Length, comp, inputDeps);
        }

        /// <summary>
        /// Creates a job that will sort a list using a comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="list">List to sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <returns>The job that will sort the list. Scheduling the job is left to the user.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static SortJob<T, U> SortJob<T, U>(this UnsafeList<T> list, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return SortJob(list.Ptr, list.Length, comp);
        }

        /// <summary>
        /// Binary search for the value in the sorted container.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="container">The container to perform search.</param>
        /// <param name="value">The value to search for.</param>
        /// <returns>Positive index of the specified value if value is found. Otherwise bitwise complement of index of first greater value.</returns>
        /// <remarks>Array must be sorted, otherwise value searched might not be found even when it is in array. IComparer corresponds to IComparer used by sort.</remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public static int BinarySearch<T>(this UnsafeList<T> container, T value)
            where T : unmanaged, IComparable<T>
        {
            return container.BinarySearch(value, new DefaultComparer<T>());
        }

        /// <summary>
        /// Binary search for the value in the sorted container.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="container">The container to perform search.</param>
        /// <param name="value">The value to search for.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <returns>Positive index of the specified value if value is found. Otherwise bitwise complement of index of first greater value.</returns>
        /// <remarks>Array must be sorted, otherwise value searched might not be found even when it is in array. IComparer corresponds to IComparer used by sort.</remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static int BinarySearch<T, U>(this UnsafeList<T> container, T value, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return BinarySearch(container.Ptr, container.Length, value, comp);
        }

        /// <summary>
        /// Sorts a slice in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="slice">Slice to perform sort.</param>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static void Sort<T>(this NativeSlice<T> slice) where T : struct, IComparable<T>
        {
            slice.Sort(new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts a slice using a custom comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="slice">List to perform sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static void Sort<T, U>(this NativeSlice<T> slice, U comp) where T : struct where U : IComparer<T>
        {
            CheckStrideMatchesSize<T>(slice.Stride);
            IntroSort<T, U>(slice.GetUnsafePtr(), slice.Length, comp);
        }

        /// <summary>
        /// Sorts the container in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="container">The container to perform sort.</param>
        /// <param name="inputDeps">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that sorts
        /// the container.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        [Obsolete("Instead call SortJob(this NativeSlice<T>).Schedule(JobHandle). (RemovedAfter 2021-06-20)", false)]
        public unsafe static JobHandle Sort<T>(this NativeSlice<T> container, JobHandle inputDeps)
            where T : unmanaged, IComparable<T>
        {
            return container.Sort(new DefaultComparer<T>(), inputDeps);
        }

        /// <summary>
        /// Creates a job that will sort a slice in ascending order.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="slice">Slice to sort.</param>
        /// <returns>The job that will sort the slice. Scheduling the job is left to the user.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public unsafe static SortJob<T, DefaultComparer<T>> SortJob<T>(this NativeSlice<T> slice)
            where T : unmanaged, IComparable<T>
        {
            CheckStrideMatchesSize<T>(slice.Stride);  // TODO would we want this check to be done in the job itself? Is this necessary at all?
            return SortJob((T*)slice.GetUnsafePtr(), slice.Length, new DefaultComparer<T>());
        }

        /// <summary>
        /// Sorts the container using a custom comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="container">The container to perform sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <param name="inputDeps">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns>A new job handle containing the prior handles as well as the handle for the job that sorts
        /// the container.</returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        [Obsolete("Instead call SortJob(this NativeSlice<T>, U).Schedule(JobHandle). (RemovedAfter 2021-06-20)", false)]
        public unsafe static JobHandle Sort<T, U>(this NativeSlice<T> container, U comp, JobHandle inputDeps)
            where T : unmanaged
            where U : IComparer<T>
        {
            return Sort((T*)container.GetUnsafePtr(), container.Length, comp, inputDeps);
        }

        /// <summary>
        /// Creates a job that will sort a slice using a comparison function.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="slice">Slice to sort.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <returns>The job that will sort the slice. Scheduling the job is left to the user.</returns>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static SortJob<T, U> SortJob<T, U>(this NativeSlice<T> slice, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            CheckStrideMatchesSize<T>(slice.Stride);  // TODO would we want this check to be done in the job itself?
            return SortJob((T*)slice.GetUnsafePtr(), slice.Length, comp);
        }

        /// <summary>
        /// Binary search for the value in the sorted container.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <param name="container">The container to perform search.</param>
        /// <param name="value">The value to search for.</param>
        /// <returns>Positive index of the specified value if value is found. Otherwise bitwise complement of index of first greater value.</returns>
        /// <remarks>Array must be sorted, otherwise value searched might not be found even when it is in array. IComparer corresponds to IComparer used by sort.</remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int) })]
        public static int BinarySearch<T>(this NativeSlice<T> container, T value)
            where T : unmanaged, IComparable<T>
        {
            return container.BinarySearch(value, new DefaultComparer<T>());
        }

        /// <summary>
        /// Binary search for the value in the sorted container.
        /// </summary>
        /// <typeparam name="T">Source type of elements</typeparam>
        /// <typeparam name="U">The comparer type.</typeparam>
        /// <param name="container">The container to perform search.</param>
        /// <param name="value">The value to search for.</param>
        /// <param name="comp">A comparison function that indicates whether one element in the array is less than, equal to, or greater than another element.</param>
        /// <returns>Positive index of the specified value if value is found. Otherwise bitwise complement of index of first greater value.</returns>
        /// <remarks>Array must be sorted, otherwise value searched might not be found even when it is in array. IComparer corresponds to IComparer used by sort.</remarks>
        [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(DefaultComparer<int>) })]
        public unsafe static int BinarySearch<T, U>(this NativeSlice<T> container, T value, U comp)
            where T : unmanaged
            where U : IComparer<T>
        {
            return BinarySearch((T*)container.GetUnsafePtr(), container.Length, value, comp);
        }

        /// -- Internals

        unsafe static void IntroSort<T, U>(void* array, int length, U comp) where T : struct where U : IComparer<T>
        {
            IntroSort<T, U>(array, 0, length - 1, 2 * CollectionHelper.Log2Floor(length), comp);
        }

        const int k_IntrosortSizeThreshold = 16;
        unsafe static void IntroSort<T, U>(void* array, int lo, int hi, int depth, U comp) where T : struct where U : IComparer<T>
        {
            while (hi > lo)
            {
                int partitionSize = hi - lo + 1;
                if (partitionSize <= k_IntrosortSizeThreshold)
                {
                    if (partitionSize == 1)
                    {
                        return;
                    }
                    if (partitionSize == 2)
                    {
                        SwapIfGreaterWithItems<T, U>(array, lo, hi, comp);
                        return;
                    }
                    if (partitionSize == 3)
                    {
                        SwapIfGreaterWithItems<T, U>(array, lo, hi - 1, comp);
                        SwapIfGreaterWithItems<T, U>(array, lo, hi, comp);
                        SwapIfGreaterWithItems<T, U>(array, hi - 1, hi, comp);
                        return;
                    }

                    InsertionSort<T, U>(array, lo, hi, comp);
                    return;
                }

                if (depth == 0)
                {
                    HeapSort<T, U>(array, lo, hi, comp);
                    return;
                }
                depth--;

                int p = Partition<T, U>(array, lo, hi, comp);
                IntroSort<T, U>(array, p + 1, hi, depth, comp);
                hi = p - 1;
            }
        }

        unsafe static void InsertionSort<T, U>(void* array, int lo, int hi, U comp) where T : struct where U : IComparer<T>
        {
            int i, j;
            T t;
            for (i = lo; i < hi; i++)
            {
                j = i;
                t = UnsafeUtility.ReadArrayElement<T>(array, i + 1);
                while (j >= lo && comp.Compare(t, UnsafeUtility.ReadArrayElement<T>(array, j)) < 0)
                {
                    UnsafeUtility.WriteArrayElement<T>(array, j + 1, UnsafeUtility.ReadArrayElement<T>(array, j));
                    j--;
                }
                UnsafeUtility.WriteArrayElement<T>(array, j + 1, t);
            }
        }

        unsafe static int Partition<T, U>(void* array, int lo, int hi, U comp) where T : struct where U : IComparer<T>
        {
            int mid = lo + ((hi - lo) / 2);
            SwapIfGreaterWithItems<T, U>(array, lo, mid, comp);
            SwapIfGreaterWithItems<T, U>(array, lo, hi, comp);
            SwapIfGreaterWithItems<T, U>(array, mid, hi, comp);

            T pivot = UnsafeUtility.ReadArrayElement<T>(array, mid);
            Swap<T>(array, mid, hi - 1);
            int left = lo, right = hi - 1;

            while (left < right)
            {
                while (comp.Compare(pivot, UnsafeUtility.ReadArrayElement<T>(array, ++left)) > 0) ;
                while (comp.Compare(pivot, UnsafeUtility.ReadArrayElement<T>(array, --right)) < 0) ;

                if (left >= right)
                    break;

                Swap<T>(array, left, right);
            }

            Swap<T>(array, left, (hi - 1));
            return left;
        }

        unsafe static void HeapSort<T, U>(void* array, int lo, int hi, U comp) where T : struct where U : IComparer<T>
        {
            int n = hi - lo + 1;

            for (int i = n / 2; i >= 1; i--)
            {
                Heapify<T, U>(array, i, n, lo, comp);
            }

            for (int i = n; i > 1; i--)
            {
                Swap<T>(array, lo, lo + i - 1);
                Heapify<T, U>(array, 1, i - 1, lo, comp);
            }
        }

        unsafe static void Heapify<T, U>(void* array, int i, int n, int lo, U comp) where T : struct where U : IComparer<T>
        {
            T val = UnsafeUtility.ReadArrayElement<T>(array, lo + i - 1);
            int child;
            while (i <= n / 2)
            {
                child = 2 * i;
                if (child < n && (comp.Compare(UnsafeUtility.ReadArrayElement<T>(array, lo + child - 1), UnsafeUtility.ReadArrayElement<T>(array, (lo + child))) < 0))
                {
                    child++;
                }
                if (comp.Compare(UnsafeUtility.ReadArrayElement<T>(array, (lo + child - 1)), val) < 0)
                    break;

                UnsafeUtility.WriteArrayElement(array, lo + i - 1, UnsafeUtility.ReadArrayElement<T>(array, lo + child - 1));
                i = child;
            }
            UnsafeUtility.WriteArrayElement(array, lo + i - 1, val);
        }

        unsafe static void Swap<T>(void* array, int lhs, int rhs) where T : struct
        {
            T val = UnsafeUtility.ReadArrayElement<T>(array, lhs);
            UnsafeUtility.WriteArrayElement(array, lhs, UnsafeUtility.ReadArrayElement<T>(array, rhs));
            UnsafeUtility.WriteArrayElement(array, rhs, val);
        }

        unsafe static void SwapIfGreaterWithItems<T, U>(void* array, int lhs, int rhs, U comp) where T : struct where U : IComparer<T>
        {
            if (lhs != rhs)
            {
                if (comp.Compare(UnsafeUtility.ReadArrayElement<T>(array, lhs), UnsafeUtility.ReadArrayElement<T>(array, rhs)) > 0)
                {
                    Swap<T>(array, lhs, rhs);
                }
            }
        }

        [BurstCompile]
        unsafe struct SegmentSort<T, U> : IJobParallelFor
            where T : unmanaged
            where U : IComparer<T>
        {
            [NativeDisableUnsafePtrRestriction]
            public T* Data;
            public U Comp;

            public int Length;
            public int SegmentWidth;

            public void Execute(int index)
            {
                var startIndex = index * SegmentWidth;
                var segmentLength = ((Length - startIndex) < SegmentWidth) ? (Length - startIndex) : SegmentWidth;
                Sort(Data + startIndex, segmentLength, Comp);
            }
        }

        [BurstCompile]
        unsafe struct SegmentSortMerge<T, U> : IJob
            where T : unmanaged
            where U : IComparer<T>
        {
            [NativeDisableUnsafePtrRestriction]
            public T* Data;
            public U Comp;

            public int Length;
            public int SegmentWidth;

            public void Execute()
            {
                var segmentCount = (Length + (SegmentWidth - 1)) / SegmentWidth;
                var segmentIndex = stackalloc int[segmentCount];

                var resultCopy = (T*)Memory.Unmanaged.Allocate(UnsafeUtility.SizeOf<T>() * Length, 16, Allocator.Temp);

                for (int sortIndex = 0; sortIndex < Length; sortIndex++)
                {
                    // find next best
                    int bestSegmentIndex = -1;
                    T bestValue = default(T);

                    for (int i = 0; i < segmentCount; i++)
                    {
                        var startIndex = i * SegmentWidth;
                        var offset = segmentIndex[i];
                        var segmentLength = ((Length - startIndex) < SegmentWidth) ? (Length - startIndex) : SegmentWidth;
                        if (offset == segmentLength)
                            continue;

                        var nextValue = Data[startIndex + offset];
                        if (bestSegmentIndex != -1)
                        {
                            if (Comp.Compare(nextValue, bestValue) > 0)
                                continue;
                        }

                        bestValue = nextValue;
                        bestSegmentIndex = i;
                    }

                    segmentIndex[bestSegmentIndex]++;
                    resultCopy[sortIndex] = bestValue;
                }

                UnsafeUtility.MemCpy(Data, resultCopy, UnsafeUtility.SizeOf<T>() * Length);
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckStrideMatchesSize<T>(int stride) where T : struct
        {
            if (stride != UnsafeUtility.SizeOf<T>())
            {
                throw new InvalidOperationException("Sort requires that stride matches the size of the source type");
            }
        }
    }

    /// <summary>
    /// Returned by the NativeSortExtension.SortJob methods. Call Schedule() to schedule the sorting.
    /// </summary>
    /// <typeparam name="T">Source type of elements</typeparam>
    /// <typeparam name="U">The comparer type.</typeparam>
    [BurstCompatible(GenericTypeArguments = new[] { typeof(int), typeof(NativeSortExtension.DefaultComparer<int>) })]
    public unsafe struct SortJob<T, U> where T : unmanaged where U : IComparer<T>
    {
        /// <summary>
        /// Data.
        /// </summary>
        public T* Data;

        /// <summary>
        /// Comparer.
        /// </summary>
        public U Comp;

        /// <summary>
        /// Length.
        /// </summary>
        public int Length;

        [BurstCompile]
        struct SegmentSort : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public T* Data;
            public U Comp;

            public int Length;
            public int SegmentWidth;

            public void Execute(int index)
            {
                var startIndex = index * SegmentWidth;
                var segmentLength = ((Length - startIndex) < SegmentWidth) ? (Length - startIndex) : SegmentWidth;
                NativeSortExtension.Sort(Data + startIndex, segmentLength, Comp);
            }
        }

        [BurstCompile]
        struct SegmentSortMerge : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public T* Data;
            public U Comp;

            public int Length;
            public int SegmentWidth;

            public void Execute()
            {
                var segmentCount = (Length + (SegmentWidth - 1)) / SegmentWidth;
                var segmentIndex = stackalloc int[segmentCount];

                var resultCopy = (T*)Memory.Unmanaged.Allocate(UnsafeUtility.SizeOf<T>() * Length, 16, Allocator.Temp);

                for (int sortIndex = 0; sortIndex < Length; sortIndex++)
                {
                    // find next best
                    int bestSegmentIndex = -1;
                    T bestValue = default(T);

                    for (int i = 0; i < segmentCount; i++)
                    {
                        var startIndex = i * SegmentWidth;
                        var offset = segmentIndex[i];
                        var segmentLength = ((Length - startIndex) < SegmentWidth) ? (Length - startIndex) : SegmentWidth;
                        if (offset == segmentLength)
                            continue;

                        var nextValue = Data[startIndex + offset];
                        if (bestSegmentIndex != -1)
                        {
                            if (Comp.Compare(nextValue, bestValue) > 0)
                                continue;
                        }

                        bestValue = nextValue;
                        bestSegmentIndex = i;
                    }

                    segmentIndex[bestSegmentIndex]++;
                    resultCopy[sortIndex] = bestValue;
                }

                UnsafeUtility.MemCpy(Data, resultCopy, UnsafeUtility.SizeOf<T>() * Length);
            }
        }

        /// <summary>
        /// Schedule jobs to sort values.
        /// </summary>
        /// <param name="inputDeps">The job handle or handles for any scheduled jobs that use this container.</param>
        /// <returns></returns>
        [NotBurstCompatible /* This is not burst compatible because of IJob's use of a static IntPtr. Should switch to IJobBurstSchedulable in the future */]
        public JobHandle Schedule(JobHandle inputDeps = new JobHandle())
        {
            if (Length == 0)
                return inputDeps;
            var segmentCount = (Length + 1023) / 1024;
            var workerCount = math.max(1, JobsUtility.MaxJobThreadCount);
            var workerSegmentCount = segmentCount / workerCount;
            var segmentSortJob = new SegmentSort { Data = Data, Comp = Comp, Length = Length, SegmentWidth = 1024 };
            var segmentSortJobHandle = segmentSortJob.Schedule(segmentCount, workerSegmentCount, inputDeps);
            var segmentSortMergeJob = new SegmentSortMerge { Data = Data, Comp = Comp, Length = Length, SegmentWidth = 1024 };
            var segmentSortMergeJobHandle = segmentSortMergeJob.Schedule(segmentSortJobHandle);
            return segmentSortMergeJobHandle;
        }
    }
}
