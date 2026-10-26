using Microsoft.AspNetCore.Http;
using Repository.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Dto.Image
{
    public class ImageCreateDto
    {
        public ItemType ItemType { get; set; }
        public int? RouteId { get; set; }
        public int? PlaceId { get; set; }
        public byte[]? Image { get; set; }
        public IFormFile? FileImage { get; set; }
        public bool IsMain { get; set; }
        public int CreatedByUserId { get; set; }
    }
}
