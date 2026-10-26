using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Repository.Entities
{ 
    public class Place
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        [ForeignKey("Type")]
        public int TypeId { get; set; }
        public virtual Type Type { get; set; }
        [ForeignKey("Region")]
        public int RegionId { get; set; }
        public virtual Region Region { get; set; }
        public Accessibility Accessibility {  get; set; }
        public double Price { get; set; }
        public TimeOnly OpeningTime { get; set; }
        public TimeOnly ClosingTime { get; set; }
        public int? AverageStayMinutes { get; set; }
        public double? AverageRating { get; set; }
        public int? RatingsCount { get; set; }
        public int NumberOfViews { get; set; }
        [ForeignKey("User")]
        public int CreatedByUserId { get; set; }
        public virtual User User { get; set; }
        public DateOnly CreatedAt { get; set; }
        public ApprovalStatus ApprovalStatus { get; set; }
        public DateOnly? ApprovedAt { get; set; }
        public string? RejectReason { get; set; }
        public ICollection<DayTripItem> DayTripItems { get; set; }
        public ICollection<Image> Images { get; set; }
        public ICollection<Review> Reviews { get; set; }
        
        // ==========================
        // Weather suitability fields
        // ==========================

        // Temperature in degrees Celsius
        public double? MinTemperature { get; set; }    // minimum suitable temperature
        public double? MaxTemperature { get; set; }    // maximum suitable temperature

        // Maximum wind speed
        public double? MaxWindSpeed { get; set; }

        // Maximum suitable chance of rain (0-100)
        public double? MaxRainProbability { get; set; }

        // Maximum suitable humidity (optional)
        public double? MaxHumidity { get; set; }

        // Cloud cover (optional, 0-100 percent)
        public double? MaxCloudCoverage { get; set; }

        // Whether the place is suitable in rain at all
        public bool AllowRain { get; set; } = true;

        public bool HasCommonWeather { get; set; }
    }
}
