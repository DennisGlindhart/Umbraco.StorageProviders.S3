using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Web.Caching;
using SixLabors.ImageSharp.Web.Providers;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Infrastructure.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using Umbraco.Extensions;
using Umbraco.StorageProviders.S3;
using Umbraco.StorageProviders.S3.Imaging;
using Umbraco.StorageProviders.S3.IO;

// ReSharper disable once CheckNamespace
// uses same namespace as Umbraco Core for easier discoverability
namespace Umbraco.Cms.Core.DependencyInjection
{
    /// <summary>
    /// Extension methods to help registering S3 Storage file systems for Umbraco media.
    /// </summary>
    public static class S3MediaFileSystemExtensions
    {
        /// <summary>
        /// Registers an <see cref="IS3FileSystem" /> and it's dependencies configured for media.
        /// </summary>
        /// <param name="builder">The <see cref="IUmbracoBuilder" />.</param>
        /// <returns>
        /// The <see cref="IUmbracoBuilder" />.
        /// </returns>
        /// <remarks>
        /// This will also configure the ImageSharp.Web middleware to use S3 Storage to retrieve the original and cache the processed images.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">builder</exception>
        public static IUmbracoBuilder AddS3MediaFileSystem(this IUmbracoBuilder builder)
            => builder.AddS3MediaFileSystem(true);

        /// <summary>
        /// Registers an <see cref="IS3FileSystem" /> and it's dependencies configured for media.
        /// </summary>
        /// <param name="builder">The <see cref="IUmbracoBuilder" />.</param>
        /// <param name="useS3ImageCache">If set to <c>true</c> also configures S3 Storage for the image cache.</param>
        /// <returns>
        /// The <see cref="IUmbracoBuilder" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">builder</exception>
        public static IUmbracoBuilder AddS3MediaFileSystem(this IUmbracoBuilder builder, bool useS3ImageCache)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.AddS3FileSystem(S3FileSystemOptions.MediaFileSystemName, "~/media",
                (options, provider) =>
                {
                    var globalSettingsOptions = provider.GetRequiredService<IOptions<GlobalSettings>>();
                    options.VirtualPath = globalSettingsOptions.Value.UmbracoMediaPath;
                });

            builder.Services.TryAddSingleton<S3FileSystemMiddleware>();

            // ImageSharp image provider/cache
            builder.Services.Insert(0, ServiceDescriptor.Singleton<IImageProvider, S3FileSystemImageProvider>());

            if (useS3ImageCache)
                throw new NotSupportedException("We don't yet support S3 as image cache. Please disable for now");
                //builder.Services.AddUnique<IImageCache, S3FileSystemImageCache>();

            builder.SetMediaFileSystem(provider => provider.GetRequiredService<IS3FileSystemProvider>()
                .GetFileSystem(S3FileSystemOptions.MediaFileSystemName));

            return builder;
        }

        /// <summary>
        /// Registers a <see cref="IS3FileSystem" /> and it's dependencies configured for media.
        /// </summary>
        /// <param name="builder">The <see cref="IUmbracoBuilder" />.</param>
        /// <param name="configure">An action used to configure the <see cref="S3FileSystemOptions" />.</param>
        /// <returns>
        /// The <see cref="IUmbracoBuilder" />.
        /// </returns>
        /// <remarks>
        /// This will also configure the ImageSharp.Web middleware to use S3 Storage to retrieve the original and cache the processed images.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">builder
        /// or
        /// configure</exception>
        public static IUmbracoBuilder AddS3MediaFileSystem(this IUmbracoBuilder builder, Action<S3FileSystemOptions> configure)
            => builder.AddS3MediaFileSystem(true, configure);

        /// <summary>
        /// Registers a <see cref="IS3FileSystem" /> and it's dependencies configured for media.
        /// </summary>
        /// <param name="builder">The <see cref="IUmbracoBuilder" />.</param>
        /// <param name="useS3ImageCache">If set to <c>true</c> also configures S3 Storage for the image cache.</param>
        /// <param name="configure">An action used to configure the <see cref="S3FileSystemOptions" />.</param>
        /// <returns>
        /// The <see cref="IUmbracoBuilder" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">builder
        /// or
        /// configure</exception>
        public static IUmbracoBuilder AddS3MediaFileSystem(this IUmbracoBuilder builder, bool useS3ImageCache, Action<S3FileSystemOptions> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            AddS3MediaFileSystem(builder, useS3ImageCache);

            builder.Services
                .AddOptions<S3FileSystemOptions>(S3FileSystemOptions.MediaFileSystemName)
                .Configure(configure);

            return builder;
        }

        /// <summary>
        /// Registers a <see cref="IS3FileSystem" /> and it's dependencies configured for media.
        /// </summary>
        /// <param name="builder">The <see cref="IUmbracoBuilder" />.</param>
        /// <param name="configure">An action used to configure the <see cref="S3FileSystemOptions" />.</param>
        /// <returns>
        /// The <see cref="IUmbracoBuilder" />.
        /// </returns>
        /// <remarks>
        /// This will also configure the ImageSharp.Web middleware to use S3 Storage to retrieve the original and cache the processed images.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">builder
        /// or
        /// configure</exception>
        public static IUmbracoBuilder AddS3MediaFileSystem(this IUmbracoBuilder builder, Action<S3FileSystemOptions, IServiceProvider> configure)
            => builder.AddS3MediaFileSystem(true, configure);

        /// <summary>
        /// Registers a <see cref="IS3FileSystem" /> and it's dependencies configured for media.
        /// </summary>
        /// <param name="builder">The <see cref="IUmbracoBuilder" />.</param>
        /// <param name="useS3ImageCache">If set to <c>true</c> also configures S3 Storage for the image cache.</param>
        /// <param name="configure">An action used to configure the <see cref="S3FileSystemOptions" />.</param>
        /// <returns>
        /// The <see cref="IUmbracoBuilder" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">builder
        /// or
        /// configure</exception>
        public static IUmbracoBuilder AddS3MediaFileSystem(this IUmbracoBuilder builder, bool useS3ImageCache, Action<S3FileSystemOptions, IServiceProvider> configure)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            AddS3MediaFileSystem(builder, useS3ImageCache);

            builder.Services
                .AddOptions<S3FileSystemOptions>(S3FileSystemOptions.MediaFileSystemName)
                .Configure(configure);

            return builder;
        }

        /// <summary>
        /// Adds the <see cref="S3FileSystemMiddleware" />.
        /// </summary>
        /// <param name="builder">The <see cref="IUmbracoApplicationBuilderContext" />.</param>
        /// <returns>
        /// The <see cref="IUmbracoApplicationBuilderContext" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">builder</exception>
        public static IUmbracoApplicationBuilderContext UseS3MediaFileSystem(this IUmbracoApplicationBuilderContext builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            UseS3MediaFileSystem(builder.AppBuilder);

            return builder;
        }

        /// <summary>
        /// Adds the <see cref="S3FileSystemMiddleware" />.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder" />.</param>
        /// <returns>
        /// The <see cref="IApplicationBuilder" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">app</exception>
        public static IApplicationBuilder UseS3MediaFileSystem(this IApplicationBuilder app)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));

            app.UseMiddleware<S3FileSystemMiddleware>();

            return app;
        }
    }
}
