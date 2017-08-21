# Official Couchbase .NET SDK [![Build Status](http://sdkbuilds.sc.couchbase.com/buildStatus/icon?job=netclient-build-test)](http://sdkbuilds.sc.couchbase.com/job/netclient-build-test/) [![Join the chat at https://gitter.im/couchbase/couchbase-dotnet-sdk](https://badges.gitter.im/couchbase/couchbase-dotnet-sdk.svg)](https://gitter.im/couchbase/couchbase-dotnet-sdk?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

* master is 2.0 development branch
* release13 is 1.3.X development branch

## Getting Started

To get up and running with the SDK, please visit the [online documentation](http://developer.couchbase.com/documentation/server/4.5/sdk/dotnet/start-using-sdk.html).

## Running Tests

We maintain a collection of both unit and integration test projects, with a version for the full .NET framework and Net Standard (projects with a NetStandard suffix).

### Unit Tests

There are two unit tests projects, Couchbase.UnitTests and Couchbase.UnitTests.NetStandard, that contain environment independent tests and do not require a local cluster to run.

### Running the Integration Tests ##

There are two integration test projects, Couchbase.IntegrationTests and Couchbase.IntegationTests.NetStandard, and require the following  to run:

1. Couchbase Server >= 4.0 installed on localhost
2. The "beer-sample" and "travel-sample" sample buckets installed. They can be installed by logging into the Couchbase Console (http://localhost:8091) and then Settings->Sample Buckets.
3. The following buckets installed on localhost:
	1. "default" - the standard default bucket
	2. "authenticated" - a Couchbase bucket with a password of "secret"
	3. "memcached" - a Memcached bucket with no password
4. Install an SSL certificate (copied from the Couchbase console Security->Root Certificate)
5. A default primary index configured for both the `default` and `authenticated` buckets (eg <code>create primary index on &#96;default&#96;</code> and <code>create primary index on &#96;authenticated&#96;</code>)
6. Add an FTS index to the *travel-sample* bucket called `idx-travel`

## Pull Requests and Submissions ##
Being an Open Source project, the Couchbase SDK depends upon feedback and submissions from the community. If you feel as if you want to submit a bug fix or a feature, please post a Pull Request. The Pull Request will go through a formal code review process and merged after being +2'd by a Couchbase Engineer. In order to accept a submission, Couchbase requires that all contributors sign the Contributor License Agreement (CLA). You can do this by creating an account in [Gerrit](http://review.couchbase.com), our official Code Review system. After you have created your account, login and check the CLA checkbox.

Once the CLA is signed, a Couchbase engineer will push the pull request to Gerrit and one or more Couchbase engineers will review the submission. If it looks good they will then +2 the changeset and merge it with master. In addition, if the submission needs more work, you will need to amend the Changeset with another Patchset. Note that is strongly encouraged to submit a Unit Test with each submission and also include a description of the submission, what changed and what the result is.

<img src="https://d3nmt5vlzunoa1.cloudfront.net/dotnet/files/2016/08/ReSharper2016_2_2_512x197.png" height="100"></img>
