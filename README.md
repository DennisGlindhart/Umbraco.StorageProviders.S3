# S3 Media/Filesystem Provider for Umbraco 9 Media

Based on AzureBlob provider https://github.com/umbraco/Umbraco.StorageProviders.git and inspired by https://github.com/DannerrQ/Umbraco-S3-Provider

Usage
-----

In startup.cs

ConfigureServices:

```
     services.AddUmbraco(_env, _config)
           .AddBackOffice()
           .AddWebsite()
           .AddComposers()
           //Add this, with false for now (Virtual/CDN not supported)
           .AddS3MediaFileSystem(false)
```

Configure:
```

     app.UseUmbraco()
          .WithMiddleware(u =>
          {
               u.UseBackOffice();
               u.UseWebsite();
               //Add this
               u.UseS3MediaFileSystem();
           })
```

Configuration
-------------

Add the "S3" to "Storage"-section in appsettings.json with a "Media"-identifier (for Media which is the only tested for now)

For Minio use ServiceUrl and ForcePathStyle=true.

```
  "Umbraco": {
    "Storage": {
      "S3": {
        "Media": {
          "BucketName": "umbraco",
          "ServiceUrl": "http://localhost:9000",
          "ForcePathStyle": true,
          "BucketPrefix": "media",
          "AccessKey": "123456789",
          "SecretKey": "123456789",
          "Region": "us-east-1"
        }
      }
    },
    "CMS": {
    }
  }
```

For AWS S3 use BucketHostName and ForcePathStyle=false (Untested)

```
  "Umbraco": {
    "Storage": {
      "S3": {
        "Media": {
          "BucketName": "umbraco",
          "BucketHostName": "xxx.s3.us-east-1.amazonaws.com",
          "ForcePathStyle": false,
          "BucketPrefix": "media",
          "AccessKey": "123456789",
          "SecretKey": "123456789",
          "Region": "us-east-1"
        }
      }
    },
    "CMS": {
    }
  }
```

Status
------

Very early prototype. Only basic Media upload to S3 storage and view/scaling (Middleware/ImageProcessor integration) has been tested

Following is probably not working/not implemented yet:

- Only tested with Minio (ForcePathStyle = true)
- No serving from External CDN URL - Only pass-through via Umbraco-app
- Cache stored in S3 not supported
- Optimizations like HTTP Range-headers and NotModified, Etag etc. not yet taken into account
- Only tested for Media
- Probably more :)
