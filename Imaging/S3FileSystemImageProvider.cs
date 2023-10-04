using System;
using System.Threading.Tasks;
using Amazon.S3;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Web;
using SixLabors.ImageSharp.Web.Providers;
using SixLabors.ImageSharp.Web.Resolvers;
using Umbraco.Cms.Core.Hosting;
using Umbraco.StorageProviders.S3.IO;

namespace Umbraco.StorageProviders.S3.Imaging
{
    /// <inheritdoc />
    public class S3FileSystemImageProvider : IImageProvider
    {
        private readonly string _name;
        private readonly IS3FileSystemProvider _fileSystemProvider;
        private string _rootPath;
        private readonly FormatUtilities _formatUtilities;

        /// <summary>
        /// A match function used by the resolver to identify itself as the correct resolver to use.
        /// </summary>
        private Func<HttpContext, bool>? _match;
        private S3FileSystemOptions s3config;

        /// <summary>
        /// Initializes a new instance of the <see cref="S3FileSystemImageProvider" /> class.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="fileSystemProvider">The file system provider.</param>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <param name="formatUtilities">The format utilities.</param>
        public S3FileSystemImageProvider(IOptionsMonitor<S3FileSystemOptions> options, IS3FileSystemProvider fileSystemProvider, IHostingEnvironment hostingEnvironment, FormatUtilities formatUtilities)
            : this(S3FileSystemOptions.MediaFileSystemName, options, fileSystemProvider, hostingEnvironment, formatUtilities)
        { }

        /// <summary>
        /// Creates a new instance of <see cref="S3FileSystemImageProvider" />.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <param name="fileSystemProvider">The file system provider.</param>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <param name="formatUtilities">The format utilities.</param>
        /// <exception cref="System.ArgumentNullException">optionsFactory
        /// or
        /// hostingEnvironment
        /// or
        /// name
        /// or
        /// fileSystemProvider
        /// or
        /// formatUtilities</exception>
        protected S3FileSystemImageProvider(string name, IOptionsMonitor<S3FileSystemOptions> options, IS3FileSystemProvider fileSystemProvider, IHostingEnvironment hostingEnvironment, FormatUtilities formatUtilities)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (hostingEnvironment == null) throw new ArgumentNullException(nameof(hostingEnvironment));

            _name = name ?? throw new ArgumentNullException(nameof(name));
            _fileSystemProvider = fileSystemProvider ?? throw new ArgumentNullException(nameof(fileSystemProvider));
            _formatUtilities = formatUtilities ?? throw new ArgumentNullException(nameof(formatUtilities));

            var fileSystemOptions = options.Get(name);
            s3config = fileSystemOptions;
            _rootPath = hostingEnvironment.ToAbsolute(fileSystemOptions.VirtualPath);

            options.OnChange((o, n) => OptionsOnChange(o, n, hostingEnvironment));
        }

        /// <inheritdoc />
        public bool IsValidRequest(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            return _formatUtilities.TryGetExtensionFromUri(context.Request.GetDisplayUrl(), out var _);
        }

        /// <inheritdoc />
        public Task<IImageResolver?> GetAsync(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            return GetResolverAsync(context);
        }

        private async Task<IImageResolver?> GetResolverAsync(HttpContext context)
        {
            var blob = _fileSystemProvider
                .GetFileSystem(_name)
                .GetS3Client();

            try
            {
                var path = context.Request.Path.Value!.TrimStart('/');
                var image = await blob.GetObjectAsync(s3config.BucketName, path).ConfigureAwait(false);
                return new S3StorageImageResolver(image);
            }
            catch (AggregateException e)
            {
                if (e.InnerException is AmazonS3Exception)
                {
                    AmazonS3Exception ex = (AmazonS3Exception)e.InnerException;
                    // the blob or file does not exist, let other middleware handle it
                    if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return null;
                    }
                }
                throw e.InnerException ?? e;
            }
        }

        /// <inheritdoc />
        public ProcessingBehavior ProcessingBehavior => ProcessingBehavior.CommandOnly;

        /// <inheritdoc/>
        public Func<HttpContext, bool> Match
        {
            get => this._match ?? IsMatch;
            set => this._match = value;
        }

        private bool IsMatch(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            return context.Request.Path.StartsWithSegments(_rootPath, StringComparison.InvariantCultureIgnoreCase);
        }

        private void OptionsOnChange(S3FileSystemOptions options, string name, IHostingEnvironment hostingEnvironment)
        {
            if (name != _name) return;

            s3config = options;
            _rootPath = hostingEnvironment.ToAbsolute(options.VirtualPath);
        }
    }
}
