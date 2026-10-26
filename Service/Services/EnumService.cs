using Repository.Entities;
using Service.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Services
{
    public class EnumService
    {
        public List<EnumValueDto> GetAccessibilityValues()
        {
            return Enum.GetValues<Accessibility>()
                .Cast<Accessibility>()
                .Select(e => new EnumValueDto { Id = (int)e, Name = GetAccessibilityName(e) })
                .ToList();
        }

        public List<EnumValueDto> GetDifficultyValues()
        {
            return Enum.GetValues<Difficulty>()
                .Cast<Difficulty>()
                .Select(e => new EnumValueDto { Id = (int)e, Name = GetDifficultyName(e) })
                .ToList();
        }

        public List<EnumValueDto> GetTravelModeValues()
        {
            return Enum.GetValues<TravelMode>()
                .Cast<TravelMode>()
                .Select(e => new EnumValueDto { Id = (int)e, Name = GetTravelModeName(e) })
                .ToList();
        }
        public List<EnumValueDto> GetApprovalStatusValues()
        {
            return Enum.GetValues<ApprovalStatus>()
                .Cast<ApprovalStatus>()
                .Select(e => new EnumValueDto { Id = (int)e, Name = GetApprovalStatusName(e) })
                .ToList();
        }

        // ==========================
        // Hebrew display names for enums
        // ==========================
        private string GetAccessibilityName(Accessibility value)
        {
            return value switch
            {
                Accessibility.NotAccessible => "לא נגיש בכלל",
                Accessibility.Low => "נגישות נמוכה",
                Accessibility.Partial => "נגיש חלקית",
                Accessibility.WheelchairAccessible => "נגיש לכיסאות גלגלים",
                Accessibility.FullyAccessible => "נגישות מלאה",
                _ => value.ToString()
            };
        }

        private string GetDifficultyName(Difficulty value)
        {
            return value switch
            {
                Difficulty.Easy => "קל",
                Difficulty.EasyMedium => "קל-בינוני",
                Difficulty.Medium => "בינוני",
                Difficulty.MediumHard => "בינוני-קשה",
                Difficulty.Hard => "קשה",
                _ => value.ToString()
            };
        }

        private string GetTravelModeName(TravelMode value)
        {
            return value switch
            {
                TravelMode.Walking => "הליכה",
                TravelMode.Car => "רכב",
                TravelMode.Bike => "אופניים",
                _ => value.ToString()
            };
        }

        private string GetApprovalStatusName(ApprovalStatus value)
        {
            return value switch
            {
                ApprovalStatus.Draft => "טיוטה",
                ApprovalStatus.Pending => "ממתין לאישור",
                ApprovalStatus.Approved => "מאושר",
                ApprovalStatus.Rejected => "נדחה",
                _ => value.ToString()
            };
        }
    }
}
