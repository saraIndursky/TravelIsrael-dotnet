using Repository.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Service.Dto.DayTrip
{
    public class TripFilterDto
    {
        public string? Search { get; set; }
        public int? Region { get; set; }
        public int? Type { get; set; }
        public Difficulty? Difficulty { get; set; }
        public Accessibility? Accessibility { get; set; }
        public double? Duration { get; set; }
        public double? LengthKM { get; set; }
        public double? Price { get; set; }
        public int? StopsCount { get; set; }
        //public bool? FamilyFriendly { get; set; }
        public int Skip { get; set; } = 0;    // number of trips to skip
        public int Take { get; set; } = 20;   // number of trips to return
        public string SortBy { get; set; }
        public string SortDirection { get; set; }
    }
}