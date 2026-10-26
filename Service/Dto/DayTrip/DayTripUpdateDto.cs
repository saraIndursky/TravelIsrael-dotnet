using Microsoft.AspNetCore.Http;
using Repository.Entities;
using Service.Dto.DayTripItem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Dto.DayTrip
{
    public class DayTripUpdateDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public byte[]? Image { get; set; }
        public IFormFile? FileImage { get; set; }
        public string? ImageUrl { get; set; }
        public int? TypeId { get; set; }
        public TimeOnly? StartTime { get; set; }
        public List<DayTripItemToAdd> Items { get; set; } = new();
    }
}
