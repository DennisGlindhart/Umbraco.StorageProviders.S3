using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3.Model;
using SixLabors.ImageSharp.Web;
using SixLabors.ImageSharp.Web.Resolvers;

namespace Umbraco.StorageProviders.S3.Imaging
{
    public class S3StorageImageResolver : IImageResolver
    {
        private readonly GetObjectResponse image;

        public S3StorageImageResolver(GetObjectResponse image)
            => this.image = image;

        public async Task<ImageMetadata> GetMetaDataAsync()
        {
            TimeSpan maxAge = TimeSpan.MinValue;
            /*
            if (CacheControlHeaderValue.TryParse(properties.CacheControl, out CacheControlHeaderValue cacheControl))
            {
                // Weirdly passing null to TryParse returns true.
                if (cacheControl?.MaxAge.HasValue == true)
                {
                    maxAge = cacheControl.MaxAge.Value;
                }
            }
            */

            return new ImageMetadata(image.LastModified.ToUniversalTime(), maxAge, image.ContentLength);
        }

        /// <inheritdoc/>
        public Task<Stream> OpenReadAsync() => Task.FromResult<Stream>(this.image.ResponseStream);
    }
}