using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management.Buckets
{
    public static class BucketManagerExtensions
    {
        public static Task CreateBucketAsync(this IBucketManager bucketManager, BucketSettings settings)
        {
            return bucketManager.CreateBucketAsync(settings, CreateBucketOptions.Default);
        }

        public static Task CreateBucketAsync(this IBucketManager bucketManager, BucketSettings settings, Action<CreateBucketOptions> configureOptions)
        {
            var options = new CreateBucketOptions();
            configureOptions(options);

            return bucketManager.CreateBucketAsync(settings, options);
        }

        public static Task UpdateBucketAsync(this IBucketManager bucketManager, BucketSettings settings)
        {
            return bucketManager.UpdateBucketAsync(settings, UpdateBucketOptions.Default);
        }

        public static Task UpdateBucketAsync(this IBucketManager bucketManager, BucketSettings settings, Action<UpdateBucketOptions> configureOptions)
        {
            var options = new UpdateBucketOptions();
            configureOptions(options);

            return bucketManager.UpdateBucketAsync(settings, options);
        }

        public static Task DropBucketAsync(this IBucketManager bucketManager, string bucketName)
        {
            return bucketManager.DropBucketAsync(bucketName, DropBucketOptions.Default);
        }

        public static Task DropBucketAsync(this IBucketManager bucketManager, string bucketName, Action<DropBucketOptions> configureOptions)
        {
            var options = new DropBucketOptions();
            configureOptions(options);

            return bucketManager.DropBucketAsync(bucketName, options);
        }

        public static Task<BucketSettings> GetBucketAsync(this IBucketManager bucketManager, string bucketName)
        {
            return bucketManager.GetBucketAsync(bucketName, GetBucketOptions.Default);
        }

        public static Task<BucketSettings> GetBucketAsync(this IBucketManager bucketManager, string bucketName, Action<GetBucketOptions> configureOptions)
        {
            var options = new GetBucketOptions();
            configureOptions(options);

            return bucketManager.GetBucketAsync(bucketName, options);
        }

        public static Task<Dictionary<string, BucketSettings>> GetAllBucketsAsync(this IBucketManager bucketManager)
        {
            return bucketManager.GetAllBucketsAsync(GetAllBucketsOptions.Default);
        }

        public static Task<Dictionary<string, BucketSettings>> GetAllBucketsAsync(this IBucketManager bucketManager, Action<GetAllBucketsOptions> configureOptions)
        {
            var options = new GetAllBucketsOptions();
            configureOptions(options);

            return bucketManager.GetAllBucketsAsync(options);
        }

        public static Task FlushBucketAsync(this IBucketManager bucketManager, string bucketName)
        {
            return bucketManager.FlushBucketAsync(bucketName, FlushBucketOptions.Default);
        }

        public static Task FlushBucketAsync(this IBucketManager bucketManager, string bucketName, Action<FlushBucketOptions> configureOptions)
        {
            var options= new FlushBucketOptions();
            configureOptions(options);

            return bucketManager.FlushBucketAsync(bucketName, options);
        }
    }
}
