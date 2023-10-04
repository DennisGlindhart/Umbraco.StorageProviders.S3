using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.IO;
using Umbraco.Extensions;

namespace Umbraco.StorageProviders.S3.IO
{
    /// <inheritdoc />
    public class S3FileSystem : IS3FileSystem
    {
        private readonly Amazon.S3.AmazonS3Client client;
        readonly S3FileSystemOptions Config;
        private readonly IContentTypeProvider _contentTypeProvider;
        private readonly IIOHelper _ioHelper;
        private readonly string _rootUrl;

        /// <summary>
        ///     Creates a new instance of <see cref="S3FileSystem" />.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="hostingEnvironment"></param>
        /// <param name="ioHelper"></param>
        /// <param name="contentTypeProvider"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public S3FileSystem(S3FileSystemOptions options, IHostingEnvironment hostingEnvironment,
            IIOHelper ioHelper, IContentTypeProvider contentTypeProvider)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (hostingEnvironment == null) throw new ArgumentNullException(nameof(hostingEnvironment));

            Config = options;

            _ioHelper = ioHelper ?? throw new ArgumentNullException(nameof(ioHelper));
            _contentTypeProvider = contentTypeProvider ?? throw new ArgumentNullException(nameof(contentTypeProvider));

            _rootUrl = EnsureUrlSeparatorChar(hostingEnvironment.ToAbsolute(options.VirtualPath)).TrimEnd('/');
        }

        /// <inheritdoc />
        public IEnumerable<string> GetDirectories(string path)
        {
            if (string.IsNullOrEmpty(path))
                path = "/";

            path = ResolveS3Path(path, true);

            var request = new ListObjectsRequest
            {
                BucketName = Config.BucketName,
                Delimiter = Delimiter,
                Prefix = path
            };

            var response = ExecuteWithContinuation(request);

            return response
                .SelectMany(p => p.CommonPrefixes)
                .Select(p => RemovePrefix(p))
                .ToArray();

        }

        /// <inheritdoc />
        public void DeleteDirectory(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            DeleteDirectory(path, true);
        }

        /// <inheritdoc />
        public void DeleteDirectory(string path, bool recursive)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            //List Objects To Delete
            var listRequest = new ListObjectsRequest
            {
                BucketName = Config.BucketName,
                Prefix = ResolveS3Path(path, true)
            };

            var listResponse = ExecuteWithContinuation(listRequest);
            var keys = listResponse
                .SelectMany(p => p.S3Objects)
                .Select(p => new KeyVersion { Key = p.Key })
                .ToArray();

            //Batch Deletion Requests
            foreach (var items in keys.Chunk(100))
            {
                var deleteRequest = new DeleteObjectsRequest
                {
                    BucketName = Config.BucketName,
                    Objects = items.ToList()
                };
                ExecuteAsync(client => GetS3Client().DeleteObjectsAsync(deleteRequest));
            }
        }

        /// <inheritdoc />
        public bool DirectoryExists(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var request = new ListObjectsRequest
            {
                BucketName = Config.BucketName,
                Prefix = ResolveS3Path(path, true),
                MaxKeys = 1
            };

            var response = ExecuteAsync(client => GetS3Client().ListObjectsAsync(request));
            return response.S3Objects.Any();
        }

        /// <inheritdoc />
        public void AddFile(string path, Stream stream)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            AddFile(path, stream, true);
        }

        /// <inheritdoc />
        public void AddFile(string path, Stream stream, bool overrideIfExists)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                var request = new PutObjectRequest
                {
                    BucketName = Config.BucketName,
                    Key = ResolveS3Path(path),
                    CannedACL = new S3CannedACL("public-read"),
                    InputStream = memoryStream,
                    ServerSideEncryptionMethod = ServerSideEncryptionMethod.None
                };

                if (_contentTypeProvider.TryGetContentType(path, out string contentType)) request.ContentType = contentType;

                var response = ExecuteAsync(client => GetS3Client().PutObjectAsync(request));
            }
        }

        /// <inheritdoc />
        public void AddFile(string path, string physicalPath, bool overrideIfExists = true, bool copy = false)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (physicalPath == null) throw new ArgumentNullException(nameof(physicalPath));

            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public IEnumerable<string> GetFiles(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            return GetFiles(path, null);
        }

        /// <inheritdoc />
        public IEnumerable<string> GetFiles(string path, string? filter)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            path = ResolveS3Path(path, true);

            string filename = Path.GetFileNameWithoutExtension(filter);
            if (filename.EndsWith("*"))
                filename = filename.Remove(filename.Length - 1);

            string ext = Path.GetExtension(filter);
            if (ext.Contains("*"))
                ext = string.Empty;

            var request = new ListObjectsRequest
            {
                BucketName = Config.BucketName,
                Delimiter = Delimiter,
                Prefix = path + filename
            };

            var response = ExecuteWithContinuation(request);
            return response
                .SelectMany(p => p.S3Objects)
                .Select(p => RemovePrefix(p.Key))
                .Where(p => !string.IsNullOrEmpty(p) && p.EndsWith(ext))
                .ToArray();

        }

        /// <inheritdoc />
        public Stream OpenFile(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var request = new GetObjectRequest
            {
                BucketName = Config.BucketName,
                Key = ResolveS3Path(path)
            };

            MemoryStream stream;
            using (var response = ExecuteAsync(client => GetS3Client().GetObjectAsync(request)))
            {
                stream = new MemoryStream();
                response.ResponseStream.CopyTo(stream);
            }

            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);

            return stream;
        }

        /// <inheritdoc />
        public void DeleteFile(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var request = new DeleteObjectRequest
            {
                BucketName = Config.BucketName,
                Key = ResolveS3Path(path)
            };

            ExecuteAsync(client => GetS3Client().DeleteObjectAsync(request));
        }

        /// <inheritdoc />
        public bool FileExists(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var request = new GetObjectMetadataRequest
            {
                BucketName = Config.BucketName,
                Key = ResolveS3Path(path)
            };

            try
            {
                ExecuteAsync(client => GetS3Client().GetObjectMetadataAsync(request));
                return true;
            }
            catch (AggregateException e)
            {
                if (e.InnerException is AmazonS3Exception)
                {
                    AmazonS3Exception ex = (AmazonS3Exception)e.InnerException;
                    if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                        return false;
                }
                throw e.InnerException ?? e;
            }
            catch (AmazonS3Exception ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return false;
                throw;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        /// <inheritdoc />
        public string GetRelativePath(string fullPathOrUrl)
        {
            if (fullPathOrUrl == null) throw new ArgumentNullException(nameof(fullPathOrUrl));

            // test url
            var path = EnsureUrlSeparatorChar(fullPathOrUrl); // ensure url separator char

            // if it starts with the root url, strip it and trim the starting slash to make it relative
            // eg "/Media/1234/img.jpg" => "1234/img.jpg"
            if (_ioHelper.PathStartsWith(path, _rootUrl, '/'))
                path = path[_rootUrl.Length..].TrimStart('/');

            // unchanged - what else?
            return path;
        }

        /// <inheritdoc />
        public string GetFullPath(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            path = EnsureUrlSeparatorChar(path);
            return (_ioHelper.PathStartsWith(path, _rootUrl, '/') ? path : $"{_rootUrl}/{path}").Trim('/');
        }

        /// <inheritdoc />
        public string GetUrl(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            return $"{_rootUrl}/{EnsureUrlSeparatorChar(path).Trim('/')}";
        }

        /// <inheritdoc />
        public DateTimeOffset GetLastModified(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var request = new GetObjectMetadataRequest
            {
                BucketName = Config.BucketName,
                Key = ResolveS3Path(path)
            };

            var response = ExecuteAsync(client => GetS3Client().GetObjectMetadataAsync(request));
            return new DateTimeOffset(response.LastModified);
        }

        /// <inheritdoc />
        public DateTimeOffset GetCreated(string path)
        {
            //S3 doesn't have Created - just use LastModified
            return GetLastModified(path);

        }

        /// <inheritdoc />
        public long GetSize(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            var request = new GetObjectMetadataRequest
            {
                BucketName = Config.BucketName,
                Key = ResolveS3Path(path)
            };

            var response = ExecuteAsync(client => GetS3Client().GetObjectMetadataAsync(request));
            return response.ContentLength;
        }

        /// <inheritdoc />
        public AmazonS3Client GetS3Client()
        {
            var s3config = new AmazonS3Config()
            {
                ServiceURL = Config.ServiceUrl,
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(Config.Region),
                ForcePathStyle = Config.ForcePathStyle,
            };

            BasicAWSCredentials creds = new BasicAWSCredentials(Config.AccessKey, Config.SecretKey);

            return new AmazonS3Client(creds, s3config);
        }

        /// <inheritdoc />
        public bool CanAddPhysical => false;

        private static string EnsureUrlSeparatorChar(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            return path.Replace("\\", "/", StringComparison.InvariantCultureIgnoreCase);
        }

        protected const string Delimiter = "/";

        protected virtual T ExecuteAsync<T>(Func<IAmazonS3, Task<T>> request)
        {
            try
            {
                var t = Task.Run(async () => await request(GetS3Client()).ConfigureAwait(false));
                t.ConfigureAwait(false);
                return t.Result;
            }
            catch (AggregateException e)
            {
                if (e.InnerException is AmazonS3Exception)
                {
                    AmazonS3Exception ex = (AmazonS3Exception)e.InnerException;
                    if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                        throw new FileNotFoundException(ex.Message, ex);
                    if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        throw new UnauthorizedAccessException(ex.Message, ex);
                }
                throw e.InnerException ?? e;
            }
            catch (AmazonS3Exception ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    throw new FileNotFoundException(ex.Message, ex);
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new UnauthorizedAccessException(ex.Message, ex);
                throw;
            }
        }

        protected virtual IEnumerable<ListObjectsResponse> ExecuteWithContinuation(ListObjectsRequest request)
        {
            var response = ExecuteAsync(client => GetS3Client().ListObjectsAsync(request));
            yield return response;

            while (response.IsTruncated)
            {
                request.Marker = response.NextMarker;
                response = ExecuteAsync(client => GetS3Client().ListObjectsAsync(request));
                yield return response;
            }
        }

        protected virtual string ResolveS3Path(string path, bool isDir = false)
        {
            if (string.IsNullOrEmpty(path))
                return Config.BucketPrefix;

            // Equalise delimiters
            path = EnsureUrlSeparatorChar(path).TrimStart('/');

            //Remove Key Prefix If Duplicate
            //TODO: Will this every be relevant?
            if (path.StartsWith(Config.BucketPrefix, StringComparison.InvariantCultureIgnoreCase))
                path = path.Substring(Config.BucketPrefix.Length);

            //Add forward slash at the end if this is a dir (necessary for S3 to indicate "dir")
            if (isDir && !path.EndsWith(Delimiter))
                path = string.Concat(path, Delimiter);

            return string.Concat(Config.BucketPrefix, "/", path.TrimStart('/'));
        }

        protected virtual string RemovePrefix(string key)
        {
            if (!string.IsNullOrEmpty(Config.BucketPrefix) && key.StartsWith(Config.BucketPrefix))
                key = key.Substring(Config.BucketPrefix.Length);

            return key.TrimStart(Delimiter.ToCharArray()).TrimEnd(Delimiter.ToCharArray());
        }
    }
}
