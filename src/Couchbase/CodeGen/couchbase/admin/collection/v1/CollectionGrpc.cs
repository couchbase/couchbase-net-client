// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: couchbase/admin/collection/v1/collection.proto
// </auto-generated>
#pragma warning disable 0414, 1591, 8981
#region Designer generated code

using grpc = global::Grpc.Core;

namespace Couchbase.Protostellar.Admin.Collection.V1 {
  public static partial class CollectionAdminService
  {
    static readonly string __ServiceName = "couchbase.admin.collection.v1.CollectionAdminService";

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static void __Helper_SerializeMessage(global::Google.Protobuf.IMessage message, grpc::SerializationContext context)
    {
      #if !GRPC_DISABLE_PROTOBUF_BUFFER_SERIALIZATION
      if (message is global::Google.Protobuf.IBufferMessage)
      {
        context.SetPayloadLength(message.CalculateSize());
        global::Google.Protobuf.MessageExtensions.WriteTo(message, context.GetBufferWriter());
        context.Complete();
        return;
      }
      #endif
      context.Complete(global::Google.Protobuf.MessageExtensions.ToByteArray(message));
    }

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static class __Helper_MessageCache<T>
    {
      public static readonly bool IsBufferMessage = global::System.Reflection.IntrospectionExtensions.GetTypeInfo(typeof(global::Google.Protobuf.IBufferMessage)).IsAssignableFrom(typeof(T));
    }

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static T __Helper_DeserializeMessage<T>(grpc::DeserializationContext context, global::Google.Protobuf.MessageParser<T> parser) where T : global::Google.Protobuf.IMessage<T>
    {
      #if !GRPC_DISABLE_PROTOBUF_BUFFER_SERIALIZATION
      if (__Helper_MessageCache<T>.IsBufferMessage)
      {
        return parser.ParseFrom(context.PayloadAsReadOnlySequence());
      }
      #endif
      return parser.ParseFrom(context.PayloadAsNewBuffer());
    }

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsRequest> __Marshaller_couchbase_admin_collection_v1_ListCollectionsRequest = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsRequest.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsResponse> __Marshaller_couchbase_admin_collection_v1_ListCollectionsResponse = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsResponse.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeRequest> __Marshaller_couchbase_admin_collection_v1_CreateScopeRequest = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeRequest.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeResponse> __Marshaller_couchbase_admin_collection_v1_CreateScopeResponse = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeResponse.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeRequest> __Marshaller_couchbase_admin_collection_v1_DeleteScopeRequest = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeRequest.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeResponse> __Marshaller_couchbase_admin_collection_v1_DeleteScopeResponse = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeResponse.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionRequest> __Marshaller_couchbase_admin_collection_v1_CreateCollectionRequest = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionRequest.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionResponse> __Marshaller_couchbase_admin_collection_v1_CreateCollectionResponse = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionResponse.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionRequest> __Marshaller_couchbase_admin_collection_v1_DeleteCollectionRequest = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionRequest.Parser));
    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Marshaller<global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionResponse> __Marshaller_couchbase_admin_collection_v1_DeleteCollectionResponse = grpc::Marshallers.Create(__Helper_SerializeMessage, context => __Helper_DeserializeMessage(context, global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionResponse.Parser));

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsRequest, global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsResponse> __Method_ListCollections = new grpc::Method<global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsRequest, global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsResponse>(
        grpc::MethodType.Unary,
        __ServiceName,
        "ListCollections",
        __Marshaller_couchbase_admin_collection_v1_ListCollectionsRequest,
        __Marshaller_couchbase_admin_collection_v1_ListCollectionsResponse);

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeRequest, global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeResponse> __Method_CreateScope = new grpc::Method<global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeRequest, global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeResponse>(
        grpc::MethodType.Unary,
        __ServiceName,
        "CreateScope",
        __Marshaller_couchbase_admin_collection_v1_CreateScopeRequest,
        __Marshaller_couchbase_admin_collection_v1_CreateScopeResponse);

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeRequest, global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeResponse> __Method_DeleteScope = new grpc::Method<global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeRequest, global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeResponse>(
        grpc::MethodType.Unary,
        __ServiceName,
        "DeleteScope",
        __Marshaller_couchbase_admin_collection_v1_DeleteScopeRequest,
        __Marshaller_couchbase_admin_collection_v1_DeleteScopeResponse);

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionRequest, global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionResponse> __Method_CreateCollection = new grpc::Method<global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionRequest, global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionResponse>(
        grpc::MethodType.Unary,
        __ServiceName,
        "CreateCollection",
        __Marshaller_couchbase_admin_collection_v1_CreateCollectionRequest,
        __Marshaller_couchbase_admin_collection_v1_CreateCollectionResponse);

    [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
    static readonly grpc::Method<global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionRequest, global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionResponse> __Method_DeleteCollection = new grpc::Method<global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionRequest, global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionResponse>(
        grpc::MethodType.Unary,
        __ServiceName,
        "DeleteCollection",
        __Marshaller_couchbase_admin_collection_v1_DeleteCollectionRequest,
        __Marshaller_couchbase_admin_collection_v1_DeleteCollectionResponse);

    /// <summary>Service descriptor</summary>
    public static global::Google.Protobuf.Reflection.ServiceDescriptor Descriptor
    {
      get { return global::Couchbase.Protostellar.Admin.Collection.V1.CollectionReflection.Descriptor.Services[0]; }
    }

    /// <summary>Client for CollectionAdminService</summary>
    public partial class CollectionAdminServiceClient : grpc::ClientBase<CollectionAdminServiceClient>
    {
      /// <summary>Creates a new client for CollectionAdminService</summary>
      /// <param name="channel">The channel to use to make remote calls.</param>
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public CollectionAdminServiceClient(grpc::ChannelBase channel) : base(channel)
      {
      }
      /// <summary>Creates a new client for CollectionAdminService that uses a custom <c>CallInvoker</c>.</summary>
      /// <param name="callInvoker">The callInvoker to use to make remote calls.</param>
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public CollectionAdminServiceClient(grpc::CallInvoker callInvoker) : base(callInvoker)
      {
      }
      /// <summary>Protected parameterless constructor to allow creation of test doubles.</summary>
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      protected CollectionAdminServiceClient() : base()
      {
      }
      /// <summary>Protected constructor to allow creation of configured clients.</summary>
      /// <param name="configuration">The client configuration.</param>
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      protected CollectionAdminServiceClient(ClientBaseConfiguration configuration) : base(configuration)
      {
      }

      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsResponse ListCollections(global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsRequest request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return ListCollections(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsResponse ListCollections(global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsRequest request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_ListCollections, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsResponse> ListCollectionsAsync(global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsRequest request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return ListCollectionsAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsResponse> ListCollectionsAsync(global::Couchbase.Protostellar.Admin.Collection.V1.ListCollectionsRequest request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_ListCollections, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeResponse CreateScope(global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeRequest request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return CreateScope(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeResponse CreateScope(global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeRequest request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_CreateScope, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeResponse> CreateScopeAsync(global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeRequest request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return CreateScopeAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeResponse> CreateScopeAsync(global::Couchbase.Protostellar.Admin.Collection.V1.CreateScopeRequest request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_CreateScope, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeResponse DeleteScope(global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeRequest request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return DeleteScope(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeResponse DeleteScope(global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeRequest request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_DeleteScope, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeResponse> DeleteScopeAsync(global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeRequest request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return DeleteScopeAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeResponse> DeleteScopeAsync(global::Couchbase.Protostellar.Admin.Collection.V1.DeleteScopeRequest request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_DeleteScope, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionResponse CreateCollection(global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionRequest request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return CreateCollection(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionResponse CreateCollection(global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionRequest request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_CreateCollection, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionResponse> CreateCollectionAsync(global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionRequest request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return CreateCollectionAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionResponse> CreateCollectionAsync(global::Couchbase.Protostellar.Admin.Collection.V1.CreateCollectionRequest request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_CreateCollection, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionResponse DeleteCollection(global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionRequest request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return DeleteCollection(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionResponse DeleteCollection(global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionRequest request, grpc::CallOptions options)
      {
        return CallInvoker.BlockingUnaryCall(__Method_DeleteCollection, null, options, request);
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionResponse> DeleteCollectionAsync(global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionRequest request, grpc::Metadata headers = null, global::System.DateTime? deadline = null, global::System.Threading.CancellationToken cancellationToken = default(global::System.Threading.CancellationToken))
      {
        return DeleteCollectionAsync(request, new grpc::CallOptions(headers, deadline, cancellationToken));
      }
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      public virtual grpc::AsyncUnaryCall<global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionResponse> DeleteCollectionAsync(global::Couchbase.Protostellar.Admin.Collection.V1.DeleteCollectionRequest request, grpc::CallOptions options)
      {
        return CallInvoker.AsyncUnaryCall(__Method_DeleteCollection, null, options, request);
      }
      /// <summary>Creates a new instance of client from given <c>ClientBaseConfiguration</c>.</summary>
      [global::System.CodeDom.Compiler.GeneratedCode("grpc_csharp_plugin", null)]
      protected override CollectionAdminServiceClient NewInstance(ClientBaseConfiguration configuration)
      {
        return new CollectionAdminServiceClient(configuration);
      }
    }

  }
}
#endregion
