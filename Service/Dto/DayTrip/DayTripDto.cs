using Service.Dto.DayTripItem;
using Service.Dto.Region;
using Service.Dto.Review;
using Service.Dto.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Dto.DayTrip
{
    public class DayTripDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public byte[]? Image { get; set; }
        public double TotalDurationHours { get; set; }
        public RegionDto Region { get; set; }
        public EnumValueDto Accessibility { get; set; }
        public EnumValueDto Difficulty { get; set; }
        public double? AverageRating { get; set; }
        public int? RatingsCount { get; set; }
        public int? ReviewsCount { get; set; }
        public int StopsCount { get; set; }
    }
}
