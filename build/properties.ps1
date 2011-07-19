$buildParams =  @{
	"solution_name" = "CouchbaseDriver.sln";
	"projects" = @( "Couchbase" );
	"extras" = @{ "libs\EnyimMemcached\Enyim.Caching.Log4NetAdapter" = "log4net"; "libs\EnyimMemcached\Enyim.Caching.NLogAdapter" = "NLog" };
	"packages" = @( "Couchbase" );
}
