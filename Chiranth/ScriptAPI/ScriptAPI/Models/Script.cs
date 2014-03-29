using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScriptAPI.Models
{
    public class Script
    {
        [Key]
        public int SID { get; set; }
        public string SName { get; set; }
        public string SAuthor { get; set; }
        public string SType { get; set; }
        [DataType(DataType.MultilineText)]
        public string SData { get; set; }
        public decimal Sversion { get; set; }
        public DateTime UploadDate { get; set; }
    }

    public class ScriptDBContext : DbContext
    {
        public DbSet<Script> Scripts { get; set; }
        //public DbSet<ScriptData> ScriptssData { get; set; }
    }
}