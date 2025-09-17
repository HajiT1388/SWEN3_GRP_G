using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMSG3.Domain.Entities
{
    public class Document
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = null!;
        public string FileContent { get; set; } = null!;
        public DateTime UploadTime { get; set; } = DateTime.UtcNow;
    }
}