using FPS.Booking.Domain.Entities;
using FPS.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FPS.Booking.Infrastructure.Persistence.Repositories
{
    public class BookingRequestRepository : IRepository<BookingRequest, BookingRequestId>
    {
        private readonly BookingDbContext _dbContext;

        public BookingRequestRepository(BookingDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<BookingRequest?> GetByIdAsync(BookingRequestId id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.BookingRequests
                .Include(b => b.ParkingSlot)
                .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<BookingRequest>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.BookingRequests
                .Include(b => b.ParkingSlot)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(BookingRequest entity, CancellationToken cancellationToken = default)
        {
            await _dbContext.BookingRequests.AddAsync(entity, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(BookingRequest entity, CancellationToken cancellationToken = default)
        {
            _dbContext.BookingRequests.Update(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(BookingRequest entity, CancellationToken cancellationToken = default)
        {
            _dbContext.BookingRequests.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}