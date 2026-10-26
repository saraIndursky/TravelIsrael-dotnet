using Repository.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Dto.Image
{
    public class ImageDto
    {
        public int Id { get; set; }
        public ItemType ItemType { get; set; }
        public int? RouteId { get; set; }
        public int? PlaceId { get; set; }
        public byte[]? Image { get; set; }
        public bool IsMain { get; set; }
        public int CreatedByUserId { get; set; }
        public DateOnly CreatedAt { get; set; }
    }
}
