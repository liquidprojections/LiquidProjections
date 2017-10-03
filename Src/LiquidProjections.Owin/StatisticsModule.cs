using System;
using System.Collections.Generic;
using System.Linq;
using LiquidProjections.Statistics;
using Nancy;
using Nancy.Linker;
using Nancy.Swagger;
using Nancy.Swagger.Modules;
using Nancy.Swagger.Services;
using Nancy.Swagger.Services.RouteUtils;
using Swagger.ObjectModel;

// ReSharper disable VirtualMemberCallInConstructor

namespace LiquidProjections.Owin
{
    internal class StatisticsModule : NancyModule
    {
        public StatisticsModule(IProjectionStats stats, IResourceLinker resourceLinker)
        {
            Get("/", args =>
            {
                var results = stats.OrderBy(p => p.ProjectorId).Select(p => new ProjectorSummary
                {
                    ProjectorId = p.ProjectorId,
                    LastCheckpoint = p.LastCheckpoint.Checkpoint,
                    LastCheckpointUpdatedUtc = p.LastCheckpoint.TimestampUtc,
                    Url = Context.Request.Url + $"/{p.ProjectorId}"
                });

                return results;
            }, null, "GetAll");

            Get("/{id}", args =>
            {
                string id = args.Id;
                
                return new
                {
                    ProjectorId = id,
                    LastCheckpoint = stats.Get(id).LastCheckpoint.Checkpoint,
                    LastCheckpointUpdatedUtc = stats.Get(id).LastCheckpoint.TimestampUtc,
                    Properties = stats.Get(id).GetProperties().Select(p => new ProjectorProperty
                    {
                        Key = p.Key,
                        Value = p.Value.Value,
                        LastUpdatedUtc = p.Value.TimestampUtc
                    }),
                    EventsUrl = resourceLinker.BuildAbsoluteUri(Context, "GetEvents", new
                    {
                        args.id
                    }).ToString()
                };
            }, null, "GetSpecific");

            Get("/{id}/events", args =>
            {
                string id = args.Id;

                return new ProjectorEventCollection
                {
                    ProjectorId = id,
                    Events = stats.Get(id).GetEvents().Select(@event => new ProjectorEvent
                    {
                        Body = @event.Body,
                        TimestampUtc = @event.TimestampUtc
                    })
                };
            }, null, "GetEvents");

            Get("/{id}/eta/{targetCheckpoint}", args =>
            {
                string id = args.Id;

                TimeSpan? eta = stats.GetTimeToReach(id, args.targetCheckpoint);

                return new
                {
                    Eta = eta
                };
            }, null, "GetEta");
        }
    }

    internal class StatisticsMetadataModule : SwaggerMetadataModule
    {
        public StatisticsMetadataModule(ISwaggerModelCatalog modelCatalog, ISwaggerTagCatalog tagCatalog)
            : base(modelCatalog, tagCatalog)
        {
            SwaggerTypeMapping.AddTypeMapping(typeof(DateTime), typeof(DateTime));

            RouteDescriber.AddBaseTag(new Tag
            {
                Description = "Operations for getting projection statistics",
                Name = "Statistics"
            });

            RouteDescriber.DescribeRoute<IEnumerable<ProjectorSummary>>("GetAll", "",
                "Returns a list of all known projectors and a summary of their status", new[]
                {
                    new HttpResponseMetadata {Code = 200, Message = "OK"}
                });
            
            RouteDescriber
                .DescribeRoute<ProjectorDetails>("GetSpecific", "", "Returns the details of a specific projector", new[]
                {
                    new HttpResponseMetadata {Code = 200, Message = "OK"}
                })
                .Parameter(p => p.Name("id").In(ParameterIn.Path).Description("Identifies the projector"));


            RouteDescriber
                .DescribeRoute<ProjectorEventCollection>("GetEvents", "", "Returns the events logged for a specific projector", new[]
                {
                    new HttpResponseMetadata {Code = 200, Message = "OK"}
                })
                .Parameter(p => p.Name("id").In(ParameterIn.Path).Description("Identifies the projector")); ;

            RouteDescriber
                .DescribeRoute<string>("GetEta", "", "Returns the ETA for a specific projector to reach a certain checkpoint", new[]
                {
                    new HttpResponseMetadata {Code = 200, Message = "OK"}
                })
                .Parameter(p => p.Name("id").In(ParameterIn.Path).Description("Identifies the projector"))
                .Parameter(p => p.Name("targetCheckpoint").In(ParameterIn.Path).Description("The target checkpoint for which to calculate the ETA"));

            RouteDescriber.AddAdditionalModels(
                typeof(ProjectorEvent), typeof(ProjectorProperty), typeof(ProjectorSummary));
        }
    }

    internal class ProjectorEventCollection
    {
        public string ProjectorId { get; set; }
        public IEnumerable<ProjectorEvent> Events { get; set; }
    }

    internal class ProjectorProperty
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }

    internal class ProjectorSummary
    {
        public string ProjectorId { get; set; }
        public long LastCheckpoint { get; set; }
        public DateTime LastCheckpointUpdatedUtc { get; set; }
        public string Url { get; set; }
    }

    internal class ProjectorEvent
    {
        public string Body { get; set; }
        public DateTime TimestampUtc { get; set; }
    }

    internal class ProjectorDetails
    {
        public string ProjectorId { get; set; }
        public long LastCheckpoint { get; set; }
        public DateTime LastCheckpointUpdatedUtc { get; set; }
        public ProjectorProperty[] Properties { get; set; }
        public string EventsUrl { get; set; }
    }
}