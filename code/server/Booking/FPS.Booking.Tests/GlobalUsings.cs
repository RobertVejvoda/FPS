global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

// Domain
global using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
global using FPS.Booking.Domain.Aggregates.SlotAllocationAggregate;
global using FPS.Booking.Domain.Exceptions;
global using FPS.Booking.Domain.Interfaces;
global using FPS.Booking.Domain.Services;
global using FPS.Booking.Domain.ValueObjects;
global using FPS.Booking.Domain.Events;

// Application
global using FPS.Booking.Application.Commands;
global using FPS.Booking.Application.Exceptions;
global using FPS.Booking.Application.Models;
global using FPS.Booking.Application.Queries;
global using FPS.Booking.Application.Repositories;
global using FPS.Booking.Application.Services;

// Shared kernel
global using FPS.SharedKernel.DomainEvents;
global using FPS.SharedKernel.Profile;
global using FPS.SharedKernel.Time;

// Test helpers
global using Microsoft.Extensions.Logging.Abstractions;
global using Moq;
global using Xunit;
