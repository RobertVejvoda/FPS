global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

// ASP.NET Core references
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Hosting;

// Domain
global using FPS.Booking.Domain.Exceptions;
global using FPS.Booking.Domain.ValueObjects;
global using FPS.Booking.Domain.Events;
global using FPS.Booking.Domain.Aggregates.SlotAllocationAggregate;

// Application layer
global using FPS.Booking.Application.Commands;

// Dapr
global using Dapr;
global using Dapr.Client;

// Health checks
global using Microsoft.Extensions.Diagnostics.HealthChecks;
global using FPS.SharedKernel.HealthChecks;
global using FPS.SharedKernel.Time;

// Shared kernel
global using FPS.SharedKernel.Domain;
global using FPS.SharedKernel.DomainEvents;
global using FPS.SharedKernel.Interfaces;
global using FPS.SharedKernel.ValueObjects;

using MediatR;
