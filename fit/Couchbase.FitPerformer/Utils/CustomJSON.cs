using System;
namespace Couchbase.FitPerformer.Utils
{
	// This is a custom class to shape the data to turn into JSON for the stream.
	public record CustomJSON<T>(T content, bool Serialized);
}

