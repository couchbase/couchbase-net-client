@echo off
build\tools\nuget i Couchbase\packages.config -o packages 
build\tools\nuget i CouchbaseTests\packages.config -o packages 
