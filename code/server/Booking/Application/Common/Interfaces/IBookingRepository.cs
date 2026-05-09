using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FPS.Domain.Entities;

namespace FPS.Application.Common.Interfaces
{
    public interface IBookingRepository
    {
        Task<BookingRequest> GetByIdAsync(int id);
        Task<List<BookingRequest>> GetBookingsByEmployeeIdAsync(string employeeId, DateTime? fromDate = null, DateTime? toDate = null);
        void Add(BookingRequest booking);
        void Update(BookingRequest booking);
        void Delete(BookingRequest booking);
    }
}