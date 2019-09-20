using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management.Buckets
{
    public static class BucketManagerExtensions
    {
        public static Task CreateAsync(this IBucketManager bucketManager, BucketSettings settings)
        {
            return bucketManager.CreateAsync(settings, CreateBucketOptions.Default);
        }

        public static Task CreateAsync(this IBucketManager bucketManager, BucketSettings settings, Action<CreateBucketOptions> configureOptions)
        {
            var options = new CreateBucketOptions();
            configureOptions(options);

            return bucketManager.CreateAsync(settings, options);
        }

        public static Task UpsertAsync(this IBucketManager bucketManager, BucketSettings settings)
        {
            return bucketManager.UpsertAsync(settings, UpsertBucketOptions.Default);
        }

        public static Task UpsertAsync(this IBucketManager bucketManager, BucketSettings settings, Action<UpsertBucketOptions> configureOptions)
        {
            var options = new UpsertBucketOptions();
            configureOptions(options);

            return bucketManager.UpsertAsync(settings, options);
        }

        public static Task DropAsync(this IBucketManager bucketManager, string bucketName)
        {
            return bucketManager.DropAsync(bucketName, DropBucketOptions.Default);
        }

        public static Task DropAsync(this IBucketManager bucketManager, string bucketName, Action<DropBucketOptions> configureOptions)
        {
            var options = new DropBucketOptions();
            configureOptions(options);

            return bucketManager.DropAsync(bucketName, options);
        }

        public static Task<BucketSettings> GetAsync(this IBucketManager bucketManager, string bucketName)
        {
            return bucketManager.GetAsync(bucketName, GetBucketOptions.Default);
        }

        public static Task<BucketSettings> GetAsync(this IBucketManager bucketManager, string bucketName, Action<GetBucketOptions> configureOptions)
        {
            var options = new GetBucketOptions();
            configureOptions(options);

            return bucketManager.GetAsync(bucketName, options);
        }

        public static Task<Dictionary<string, BucketSettings>> GetAllAsync(this IBucketManager bucketManager)
        {
            return bucketManager.GetAllAsync(GetAllBucketOptions.Default);
        }

        public static Task<Dictionary<string, BucketSettings>> GetAllAsync(this IBucketManager bucketManager, Action<GetAllBucketOptions> configureOptions)
        {
            var options = new GetAllBucketOptions();
            configureOptions(options);

            return bucketManager.GetAllAsync(options);
        }

        public static Task FlushAsync(this IBucketManager bucketManager, string bucketName)
        {
            return bucketManager.Flush(bucketName, FlushBucketOptions.Default);
        }

        public static Task FlushAsync(this IBucketManager bucketManager, string bucketName, Action<FlushBucketOptions> configureOptions)
        {
            var options= new FlushBucketOptions();
            configureOptions(options);

            return bucketManager.Flush(bucketName, options);
        }
    }
}
