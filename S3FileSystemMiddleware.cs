using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Extensions;
using Umbraco.StorageProviders.S3.IO;

namespace Umbraco.StorageProviders.S3
{
    public class S3FileSystemMiddleware : IMiddleware
    {
        private readonly string _name;
        private readonly IS3FileSystemProvider _fileSystemProvider;
        private string _rootPath;
        private readonly TimeSpan? _maxAge = TimeSpan.FromDays(7);
        private S3FileSystemOptions s3config;

        public S3FileSystemMiddleware(IOptionsMonitor<S3FileSystemOptions> options, IS3FileSystemProvider fileSystemProvider, IHostingEnvironment hostingEnvironment)
            : this(S3FileSystemOptions.MediaFileSystemName, options, fileSystemProvider, hostingEnvironment)
        {
        }

        protected S3FileSystemMiddleware(string name, IOptionsMonitor<S3FileSystemOptions> options, IS3FileSystemProvider fileSystemProvider, IHostingEnvironment hostingEnvironment)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (hostingEnvironment == null) throw new ArgumentNullException(nameof(hostingEnvironment));

            _name = name ?? throw new ArgumentNullException(nameof(name));
            _fileSystemProvider = fileSystemProvider ?? throw new ArgumentNullException(nameof(fileSystemProvider));

            var fileSystemOptions = options.Get(name);
            s3config = fileSystemOptions;
            _rootPath = hostingEnvironment.ToAbsolute(fileSystemOptions.VirtualPath);

            options.OnChange((o, n) => OptionsOnChange(o, n, hostingEnvironment));
        }

        /// <inheritdoc />
        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (next == null) throw new ArgumentNullException(nameof(next));

            return HandleRequestAsync(context, next);
        }

        private async Task HandleRequestAsync(HttpContext context, RequestDelegate next)
        {
            var request = context.Request;
            var response = context.Response;

            if (!context.Request.Path.StartsWithSegments(_rootPath, StringComparison.InvariantCultureIgnoreCase))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            var client = _fileSystemProvider.GetFileSystem(_name).GetS3Client();
            GetObjectResponse properties;

            //TODO: Handle NotModified, Ranges etc.
            //var x = new GetObjectMetadataRequest()
            //{
            //    x.ModifiedSinceDate = 
            //};

            try
            {
                var path = context.Request.Path.Value.TrimStart("/");
                properties = await client.GetObjectAsync(s3config.BucketName, path).ConfigureAwait(false);
            } catch (AggregateException e) {
                if (e.InnerException is AmazonS3Exception)
                {
                    AmazonS3Exception ex = (AmazonS3Exception)e.InnerException;
                    // the file does not exist, let other middleware handle it
                    if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        await next(context).ConfigureAwait(false);
                        return;
                    }
                }
                throw e.InnerException ?? e;
            }

            var responseHeaders = response.GetTypedHeaders();

            responseHeaders.CacheControl =
                new CacheControlHeaderValue
                {
                    Public = true,
                    MustRevalidate = true,
                    MaxAge = _maxAge,
                };

            responseHeaders.LastModified = properties.LastModified;
            responseHeaders.ETag = new EntityTagHeaderValue(properties.ETag);
            responseHeaders.Append(HeaderNames.Vary, "Accept-Encoding");

            var requestHeaders = request.GetTypedHeaders();

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = properties.Headers.ContentType;
            responseHeaders.ContentLength = properties.ContentLength;
            responseHeaders.Append(HeaderNames.AcceptRanges, "bytes");

            await response.StartAsync().ConfigureAwait(false);
            await DownloadRangeToStreamAsync(properties, response.Body, 0L, properties.ContentLength, context.RequestAborted).ConfigureAwait(false);
        }

        private static bool ValidateRanges(ICollection<RangeItemHeaderValue> ranges, long length)
        {
            if (ranges.Count == 0)
                return false;

            foreach (var range in ranges)
            {
                if (range.From > range.To)
                    return false;
                if (range.To >= length)
                    return false;
            }

            return true;
        }

        private static ContentRangeHeaderValue GetRangeHeader(GetObjectResponse properties, RangeItemHeaderValue range)
        {
            var length = properties.ContentLength - 1;

            long from;
            long to;
            if (range.To.HasValue)
            {
                if (range.From.HasValue)
                {
                    to = Math.Min(range.To.Value, length);
                    from = range.From.Value;
                }
                else
                {
                    to = length;
                    from = Math.Max(properties.ContentLength - range.To.Value, 0L);
                }
            }
            else if (range.From.HasValue)
            {
                to = length;
                from = range.From.Value;
            }
            else
            {
                to = length;
                from = 0L;
            }

            return new ContentRangeHeaderValue(from, to, properties.ContentLength);
        }

        private static async Task DownloadRangeToStreamAsync(GetObjectResponse properties,
            Stream outputStream, ContentRangeHeaderValue contentRange, CancellationToken cancellationToken)
        {
            var offset = contentRange.From.GetValueOrDefault(0L);
            var length = properties.ContentLength;

            if (contentRange.To.HasValue && contentRange.From.HasValue)
            {
                length = contentRange.To.Value - contentRange.From.Value + 1;
            }
            else if (contentRange.To.HasValue)
            {
                length = contentRange.To.Value + 1;
            }
            else if (contentRange.From.HasValue)
            {
                length = properties.ContentLength - contentRange.From.Value + 1;
            }

            await DownloadRangeToStreamAsync(properties, outputStream, offset, length, cancellationToken).ConfigureAwait(false);
        }

        private static async Task DownloadRangeToStreamAsync(GetObjectResponse blob, Stream outputStream,
            long offset, long length, CancellationToken cancellationToken)
        {
            try
            {
                if (length == 0) return;
                await blob.ResponseStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // client cancelled the request before it could finish, just ignore
            }
        }

        private void OptionsOnChange(S3FileSystemOptions options, string name, IHostingEnvironment hostingEnvironment)
        {
            if (name != _name) return;

            s3config = options;
            _rootPath = hostingEnvironment.ToAbsolute(options.VirtualPath);
        }
    }
}
