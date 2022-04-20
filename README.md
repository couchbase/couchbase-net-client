The Official Couchbase .NET SDK 

[Chat with us on Discord](https://forums.couchbase.com/c/net-sdk/6) | [Couchbase Forums](https://forums.couchbase.com/c/net-sdk/6)

* master is 3.0 development branch
* release27 is 2.7.X development branch
* release13 is 1.3.X development branch

## Getting Started

To get up and running with the SDK, please visit the [online documentation](http://developer.couchbase.com/documentation/server/4.5/sdk/dotnet/start-using-sdk.html).

## Running Tests

We maintain a collection of both unit and integration test projects.

### Unit Tests

Couchbase.UnitTests contains environment independent tests and do not require a local cluster to run.

### Running the Integration Tests ##

Couchbase.IntegrationTests contains tests that are run against a real Couchbase Server and has different requirements depending on what server version you are running them against:

Couchbase Server 4.0+
1. The "beer-sample" and "travel-sample" sample buckets installed. They can be installed by logging into the Couchbase Console and then Settings->Sample Buckets.
2. Create the following buckets:
	1. "default" - a Couchbase bucket with no password
	2. "authenticated" - a Couchbase bucket with a password of "secret"
	3. "memcached" - a Memcached bucket with no password
3. Install an SSL certificate (copied from the Couchbase console Security->Root Certificate)
4. A default primary index configured on the following buckets: `default`, `authenticated`, `beer-sample` and `travel-sample` (eg <code>create primary index on &#96;default&#96;</code>)
5. Add an FTS index to the `travel-sample` bucket called `idx-travel`
6. Update config.json with the hostname of your Couchbase Server IP and set enhancedAuth to `false`
7. Update app.config's hostname and *basic* Couchbase client section with the Couchbase Server IP

Couchbase Server 5.0+ - In addition to the steps above:
1. "ephemeral" - an Ephemeral bucket
2. Update config.json enhancedAuth to `true`
3. A user called `authenticated` with a password of `secret`

NOTE: Couchbase Server 5.0+ uses Role-Based Access Control (RBAC) for authentication. This supersedes configuring bucket passwords with discrete users with their own passwords and offers much more granular control.

## Pull Requests and Submissions ##
Being an Open Source project, the Couchbase SDK depends upon feedback and submissions from the community. If you feel as if you want to submit a bug fix or a feature, please post a Pull Request. The Pull Request will go through a formal code review process and merged after being +2'd by a Couchbase Engineer. In order to accept a submission, Couchbase requires that all contributors sign the Contributor License Agreement (CLA). You can do this by creating an account in [Gerrit](http://review.couchbase.org), our official Code Review system. After you have created your account, login and check the CLA checkbox.

Once the CLA is signed, a Couchbase engineer will push the pull request to Gerrit and one or more Couchbase engineers will review the submission. If it looks good they will then +2 the changeset and merge it with master. In addition, if the submission needs more work, you will need to amend the Changeset with another Patchset. Note that is strongly encouraged to submit a Unit Test with each submission and also include a description of the submission, what changed and what the result is.

<img src="https://d3nmt5vlzunoa1.cloudfront.net/dotnet/files/2016/08/ReSharper2016_2_2_512x197.png" height="100"></img>
