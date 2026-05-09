using FPS.Booking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;

namespace FPS.Booking.Infrastructure.Persistence
{
    public class BookingDbContext : DbContext
    {
        public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
        {
        }

        public DbSet<BookingRequest> BookingRequests { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Configure entity mappings
            modelBuilder.Entity<BookingRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                // Configure additional properties and relationships as needed
            });
        }
    }
}