using System.ComponentModel.DataAnnotations;
using Amazon.S3;

namespace Umbraco.StorageProviders.S3.IO
{
    /// <summary>
    /// The S3 File System Options.
    /// </summary>
    public class S3FileSystemOptions
    {
        /// <summary>
        /// The media filesystem name.
        /// </summary>
        public const string MediaFileSystemName = "Media";

        [Required]
        public string VirtualPath { get; set; } = null!;
        
        [Required]
        /// <summary>
        /// Bucket name
        /// </summary>
        public string BucketName { get; set; }

        
        /// <summary>
        /// The hostname for AWS S3-bucket. Use ServiceUrl instead for path-style/Minio
        // eg. xxx.s3.us-east-1.amazonaws.com
        /// </summary>
        public string BucketHostName { get; set; } = string.Empty;

        /// <summary>
        /// URL to the Minio S3 storage if not using AWS S3. Use BucketHostName instead for AWS S3
        /// eg. https://myminioinstance.mydomain.com:9000
        /// </summary>
        public string ServiceUrl { get; set; } = null!;

        /// <summary>
        /// Use Path-style S3. Should be true for Minio
        /// </summary>
        public bool ForcePathStyle { get; set; } = false;

        /// <summary>
        /// Prefix of files in bucket
        /// </summary>
        public string BucketPrefix { get; set; }

        [Required]
        /// <summary>
        /// AWS Region
        /// </summary>
        public string Region { get; set; }
        [Required]
        /// <summary>
        /// AWS AccessKey
        /// </summary>
        public string AccessKey { get; set; }
        [Required]
        /// <summary>
        /// AWS SecretKye
        /// </summary>
        public string SecretKey { get; set; }
    }
}
