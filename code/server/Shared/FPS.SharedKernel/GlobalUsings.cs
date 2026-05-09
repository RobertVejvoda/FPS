global using System;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;

// Domain events
global using FPS.SharedKernel.DomainEvents;

// Common abstractions
global using FPS.SharedKernel.Exceptions;
global using FPS.SharedKernel.Interfaces;

// Value objects
global using FPS.SharedKernel.ValueObjects;

global using Dapr.Client;
global using Microsoft.Extensions.Diagnostics.HealthChecks;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using Microsoft.Extensions.DependencyInjection;

