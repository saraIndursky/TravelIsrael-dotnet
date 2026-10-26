using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.Entities
{
    public enum ItemType { Route, Place }
    public enum Accessibility
    {
        NotAccessible,        // not accessible at all
        Low,                  // low accessibility (stairs, difficult trails)
        Partial,              // partially accessible
        WheelchairAccessible, // wheelchair accessible
        FullyAccessible        // fully accessible (including restrooms, parking, etc.)
    }
    public enum ContentType { Route, Place, DayTrip }
    public enum Difficulty { Easy, EasyMedium, Medium, MediumHard, Hard }
    public enum Role { User, Admin }
    public enum TravelMode
    {
        Walking,
        Car,
        Bike
    }
    //public enum RoutePointType
    //{
    //    Start,
    //    Instruction,
    //    ViewPoint,
    //    WaterSpot,
    //    RestArea,
    //    End
    //}

    public enum ApprovalStatus
    {
        Draft = 0,
        Pending = 1,
        Approved = 2,
        Rejected = 3
    }

    public class Enums
    {
    }
}
