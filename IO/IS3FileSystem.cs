using Amazon.S3;
using Umbraco.Cms.Core.IO;

namespace Umbraco.StorageProviders.S3.IO
{
    /// <summary>
    /// The S3 File System.
    /// </summary>
    public interface IS3FileSystem : IFileSystem
    {
        /// <summary>
        /// Get the <see cref="BlobClient"/>.
        /// </summary>
        /// <param name="path">The relative path to the blob.</param>
        /// <returns>A <see cref="BlobClient"/></returns>
        AmazonS3Client GetS3Client();
    }
}
