using System;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.IO;
using Umbraco.Extensions;

namespace Umbraco.StorageProviders.S3.IO
{
    /// <inheritdoc />
    public class S3FileSystemProvider : IS3FileSystemProvider
    {
        private readonly ConcurrentDictionary<string, IS3FileSystem> _fileSystems = new();
        private readonly IOptionsMonitor<S3FileSystemOptions> _optionsMonitor;
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly IIOHelper _ioHelper;
        private readonly FileExtensionContentTypeProvider _fileExtensionContentTypeProvider;

        /// <summary>
        /// Creates a new instance of <see cref="S3FileSystemProvider" />.
        /// </summary>
        /// <param name="optionsMonitor">The options monitor.</param>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <param name="ioHelper">The IO helper.</param>
        /// <exception cref="System.ArgumentNullException">optionsMonitor
        /// or
        /// hostingEnvironment
        /// or
        /// ioHelper</exception>
        public S3FileSystemProvider(IOptionsMonitor<S3FileSystemOptions> optionsMonitor, IHostingEnvironment hostingEnvironment, IIOHelper ioHelper)
        {
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
            _hostingEnvironment = hostingEnvironment ?? throw new ArgumentNullException(nameof(hostingEnvironment));
            _ioHelper = ioHelper ?? throw new ArgumentNullException(nameof(ioHelper));

            _fileExtensionContentTypeProvider = new FileExtensionContentTypeProvider();
            _optionsMonitor.OnChange(OptionsOnChange);
        }

        /// <inheritdoc />
        public IS3FileSystem GetFileSystem(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            return _fileSystems.GetOrAdd(name, CreateInstance);
        }

        private IS3FileSystem CreateInstance(string name)
        {
            var options = _optionsMonitor.Get(name);

            return CreateInstance(options);
        }

        private IS3FileSystem CreateInstance(S3FileSystemOptions options)
        {
            return new S3FileSystem(options, _hostingEnvironment, _ioHelper, _fileExtensionContentTypeProvider);
        }

        private void OptionsOnChange(S3FileSystemOptions options, string name)
        {
            _fileSystems.TryUpdate(name, _ => CreateInstance(options));
        }
    }
}
