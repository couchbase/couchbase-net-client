using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Views
{
    public static class ViewManagerExtensions
    {
        public static Task<DesignDocument> GetAsync(this IViewManager viewManager, string designDocName)
        {
            return viewManager.GetAsync(designDocName, GetViewIndexOptions.Default);
        }

        public static Task<DesignDocument> GetAsync(this IViewManager viewManager, string designDocName, Action<GetViewIndexOptions> configureOptions)
        {
            var options = new GetViewIndexOptions();
            configureOptions(options);

            return viewManager.GetAsync(designDocName, options);
        }

        public static Task<IEnumerable<DesignDocument>> GetAllAsync(this IViewManager viewManager)
        {
            return viewManager.GetAllAsync(GetAllViewIndexOptions.Default);
        }

        public static Task<IEnumerable<DesignDocument>> GetAllAsync(this IViewManager viewManager, Action<GetAllViewIndexOptions> configureOptions)
        {
            var options = new GetAllViewIndexOptions();
            configureOptions(options);

            return viewManager.GetAllAsync(options);
        }

        public static Task CreateAsync(this IViewManager viewManager, DesignDocument designDocument)
        {
            return viewManager.CreateAsync(designDocument, CreateViewIndexOptions.Default);
        }

        public static Task CreateAsync(this IViewManager viewManager, DesignDocument designDocument, Action<CreateViewIndexOptions> configureOptions)
        {
            var options = new CreateViewIndexOptions();
            configureOptions(options);

            return viewManager.CreateAsync(designDocument, options);
        }

        public static Task UpsertAsync(this IViewManager viewManager, DesignDocument designDocument)
        {
            return viewManager.UpsertAsync(designDocument, UpsertViewIndexOptions.Default);
        }

        public static Task UpsertAsync(this IViewManager viewManager, DesignDocument designDocument, Action<UpsertViewIndexOptions> configureOptions)
        {
            var options = new UpsertViewIndexOptions();
            configureOptions(options);

            return viewManager.UpsertAsync(designDocument, options);
        }

        public static Task DropAsync(this IViewManager viewManager, string designDocumentName)
        {
            return viewManager.DropAsync(designDocumentName, DropViewIndexOptions.Default);
        }

        public static Task DropAsync(this IViewManager viewManager, string designDocumentName, Action<DropViewIndexOptions> configureOptions)
        {
            var options = new DropViewIndexOptions();
            configureOptions(options);

            return viewManager.DropAsync(designDocumentName, options);
        }

        public static Task PublishAsync(this IViewManager viewManager, string designDocumentName)
        {
            return viewManager.PublishAsync(designDocumentName, PublishIndexOptions.Default);
        }

        public static Task PublishAsync(this IViewManager viewManager, string designDocumentName, Action<PublishIndexOptions> configureOptions)
        {
            var options = new PublishIndexOptions();
            configureOptions(options);

            return viewManager.PublishAsync(designDocumentName, options);
        }
    }
}
