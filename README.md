Official Couchbase .NET SDK
====================

* master is 2.0 development branch
* release13 is 1.3.X development branch

## Getting Started ##

To get up and running with the SDK, please visit the [online documentation](http://developer.couchbase.com/documentation/server/4.5/sdk/dotnet/start-using-sdk.html).

## Running the Unit Tests ##

To run the unit tests (for master), the following are required:


1. Couchbase Server >= 3.0 installed on localhost
2. N1QL DP4 downloaded, copied to disk and connected to your localhost Couchbase Server: see [here](http://docs.couchbase.com/developer/n1ql-dp3/n1ql-intro.html).
4. The ["beer-sample"](http://docs.couchbase.com/admin/admin/Misc/sample-bucket-beer.html) sample Bucket and data set installed. This can be installed by logging into the Couchbase Console (http://localhost:8091) and then Settings->Sample Buckets.
4. The following buckets installed on localhost:
	1. "default" - the standard default bucket
	2. "authenticated" - a Couchbase bucket with a password of "secret"
	3. "memcached" - a Memcached bucket with no password
5. Install an SSL certificate (copied from the Couchbase console) if you wish to run the SSL/TLS tests

Note that some tests require a cluster (Observe tests and Replica Read tests for example) and will fail if running on localhost.

## Pull Requests and Submissions ##
Being an Open Source project, the Couchbase SDK depends upon feedback and submissions from the community. If you feel as if you want to submit a bug fix or a feature, please post a Pull Request. The Pull Request will go through a formal code review process and merged after being +2'd by a Couchbase Engineer. In order to accept a submission, Couchbase requires that all contributors sign the Contributor License Agreement (CLA). You can do this by creating an account in [Gerrit](http://review.couchbase.com), our official Code Review system. After you have created your account, login and check the CLA checkbox.

Once the CLA is signed, a Couchbase engineer will push the pull request to Gerrit and one or more Couchbase engineers will review the submission. If it looks good they will then +2 the changeset and merge it with master. In addition, if the submission needs more work, you will need to amend the Changeset with another Patchset. Note that is strongly encouraged to submit a Unit Test with each submission and also include a description of the submission, what changed and what the result is.

<img src="https://d3nmt5vlzunoa1.cloudfront.net/dotnet/files/2016/08/ReSharper2016_2_2_512x197.png" height="100"></img>
