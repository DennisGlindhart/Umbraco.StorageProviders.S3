//Copied from https://github.com/dotnet/runtime/blob/main/src/libraries/System.Linq/src/System/Linq/Chunk.cs
//This is included in .NET 6.0, but for now we only target 5.0

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Umbraco.StorageProviders.S3.IO
{
    public static partial class Enumerable
    {
        /// <summary>
        /// Split the elements of a sequence into chunks of size at most <paramref name="size"/>.
        /// </summary>
        /// <remarks>
        /// Every chunk except the last will be of size <paramref name="size"/>.
        /// The last chunk will contain the remaining elements and may be of a smaller size.
        /// </remarks>
        /// <param name="source">
        /// An <see cref="IEnumerable{T}"/> whose elements to chunk.
        /// </param>
        /// <param name="size">
        /// Maximum size of each chunk.
        /// </param>
        /// <typeparam name="TSource">
        /// The type of the elements of source.
        /// </typeparam>
        /// <returns>
        /// An <see cref="IEnumerable{T}"/> that contains the elements the input sequence split into chunks of size <paramref name="size"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="source"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="size"/> is below 1.
        /// </exception>
        public static IEnumerable<TSource[]> Chunk<TSource>(this IEnumerable<TSource> source, int size)
        {
            if (source == null)
            {
                throw new ArgumentException();
            }

            if (size < 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            return ChunkIterator(source, size);
        }

        private static IEnumerable<TSource[]> ChunkIterator<TSource>(IEnumerable<TSource> source, int size)
        {
            using IEnumerator<TSource> e = source.GetEnumerator();

            if (e.MoveNext())
            {
                List<TSource> chunkBuilder = new();
                while (true)
                {
                    do
                    {
                        chunkBuilder.Add(e.Current);
                    }
                    while (chunkBuilder.Count < size && e.MoveNext());

                    yield return chunkBuilder.ToArray();

                    if (chunkBuilder.Count < size || !e.MoveNext())
                    {
                        yield break;
                    }
                    chunkBuilder.Clear();
                }
            }
        }
    }
}