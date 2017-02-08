# Liquid Projections [![Build status](https://ci.appveyor.com/api/projects/status/85nutadfiq44bwq1?svg=true)](https://ci.appveyor.com/project/dennisdoomen/liquidprojections)

## What is this?
Liquid Projections is designed as a library (and not a framework) that should help you write maintainable projection code in an [event sourcing](https://www.martinfowler.com/eaaDev/EventSourcing.html) architecture. After [evaluating](http://www.continuousimprover.com/2016/06/event-sourcing-from-trenches-projections.html) several years of working with an event sourced architecture, I decided I needed a fresh start. This project has been designed based on the following principles:

* Designed as a library which doesn't hide the magic behind abstractions and tries to avoid the need to sub-class anything. 
* Projections must be autonomous so whether a projector stores its projections in-memory, in RavenDB or in a RDBMS is a local concern. 
* Similarly, whether it restarts from scratch every time or stores progress information in-between is a decision each projection can make.
* Is distributed as separate NuGet packages so that you don't need to take dependencies on stuff you don't need. 
* `async` all the way

## What does it offer?
Liquid Projections offers several components that are designed to be used independently.
* An `EventMapBuilder` to fluently build an `EventMap` that defines how events should be handled during projection. 
* A thin abstraction named `IEventStore` to represent an event store. Currently only NEventStore is supported as well as a in-memory event store for testing purposes. 
* Projector classes for RavenDB, NHibernate as well as an agnostic Projector that you can use *within* your projectors. 
* Support for look-ups by allowing you to nest projectors.
* A `Dispatcher` that can be used to connect an `IEventStore` implementation with a projector. 
* A `ProjectionException` that provides everything you would like to know to understand why a projection fails. 

## How to get started?
For now, please review the unit tests. 
