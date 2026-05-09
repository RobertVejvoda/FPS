using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FPS.Application.Common.Interfaces;
using FPS.Domain.Entities;
using FPS.Infrastructure.Persistence;

namespace FPS.Infrastructure.Repositories
{
    public class BookingRepository : IBookingRepository
    {
        private readonly ApplicationDbContext _context;

        public BookingRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<BookingRequest> GetByIdAsync(int id)
        {
            return await _context.Bookings.FindAsync(id);
        }

        public async Task<List<BookingRequest>> GetBookingsByEmployeeIdAsync(string employeeId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.Bookings.Where(b => b.EmployeeId == employeeId);
            
            if (fromDate.HasValue)
            {
                query = query.Where(b => b.StartTime >= fromDate.Value);
            }
            
            if (toDate.HasValue)
            {
                query = query.Where(b => b.StartTime <= toDate.Value);
            }
            
            return await query.ToListAsync();
        }

        public void Add(BookingRequest booking)
        {
            _context.Bookings.Add(booking);
        }

        public void Update(BookingRequest booking)
        {
            _context.Entry(booking).State = EntityState.Modified;
        }

        public void Delete(BookingRequest booking)
        {
            _context.Bookings.Remove(booking);
        }
    }
}