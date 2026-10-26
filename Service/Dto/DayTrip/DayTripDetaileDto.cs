using Repository.Entities;
using Service.Dto.DayTripItem;
using Service.Dto.Region;
using Service.Dto.Review;
using Service.Dto.Type;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Dto.DayTrip
{
    public class DayTripDetaileDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public byte[]? Image { get; set; }
        public double TotalDurationHours { get; set; }
        public double TotalLengthKM { get; set; }
        public TypeDto Type { get; set; }
        public RegionDto Region { get; set; }
        public EnumValueDto Accessibility { get; set; }
        public EnumValueDto Difficulty { get; set; }
        public double Price { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public double? AverageRating { get; set; }
        public int? RatingsCount { get; set; }
        public int? ReviewsCount { get; set; }
        public int StopsCount { get; set; }
        public double? MinTemperature { get; set; }
        public double? MaxTemperature { get; set; }
        public double? MaxWindSpeed { get; set; }
        public double? MaxRainProbability { get; set; }
        public double? MaxHumidity { get; set; }
        public double? MaxCloudCoverage { get; set; }
        public bool AllowRain { get; set; }
        public bool HasCommonWeather { get; set; }
        public int CreatedByUserId { get; set; }
        public DateOnly CreatedAt { get; set; }
        public EnumValueDto ApprovalStatus { get; set; }
        public DateOnly? SubmittedDate { get; set; }
        public DateOnly? ApprovedAt { get; set; }
        public string? RejectReason { get; set; }
        public string TripHash { get; set; }
        public ICollection<DayTripItemDto> DayTripItems { get; set; }
        public ICollection<ReviewDto> Reviews { get; set; }
    }
}
